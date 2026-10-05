/**
 * Registers `public/sw.js` in production builds only. A worker in development would serve stale
 * modules over Vite's hot reload, which is a confusing way to lose an afternoon.
 */
export function registerServiceWorker(): void {
  if (!import.meta.env.PROD || !('serviceWorker' in navigator)) return

  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch(() => {
      // Offline support is a bonus. A browser that refuses the worker still gets the whole site.
    })
  })
}
