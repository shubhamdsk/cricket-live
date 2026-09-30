import { fileURLToPath, URL } from 'node:url'

import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

/**
 * Refuses to build a bundle that cannot reach the API.
 *
 * Vite inlines `import.meta.env.VITE_*` at build time, so an unset variable does not fail — it
 * becomes the literal string `undefined` and every request goes to `undefined/api/...`. A
 * Lighthouse run on the production build found exactly that: three 404s and no data, from a build
 * that had reported success.
 *
 * There is no sensible default to fall back to. A localhost URL would be wrong in production and
 * a relative path assumes the API is served from the same origin, which is not this project's
 * deployment shape. So the only honest option is to stop.
 */
function apiBaseUrl(mode: string): string {
  // Third argument '' loads every variable rather than only the VITE_-prefixed ones, so a
  // misspelled name shows up as missing here instead of silently at runtime.
  const value = loadEnv(mode, process.cwd(), '').VITE_API_BASE_URL

  if (value === undefined || value.trim() === '') {
    throw new Error(
      `VITE_API_BASE_URL is not set for mode "${mode}". Vite would inline it as the string ` +
        '"undefined" and every API call would 404. Set it in the environment or in a .env file ' +
        'for this mode — see frontend/.env.example.',
    )
  }

  try {
    new URL(value)
  } catch {
    throw new Error(
      `VITE_API_BASE_URL is "${value}", which is not a valid absolute URL. It is concatenated ` +
        'with a path, so it must include the scheme and host and must not end in a slash.',
    )
  }

  if (value.endsWith('/')) {
    // `apiUrl` joins with a path that already starts with one, so a trailing slash here produces
    // a double slash. Harmless on most servers and confusing in every log.
    throw new Error(`VITE_API_BASE_URL is "${value}"; remove the trailing slash.`)
  }

  return value
}

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  apiBaseUrl(mode)

  return {
    plugins: [react(), tailwindcss()],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    server: {
      port: 5173,
    },
  }
})
