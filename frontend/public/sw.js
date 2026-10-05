/*
 * Cricket Live service worker: makes the site installable and readable offline.
 *
 * Three rules, chosen so it can never make the site worse online:
 *   - Pages: network first, falling back to the last app shell this device loaded.
 *   - Hashed build assets: cache first. Their names change whenever their content does.
 *   - API reads: network first, falling back to the last answer this device saw. A fallback is
 *     stamped with `asOfUtc`, so lists say how old they are instead of passing an old score off
 *     as live. The live stream is never touched; EventSource reconnects on its own.
 *
 * Bump VERSION to drop every cache on the next visit.
 */
const VERSION = 'v1'
const SHELL = `shell-${VERSION}`
const ASSETS = `assets-${VERSION}`
const API = `api-${VERSION}`
const SAVED_AT = 'x-cricket-live-saved-at'

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches
      .open(SHELL)
      .then((cache) => cache.add('/'))
      .then(() => self.skipWaiting()),
  )
})

self.addEventListener('activate', (event) => {
  const current = new Set([SHELL, ASSETS, API])

  event.waitUntil(
    caches
      .keys()
      .then((names) =>
        Promise.all(
          names.filter((name) => !current.has(name)).map((name) => caches.delete(name)),
        ),
      )
      .then(() => self.clients.claim()),
  )
})

self.addEventListener('fetch', (event) => {
  const { request } = event
  if (request.method !== 'GET') return

  const url = new URL(request.url)
  const sameOrigin = url.origin === self.location.origin

  if (request.mode === 'navigate') {
    event.respondWith(page(request))
  } else if (sameOrigin && url.pathname.startsWith('/assets/')) {
    event.respondWith(asset(request))
  } else if (!sameOrigin && url.pathname.startsWith('/api/') && isCacheableApi(url.pathname)) {
    event.respondWith(api(request))
  }
})

function isCacheableApi(pathname) {
  return (
    !pathname.endsWith('/stream') &&
    !pathname.startsWith('/api/health') &&
    !pathname.startsWith('/api/crests')
  )
}

async function page(request) {
  try {
    const response = await fetch(request)
    if (response.ok) {
      const cache = await caches.open(SHELL)
      await cache.put('/', response.clone())
    }
    return response
  } catch {
    return (await caches.match('/', { cacheName: SHELL })) ?? Response.error()
  }
}

async function asset(request) {
  const cached = await caches.match(request, { cacheName: ASSETS })
  if (cached) return cached

  const response = await fetch(request)
  if (response.ok) {
    const cache = await caches.open(ASSETS)
    await cache.put(request, response.clone())
  }
  return response
}

async function api(request) {
  try {
    const response = await fetch(request)
    if (response.ok) {
      const body = await response.clone().text()
      const headers = new Headers(response.headers)
      headers.set(SAVED_AT, new Date().toISOString())
      const cache = await caches.open(API)
      await cache.put(request, new Response(body, { status: response.status, headers }))
    }
    return response
  } catch {
    const cached = await caches.match(request, { cacheName: API })
    return cached ? stamped(cached) : Response.error()
  }
}

/** The saved answer, with the time it was saved as `asOfUtc` unless the API already set one. */
async function stamped(cached) {
  const savedAt = cached.headers.get(SAVED_AT)

  try {
    const envelope = await cached.clone().json()
    if (savedAt && envelope && typeof envelope === 'object' && !envelope.asOfUtc) {
      envelope.asOfUtc = savedAt
    }
    return new Response(JSON.stringify(envelope), { status: 200, headers: cached.headers })
  } catch {
    return cached
  }
}
