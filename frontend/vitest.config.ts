import { fileURLToPath, URL } from 'node:url'

import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// Separate from vite.config.ts, which refuses to start without a real API address. Tests never
// reach the network, so a fixed placeholder is the honest value here.
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  test: {
    environment: 'jsdom',
    include: ['src/**/*.test.{ts,tsx}'],
    setupFiles: ['./src/test/setup.ts'],
    env: { VITE_API_BASE_URL: 'https://api.test' },
    restoreMocks: true,
    unstubGlobals: true,
  },
})
