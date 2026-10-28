/*
 * theme.js - Theme toggle and header navigation interactions.
 * Persists choice in localStorage (safely wrapped in try/catch).
 */

const doc = document.documentElement;
const $ = (sel, root = document) => root.querySelector(sel);

export function initHeader() {
  const header = $("#site-header");
  if (header) {
    const onScroll = () => header.classList.toggle("is-scrolled", window.scrollY > 8);
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
  }

  const toggle = $("#nav-toggle");
  const nav = $("#site-nav");
  if (toggle && nav) {
    const setOpen = (open) => {
      nav.classList.toggle("is-open", open);
      toggle.setAttribute("aria-expanded", String(open));
    };
    toggle.addEventListener("click", () => setOpen(toggle.getAttribute("aria-expanded") !== "true"));
    nav.addEventListener("click", (e) => { if (e.target.closest("a")) setOpen(false); });
    document.addEventListener("keydown", (e) => {
      if (e.key === "Escape" && toggle.getAttribute("aria-expanded") === "true") {
        setOpen(false);
        toggle.focus();
      }
    });
    document.addEventListener("click", (e) => {
      if (!e.target.closest("#site-nav, #nav-toggle")) setOpen(false);
    });
    window.matchMedia("(min-width: 861px)").addEventListener("change", (m) => { if (m.matches) setOpen(false); });
  }

  const themeBtn = $("#theme-toggle");
  if (themeBtn) {
    const effective = () => {
      const t = doc.getAttribute("data-theme");
      if (t === "light" || t === "dark") return t;
      return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
    };
    const label = () => themeBtn.setAttribute("aria-label", effective() === "dark" ? "Switch to light theme" : "Switch to dark theme");
    label();
    themeBtn.addEventListener("click", () => {
      const next = effective() === "dark" ? "light" : "dark";
      doc.setAttribute("data-theme", next);
      try { localStorage.setItem("fcms-theme", next); } catch (e) { /* not persisted */ }
      label();
    });
  }
}
