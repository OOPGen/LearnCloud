import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      // Local API from `dotnet run --project src/LearnCloud.Api` (see docs/LOCAL_DEVELOPMENT.md).
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET || 'http://localhost:5080',
        changeOrigin: true
      }
    }
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
    // PERFORMANCE FIX C4: Code splitting + manual chunks for bundle size optimization
    rollupOptions: {
      output: {
        manualChunks: {
          // Vendor split - React, React Router DOM in separate chunk, cached longer
          vendor: ['react', 'react-dom', 'react-router-dom'],
          // UI components chunk
          ui: ['./src/components/ui/Button.jsx', './src/components/ui/Input.jsx', './src/components/ui/Dialog.jsx'],
        },
        // Chunk file names with hash for caching
        chunkFileNames: 'assets/[name]-[hash].js',
        entryFileNames: 'assets/[name]-[hash].js',
        assetFileNames: 'assets/[name]-[hash].[ext]'
      }
    },
    // Optimize chunk size
    chunkSizeWarningLimit: 500, // Warn if chunk >500KB
  }
})
