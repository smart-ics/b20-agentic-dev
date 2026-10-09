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
      // Forward API calls to local IIS backend at http://localhost:8084
      '/api': {
        target: 'http://localhost:8084',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
