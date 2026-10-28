/*
 * sw.js - Offline service worker for FCMS Pro.
 * Caches application shell + assets. Network-first for /data/*.json.
 */

const CACHE_NAME = 'fcms-cache-34580f8fc7d3';

const SHELL_URLS = [
  './',
  './index.html',
  './download/',
  './download/index.html',
  './changelog/',
  './changelog/index.html',
  './docs/',
  './docs/index.html',
  './404.html',
  './assets/css/base.css',
  './assets/css/components.css',
  './assets/css/pages.css',
  './assets/css/mockups.css',
  './assets/js/main.js',
  './assets/js/release.js',
  './assets/js/theme.js',
  './assets/js/copy.js',
  './assets/js/os-detect.js',
  './assets/js/docs.js',
  './assets/js/search.js',
  './assets/fonts/inter-var.woff2',
  './assets/img/logo-64.png',
  './assets/img/apple-touch-icon.png',
  './assets/img/icon-192.png',
  './assets/img/icon-512.png',
  './assets/img/logo.svg',
  './favicon.ico',
  './site.webmanifest'
];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(SHELL_URLS)).then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(
        keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key))
      )
    ).then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (event) => {
  const req = event.request;
  if (req.method !== 'GET') return;

  const url = new URL(req.url);

  // Cross-origin (e.g. api.github.com): do not handle here
  if (url.origin !== self.location.origin) return;

  // Data requests (/data/*.json): network-first, fallback to cache
  if (url.pathname.includes('/data/')) {
    event.respondWith(
      fetch(req)
        .then((resp) => {
          if (resp && resp.ok) {
            const clone = resp.clone();
            caches.open(CACHE_NAME).then((cache) => cache.put(req, clone));
          }
          return resp;
        })
        .catch(() => caches.match(req))
    );
    return;
  }

  // Navigation requests: network-first or cache fallback
  if (req.mode === 'navigate') {
    event.respondWith(
      fetch(req)
        .then((resp) => {
          if (resp && resp.ok) {
            const clone = resp.clone();
            caches.open(CACHE_NAME).then((cache) => cache.put(req, clone));
          }
          return resp;
        })
        .catch(async () => {
          const match = await caches.match(req);
          if (match) return match;
          const alt = await caches.match('./index.html');
          return alt || caches.match('./404.html');
        })
    );
    return;
  }

  // Static assets: cache-first with network fallback
  event.respondWith(
    caches.match(req).then((cached) => {
      if (cached) return cached;
      return fetch(req).then((resp) => {
        if (resp && resp.ok) {
          const clone = resp.clone();
          caches.open(CACHE_NAME).then((cache) => cache.put(req, clone));
        }
        return resp;
      });
    })
  );
});
