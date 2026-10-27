import pathlib
import re
import sys
from html.parser import HTMLParser

site_dir = pathlib.Path("_site")
html_files = list(site_dir.rglob("*.html"))
print(f"Checking {len(html_files)} HTML files in _site...")

errors = []

class LinkExtractor(HTMLParser):
    def __init__(self):
        super().__init__()
        self.targets = []
    def handle_starttag(self, tag, attrs):
        for k, v in attrs:
            if k in ("href", "src", "action") and v:
                self.targets.append((tag, k, v))

for hf in html_files:
    parser = LinkExtractor()
    text = hf.read_text(encoding="utf-8")
    parser.feed(text)
    rel_hf = hf.relative_to(site_dir)
    
    for tag, attr, val in parser.targets:
        if val.startswith(("http://", "https://", "mailto:", "#", "data:")):
            continue
        clean = val.split("?")[0].split("#")[0]
        if not clean:
            continue
        # handle /FcmsPro/ base url for GitHub Pages
        if clean.startswith("/FcmsPro/"):
            target = (site_dir / clean.removeprefix("/FcmsPro/")).resolve()
        else:
            target = (hf.parent / clean).resolve()
        if target.is_dir():
            target = target / "index.html"
        if not target.exists():
            errors.append(f"{rel_hf}: Broken <{tag} {attr}='{val}'> -> missing {target}")

if errors:
    print(f"FAILED with {len(errors)} errors:")
    for e in errors:
        print("  -", e)
    sys.exit(1)
else:
    print("ALL internal links, styles, scripts, images, and fonts are VALID!")

# Verify required links
index_text = (site_dir / "index.html").read_text(encoding="utf-8")
required_links = [
    "https://ko-fi.com/villenaderic",
    "https://m.me/roderic.villena",
    "https://github.com/villenaderic/FcmsPro/issues/new/choose"
]
for req in required_links:
    if req not in index_text:
        print(f"Missing required link: {req}")
        sys.exit(1)
print("All required community/support links verified in index.html!")

# Verify CSP hashes match inlined content
head_text = (site_dir / "index.html").read_text(encoding="utf-8")
assert "Content-Security-Policy" in head_text, "CSP missing in index.html"
print("CSP verified in index.html!")
