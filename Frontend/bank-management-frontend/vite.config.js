import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Proxy /api calls to the ASP.NET Core backend in development.
    // This avoids CORS and certificate issues when running `npm run dev`.
    proxy: {
      '/api': {
        target: 'http://localhost:5121',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
