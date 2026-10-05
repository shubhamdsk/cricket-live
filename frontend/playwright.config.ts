import { defineConfig, devices } from '@playwright/test'

// The built site, served by `vite preview`, with every API call answered by the test itself. The
// address is never resolved: requests to it are intercepted before they leave the browser, so
// this suite spends no provider allowance and needs no backend.
export const apiBaseUrl = 'http://api.e2e.test'

// Not Vite's default preview port, and never reused: a preview left running from a normal build
// points at the real API, and reusing it would test that instead.
const port = 4317

export default defineConfig({
  testDir: './e2e',
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    baseURL: `http://localhost:${port}`,
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'phone', use: { ...devices['Pixel 7'] } }],
  webServer: {
    command: `npm run build && npm run preview -- --port ${port} --strictPort`,
    url: `http://localhost:${port}`,
    reuseExistingServer: false,
    timeout: 180_000,
    env: { VITE_API_BASE_URL: apiBaseUrl },
  },
})
