/*
 * sw.js - Offline service worker for FCMS Pro.
 * Caches application shell + assets. Network-first for /data/*.json.
 */

const CACHE_NAME = 'fcms-cache-{{build_id}}';

const SHELL_URLS = [
  '{{root}}',
  '{{root}}index.html',
  '{{root}}download/',
  '{{root}}download/index.html',
  '{{root}}changelog/',
  '{{root}}changelog/index.html',
  '{{root}}docs/',
  '{{root}}docs/index.html',
  '{{root}}404.html',
  '{{root}}assets/css/base.css',
  '{{root}}assets/css/components.css',
  '{{root}}assets/css/pages.css',
  '{{root}}assets/css/mockups.css',
  '{{root}}assets/js/main.js',
  '{{root}}assets/js/release.js',
  '{{root}}assets/js/theme.js',
  '{{root}}assets/js/copy.js',
  '{{root}}assets/js/os-detect.js',
  '{{root}}assets/js/docs.js',
  '{{root}}assets/js/search.js',
  '{{root}}assets/fonts/inter-var.woff2',
  '{{root}}assets/img/logo-64.png',
  '{{root}}assets/img/apple-touch-icon.png',
  '{{root}}assets/img/icon-192.png',
  '{{root}}assets/img/icon-512.png',
  '{{root}}assets/img/logo.svg',
  '{{root}}favicon.ico',
  '{{root}}site.webmanifest'
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
          const alt = await caches.match('{{root}}index.html');
          return alt || caches.match('{{root}}404.html');
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
