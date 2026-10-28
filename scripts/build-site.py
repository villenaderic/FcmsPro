#!/usr/bin/env python3
"""Builds the static FCMS Pro website into a deployable folder.

    python3 scripts/build-site.py --src site --data _build/data \
        --guide docs/USER_GUIDE.md --changelog CHANGELOG.md \
        --site-url https://villenaderic.github.io/FcmsPro --out _site

What it does (Python standard library only, no network access):
  * copies everything in --src to --out, skipping names that start with "_" or "."
    (so `site/_partials/` is a template folder, never published) and *.md sources;
  * fills `{{tokens}}` in .html/.txt/.xml/.webmanifest and sw.js from the release
    data written by build-site-data.py, so versions, dates, download links, sizes,
    checksums, notes, the history table and JSON-LD are PRE-RENDERED (works with JS
    off and for crawlers); release.js only refreshes them live afterwards;
  * renders docs/USER_GUIDE.md (and any other markdown a page asks for) to HTML,
    with a table of contents and a search index;
  * renders CHANGELOG data into the changelog page;
  * writes sitemap.xml, data/*.json and data/search-index.json.

An unknown {{token}} FAILS the build: a typo must never reach production.

TOKENS (use them in site/ source; nothing is ever hard-coded to a version)
  {{root}}                 relative path back to the site root ("./", "../"); on 404.html
                           it is absolute because Pages serves 404 from any depth
  {{site_url}} {{canonical}} {{page_id}} {{page_path}} {{year}} {{build_id}}
  {{version}} {{tag}} {{release_url}} {{published}} {{published|date}}
  {{asset.KEY.FIELD}}      KEY: windows | macos | linux_deb | linux_appimage
                           FIELD: url | name | size | sha256 | available
                           filters: |mb (size)  |sha_short (hash)
                           a missing asset yields the /releases/latest URL, never a stale file
  {{highlights_html}} {{history_rows_html}} {{changelog_html}} {{jsonld_json}}
  {{guide_html}} {{guide_toc_html}}
  {{md:docs/x.md}} {{toc:docs/x.md}}      render a markdown file that lives under --src
  {{include:_partials/header.html}}       include another template (processed recursively)
  {{shot:NAME}} {{shot:NAME|hero}}         real screenshot if assets/img/screenshots/NAME.png exists,
                           else the Sample-data mockup _partials/mockups/NAME.html (|hero = eager)
  {{current:download}}                    ` aria-current="page"` when page_id matches
  {{inline:path}} {{hash:path}}           inline a CSS/JS file and emit its CSP sha256 hash
Values are HTML-escaped unless the token name ends in _html or _json.
"""
import argparse
import base64
import hashlib
import html
import json
import re
import struct
import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import urlparse

REPO = "villenaderic/FcmsPro"
DEFAULT_SITE_URL = "https://villenaderic.github.io/FcmsPro/"
LATEST_URL = f"https://github.com/{REPO}/releases/latest"
ASSET_KEYS = ("windows", "macos", "linux_deb", "linux_appimage")
ASSET_LABELS = {
    "windows": "Windows (.exe)",
    "macos": "macOS (.dmg)",
    "linux_deb": "Linux (.deb)",
    "linux_appimage": "Linux (.AppImage)",
}
TEXT_EXT = {".html", ".txt", ".xml", ".webmanifest"}
PROCESSED_NAMES = {"sw.js"}
# Screenshot slots. {{shot:NAME}} renders a real <picture> when
# site/assets/img/screenshots/NAME.png exists (WebP is used too if NAME.webp exists),
# otherwise the labelled "Sample data" mockup in _partials/mockups/NAME.html.
# Dropping the real file in is therefore the ONLY step needed: no template edit.
SHOT_ALT = {
    "dashboard": "FCMS Pro dashboard with income, balances and due-soon warnings",
    "commissions": "FCMS Pro commissions table with status badges and client details",
    "commissions-kanban": "FCMS Pro commissions table grouped by status",
    "clients": "FCMS Pro client profile with balances, history and attachments",
    "tax-pdf": "Tax and quarterly summary exported to PDF from FCMS Pro",
    "invoice-pdf": "Tax and quarterly summary exported to PDF from FCMS Pro",
    "analytics": "FCMS Pro analytics with income breakdown charts",
    "search": "FCMS Pro global search opened with the / shortcut",
    "dashboard-light": "FCMS Pro dashboard in the light theme",
}


def png_size(path):
    """(width, height) from a PNG header, stdlib only."""
    with open(path, "rb") as f:
        head = f.read(24)
    if head[:8] != b"\x89PNG\r\n\x1a\n":
        raise BuildError(f"Not a PNG file: {path}")
    return struct.unpack(">II", head[16:24])


TOKEN_RE = re.compile(r"\{\{\s*([^{}]+?)\s*\}\}")


class BuildError(Exception):
    pass


def log(msg):
    print(msg, flush=True)


def warn(msg):
    print(f"::warning::{msg}", flush=True)


# --------------------------------------------------------------------------- #
# Small helpers
# --------------------------------------------------------------------------- #
def esc(value):
    return html.escape("" if value is None else str(value), quote=True)


def fmt_mb(n):
    try:
        return f"{float(n) / 1048576:.1f} MB"
    except (TypeError, ValueError):
        return "\u2014"


def fmt_date(iso):
    try:
        d = datetime.fromisoformat(str(iso).replace("Z", "+00:00"))
        return f"{d.day} {d.strftime('%b %Y')}"
    except ValueError:
        return ""


def json_for_script(obj):
    """JSON that is safe inside <script type=application/ld+json>."""
    s = json.dumps(obj, indent=2, ensure_ascii=False)
    return s.replace("<", "\\u003c").replace(">", "\\u003e").replace("&", "\\u0026")


def slugify(text):
    s = re.sub(r"[^\w\s-]", "", text.lower(), flags=re.UNICODE)
    s = re.sub(r"[\s_]+", "-", s.strip())
    return re.sub(r"-{2,}", "-", s).strip("-") or "section"


def strip_tags(s):
    return re.sub(r"\s+", " ", html.unescape(re.sub(r"<[^>]+>", " ", s))).strip()


def read_json(path):
    try:
        return json.loads(Path(path).read_text(encoding="utf-8"))
    except (OSError, ValueError) as e:
        raise BuildError(f"Cannot read {path}: {e}")


# --------------------------------------------------------------------------- #
# Minimal Markdown -> HTML (headings, paragraphs, lists, tables, quotes, code)
# Everything is escaped first; only whitelisted tags are produced.
# --------------------------------------------------------------------------- #
def safe_url(u):
    u = u.strip().replace('"', "%22")
    if re.match(r"^(https?://|mailto:|#|\.{0,2}/|[\w./-]+$)", u, re.I):
        return u
    return "#"


def inline(text):
    codes = []

    def stash(m):
        codes.append(m.group(1))
        return f"\x00{len(codes) - 1}\x00"

    t = re.sub(r"`([^`]+)`", stash, text)
    t = html.escape(t, quote=False)

    def link(m):
        url = safe_url(html.unescape(m.group(2)))
        ext = ' rel="noopener noreferrer"' if re.match(r"https?://", url, re.I) else ""
        return f'<a href="{url}"{ext}>{m.group(1)}</a>'

    t = re.sub(r"\[([^\]]+)\]\(([^)\s]+)\)", link, t)
    t = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", t)
    t = re.sub(r"(?<![\w*])\*(?!\s)(.+?)(?<!\s)\*(?![\w*])", r"<em>\1</em>", t)
    t = re.sub(
        r"\x00(\d+)\x00",
        lambda m: f"<code>{html.escape(codes[int(m.group(1))], quote=False)}</code>",
        t,
    )
    return t


_SEP_RE = re.compile(r"^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)*\|?\s*$")
_LIST_RE = re.compile(r"^(\s*)([-*+]|\d+[.)])\s+(.*)$")


def split_row(line):
    line = line.strip()
    if line.startswith("|"):
        line = line[1:]
    if line.endswith("|") and not line.endswith("\\|"):
        line = line[:-1]
    return [c.strip().replace("\\|", "|") for c in re.split(r"(?<!\\)\|", line)]


def render_md(src):
    """Returns (html, toc, sections, title).

    toc      [(level, id, text)] for h2/h3
    sections [{"id","title","text"}] for the search index
    """
    lines = src.replace("\r\n", "\n").replace("\r", "\n").split("\n")
    out, toc, sections, used = [], [], [], set()
    title = ""
    cur = {"id": "", "title": "", "text": []}
    para = []

    def add_text(h):
        cur["text"].append(strip_tags(h))

    def flush_para():
        if para:
            h = inline(" ".join(s.strip() for s in para))
            out.append(f"<p>{h}</p>")
            add_text(h)
            para.clear()

    def close_section():
        text = " ".join(t for t in cur["text"] if t)
        if cur["title"] or text:
            sections.append({"id": cur["id"], "title": cur["title"], "text": text})

    i, n = 0, len(lines)
    while i < n:
        line = lines[i]
        stripped = line.strip()

        if not stripped:
            flush_para()
            i += 1
            continue

        fence = re.match(r"^\s*(```+|~~~+)\s*([\w+-]*)", line)
        if fence:
            flush_para()
            marker, lang = fence.group(1), fence.group(2)
            i += 1
            buf = []
            while i < n and not lines[i].strip().startswith(marker[:3]):
                buf.append(lines[i])
                i += 1
            i += 1
            cls = f' class="language-{esc(lang)}"' if lang else ""
            code = html.escape("\n".join(buf), quote=False)
            out.append(f"<pre><code{cls}>{code}</code></pre>")
            add_text(code)
            continue

        m = re.match(r"^(#{1,6})\s+(.*?)\s*#*\s*$", line)
        if m:
            flush_para()
            level, raw = len(m.group(1)), m.group(2)
            text_html = inline(raw)
            plain = strip_tags(text_html)
            base = slugify(plain)
            hid, k = base, 2
            while hid in used:
                hid, k = f"{base}-{k}", k + 1
            used.add(hid)
            if level == 1 and not title:
                title = plain
            if level in (2, 3):
                toc.append((level, hid, plain))
            if level <= 3:
                close_section()
                cur = {"id": hid, "title": plain, "text": []}
            anchor = (
                f' <a class="anchor" href="#{hid}" aria-label="Link to {esc(plain)}">#</a>'
                if level >= 2
                else ""
            )
            out.append(f'<h{level} id="{hid}">{text_html}{anchor}</h{level}>')
            i += 1
            continue

        if re.match(r"^\s*([-*_])(\s*\1){2,}\s*$", line):
            flush_para()
            out.append("<hr>")
            i += 1
            continue

        if "|" in line and i + 1 < n and _SEP_RE.match(lines[i + 1]) and "-" in lines[i + 1]:
            flush_para()
            head = split_row(line)
            i += 2
            rows = []
            while i < n and lines[i].strip() and "|" in lines[i]:
                rows.append(split_row(lines[i]))
                i += 1
            th = "".join(f'<th scope="col">{inline(c)}</th>' for c in head)
            body = "".join(
                "<tr>" + "".join(f"<td>{inline(c)}</td>" for c in r) + "</tr>" for r in rows
            )
            tbl = (
                '<div class="table-wrap"><table><thead><tr>'
                f"{th}</tr></thead><tbody>{body}</tbody></table></div>"
            )
            out.append(tbl)
            add_text(tbl)
            continue

        if stripped.startswith(">"):
            flush_para()
            buf = []
            while i < n and lines[i].strip().startswith(">"):
                buf.append(re.sub(r"^\s*>\s?", "", lines[i]))
                i += 1
            inner, _, secs, _ = render_md("\n".join(buf))
            out.append(f"<blockquote>{inner}</blockquote>")
            add_text(inner)
            continue

        lm = _LIST_RE.match(line)
        if lm:
            flush_para()
            base_indent = len(lm.group(1).expandtabs(4))
            ordered = lm.group(2)[0].isdigit()
            items = []
            while i < n:
                lm2 = _LIST_RE.match(lines[i])
                if lm2 and len(lm2.group(1).expandtabs(4)) == base_indent:
                    items.append([lm2.group(3)])
                    i += 1
                elif lines[i].strip() and items and (
                    len(lines[i]) - len(lines[i].lstrip())
                ) > base_indent:
                    items[-1].append(lines[i])
                    i += 1
                else:
                    break
            tag = "ol" if ordered else "ul"
            lis = []
            for it in items:
                head_txt, rest = it[0], it[1:]
                li = inline(head_txt)
                if rest:
                    nested = "\n".join(r[base_indent + 2:] if len(r) > base_indent + 2 else r.strip() for r in rest)
                    li += render_md(nested)[0]
                lis.append(f"<li>{li}</li>")
            block = f"<{tag}>{''.join(lis)}</{tag}>"
            out.append(block)
            add_text(block)
            continue

        para.append(line)
        i += 1

    flush_para()
    close_section()
    return "\n".join(out), toc, sections, title


def render_toc(toc):
    if not toc:
        return ""
    parts = ['<ul class="toc">']
    for level, hid, text in toc:
        cls = "toc-l3" if level == 3 else "toc-l2"
        parts.append(f'<li class="{cls}"><a href="#{hid}">{esc(text)}</a></li>')
    parts.append("</ul>")
    return "\n".join(parts)


# --------------------------------------------------------------------------- #
# Data-driven fragments
# --------------------------------------------------------------------------- #
def build_jsonld(release, site_url):
    return {
        "@context": "https://schema.org",
        "@type": "SoftwareApplication",
        "name": "FCMS Pro",
        "alternateName": "Freelance Commission Management System",
        "applicationCategory": "BusinessApplication",
        "operatingSystem": "Windows, macOS, Linux",
        "softwareVersion": release["version"],
        "datePublished": release.get("published_at", ""),
        "downloadUrl": release.get("html_url") or LATEST_URL,
        "releaseNotes": release.get("html_url") or LATEST_URL,
        "url": site_url,
        "license": "https://opensource.org/licenses/MIT",
        "isAccessibleForFree": True,
        "offers": {"@type": "Offer", "price": "0", "priceCurrency": "USD"},
        "author": {"@type": "Person", "name": "Roderic Villena"},
        "description": (
            "Local-first desktop app for freelancers to manage clients, commissions, "
            "quotes, payments, invoices and expenses. No account, no subscription, no telemetry."
        ),
    }


def build_highlights(release):
    items = release.get("highlights") or []
    return "\n".join(f"<li>{inline(h)}</li>" for h in items)


def build_history_rows(releases):
    rows = []
    for r in releases.get("releases", []):
        links = []
        for key in ASSET_KEYS:
            a = (r.get("assets") or {}).get(key)
            if a and a.get("url"):
                links.append(
                    f'<a href="{esc(a["url"])}" rel="noopener noreferrer">{esc(ASSET_LABELS[key])}</a>'
                )
        links.append(
            f'<a href="{esc(r.get("html_url") or LATEST_URL)}" rel="noopener noreferrer">Release notes</a>'
        )
        pre = ' <span class="badge badge-pre">Pre-release</span>' if r.get("prerelease") else ""
        rows.append(
            f'<tr><th scope="row"><span class="mono">v{esc(r.get("version"))}</span>{pre}</th>'
            f'<td><time datetime="{esc(r.get("published_at"))}">{esc(fmt_date(r.get("published_at")))}</time></td>'
            f'<td class="dl-links">{" ".join(links)}</td></tr>'
        )
    return "\n".join(rows)


def build_changelog_html(changelog, releases, latest_version):
    dates = {r.get("version"): r.get("published_at") for r in releases.get("releases", [])}
    urls = {r.get("version"): r.get("html_url") for r in releases.get("releases", [])}
    out = []
    for v in changelog.get("versions", []):
        ver = v.get("version", "")
        groups = v.get("groups") or {}
        types = sorted({g.lower() for g in groups})
        date = dates.get(ver)
        latest = ' <span class="badge badge-latest">Latest</span>' if ver == latest_version else ""
        when = (
            f'<time datetime="{esc(date)}">{esc(fmt_date(date))}</time>' if date else ""
        )
        gh = (
            f'<a href="{esc(urls[ver])}" rel="noopener noreferrer">Release on GitHub</a>'
            if urls.get(ver)
            else ""
        )
        sections = []
        for gname, items in groups.items():
            slug = slugify(gname)
            lis = "".join(f"<li>{inline(it)}</li>" for it in items)
            sections.append(
                f'<section class="cl-group" data-type="{esc(gname.lower())}">'
                f'<h3><span class="badge badge-{esc(slug)}">{esc(gname)}</span></h3><ul>{lis}</ul></section>'
            )
        out.append(
            f'<article class="cl-version" id="v{esc(ver)}" data-version="{esc(ver)}" '
            f'data-types="{esc(" ".join(types))}">'
            f'<header class="cl-head"><h2><a href="#v{esc(ver)}"><span class="mono">v{esc(ver)}</span></a>{latest}</h2>'
            f'{when}<span class="cl-actions">{gh}'
            f'<button type="button" class="btn-link" data-copy-link="#v{esc(ver)}">Copy link</button></span></header>'
            f'{"".join(sections)}</article>'
        )
    return "\n".join(out)


# --------------------------------------------------------------------------- #
# Template engine
# --------------------------------------------------------------------------- #
class Site:
    def __init__(self, src, data, guide, changelog_md, site_url, today):
        self.src = Path(src)
        self.data_dir = Path(data)
        self.release = read_json(Path(data) / "release.json")
        self.releases = read_json(Path(data) / "releases.json")
        self.changelog = read_json(Path(data) / "changelog.json")
        for key in ("version", "tag"):
            if not self.release.get(key):
                raise BuildError(f"release.json has no '{key}'")
        self.site_url = site_url.rstrip("/") + "/"
        self.today = today
        self.guide_path = Path(guide)
        self.md_cache = {}
        self.search_entries = []
        self.pages = []  # page_path of every html page, for the sitemap
        self.build_id = "dev"
        self.inline_cache = {}

        gh, toc, secs, _ = render_md(self.guide_path.read_text(encoding="utf-8"))
        self.guide = (gh, render_toc(toc))
        self.guide_sections = secs

        rel = self.release
        self.vars = {
            "site_url": self.site_url,
            "version": rel["version"],
            "tag": rel["tag"],
            "release_url": rel.get("html_url") or LATEST_URL,
            "published": rel.get("published_at", ""),
            "year": str(today.year),
            "highlights_html": build_highlights(rel),
            "history_rows_html": build_history_rows(self.releases),
            "changelog_html": build_changelog_html(self.changelog, self.releases, rel["version"]),
            "jsonld_json": json_for_script(build_jsonld(rel, self.site_url)),
            "guide_html": self.guide[0],
            "guide_toc_html": self.guide[1],
            "latest_url": LATEST_URL,
        }

    # -- helpers ----------------------------------------------------------- #
    def read_src(self, rel):
        p = (self.src / rel).resolve()
        if self.src.resolve() not in p.parents:
            raise BuildError(f"Path escapes site source: {rel}")
        if not p.is_file():
            raise BuildError(f"Missing file referenced by template: {rel}")
        return p.read_text(encoding="utf-8")

    def inline_asset(self, rel):
        if rel not in self.inline_cache:
            text = self.read_src(rel)
            if rel.endswith(".css"):
                text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
                text = re.sub(r"\s+", " ", text)
            text = text.strip()
            digest = hashlib.sha256(text.encode("utf-8")).digest()
            self.inline_cache[rel] = (text, "'sha256-" + base64.b64encode(digest).decode() + "'")
        return self.inline_cache[rel]

    def markdown(self, rel):
        if rel not in self.md_cache:
            h, toc, secs, title = render_md(self.read_src(rel))
            self.md_cache[rel] = (h, render_toc(toc), secs, title)
        return self.md_cache[rel]

    def asset_value(self, key, field, flt):
        if key not in ASSET_KEYS:
            raise BuildError(f"Unknown asset key '{key}'")
        a = (self.release.get("assets") or {}).get(key) or {}
        if field == "available":
            val = "true" if a.get("url") else "false"
        elif field == "url":
            val = a.get("url") or LATEST_URL
        elif field == "name":
            val = a.get("name", "")
        elif field == "size":
            val = a.get("size", "")
            if flt is None:
                flt = "mb"
        elif field == "sha256":
            val = a.get("sha256", "")
        else:
            raise BuildError(f"Unknown asset field '{field}'")
        return val, flt

    def shot(self, name, hero, ctx, depth):
        if name not in SHOT_ALT:
            raise BuildError(f"Unknown screenshot slot '{name}' in {ctx['_file']}")
        base = f"assets/img/screenshots/{name}"
        png = self.src / f"{base}.png"
        if png.is_file():
            w, h = png_size(png)
            webp = (self.src / f"{base}.webp").is_file()
            root = ctx["root"]
            load = 'fetchpriority="high"' if hero else 'loading="lazy" decoding="async"'
            source = f'<source srcset="{esc(root)}{base}.webp" type="image/webp">' if webp else ""
            return (
                f'<picture>{source}<img src="{esc(root)}{base}.png" width="{w}" height="{h}" '
                f'alt="{esc(SHOT_ALT[name])}" {load}></picture>'
            )
        mock = self.render(self.read_src(f"_partials/mockups/{name}.html"), ctx, depth + 1)
        return f'<div class="mockup" role="img" aria-label="Sample data: {esc(SHOT_ALT[name])}">{mock}</div>'

    # -- rendering --------------------------------------------------------- #
    def resolve(self, expr, ctx, depth):
        flt = None
        if "|" in expr:
            expr, flt = (p.strip() for p in expr.split("|", 1))
        name = expr.strip()
        raw = name.endswith("_html") or name.endswith("_json")

        if name.startswith("shot:"):
            if depth > 5:
                raise BuildError("Template includes nested too deeply")
            return self.shot(name[5:], flt == "hero", ctx, depth), True
        if name.startswith("include:"):
            if depth > 5:
                raise BuildError("Template includes nested too deeply")
            return self.render(self.read_src(name[8:]), ctx, depth + 1), True
        if name.startswith("inline:"):
            return self.inline_asset(name[7:])[0], True
        if name.startswith("hash:"):
            return self.inline_asset(name[5:])[1], True
        if name.startswith("md:"):
            h, _, secs, _ = self.markdown(name[3:])
            self.register_search(name[3:], ctx, secs)
            return h, True
        if name.startswith("toc:"):
            return self.markdown(name[4:])[1], True
        if name.startswith("current:"):
            return (' aria-current="page"' if ctx["page_id"] == name[8:] else ""), True
        if name.startswith("asset."):
            parts = name.split(".")
            if len(parts) != 3:
                raise BuildError(f"Bad asset token: {name}")
            val, flt = self.asset_value(parts[1], parts[2], flt)
        elif name in ctx:
            val = ctx[name]
        elif name in self.vars:
            val = self.vars[name]
        else:
            raise BuildError(f"Unknown token {{{{{name}}}}} in {ctx['_file']}")

        if flt == "mb":
            val = fmt_mb(val)
        elif flt == "date":
            val = fmt_date(val)
        elif flt == "sha_short":
            val = str(val)[:12]
        elif flt is not None:
            raise BuildError(f"Unknown filter '{flt}' in {ctx['_file']}")
        return (val if raw else esc(val)), True

    def render(self, text, ctx, depth=0):
        return TOKEN_RE.sub(lambda m: self.resolve(m.group(1), ctx, depth)[0], text)

    def register_search(self, rel, ctx, secs):
        key = (rel, ctx["page_path"])
        if key in getattr(self, "_registered", set()):
            return
        self._registered = getattr(self, "_registered", set()) | {key}
        for s in secs:
            if s["title"] or s["text"]:
                self.search_entries.append(
                    {
                        "t": s["title"],
                        "u": f'{ctx["page_path"]}#{s["id"]}' if s["id"] else ctx["page_path"],
                        "x": s["text"][:1200],
                    }
                )

    def page_context(self, rel_path):
        parts = Path(rel_path).parts
        is_404 = rel_path == "404.html"
        if rel_path == "index.html":
            page_id, page_path = "home", ""
        elif rel_path.endswith("/index.html"):
            page_path = "/".join(parts[:-1]) + "/"
            page_id = parts[0]
        else:
            page_id, page_path = Path(rel_path).stem, rel_path
        if is_404:
            root = urlparse(self.site_url).path or "/"  # Pages serves 404 from any depth
        else:
            depth = len(parts) - 1
            root = "../" * depth if depth else "./"
        return {
            "root": root,
            "page_id": page_id,
            "page_path": page_path,
            "canonical": self.site_url + page_path,
            "build_id": self.build_id,
            "_file": rel_path,
        }

    # -- build ------------------------------------------------------------- #
    def build(self, out):
        out = Path(out)
        if out.exists():
            shutil.rmtree(out)
        out.mkdir(parents=True)

        # Guide search entries belong to /docs/ whether or not a template used {{guide_html}}.
        files = sorted(p for p in self.src.rglob("*") if p.is_file())
        digest = hashlib.sha1()
        deferred = []
        for p in files:
            rel = p.relative_to(self.src)
            if any(part.startswith(("_", ".")) for part in rel.parts) or p.suffix == ".md":
                continue
            rel_s = rel.as_posix()
            dest = out / rel
            dest.parent.mkdir(parents=True, exist_ok=True)
            if p.name in PROCESSED_NAMES:
                deferred.append((p, rel_s, dest))
                continue
            if p.suffix.lower() in TEXT_EXT:
                ctx = self.page_context(rel_s)
                text = self.render(p.read_text(encoding="utf-8"), ctx)
                dest.write_text(text, encoding="utf-8")
                digest.update(rel_s.encode() + text.encode())
                if p.suffix == ".html" and rel_s != "404.html":
                    self.pages.append(ctx["page_path"])
            else:
                shutil.copyfile(p, dest)
                digest.update(rel_s.encode() + dest.read_bytes())

        self.build_id = digest.hexdigest()[:12]
        for p, rel_s, dest in deferred:
            ctx = self.page_context(rel_s)
            dest.write_text(self.render(p.read_text(encoding="utf-8"), ctx), encoding="utf-8")

        self.write_extras(out)

    def write_extras(self, out):
        data = out / "data"
        data.mkdir(exist_ok=True)
        src_data = self.data_dir
        for name in ("release.json", "releases.json", "changelog.json", "SHA256SUMS.txt"):
            f = src_data / name
            if f.is_file():
                shutil.copyfile(f, data / name)

        # Search index: docs pages only (guide + any {{md:}} pages); guide sections added once.
        if not any(e["u"].startswith("docs/") for e in self.search_entries):
            for s in self.guide_sections:
                self.search_entries.append(
                    {"t": s["title"], "u": f'docs/#{s["id"]}' if s["id"] else "docs/", "x": s["text"][:1200]}
                )
        (data / "search-index.json").write_text(
            json.dumps({"schema": 1, "entries": self.search_entries}, ensure_ascii=False, separators=(",", ":")),
            encoding="utf-8",
        )

        lastmod = self.today.strftime("%Y-%m-%d")
        urls = "".join(
            f"  <url><loc>{esc(self.site_url + p)}</loc><lastmod>{lastmod}</lastmod></url>\n"
            for p in sorted(set(self.pages))
        )
        (out / "sitemap.xml").write_text(
            '<?xml version="1.0" encoding="UTF-8"?>\n'
            '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n' + urls + "</urlset>\n",
            encoding="utf-8",
        )


def main(argv=None):
    ap = argparse.ArgumentParser(description="Build the FCMS Pro static website.")
    ap.add_argument("--src", default="site")
    ap.add_argument("--data", default="_build/data")
    ap.add_argument("--guide", default="docs/USER_GUIDE.md")
    ap.add_argument("--changelog", default="CHANGELOG.md", help="kept for CLI symmetry; data comes from changelog.json")
    ap.add_argument("--site-url", default=DEFAULT_SITE_URL)
    ap.add_argument("--out", default="_site")
    args = ap.parse_args(argv)

    if not Path(args.src).is_dir():
        print(f"::error::Site source folder not found: {args.src}", file=sys.stderr)
        return 1
    if not Path(args.changelog).is_file():
        warn(f"{args.changelog} not found (changelog page uses changelog.json only)")

    today = datetime.now(timezone.utc)
    try:
        site = Site(args.src, args.data, args.guide, args.changelog, args.site_url, today)
        site.build(args.out)
    except BuildError as e:
        print(f"::error::{e}", file=sys.stderr)
        return 1

    log(f"built {len(site.pages)} pages for v{site.release['version']} -> {args.out} (build {site.build_id})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
