export function rewriteLegacyHashRoute(): void {
  const { hash, pathname, search } = window.location

  // Only a hash that is a route. A plain `#section` anchor is left alone, and so is a hash arriving
  // on a path that is already a real route — which happens exactly once, on the redirect below.
  if (!hash.startsWith('#/') || pathname !== '/') {
    return
  }

  const target = hash.slice(1)

  // `#/` on its own is the old home page, and rewriting it to `/` would be a no-op that still
  // rewrote history. Let it fall through to the same place by doing nothing but dropping the hash.
  window.history.replaceState(null, '', target === '/' ? `/${search}` : target)
}
