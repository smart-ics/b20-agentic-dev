import { fileURLToPath, URL } from 'node:url'

import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

// CAKRA - ICS Operational System
// Vite configuration for the Vue 3 + TypeScript SPA (Architecture §19.4).
//
// Build output:
//   `npm run build` emits production static assets into `dist/`.
//   Per Architecture §19.10 the `dist/` contents are ingested into
//   `src/backend/Cakra.Api/wwwroot/` during release packaging (P7-S39).
export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    sourcemap: false,
  },
  server: {
    port: 5173,
    strictPort: false,
    proxy: {
      // Development convenience only: forward API calls to the local
      // Cakra.Api host so the SPA can be developed independently.
      '/api': {
        target: 'https://localhost:7156',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
