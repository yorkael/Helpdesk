import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  // Same origin as the nginx proxy in Docker, so the API needs no CORS. Port from the API's "http" launch profile.
  server: { proxy: { '/api': 'http://localhost:5038' } },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    // Vitest replaces CSS imports with an empty string, ?raw included; the contrast test needs tokens.css as written.
    css: { include: [/\/src\/styles\/tokens\.css(\?|$)/] },
  },
})
