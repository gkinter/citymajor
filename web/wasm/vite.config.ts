import { defineConfig } from 'vite';

export default defineConfig({
  root: '.',
  server: {
    port: 5174,
    headers: {
      // Required if the worker or WASM module uses SharedArrayBuffer (not used in this spike).
      // Kept here as documentation — enable when adding pthread / shared memory.
      // 'Cross-Origin-Opener-Policy': 'same-origin',
      // 'Cross-Origin-Embedder-Policy': 'require-corp',
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
});
