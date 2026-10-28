/*
 * os-detect.js - Client-side platform and architecture detection.
 * Uses navigator.userAgentData with UA fallback. Never links wrong-arch files.
 */

const $ = (sel, root = document) => root.querySelector(sel);
const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));

export function detectPlatform(nav = navigator) {
  const uaData = nav.userAgentData;
  const ua = String(nav.userAgent || "");
  const plat = String((uaData && uaData.platform) || nav.platform || "");
  const hay = `${plat} ${ua}`;
  if (/android|iphone|ipad|ipod/i.test(hay) || (uaData && uaData.mobile)) return { os: "mobile" };
  // iPadOS reports as a Mac with touch points.
  if (/mac/i.test(plat) && nav.maxTouchPoints > 1) return { os: "mobile" };
  if (/win/i.test(plat)) return { os: "windows" };
  if (/mac/i.test(plat) || /Macintosh/i.test(ua)) return { os: "macos" };
  if (/cros/i.test(hay)) return { os: "other" };
  if (/linux|x11/i.test(hay)) return { os: "linux" };
  return { os: "other" };
}

export const OS_LABEL = { windows: "Windows", macos: "macOS", linux: "Linux" };
export const OS_NOTE = {
  windows: "Not code signed, so Windows SmartScreen may warn. Choose More info, then Run anyway.",
  macos: "For Apple Silicon Macs (M1 or newer) on macOS 11+. There is no Intel Mac build. Not code signed: right-click, Open.",
  linux: "A .deb for Debian and Ubuntu, or an AppImage for other 64-bit distributions.",
  other: "Pick your system on the download page.",
};

export async function cpuIsArm() {
  try {
    const uaData = navigator.userAgentData;
    if (!uaData || !uaData.getHighEntropyValues) return false;
    const v = await uaData.getHighEntropyValues(["architecture"]);
    return /arm/i.test(v.architecture || "");
  } catch (e) {
    return false;
  }
}

export function initPlatformHints() {
  const { os } = detectPlatform();

  $$("[data-mobile-note]").forEach((el) => { el.hidden = os !== "mobile"; });

  const labelEl = $("[data-os-label]");
  if (labelEl && OS_LABEL[os]) labelEl.textContent = `Download for ${OS_LABEL[os]}`;

  const notes = $$("[data-platform-note]");
  if (!notes.length || os === "mobile") return;
  const setNote = (text) => notes.forEach((n) => { n.textContent = text; });
  setNote(OS_NOTE[os] || OS_NOTE.other);

  // Transparent about missing architectures (Windows on ARM, Linux on ARM)
  if (os === "windows" || os === "linux") {
    cpuIsArm().then((arm) => {
      if (arm) setNote(`There is no native ${OS_LABEL[os]} ARM build yet. The x64 build may still run through emulation, but it is not tested.`);
    });
  }
}
