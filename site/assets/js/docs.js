/*
 * docs.js - User guide sidebar scrollspy and search indexing.
 */

import { scoreSearchEntries } from "./search.js";

const $ = (sel, root = document) => root.querySelector(sel);
const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));

export function initDocs() {
  const body = $("[data-docs-body]");
  if (!body) return;

  // Table of contents: mark the section being read
  const toc = $("[data-docs-toc]");
  if (toc && "IntersectionObserver" in window) {
    const links = new Map($$("a[href^='#']", toc).map((a) => [a.getAttribute("href").slice(1), a]));
    const heads = $$("h2[id], h3[id]", body).filter((h) => links.has(h.id));
    const setCurrent = (id) => links.forEach((a, key) => {
      if (key === id) a.setAttribute("aria-current", "true");
      else a.removeAttribute("aria-current");
    });
    const io = new IntersectionObserver((entries) => {
      const hit = entries.filter((e) => e.isIntersecting).sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top)[0];
      if (hit) setCurrent(hit.target.id);
    }, { rootMargin: "-80px 0px -70% 0px" });
    heads.forEach((h) => io.observe(h));
  }

  // Search input and results list
  const input = $("#docs-search");
  const results = $("[data-docs-results]");
  if (!input || !results) return;
  let entries = null;
  const siteRoot = new URL("../", location.href.split("#")[0]);

  fetch(new URL("data/search-index.json", siteRoot), { cache: "no-cache" })
    .then((r) => (r.ok ? r.json() : Promise.reject(new Error(String(r.status)))))
    .then((json) => {
      entries = Array.isArray(json.entries) ? json.entries : [];
      input.hidden = false;
    })
    .catch(() => { /* search stays hidden; contents list still functions */ });

  const render = (query) => {
    results.textContent = "";
    const scored = scoreSearchEntries(entries, query);
    if (!scored.length && query) {
      results.hidden = false;
      const li = document.createElement("li");
      li.className = "docs-none";
      li.textContent = "No matches.";
      results.append(li);
      return;
    }
    if (!scored.length) {
      results.hidden = true;
      return;
    }
    results.hidden = false;
    for (const { e } of scored.slice(0, 8)) {
      const li = document.createElement("li");
      const a = document.createElement("a");
      a.href = new URL(e.u, siteRoot).href;
      a.textContent = e.t;
      li.append(a);
      results.append(li);
    }
  };

  input.addEventListener("input", () => render(input.value.trim()));
  input.addEventListener("keydown", (e) => {
    if (e.key === "Escape") { input.value = ""; render(""); }
  });
  results.addEventListener("click", () => { input.value = ""; render(""); });
}
