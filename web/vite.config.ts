import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';

const target = 'http://127.0.0.1:8888';
export default defineConfig({
  plugins: [vue()],
  server: { port: 3000, proxy: Object.fromEntries(
    ['/identityservice', '/cameraservice', '/alertservice', '/notificationservice',
      '/systemservice', '/detect', '/ws'].map(path => [path, { target, ws: path === '/ws' }])
  ) },
});
