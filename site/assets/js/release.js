/*
 * release.js - keeps the version, date, download links, sizes, checksums and
 * "what's new" on the page current without anyone editing the site.
 *
 * The HTML is already pre-rendered at deploy time (works with JS off and for
 * crawlers). This module only REFRESHES it, in this order:
 *
 *   1. ./data/release.json   same-origin, fetched with cache: "no-cache"
 *   2. api.github.com        /repos/villenaderic/FcmsPro/releases/latest
 *                            (cached in sessionStorage for 10 minutes)
 *   3. nothing               the pre-rendered values stay, and every download
 *                            button still points at /releases/latest
 *
 * Rules:
 *   - Never replace newer data with older data (a stale CDN copy of release.json
 *     must not roll the page back).
 *   - Never write a link that is not on github.com/villenaderic/FcmsPro/.
 *   - No hard-coded versioned filenames anywhere in this file.
 *
 * DOM hooks (all optional; a page uses the ones it has):
 *   [data-release="version"]      text            1.6.9
 *   [data-release="date"]         text + datetime 24 Oct 2026
 *   [data-release="new-badge"]    hidden toggled  (release < 14 days old)
 *   [data-release="highlights"]   <li> items      first few notes
 *   [data-release="status"]       text            "Released 3 days ago"
 *   [data-release="notes-link"]   href            GitHub release page
 *   [data-asset-link="KEY"]       href (+ size in its .meta child)
 *   [data-asset-name="KEY"]       text
 *   [data-asset-size="KEY"]       text            36.0 MB
 *   [data-asset-sha="KEY"]        text (+ data-copy-text when present)
 *   [data-asset-sha-short="KEY"]  text            first 12 hex chars
 *   KEY: windows | macos | linux_deb | linux_appimage
 *
 * Public API (used by main.js and the download page):
 *   initRelease()   -> Promise<{release, source} | null>
 *   whenRelease()   -> same promise, for late subscribers
 * Event: document dispatches "fcms:release" with detail {release, source, updated}.
 */

const REPO = "villenaderic/FcmsPro";
const REPO_URL = `https://github.com/${REPO}`;
export const LATEST_URL = `${REPO_URL}/releases/latest`;
const API_URL = `https://api.github.com/repos/${REPO}/releases/latest`;
const DATA_URL = new URL("../../data/release.json", import.meta.url).href;

const CACHE_KEY = "fcms-release-api";
const CACHE_MS = 10 * 60 * 1000;
const FETCH_TIMEOUT_MS = 8000;
const NEW_DAYS = 14;
const MAX_HIGHLIGHTS = 3;
const KEYS = ["windows", "macos", "linux_deb", "linux_appimage"];
const EXT_TO_KEY = { ".exe": "windows", ".dmg": "macos", ".deb": "linux_deb", ".appimage": "linux_appimage" };
const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

let started = null;

/* ----------------------------------------------------------------------- */
/* Small pure helpers (exported for tests)                                  */
/* ----------------------------------------------------------------------- */
export function parseVersion(v) {
  const m = /^v?(\d+)\.(\d+)\.(\d+)/.exec(String(v || "").trim());
  return m ? [Number(m[1]), Number(m[2]), Number(m[3])] : null;
}

/** -1 / 0 / 1, or null when either side is not a version. */
export function compareVersions(a, b) {
  const pa = parseVersion(a);
  const pb = parseVersion(b);
  if (!pa || !pb) return null;
  for (let i = 0; i < 3; i++) {
    if (pa[i] !== pb[i]) return pa[i] < pb[i] ? -1 : 1;
  }
  return 0;
}

export function formatMB(bytes) {
  const n = Number(bytes);
  return Number.isFinite(n) && n > 0 ? `${(n / 1048576).toFixed(1)} MB` : "\u2014";
}

/** Same shape as the build-time formatter: "24 Oct 2026" (UTC, English). */
export function formatDate(iso) {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  return `${d.getUTCDate()} ${MONTHS[d.getUTCMonth()]} ${d.getUTCFullYear()}`;
}

export function daysSince(iso, now = Date.now()) {
  const t = new Date(iso).getTime();
  return Number.isNaN(t) ? null : Math.floor((now - t) / 86400000);
}

export function relativeDate(iso, now = Date.now()) {
  const days = daysSince(iso, now);
  if (days === null) return "";
  if (days < 1) return "Released today";
  if (days === 1) return "Released yesterday";
  if (days < 31) return `Released ${days} days ago`;
  if (days < 365) {
    const months = Math.max(1, Math.floor(days / 30.4));
    return `Released ${months} ${months === 1 ? "month" : "months"} ago`;
  }
  const years = Math.floor(days / 365);
  return `Released ${years} ${years === 1 ? "year" : "years"} ago`;
}

function isRepoUrl(u) {
  try {
    const url = new URL(u);
    return url.protocol === "https:" && url.hostname === "github.com" && url.pathname.startsWith(`/${REPO}/`);
  } catch (e) {
    return false;
  }
}

function escapeHtml(s) {
  return String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

/** Tiny, safe inline Markdown for release bullets: `code`, **bold**, *em*, [text](https://...). */
export function inlineMd(text) {
  const codes = [];
  let t = String(text).replace(/`([^`]+)`/g, (_, c) => `\u0000${codes.push(c) - 1}\u0000`);
  t = escapeHtml(t);
  t = t.replace(/\[([^\]]+)\]\((https?:\/\/[^)\s]+)\)/g, (_, label, url) =>
    `<a href="${url}" rel="noopener noreferrer">${label}</a>`);
  t = t.replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>");
  // No lookbehind here: it is a parse-time SyntaxError in Safari < 16.4 and would
  // stop this whole module from loading.
  t = t.replace(/(^|[^\w*])\*(?!\s)([^*]+?)\*(?![\w*])/g, (m, pre, body) =>
    /\s$/.test(body) ? m : `${pre}<em>${body}</em>`);
  return t.replace(/\u0000(\d+)\u0000/g, (_, i) => `<code>${escapeHtml(codes[Number(i)])}</code>`);
}

/* ----------------------------------------------------------------------- */
/* Normalising the two data sources to one shape                            */
/* ----------------------------------------------------------------------- */
/** Validates ./data/release.json (our own build output). Returns a clean object or null. */
export function normaliseSiteData(raw) {
  if (!raw || typeof raw !== "object" || !parseVersion(raw.version)) return null;
  const assets = {};
  for (const key of KEYS) {
    const a = raw.assets && raw.assets[key];
    if (!a || !isRepoUrl(a.url)) continue;
    assets[key] = {
      name: String(a.name || ""),
      url: a.url,
      size: Number(a.size) || 0,
      sha256: /^[0-9a-f]{64}$/i.test(a.sha256 || "") ? a.sha256.toLowerCase() : null,
    };
  }
  return {
    version: String(raw.version).replace(/^v/i, ""),
    publishedAt: raw.published_at || "",
    htmlUrl: isRepoUrl(raw.html_url) ? raw.html_url : LATEST_URL,
    highlights: Array.isArray(raw.highlights) ? raw.highlights.filter((h) => typeof h === "string").slice(0, MAX_HIGHLIGHTS) : [],
    assets,
  };
}

/** Maps a GitHub API release object to the same shape. Checksums only if GitHub gave a digest. */
export function normaliseApiData(raw) {
  if (!raw || typeof raw !== "object" || raw.draft || raw.prerelease || !parseVersion(raw.tag_name)) return null;
  const assets = {};
  for (const a of Array.isArray(raw.assets) ? raw.assets : []) {
    const name = String((a && a.name) || "");
    const dot = name.lastIndexOf(".");
    const key = dot >= 0 ? EXT_TO_KEY[name.slice(dot).toLowerCase()] : null;
    if (!key || assets[key] || !isRepoUrl(a.browser_download_url)) continue;
    const digest = String(a.digest || "");
    const sha = /^sha256:([0-9a-f]{64})$/i.exec(digest);
    assets[key] = { name, url: a.browser_download_url, size: Number(a.size) || 0, sha256: sha ? sha[1].toLowerCase() : null };
  }
  const bullets = [];
  for (const m of String(raw.body || "").matchAll(/^[-*]\s+(.*\S)\s*$/gm)) {
    bullets.push(m[1]);
    if (bullets.length >= MAX_HIGHLIGHTS) break;
  }
  return {
    version: String(raw.tag_name).replace(/^v/i, ""),
    publishedAt: raw.published_at || "",
    htmlUrl: isRepoUrl(raw.html_url) ? raw.html_url : LATEST_URL,
    highlights: bullets,
    assets,
  };
}

/* ----------------------------------------------------------------------- */
/* Fetching                                                                 */
/* ----------------------------------------------------------------------- */
async function fetchJson(url, options = {}) {
  const ctrl = typeof AbortController === "function" ? new AbortController() : null;
  const timer = ctrl ? setTimeout(() => ctrl.abort(), FETCH_TIMEOUT_MS) : null;
  try {
    const res = await fetch(url, { ...options, signal: ctrl ? ctrl.signal : undefined });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    return await res.json();
  } finally {
    if (timer) clearTimeout(timer);
  }
}

function readApiCache() {
  try {
    const hit = JSON.parse(sessionStorage.getItem(CACHE_KEY) || "null");
    if (hit && Date.now() - hit.t < CACHE_MS) return hit.data;
  } catch (e) { /* storage blocked or corrupt: ignore */ }
  return null;
}

function writeApiCache(data) {
  try {
    sessionStorage.setItem(CACHE_KEY, JSON.stringify({ t: Date.now(), data }));
  } catch (e) { /* private mode / quota: ignore */ }
}

async function loadLatest() {
  try {
    const site = normaliseSiteData(await fetchJson(DATA_URL, { cache: "no-cache" }));
    if (site) return { release: site, source: "site" };
  } catch (e) { /* fall through to the API */ }

  try {
    let raw = readApiCache();
    if (!raw) {
      raw = await fetchJson(API_URL, { headers: { Accept: "application/vnd.github+json" } });
      writeApiCache(raw);
    }
    const api = normaliseApiData(raw);
    if (api) return { release: api, source: "api" };
  } catch (e) { /* fall through: keep pre-rendered values */ }

  return null;
}

/* ----------------------------------------------------------------------- */
/* DOM                                                                      */
/* ----------------------------------------------------------------------- */
function setText(el, text) {
  if (el.textContent !== text) el.textContent = text;
}

function currentDomVersion(doc) {
  const el = doc.querySelector('[data-release="version"]');
  return el ? el.textContent.trim() : "";
}

/**
 * Writes `release` into the page. Returns true when something visible changed
 * (a newer version arrived), false when the page already showed it.
 */
export function applyRelease(release, doc = document, now = Date.now()) {
  const shown = currentDomVersion(doc);
  const cmp = compareVersions(release.version, shown);
  if (cmp === -1) return false; // never roll the page back to an older release
  const isNewer = cmp === 1;

  doc.querySelectorAll('[data-release="version"]').forEach((el) => setText(el, release.version));

  doc.querySelectorAll('[data-release="date"]').forEach((el) => {
    const label = formatDate(release.publishedAt);
    if (label) setText(el, label);
    if (release.publishedAt && el.tagName === "TIME") el.setAttribute("datetime", release.publishedAt);
  });

  const days = daysSince(release.publishedAt, now);
  doc.querySelectorAll('[data-release="new-badge"]').forEach((el) => {
    el.hidden = !(days !== null && days >= 0 && days < NEW_DAYS);
  });

  doc.querySelectorAll('[data-release="status"]').forEach((el) => {
    const rel = relativeDate(release.publishedAt, now);
    setText(el, isNewer && rel ? `Just updated \u00b7 ${rel.charAt(0).toLowerCase()}${rel.slice(1)}` : rel);
  });

  doc.querySelectorAll('[data-release="notes-link"]').forEach((el) => el.setAttribute("href", release.htmlUrl));

  // Notes: only replace when we actually have bullets (never blank a good list).
  if (release.highlights.length) {
    doc.querySelectorAll('[data-release="highlights"]').forEach((el) => {
      el.innerHTML = release.highlights.map((h) => `<li>${inlineMd(h)}</li>`).join("\n");
    });
  }

  for (const key of KEYS) {
    const a = release.assets[key];
    const url = a ? a.url : LATEST_URL; // a missing asset never leaves a stale file link

    doc.querySelectorAll(`[data-asset-link="${key}"]`).forEach((el) => {
      el.setAttribute("href", url);
      const meta = el.querySelector(".meta");
      if (meta && a) meta.textContent = meta.textContent.replace(/(\d+(\.\d+)? MB|\u2014)\s*$/, formatMB(a.size));
    });
    if (!a) continue;

    doc.querySelectorAll(`[data-asset-name="${key}"]`).forEach((el) => setText(el, a.name));
    doc.querySelectorAll(`[data-asset-size="${key}"]`).forEach((el) => setText(el, formatMB(a.size)));

    if (a.sha256) {
      doc.querySelectorAll(`[data-asset-sha="${key}"]`).forEach((el) => {
        setText(el, a.sha256);
        if (el.hasAttribute("data-copy-text")) el.setAttribute("data-copy-text", a.sha256);
      });
      doc.querySelectorAll(`[data-asset-sha-short="${key}"]`).forEach((el) => setText(el, a.sha256.slice(0, 12)));
    } else if (isNewer) {
      // A different release than the pre-rendered one and no digest for it:
      // showing the old hash would be wrong, so say where to find it.
      doc.querySelectorAll(`[data-asset-sha="${key}"], [data-asset-sha-short="${key}"]`).forEach((el) => {
        setText(el, "See SHA256SUMS.txt on the release page");
        el.removeAttribute("data-copy-text");
      });
    }
  }
  return isNewer;
}

/* ----------------------------------------------------------------------- */
/* Entry points                                                             */
/* ----------------------------------------------------------------------- */
export function initRelease() {
  if (started) return started;
  started = (async () => {
    const result = await loadLatest();
    if (!result) return null; // pre-rendered page stays as it is
    let updated = false;
    try {
      updated = applyRelease(result.release);
    } catch (e) { /* a DOM surprise must never break the page */ }
    document.dispatchEvent(new CustomEvent("fcms:release", { detail: { ...result, updated } }));
    return result;
  })();
  return started;
}

export function whenRelease() {
  return started || initRelease();
}
