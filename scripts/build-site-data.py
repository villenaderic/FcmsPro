#!/usr/bin/env python3
"""Builds the website's data files from GitHub Releases and CHANGELOG.md.

Outputs (written only if EVERYTHING succeeds, so a failed run leaves the
previously deployed site untouched):

    <out>/release.json     latest stable release: version, date, notes, per-OS assets
    <out>/releases.json    last N releases (pre-releases flagged), for the history table
    <out>/changelog.json   CHANGELOG.md parsed into versions -> Added/Changed/Fixed/...
    <out>/SHA256SUMS.txt   copy of the release's checksum file, if one was published

Rules (see docs/SITE_ARCHITECTURE.md):
  * Python standard library only.
  * Assets are matched by file EXTENSION, never by hard-coded file name.
  * Drafts and pre-releases are never "latest"; pre-releases still appear in history.
  * If the latest release has no .exe, exit non-zero (same philosophy as
    generate_appcast.py: never publish a site that links a missing installer).
  * Empty release body -> fall back to the matching CHANGELOG.md section.

Environment:
  GITHUB_TOKEN        optional; raises the API rate limit during CI
  RELEASE_JSON_FILE   optional; read releases from a local fixture instead of the
                      network. May hold one release object or a list of them.
  SHA256SUMS_FILE     optional; local checksum file to use in fixture mode
"""
import argparse
import json
import os
import re
import sys
import time
import urllib.request
from datetime import datetime, timezone

REPO = "villenaderic/FcmsPro"
HISTORY_LIMIT = 10
USER_AGENT = "FcmsPro-Site-Builder"

# extension -> asset key. Order matters only for the key names exposed to the site.
ASSET_KINDS = {
    ".exe": "windows",
    ".dmg": "macos",
    ".deb": "linux_deb",
    ".appimage": "linux_appimage",
}


def log(msg):
    print(msg, flush=True)


def warn(msg):
    # GitHub Actions turns this into a yellow annotation; harmless elsewhere.
    print(f"::warning::{msg}", flush=True)


# --------------------------------------------------------------------------- #
# Network
# --------------------------------------------------------------------------- #
def http_get(url, accept="application/vnd.github+json", attempts=5):
    """GET with exponential backoff. Returns bytes. Raises RuntimeError at the end."""
    req = urllib.request.Request(url)
    req.add_header("User-Agent", USER_AGENT)
    req.add_header("Accept", accept)
    token = os.environ.get("GITHUB_TOKEN")
    if token and url.startswith("https://api.github.com/"):
        req.add_header("Authorization", f"Bearer {token}")
    last = None
    for attempt in range(1, attempts + 1):
        try:
            with urllib.request.urlopen(req, timeout=30) as r:
                return r.read()
        except Exception as e:  # noqa: BLE001 - any failure is retried
            last = e
            wait = min(2 ** attempt, 30)
            log(f"attempt {attempt}/{attempts} for {url} failed: {e}; retrying in {wait}s")
            if attempt < attempts:
                time.sleep(wait)
    raise RuntimeError(f"could not fetch {url}: {last}")


def fetch_releases(repo):
    """Returns a list of release dicts (newest first as GitHub returns them)."""
    local = os.environ.get("RELEASE_JSON_FILE")
    if local:
        with open(local, encoding="utf-8") as f:
            data = json.load(f)
        return data if isinstance(data, list) else [data]
    raw = http_get(f"https://api.github.com/repos/{repo}/releases?per_page=100")
    return json.loads(raw)


def fetch_checksum_text(release):
    """Text of the SHA256SUMS.txt asset, or '' if there is none / it can't be read."""
    local = os.environ.get("SHA256SUMS_FILE")
    if local:
        with open(local, encoding="utf-8") as f:
            return f.read()
    if "_sha256sums_text" in release:  # fixture shortcut
        return release["_sha256sums_text"] or ""
    if os.environ.get("RELEASE_JSON_FILE"):
        return ""  # fixture mode is strictly offline
    for a in release.get("assets", []):
        if a["name"].lower() == "sha256sums.txt":
            try:
                return http_get(a["browser_download_url"], accept="application/octet-stream").decode("utf-8")
            except RuntimeError as e:
                warn(f"SHA256SUMS.txt is listed but could not be downloaded: {e}")
    return ""


# --------------------------------------------------------------------------- #
# Parsing helpers
# --------------------------------------------------------------------------- #
def parse_sha256sums(text):
    """'<hash>  <name>' lines (sha256sum format, optional '*' binary marker) -> {name: hash}."""
    out = {}
    for line in text.splitlines():
        m = re.match(r"^([0-9a-fA-F]{64})\s+\*?(.+?)\s*$", line)
        if m:
            out[m.group(2).strip()] = m.group(1).lower()
    return out


def asset_sha256(asset, sums):
    """Prefer GitHub's own per-asset digest; fall back to SHA256SUMS.txt."""
    digest = asset.get("digest") or ""
    if digest.lower().startswith("sha256:"):
        value = digest.split(":", 1)[1].strip().lower()
        if re.fullmatch(r"[0-9a-f]{64}", value):
            return value
    return sums.get(asset["name"])


def classify_assets(release, sums):
    """Match assets by extension. Returns {kind: {...}}; first match per kind wins."""
    found = {}
    for a in release.get("assets", []):
        # A release can be visible while its files are still uploading (state "starter").
        # Never link a half-uploaded file. A missing "state" (fixtures) counts as uploaded.
        if a.get("state", "uploaded") != "uploaded":
            continue
        name = a["name"]
        ext = os.path.splitext(name)[1].lower()
        kind = ASSET_KINDS.get(ext)
        if not kind or kind in found:
            continue
        entry = {
            "name": name,
            "url": a["browser_download_url"],
            "size": a.get("size", 0),
            "sha256": asset_sha256(a, sums),
        }
        if kind == "macos":
            # Only Apple Silicon is built today; read it from the file name rather than assume.
            entry["arch"] = "arm64" if "arm64" in name.lower() else "unknown"
        elif kind == "windows":
            entry["arch"] = "x64"
        else:
            entry["arch"] = "x64"
        found[kind] = entry
    return found


def clean_notes(body):
    """Drops GitHub's auto-generated 'Full Changelog' footer; trims whitespace."""
    lines = [l for l in (body or "").replace("\r\n", "\n").split("\n")
             if not l.startswith("**Full Changelog**:")]
    return "\n".join(lines).strip()


def parse_changelog(text):
    """CHANGELOG.md -> {"versions": [{version, groups: {Added: [..], ...}}]} (file order).

    Accepts '## [1.2.3]' headings, '### Added/Changed/Fixed/Removed' sub-headings, and
    '-' bullets with indented continuation lines. Bullets that appear under a version
    with no sub-heading (older entries) are grouped under 'Other' - we don't guess.
    """
    versions = []
    cur = None
    group = None
    for raw in text.replace("\r\n", "\n").split("\n"):
        m = re.match(r"^##\s+\[?v?(\d+\.\d+\.\d+[^\]\s]*)\]?", raw)
        if m:
            cur = {"version": m.group(1), "groups": {}}
            versions.append(cur)
            group = None
            continue
        if re.match(r"^##(?!#)\s*\S", raw):
            # Any other level-2 heading ('## [Unreleased]', '## Upgrade notes') ends the
            # current version; its bullets must not be attributed to the previous release.
            cur = None
            group = None
            continue
        if cur is None:
            continue
        m = re.match(r"^###\s+(.+?)\s*$", raw)
        if m:
            group = m.group(1)
            continue
        m = re.match(r"^[-*]\s+(.*)$", raw)
        if m:
            cur["groups"].setdefault(group or "Other", []).append(m.group(1).strip())
            continue
        if raw.startswith((" ", "\t")) and raw.strip():
            # Indented continuation of the previous bullet in the current group.
            items = cur["groups"].get(group or "Other")
            if items:
                items[-1] += " " + raw.strip()
    return {"versions": versions}


def changelog_section_markdown(parsed, version):
    for v in parsed["versions"]:
        if v["version"] == version:
            parts = []
            for g, items in v["groups"].items():
                parts.append(f"### {g}\n" + "\n".join(f"- {i}" for i in items))
            return "\n\n".join(parts)
    return ""


def highlights(notes_md, parsed, version, limit=3):
    """Up to `limit` short bullets for the 'what's new' card. Prefers CHANGELOG bullets."""
    for v in parsed["versions"]:
        if v["version"] == version:
            items = [i for g in v["groups"].values() for i in g]
            if items:
                return items[:limit]
    items = [m.group(1).strip() for m in re.finditer(r"^[-*]\s+(.*)$", notes_md or "", re.M)]
    return items[:limit]


def iso(dt_str):
    # Normalise to UTC 'Z' form.
    dt = datetime.fromisoformat(dt_str.replace("Z", "+00:00"))
    return dt.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def version_of(release):
    return release["tag_name"].lstrip("vV")


# --------------------------------------------------------------------------- #
# Build
# --------------------------------------------------------------------------- #
def build(releases, changelog_text, repo):
    parsed = parse_changelog(changelog_text)

    visible = [r for r in releases if not r.get("draft")]
    visible.sort(key=lambda r: r.get("published_at") or "", reverse=True)
    stable = [r for r in visible if not r.get("prerelease")]
    if not stable:
        raise SystemExit("No published stable release found - refusing to build site data.")

    latest = stable[0]
    version = version_of(latest)
    sums_text = fetch_checksum_text(latest)
    sums = parse_sha256sums(sums_text)
    assets = classify_assets(latest, sums)

    if "windows" not in assets:
        raise SystemExit(
            f"Release v{version} has no .exe asset - refusing to build site data. "
            f"Assets: {[a['name'] for a in latest.get('assets', [])]}")

    # Notes: release body first; empty -> CHANGELOG.md section; else warn and leave empty.
    notes = clean_notes(latest.get("body"))
    notes_source = "release"
    if not notes:
        notes = changelog_section_markdown(parsed, version)
        notes_source = "changelog"
        if notes:
            warn(f"v{version}: release body is empty; using the CHANGELOG.md section.")
        else:
            notes_source = "none"
            warn(f"v{version}: no release notes and no '## [{version}]' section in CHANGELOG.md.")

    absent = [k for k in ASSET_KINDS.values() if k not in assets]
    if absent:
        warn(f"v{version}: no installer for {absent}; the site will point those buttons at /releases/latest.")

    missing_sums = [k for k, a in assets.items() if not a["sha256"]]
    if missing_sums:
        warn(f"v{version}: no SHA-256 for {missing_sums} (no API digest and not in SHA256SUMS.txt).")

    release_json = {
        "schema": 1,
        "repo": repo,
        "version": version,
        "tag": latest["tag_name"],
        "name": latest.get("name") or f"v{version}",
        "published_at": iso(latest["published_at"]),
        "html_url": latest["html_url"],
        "notes_markdown": notes,
        "notes_source": notes_source,
        "highlights": highlights(notes, parsed, version),
        "assets": assets,
        "generated_at": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    }

    history = []
    for r in visible[:HISTORY_LIMIT]:
        v = version_of(r)
        a = classify_assets(r, {})  # history links only; checksums are shown for latest
        history.append({
            "version": v,
            "tag": r["tag_name"],
            "published_at": iso(r["published_at"]),
            "prerelease": bool(r.get("prerelease")),
            "html_url": r["html_url"],
            "assets": {k: {"name": x["name"], "url": x["url"], "size": x["size"]} for k, x in a.items()},
        })

    return release_json, {"schema": 1, "releases": history}, {"schema": 1, **parsed}, sums_text


def write_atomic(path, text):
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    os.replace(tmp, path)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", default="site/data", help="output directory (default: site/data)")
    ap.add_argument("--changelog", default="CHANGELOG.md", help="path to CHANGELOG.md")
    ap.add_argument("--repo", default=REPO, help="owner/name (default: %(default)s)")
    args = ap.parse_args(argv)

    try:
        releases = fetch_releases(args.repo)
    except RuntimeError as e:
        sys.exit(f"Could not fetch releases: {e}")

    try:
        with open(args.changelog, encoding="utf-8") as f:
            changelog_text = f.read()
    except OSError as e:
        warn(f"Could not read {args.changelog}: {e}")
        changelog_text = ""

    release_json, releases_json, changelog_json, sums_text = build(releases, changelog_text, args.repo)

    # Everything above succeeded: only now touch the output directory.
    os.makedirs(args.out, exist_ok=True)
    dump = lambda o: json.dumps(o, indent=2, ensure_ascii=False) + "\n"  # noqa: E731
    write_atomic(os.path.join(args.out, "release.json"), dump(release_json))
    write_atomic(os.path.join(args.out, "releases.json"), dump(releases_json))
    write_atomic(os.path.join(args.out, "changelog.json"), dump(changelog_json))
    if sums_text.strip():
        write_atomic(os.path.join(args.out, "SHA256SUMS.txt"), sums_text.rstrip("\n") + "\n")

    log(f"v{release_json['version']} ({release_json['published_at']}): "
        f"assets={sorted(release_json['assets'])}, notes from {release_json['notes_source']}, "
        f"history={len(releases_json['releases'])}, changelog versions={len(changelog_json['versions'])}")
    log(f"wrote data files to {args.out}")


if __name__ == "__main__":
    main()
