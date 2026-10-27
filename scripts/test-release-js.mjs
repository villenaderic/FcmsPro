// Offline tests for site/assets/js/release.js. No dependencies: `node scripts/test-release-js.mjs`
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

// ---- minimal fake DOM ------------------------------------------------------
class El {
  constructor(attrs = {}, text = "", tag = "SPAN") {
    this.attrs = { ...attrs }; this.textContent = text; this.tagName = tag;
    this.hidden = false; this.innerHTML = ""; this.children = [];
  }
  setAttribute(k, v) { this.attrs[k] = String(v); }
  getAttribute(k) { return this.attrs[k] ?? null; }
  hasAttribute(k) { return k in this.attrs; }
  removeAttribute(k) { delete this.attrs[k]; }
  querySelector(sel) { return sel === ".meta" ? this.children.find((c) => c.cls === "meta") || null : null; }
}
function makeDoc(els) {
  const match = (el, sel) => sel.split(",").map((x) => x.trim()).some((one) => {
    const m = /^\[([\w-]+)="([^"]*)"\]$/.exec(one);
    return m && el.attrs[m[1]] === m[2];
  });
  return { querySelectorAll: (sel) => els.filter((e) => match(e, sel)), querySelector: (sel) => els.find((e) => match(e, sel)) || null };
}

const R = await import("../site/assets/js/release.js");
let n = 0; const t = (name, fn) => { fn(); n++; console.log("ok -", name); };

// ---- pure helpers -------------------------------------------------------------
t("compareVersions numeric, not lexical", () => {
  assert.equal(R.compareVersions("1.10.0", "1.9.9"), 1);
  assert.equal(R.compareVersions("v1.6.9", "1.6.9"), 0);
  assert.equal(R.compareVersions("1.6.8", "1.6.9"), -1);
  assert.equal(R.compareVersions("", "1.6.9"), null);
});
t("formatMB / formatDate", () => {
  assert.equal(R.formatMB(37710982), "36.0 MB");
  assert.equal(R.formatMB(0), "\u2014");
  assert.equal(R.formatDate("2026-10-24T22:28:03Z"), "24 Oct 2026");
});
t("relativeDate", () => {
  const now = Date.parse("2026-10-27T12:00:00Z");
  assert.equal(R.relativeDate("2026-10-27T01:00:00Z", now), "Released today");
  assert.equal(R.relativeDate("2026-10-26T01:00:00Z", now), "Released yesterday");
  assert.equal(R.relativeDate("2026-10-24T01:00:00Z", now), "Released 3 days ago");
  assert.equal(R.relativeDate("2026-07-01T00:00:00Z", now), "Released 3 months ago");
  assert.equal(R.relativeDate("2025-11-12T00:00:00Z", now), "Released 11 months ago"); // never "12 months"
  assert.equal(R.relativeDate("2024-10-01T00:00:00Z", now), "Released 2 years ago");
});
t("inlineMd escapes HTML and cannot break out of href", () => {
  assert.equal(R.inlineMd("<img src=x onerror=1>"), "&lt;img src=x onerror=1&gt;");
  const out = R.inlineMd('[x](https://e.com/a"onmouseover="y)');
  assert.ok(!/"onmouseover/.test(out), out);
  assert.equal(R.inlineMd("a **b** `<c>` *d*"), "a <strong>b</strong> <code>&lt;c&gt;</code> <em>d</em>");
  assert.equal(R.inlineMd("2 * 3 * 4"), "2 * 3 * 4");
});
t("module source has no regex lookbehind (Safari < 16.4)", () => {
  assert.ok(!/\(\?<[!=]/.test(readFileSync(new URL("../site/assets/js/release.js", import.meta.url), "utf8")));
});

// ---- normalisers --------------------------------------------------------------
const good = {
  version: "1.7.0", published_at: "2026-10-27T00:00:00Z", html_url: "https://github.com/villenaderic/FcmsPro/releases/tag/v1.7.0",
  highlights: ["a", "b", "c", "d"],
  assets: {
    windows: { name: "w.exe", url: "https://github.com/villenaderic/FcmsPro/releases/download/v1.7.0/w.exe", size: 1048576, sha256: "a".repeat(64) },
    macos: { name: "evil.dmg", url: "https://evil.example/evil.dmg", size: 1, sha256: null },
  },
};
t("normaliseSiteData drops off-repo links, caps highlights", () => {
  const r = R.normaliseSiteData(good);
  assert.deepEqual(Object.keys(r.assets), ["windows"]);
  assert.equal(r.highlights.length, 3);
  assert.equal(R.normaliseSiteData({ version: "nope" }), null);
});
t("normaliseApiData matches by extension, skips drafts/prereleases, uses digest", () => {
  const api = {
    tag_name: "v1.7.0", published_at: "x", html_url: "https://github.com/villenaderic/FcmsPro/releases/tag/v1.7.0",
    body: "## Added\n- one\n- two\n* three\n- four",
    assets: [
      { name: "Whatever-Name.AppImage", size: 5, browser_download_url: "https://github.com/villenaderic/FcmsPro/releases/download/v1.7.0/Whatever-Name.AppImage", digest: "sha256:" + "b".repeat(64) },
      { name: "SHA256SUMS.txt", size: 1, browser_download_url: "https://github.com/villenaderic/FcmsPro/releases/download/v1.7.0/SHA256SUMS.txt" },
    ],
  };
  const r = R.normaliseApiData(api);
  assert.deepEqual(Object.keys(r.assets), ["linux_appimage"]);
  assert.equal(r.assets.linux_appimage.sha256, "b".repeat(64));
  assert.deepEqual(r.highlights, ["one", "two", "three"]);
  assert.equal(R.normaliseApiData({ ...api, prerelease: true }), null);
  assert.equal(R.normaliseApiData({ ...api, draft: true }), null);
});

// ---- applyRelease -------------------------------------------------------------
function page() {
  const meta = new El({}, "Windows 10/11 \u00b7 36.0 MB"); meta.cls = "meta";
  const link = new El({ "data-asset-link": "windows", href: "OLD" }); link.children.push(meta);
  return {
    meta, link,
    ver: new El({ "data-release": "version" }, "1.6.9"),
    badge: new El({ "data-release": "new-badge" }),
    status: new El({ "data-release": "status" }, "Released 3 days ago"),
    sha: new El({ "data-asset-sha": "windows", "data-copy-text": "OLDHASH" }, "OLDHASH"),
    macLink: new El({ "data-asset-link": "macos", href: "OLDMAC" }),
    list: new El({ "data-release": "highlights" }),
  };
}
const els = (p) => Object.values(p).filter((x) => x instanceof El);

t("newer release updates version, links, size, hash, badge, status", () => {
  const p = page(); const doc = makeDoc(els(p));
  const changed = R.applyRelease(R.normaliseSiteData(good), doc, Date.parse("2026-10-28T00:00:00Z"));
  assert.equal(changed, true);
  assert.equal(p.ver.textContent, "1.7.0");
  assert.match(p.link.attrs.href, /\/v1\.7\.0\/w\.exe$/);
  assert.equal(p.meta.textContent, "Windows 10/11 \u00b7 1.0 MB");
  assert.equal(p.sha.textContent, "a".repeat(64));
  assert.equal(p.sha.attrs["data-copy-text"], "a".repeat(64));
  assert.equal(p.badge.hidden, false);
  assert.match(p.status.textContent, /^Just updated/);
  assert.match(p.list.innerHTML, /<li>a<\/li>/);
});
t("asset missing from the new release falls back to /releases/latest, never a stale file", () => {
  const p = page(); const doc = makeDoc(els(p));
  R.applyRelease(R.normaliseSiteData(good), doc);
  assert.equal(p.macLink.attrs.href, R.LATEST_URL);
});
t("older data never rolls the page back", () => {
  const p = page(); const doc = makeDoc(els(p));
  const changed = R.applyRelease(R.normaliseSiteData({ ...good, version: "1.6.0" }), doc);
  assert.equal(changed, false);
  assert.equal(p.ver.textContent, "1.6.9");
  assert.equal(p.link.attrs.href, "OLD");
});
t("newer release without a digest never keeps the OLD hash", () => {
  const p = page(); const doc = makeDoc(els(p));
  const noSha = { ...good, assets: { windows: { ...good.assets.windows, sha256: undefined } } };
  R.applyRelease(R.normaliseSiteData(noSha), doc);
  assert.ok(!p.sha.textContent.includes("OLDHASH"));
  assert.equal(p.sha.hasAttribute("data-copy-text"), false);
});
t("old release hides the New badge", () => {
  const p = page(); const doc = makeDoc(els(p));
  R.applyRelease(R.normaliseSiteData(good), doc, Date.parse("2026-12-30T00:00:00Z"));
  assert.equal(p.badge.hidden, true);
});

// ---- fallback chain (initRelease) --------------------------------------------
async function chain({ site, api, apiCached }) {
  const calls = []; const store = new Map();
  if (apiCached) store.set("fcms-release-api", JSON.stringify({ t: Date.now(), data: apiCached }));
  globalThis.sessionStorage = { getItem: (k) => store.get(k) ?? null, setItem: (k, v) => store.set(k, v) };
  globalThis.document = Object.assign(makeDoc([]), { dispatchEvent() { return true; } });
  globalThis.CustomEvent = class { constructor(t, o) { this.type = t; this.detail = o.detail; } };
  globalThis.fetch = async (url, opts) => {
    calls.push({ url: String(url), cache: opts && opts.cache });
    const handler = String(url).includes("api.github.com") ? api : site;
    if (!handler) throw new Error("network down");
    return { ok: handler !== "http-error", status: handler === "http-error" ? 403 : 200, json: async () => handler };
  };
  const mod = await import("../site/assets/js/release.js?" + Math.random());
  const res = await mod.initRelease();
  return { res, calls, store };
}
const apiRel = { tag_name: "v1.7.0", published_at: "x", assets: [], body: "- hi" };

{
  const { res, calls } = await chain({ site: good });
  t("tier 1: release.json used, with cache: no-cache, API untouched", () => {
    assert.equal(res.source, "site");
    assert.equal(calls.length, 1);
    assert.equal(calls[0].cache, "no-cache");
  });
}
{
  const { res, calls, store } = await chain({ site: null, api: apiRel });
  t("tier 2: release.json down -> GitHub API, cached in sessionStorage", () => {
    assert.equal(res.source, "api");
    assert.ok(calls.some((c) => c.url.includes("api.github.com")));
    assert.ok(store.has("fcms-release-api"));
  });
}
{
  const { res, calls } = await chain({ site: null, api: null, apiCached: apiRel });
  t("tier 2: fresh sessionStorage entry avoids the API call", () => {
    assert.equal(res.source, "api");
    assert.ok(!calls.some((c) => c.url.includes("api.github.com")));
  });
}
{
  const { res } = await chain({ site: null, api: "http-error" });
  t("tier 3: everything fails -> null, page keeps pre-rendered values", () => assert.equal(res, null));
}

console.log(`\n${n} tests passed`);
