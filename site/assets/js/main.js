/*
 * main.js - site-wide behaviour. Plain ES module, no framework, no build step.
 * Coordinates modular components: theme, copy, os-detect, docs, release.
 */
import { initHeader } from "./theme.js";
import { initCopy, toast, writeClipboard } from "./copy.js";
import { initPlatformHints, detectPlatform } from "./os-detect.js";
import { initDocs } from "./docs.js";
import { initRelease } from "./release.js";

const doc = document.documentElement;
const $ = (sel, root = document) => root.querySelector(sel);
const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));
const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

export { detectPlatform, toast, writeClipboard };

/* ----------------------------------------------------------------------- */
/* Tabs                                                                     */
/* ----------------------------------------------------------------------- */
export function initTabs() {
  const { os } = detectPlatform();
  const hashKey = location.hash.replace("#", "").toLowerCase();

  $$("[data-tabs]").forEach((root) => {
    const tabs = $$('[role="tab"]', root);
    const panels = tabs.map((t) => document.getElementById(t.getAttribute("aria-controls")));
    if (!tabs.length) return;

    const select = (i, focus = false) => {
      tabs.forEach((t, n) => {
        const on = n === i;
        t.setAttribute("aria-selected", String(on));
        t.tabIndex = on ? 0 : -1;
        if (panels[n]) panels[n].hidden = !on;
      });
      if (focus) tabs[i].focus();
    };

    let start = Math.max(0, tabs.findIndex((t) => t.getAttribute("aria-selected") === "true"));
    if (root.hasAttribute("data-os-tabs")) {
      const wanted = panels.findIndex((p) => p && p.getAttribute("data-os") === (hashKey || os));
      if (wanted >= 0) start = wanted;
    }
    select(start);

    tabs.forEach((tab, i) => {
      tab.addEventListener("click", () => select(i));
      tab.addEventListener("keydown", (e) => {
        const last = tabs.length - 1;
        let to = null;
        if (e.key === "ArrowRight") to = i === last ? 0 : i + 1;
        else if (e.key === "ArrowLeft") to = i === 0 ? last : i - 1;
        else if (e.key === "Home") to = 0;
        else if (e.key === "End") to = last;
        if (to !== null) { e.preventDefault(); select(to, true); }
      });
    });
  });
}

/* ----------------------------------------------------------------------- */
/* Scroll reveal                                                            */
/* ----------------------------------------------------------------------- */
export function initReveal() {
  const items = $$(".reveal");
  if (!items.length) return;
  if (reduceMotion || !("IntersectionObserver" in window)) {
    items.forEach((el) => el.classList.add("is-visible"));
    return;
  }
  const io = new IntersectionObserver((entries) => {
    entries.forEach((en) => {
      if (en.isIntersecting) {
        en.target.classList.add("is-visible");
        io.unobserve(en.target);
      }
    });
  }, { rootMargin: "0px 0px -8% 0px", threshold: 0.05 });
  items.forEach((el) => io.observe(el));
  setTimeout(() => items.forEach((el) => el.classList.add("is-visible")), 4000);
}

/* ----------------------------------------------------------------------- */
/* Changelog: filter by change type, copy a version link                    */
/* ----------------------------------------------------------------------- */
export function initChangelog() {
  const list = $("[data-changelog]");
  if (!list) return;
  const controls = $("[data-cl-controls]");
  const empty = $("[data-cl-empty]");
  const versions = $$(".cl-version", list);
  const types = [...new Set($$(".cl-group", list).map((g) => g.getAttribute("data-type")))];

  if (controls && types.length > 1) {
    const mk = (label, type) => {
      const b = document.createElement("button");
      b.type = "button";
      b.className = "chip";
      b.textContent = label;
      b.setAttribute("data-filter", type);
      b.setAttribute("aria-pressed", type === "all" ? "true" : "false");
      return b;
    };
    controls.append(mk("All", "all"), ...types.map((t) => mk(t.charAt(0).toUpperCase() + t.slice(1), t)));
    controls.hidden = false;
    controls.addEventListener("click", (e) => {
      const chip = e.target.closest("[data-filter]");
      if (!chip) return;
      const want = chip.getAttribute("data-filter");
      $$("[data-filter]", controls).forEach((c) => c.setAttribute("aria-pressed", String(c === chip)));
      let shown = 0;
      versions.forEach((v) => {
        let any = false;
        $$(".cl-group", v).forEach((g) => {
          const on = want === "all" || g.getAttribute("data-type") === want;
          g.hidden = !on;
          any = any || on;
        });
        v.hidden = !any;
        if (any) shown++;
      });
      if (empty) empty.hidden = shown !== 0;
    });
  }

  list.addEventListener("click", async (e) => {
    const btn = e.target.closest("[data-copy-link]");
    if (!btn) return;
    const url = new URL(btn.getAttribute("data-copy-link"), location.href.split("#")[0]).href;
    toast((await writeClipboard(url)) ? "Link copied" : "Copy failed. Copy the address bar instead.");
  });
}

function start() {
  initHeader();
  initCopy();
  initTabs();
  initPlatformHints();
  initReveal();
  initChangelog();
  initDocs();
  doc.setAttribute("data-ready", "");
  // Service worker for offline capability
  if ("serviceWorker" in navigator && location.protocol.startsWith("http")) {
    window.addEventListener("load", () => {
      navigator.serviceWorker.register(new URL("sw.js", new URL("../../", import.meta.url)).href).catch(() => {});
    });
  }
  // Live release data last: it must never be able to stop the UI above.
  initRelease().catch(() => { /* pre-rendered values stay */ });
}

if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", start);
else start();
