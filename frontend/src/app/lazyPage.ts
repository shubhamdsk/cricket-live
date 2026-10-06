import { lazy, type ComponentType } from 'react'

const RELOADED_AT = 'cricket-live:reloaded-for-new-version'

/** A reload this recent means the new version did not help, so the error is shown instead. */
const RELOAD_COOLDOWN_MS = 30_000

/**
 * `lazy`, plus one recovery: a page that fails to load is usually a page from the previous
 * release.
 *
 * Every deploy renames the page bundles, and a tab opened before it still asks for the old names,
 * which no longer exist. A reload fetches the new `index.html` and with it the new names, so that is
 * what happens, once. A second failure inside the cooldown is a real fault and reaches the error
 * boundary as before, which is what stops this from becoming a reload loop.
 */
export function lazyPage<T extends ComponentType>(load: () => Promise<T>) {
  return lazy(async () => {
    try {
      return { default: await load() }
    } catch (error) {
      if (reloadForNewVersion()) return new Promise<never>(() => {})
      throw error
    }
  })
}

export function reloadForNewVersion(
  now = Date.now(),
  storage: Storage = sessionStorage,
  reload = () => window.location.reload(),
): boolean {
  const last = Number(storage.getItem(RELOADED_AT))
  if (last && now - last < RELOAD_COOLDOWN_MS) return false

  storage.setItem(RELOADED_AT, String(now))
  reload()
  return true
}
