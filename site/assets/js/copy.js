/*
 * copy.js - Clipboard management and user toast notifications.
 * Degrades gracefully with an off-screen textarea fallback.
 */

const $ = (sel, root = document) => root.querySelector(sel);

let toastTimer = 0;
export function toast(message) {
  const region = $("#toast-region");
  if (!region) return;
  region.textContent = "";
  const el = document.createElement("div");
  el.className = "toast";
  el.textContent = message;
  region.appendChild(el);
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => el.remove(), 2200);
}

export async function writeClipboard(text) {
  try {
    if (navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(text);
      return true;
    }
  } catch (e) { /* fall through to legacy fallback */ }
  try {
    const ta = document.createElement("textarea");
    ta.value = text;
    ta.setAttribute("readonly", "");
    ta.style.cssText = "position:fixed;top:0;left:0;opacity:0";
    document.body.appendChild(ta);
    ta.select();
    const ok = document.execCommand("copy");
    ta.remove();
    return ok;
  } catch (e) {
    return false;
  }
}

export function copyTextFor(btn) {
  if (btn.hasAttribute("data-copy-code")) {
    const pre = (btn.closest(".codeblock") || btn.parentElement).querySelector("pre");
    return pre ? pre.innerText.replace(/\s+$/, "") : "";
  }
  if (btn.hasAttribute("data-copy-sibling")) {
    const code = btn.parentElement && btn.parentElement.querySelector("code");
    return code ? code.getAttribute("data-copy-text") || code.textContent.trim() : "";
  }
  return btn.getAttribute("data-copy-text") || "";
}

export function initCopy() {
  document.addEventListener("click", async (e) => {
    const btn = e.target.closest("[data-copy-text], [data-copy-code], [data-copy-sibling]");
    if (!btn) return;
    const text = copyTextFor(btn);
    // A checksum that is not known yet is a sentence, not a hash: do not copy it.
    if (!text || /^See SHA256SUMS/i.test(text)) {
      toast("Nothing to copy yet. Open the release page for the checksum.");
      return;
    }
    const ok = await writeClipboard(text);
    toast(ok ? "Copied to clipboard" : "Copy failed. Select the text and copy it manually.");
    if (ok && btn.classList.contains("copy-btn")) {
      const original = btn.textContent;
      btn.textContent = "Copied";
      setTimeout(() => { btn.textContent = original; }, 1500);
    }
  });
}
