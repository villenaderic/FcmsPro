#!/usr/bin/env python3
"""Offline tests for extract-changelog.py. Run:  python3 scripts/test-extract-changelog.py"""
import importlib.util
import os
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
CHANGELOG = os.path.join(HERE, "..", "CHANGELOG.md")
spec = importlib.util.spec_from_file_location("ec", os.path.join(HERE, "extract-changelog.py"))
ec = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ec)

SAMPLE = "# Changelog\r\n\r\n## [2.0.0] - 2026-01-01\r\n\r\n### Added\r\n- A\r\n  wrapped\n- B\r\n\r\n---\r\n\r\n## [1.9.0]\r\n- old\r\n"


class Extract(unittest.TestCase):
    def test_section_only_and_mixed_endings(self):
        n = ec.extract(SAMPLE, "2.0.0")
        self.assertEqual(n, "### Added\n- A\n  wrapped\n- B")
        self.assertNotIn("\r", n)

    def test_stops_at_next_version_and_trailing_rule_removed(self):
        self.assertNotIn("old", ec.extract(SAMPLE, "2.0.0"))
        self.assertFalse(ec.extract(SAMPLE, "2.0.0").endswith("---"))

    def test_last_section(self):
        self.assertEqual(ec.extract(SAMPLE, "1.9.0"), "- old")

    def test_missing_version(self):
        self.assertEqual(ec.extract(SAMPLE, "3.0.0"), "")

    def test_no_prefix_match(self):  # 1.9 must not match 1.9.0, 1.6.1 must not match 1.6.10
        self.assertEqual(ec.extract("## [1.6.10]\n- x\n", "1.6.1"), "")

    def test_empty_section_is_empty(self):
        self.assertEqual(ec.extract("## [1.0.0]\n\n## [0.9.0]\n- x\n", "1.0.0"), "")


class Cli(unittest.TestCase):
    def test_real_changelog(self):
        out = os.path.join(tempfile.mkdtemp(), "n.md")
        self.assertEqual(ec.main(["v1.6.9", "--changelog", CHANGELOG, "--out", out]), 0)
        with open(out, encoding="utf-8") as f:
            txt = f.read()
        self.assertTrue(txt.startswith("### Fixed"))
        self.assertNotIn("1.6.8", txt)

    def test_missing_returns_3_and_writes_nothing(self):
        out = os.path.join(tempfile.mkdtemp(), "n.md")
        self.assertEqual(ec.main(["9.9.9", "--changelog", CHANGELOG, "--out", out]), 3)
        self.assertFalse(os.path.exists(out))

    def test_bad_args(self):
        self.assertEqual(ec.main(["abc", "--changelog", CHANGELOG]), 2)
        self.assertEqual(ec.main(["1.0.0", "--changelog", "/nope/CHANGELOG.md"]), 2)


if __name__ == "__main__":
    unittest.main(verbosity=2)
