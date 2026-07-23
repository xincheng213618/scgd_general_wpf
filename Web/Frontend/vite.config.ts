import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  base: '/',
  plugins: [react()],
  server: {
    proxy: {
      '/api': 'http://127.0.0.1:9998',
      '/login': 'http://127.0.0.1:9998',
      '/logout': 'http://127.0.0.1:9998',
    },
  },
})
