#!/usr/bin/env python3
"""Offline tests for build-site-data.py. Run:  python3 scripts/test-site-data.py

Uses only fixtures in scripts/fixtures (via RELEASE_JSON_FILE); never touches the network.
"""
import contextlib
import importlib.util
import io
import json
import os
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
FIX = os.path.join(HERE, "fixtures")
CHANGELOG = os.path.join(HERE, "..", "CHANGELOG.md")

spec = importlib.util.spec_from_file_location("bsd", os.path.join(HERE, "build-site-data.py"))
bsd = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bsd)


def run(fixture, changelog=CHANGELOG, out=None, extra_env=None):
    """Runs main() against a fixture. Returns (out_dir, stdout). Lets SystemExit propagate."""
    out = out or tempfile.mkdtemp()
    env = {"RELEASE_JSON_FILE": os.path.join(FIX, fixture)}
    env.update(extra_env or {})
    old = {k: os.environ.get(k) for k in list(env) + ["SHA256SUMS_FILE"]}
    os.environ.pop("SHA256SUMS_FILE", None)
    os.environ.update(env)
    buf = io.StringIO()
    try:
        with contextlib.redirect_stdout(buf):
            bsd.main(["--out", out, "--changelog", changelog])
    finally:
        for k, v in old.items():
            if v is None:
                os.environ.pop(k, None)
            else:
                os.environ[k] = v
    return out, buf.getvalue()


def read_json(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def write_json(path, data):
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f)


def read_text(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


def write_text(path, text):
    with open(path, "w", encoding="utf-8") as f:
        f.write(text)


def load(out, name):
    return read_json(os.path.join(out, name))


class NormalRelease(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.out, cls.log = run("release-1.6.9.json")
        cls.rel = load(cls.out, "release.json")
        cls.hist = load(cls.out, "releases.json")["releases"]

    def test_latest_ignores_draft_and_prerelease(self):
        self.assertEqual(self.rel["version"], "1.6.9")

    def test_assets_matched_by_extension(self):
        self.assertEqual(set(self.rel["assets"]), {"windows", "macos", "linux_deb", "linux_appimage"})
        self.assertTrue(self.rel["assets"]["windows"]["name"].endswith(".exe"))
        self.assertEqual(self.rel["assets"]["macos"]["arch"], "arm64")

    def test_checksums_file_asset_is_not_a_download(self):
        names = [a["name"] for a in self.rel["assets"].values()]
        self.assertNotIn("SHA256SUMS.txt", names)

    def test_sha256_from_api_digest(self):
        self.assertEqual(self.rel["assets"]["windows"]["sha256"], "a" * 64)
        self.assertEqual(self.rel["assets"]["linux_appimage"]["sha256"], "d" * 64)

    def test_full_changelog_footer_removed(self):
        self.assertNotIn("Full Changelog", self.rel["notes_markdown"])
        self.assertEqual(self.rel["notes_source"], "release")

    def test_urls_come_from_api_not_hardcoded(self):
        self.assertTrue(self.rel["assets"]["windows"]["url"].startswith("https://github.com/"))

    def test_history_has_prerelease_but_not_draft(self):
        tags = [h["tag"] for h in self.hist]
        self.assertNotIn("v1.7.0", tags)  # the draft
        self.assertIn("v1.7.0-rc.1", tags)
        flags = {h["tag"]: h["prerelease"] for h in self.hist}
        self.assertTrue(flags["v1.7.0-rc.1"])
        self.assertFalse(flags["v1.6.9"])

    def test_history_newest_first(self):
        dates = [h["published_at"] for h in self.hist]
        self.assertEqual(dates, sorted(dates, reverse=True))

    def test_changelog_json_written(self):
        versions = [v["version"] for v in load(self.out, "changelog.json")["versions"]]
        self.assertIn("1.6.9", versions)
        self.assertIn("1.0.0", versions)

    def test_highlights_max_three(self):
        self.assertLessEqual(len(self.rel["highlights"]), 3)
        self.assertGreaterEqual(len(self.rel["highlights"]), 1)


class NoExe(unittest.TestCase):
    def test_fails_and_writes_nothing(self):
        out = os.path.join(tempfile.mkdtemp(), "data")
        with self.assertRaises(SystemExit) as cm:
            run("release-no-exe.json", out=out)
        self.assertIn(".exe", str(cm.exception))
        self.assertFalse(os.path.exists(out), "output dir must not be created on failure")

    def test_failure_keeps_previous_data(self):
        out, _ = run("release-1.6.9.json")
        before = read_text(os.path.join(out, "release.json"))
        with self.assertRaises(SystemExit):
            run("release-no-exe.json", out=out)
        after = read_text(os.path.join(out, "release.json"))
        self.assertEqual(before, after)


class EmptyBody(unittest.TestCase):
    def test_falls_back_to_changelog_section(self):
        out, log = run("release-empty-body.json")
        rel = load(out, "release.json")
        self.assertEqual(rel["notes_source"], "changelog")
        self.assertIn("taskbar icon", rel["notes_markdown"])
        self.assertIn("::warning::", log)

    def test_no_notes_anywhere_does_not_crash(self):
        empty = os.path.join(tempfile.mkdtemp(), "CHANGELOG.md")
        write_text(empty, "# Changelog\n")
        out, log = run("release-empty-body.json", changelog=empty)
        rel = load(out, "release.json")
        self.assertEqual(rel["notes_source"], "none")
        self.assertEqual(rel["notes_markdown"], "")
        self.assertEqual(rel["highlights"], [])

    def test_missing_changelog_file_is_a_warning_not_an_error(self):
        out, log = run("release-empty-body.json", changelog="/nonexistent/CHANGELOG.md")
        self.assertEqual(load(out, "release.json")["version"], "1.6.9")


class Checksums(unittest.TestCase):
    def test_fallback_to_sha256sums_file(self):
        sums = os.path.join(tempfile.mkdtemp(), "SHA256SUMS.txt")
        write_text(sums, ("e" * 64) + "  FcmsPro-Setup-1.6.9.exe\n")
        # release-empty-body's .exe has a digest, so strip it via a temp copy
        data = read_json(os.path.join(FIX, "release-empty-body.json"))
        for r in data:
            for a in r["assets"]:
                a.pop("digest", None)
        tmp = os.path.join(tempfile.mkdtemp(), "nodigest.json")
        write_json(tmp, data)
        os.environ["RELEASE_JSON_FILE"] = tmp
        os.environ["SHA256SUMS_FILE"] = sums
        out = tempfile.mkdtemp()
        try:
            with contextlib.redirect_stdout(io.StringIO()):
                bsd.main(["--out", out, "--changelog", CHANGELOG])
        finally:
            os.environ.pop("RELEASE_JSON_FILE")
            os.environ.pop("SHA256SUMS_FILE")
        self.assertEqual(load(out, "release.json")["assets"]["windows"]["sha256"], "e" * 64)
        self.assertTrue(os.path.exists(os.path.join(out, "SHA256SUMS.txt")))

    def test_bad_digest_is_ignored(self):
        self.assertIsNone(bsd.asset_sha256({"name": "x.exe", "digest": "sha256:"}, {}))
        self.assertIsNone(bsd.asset_sha256({"name": "x.exe", "digest": "sha256:zzz"}, {}))

    def test_parse_sha256sums_formats(self):
        text = ("a" * 64) + "  one.exe\n" + ("b" * 64) + " *two.dmg\nnot a hash line\n"
        self.assertEqual(bsd.parse_sha256sums(text), {"one.exe": "a" * 64, "two.dmg": "b" * 64})


class NoHardcodedNames(unittest.TestCase):
    def test_renamed_assets_still_match(self):
        data = read_json(os.path.join(FIX, "release-1.6.9.json"))
        for r in data:
            for a in r["assets"]:
                a["name"] = "totally-different-" + a["name"].replace("FcmsPro", "App").replace("fcmspro", "app")
                a["browser_download_url"] = "https://example.invalid/" + a["name"]
        tmp = os.path.join(tempfile.mkdtemp(), "renamed.json")
        write_json(tmp, data)
        os.environ["RELEASE_JSON_FILE"] = tmp
        out = tempfile.mkdtemp()
        try:
            with contextlib.redirect_stdout(io.StringIO()):
                bsd.main(["--out", out, "--changelog", CHANGELOG])
        finally:
            os.environ.pop("RELEASE_JSON_FILE")
        self.assertEqual(set(load(out, "release.json")["assets"]),
                         {"windows", "macos", "linux_deb", "linux_appimage"})


class OnlyPrereleases(unittest.TestCase):
    def test_no_stable_release_fails(self):
        data = [r for r in read_json(os.path.join(FIX, "release-1.6.9.json")) if r["prerelease"]]
        tmp = os.path.join(tempfile.mkdtemp(), "pre.json")
        write_json(tmp, data)
        os.environ["RELEASE_JSON_FILE"] = tmp
        try:
            with self.assertRaises(SystemExit), contextlib.redirect_stdout(io.StringIO()):
                bsd.main(["--out", tempfile.mkdtemp(), "--changelog", CHANGELOG])
        finally:
            os.environ.pop("RELEASE_JSON_FILE")


class ChangelogParser(unittest.TestCase):
    def test_groups_continuations_and_other(self):
        md = ("# Changelog\n\n## [2.0.0]\n\n### Added\n- One\n- Two that\n  wraps\n\n### Fixed\n- Three\n\n"
              "## [1.9.0]\n- Old style bullet\n")
        p = bsd.parse_changelog(md)
        self.assertEqual([v["version"] for v in p["versions"]], ["2.0.0", "1.9.0"])
        self.assertEqual(p["versions"][0]["groups"]["Added"], ["One", "Two that wraps"])
        self.assertEqual(p["versions"][0]["groups"]["Fixed"], ["Three"])
        self.assertEqual(p["versions"][1]["groups"], {"Other": ["Old style bullet"]})

    def test_real_changelog_parses_all_versions(self):
        p = bsd.parse_changelog(read_text(CHANGELOG))
        self.assertEqual(len(p["versions"]), 19)
        self.assertEqual(p["versions"][0]["version"], "1.6.9")


class ChangelogSectionBoundaries(unittest.TestCase):
    def test_other_h2_heading_does_not_leak_into_previous_version(self):
        md = "## [1.0.0]\n### Added\n- real\n\n## Upgrade notes\n- not a change\n"
        p = bsd.parse_changelog(md)
        self.assertEqual(p["versions"][0]["groups"], {"Added": ["real"]})

    def test_unreleased_section_is_ignored(self):
        md = "## [Unreleased]\n- wip\n\n## [1.0.0]\n### Added\n- x\n"
        p = bsd.parse_changelog(md)
        self.assertEqual([v["version"] for v in p["versions"]], ["1.0.0"])
        self.assertEqual(p["versions"][0]["groups"], {"Added": ["x"]})

    def test_h3_is_not_mistaken_for_a_section_end(self):
        md = "## [1.0.0]\n### Fixed\n- a\n### Added\n- b\n"
        g = bsd.parse_changelog(md)["versions"][0]["groups"]
        self.assertEqual(g, {"Fixed": ["a"], "Added": ["b"]})


class AssetCompleteness(unittest.TestCase):
    def _stable(self):
        rels = read_json(os.path.join(FIX, "release-1.6.9.json"))
        return [r for r in rels if not r.get("draft") and not r.get("prerelease")][0]

    def test_half_uploaded_asset_is_not_linked(self):
        r = self._stable()
        exe = next(a for a in r["assets"] if a["name"].endswith(".exe"))
        exe["state"] = "starter"
        self.assertNotIn("windows", bsd.classify_assets(r, {}))

    def test_missing_state_and_uploaded_state_both_count(self):
        r = self._stable()
        for a in r["assets"]:
            a["state"] = "uploaded"
        self.assertEqual(set(bsd.classify_assets(r, {})), {"windows", "macos", "linux_deb", "linux_appimage"})
        for a in r["assets"]:
            a.pop("state")
        self.assertEqual(len(bsd.classify_assets(r, {})), 4)

    def test_release_whose_only_exe_is_still_uploading_is_refused(self):
        r = self._stable()
        for a in r["assets"]:
            if a["name"].endswith(".exe"):
                a["state"] = "starter"
        os.environ["RELEASE_JSON_FILE"] = "offline"  # keeps build() off the network
        try:
            with self.assertRaises(SystemExit), contextlib.redirect_stdout(io.StringIO()):
                bsd.build([r], "", bsd.REPO)
        finally:
            os.environ.pop("RELEASE_JSON_FILE")

    def test_missing_installers_are_warned_about(self):
        r = self._stable()
        r["assets"] = [a for a in r["assets"] if not a["name"].endswith((".dmg", ".deb"))]
        os.environ["RELEASE_JSON_FILE"] = "offline"
        buf = io.StringIO()
        try:
            with contextlib.redirect_stdout(buf):
                out = bsd.build([r], "", bsd.REPO)[0]
        finally:
            os.environ.pop("RELEASE_JSON_FILE")
        self.assertEqual(set(out["assets"]), {"windows", "linux_appimage"})
        self.assertIn("::warning::", buf.getvalue())
        self.assertIn("macos", buf.getvalue())
        self.assertIn("linux_deb", buf.getvalue())


if __name__ == "__main__":
    unittest.main(verbosity=2)
