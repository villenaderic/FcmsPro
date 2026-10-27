#!/usr/bin/env python3
"""Extracts the '## [X.Y.Z]' section of CHANGELOG.md for use as GitHub Release notes.

Usage:  python3 scripts/extract-changelog.py X.Y.Z [--changelog CHANGELOG.md] [--out notes.md]

  * Prints the section body (heading line excluded) to --out, or stdout if omitted.
  * Exit 0 and writes the notes when the section exists and is non-empty.
  * Exit 3 (and writes nothing) when it is missing/empty, so the workflow can warn
    and fall back to GitHub's generated notes instead of failing the release.
  * Exit 2 on bad usage / unreadable file.

Python standard library only. Handles CRLF, LF and mixed line endings.
"""
import argparse
import re
import sys


def extract(text, version):
    """Returns the markdown body of '## [version]' (heading excluded), or ''."""
    lines = text.replace("\r\n", "\n").replace("\r", "\n").split("\n")
    head = re.compile(r"^##\s+\[?v?" + re.escape(version) + r"\]?(?:\s|$|-)")
    any_h2 = re.compile(r"^##\s+\S")
    start = next((i for i, l in enumerate(lines) if head.match(l)), None)
    if start is None:
        return ""
    body = []
    for l in lines[start + 1:]:
        if any_h2.match(l):
            break
        body.append(l.rstrip())
    # Drop a trailing horizontal rule or link-reference definitions some changelogs use.
    while body and (not body[-1].strip() or body[-1].strip() == "---"):
        body.pop()
    return "\n".join(body).strip()


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("version", help="X.Y.Z (a leading 'v' is accepted)")
    ap.add_argument("--changelog", default="CHANGELOG.md")
    ap.add_argument("--out", help="write notes here instead of stdout")
    args = ap.parse_args(argv)
    version = args.version.lstrip("vV")
    if not re.fullmatch(r"\d+\.\d+\.\d+", version):
        print(f"Version must look like X.Y.Z (got '{args.version}')", file=sys.stderr)
        return 2
    try:
        with open(args.changelog, encoding="utf-8") as f:
            text = f.read()
    except OSError as e:
        print(f"Could not read {args.changelog}: {e}", file=sys.stderr)
        return 2
    notes = extract(text, version)
    if not notes:
        print(f"No non-empty '## [{version}]' section in {args.changelog}", file=sys.stderr)
        return 3
    if args.out:
        with open(args.out, "w", encoding="utf-8", newline="\n") as f:
            f.write(notes + "\n")
    else:
        print(notes)
    return 0


if __name__ == "__main__":
    sys.exit(main())
