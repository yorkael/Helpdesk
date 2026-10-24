import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    // Vitest replaces CSS imports with an empty string, ?raw included; the contrast test needs tokens.css as written.
    css: { include: [/\/src\/styles\/tokens\.css(\?|$)/] },
  },
})
