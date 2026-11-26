import basicSsl from '@vitejs/plugin-basic-ssl'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [vue(), basicSsl()],
  server: {
    host: '127.0.0.1',
    port: 5900,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5055',
        changeOrigin: true,
        secure: false,
        ws: true,
      },
    },
  },
})
