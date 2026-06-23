import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  base: '/',
  plugins: [react()],
  build: {
    chunkSizeWarningLimit: 1600,
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (id.includes('node_modules/react') || id.includes('node_modules/react-dom') || id.includes('node_modules/react-router-dom')) {
            return 'react'
          }
          if (id.includes('node_modules/@ant-design/pro')) {
            return 'pro-components'
          }
          if (id.includes('node_modules/antd') || id.includes('node_modules/@ant-design')) {
            return 'antd'
          }
          return undefined
        },
      },
    },
  },
  server: {
    proxy: {
      '/api': 'http://127.0.0.1:9998',
      '/login': 'http://127.0.0.1:9998',
      '/logout': 'http://127.0.0.1:9998',
    },
  },
})
