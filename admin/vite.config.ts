import { appUrl, runtimeConfig, serverAddress, vendorChunks } from '@wattwise/core/vite'
import { appearanceScript } from '@wattwise/ui/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import { VitePWA } from 'vite-plugin-pwa'
import pwaManifest from './src/app/pwa-manifest.ts'

// In Development, 1Password supplies APP_URL (see .env.op.development); without it (CI, tests,
// builds) Vite's defaults apply.
const listen = serverAddress(process.env.APP_URL)

export default defineConfig({
  server: listen,
  // `preview.port` doesn't inherit `server.port`.
  preview: listen,
  plugins: [
    react(),
    appearanceScript(),
    runtimeConfig({ frontendUrl: true }),
    appUrl(),
    VitePWA({
      // The user decides when to switch to a new version: `PwaUpdatePrompt` registers the worker.
      registerType: 'prompt',
      injectRegister: false,
      manifest: pwaManifest,
      workbox: {
        // The app shell: scripts, styles, the HTML, icons and the bundled Red Hat Display font files.
        globPatterns: ['**/*.{js,css,html,ico,png,svg,woff2}'],
        navigateFallback: 'index.html',
        navigateFallbackDenylist: [/^\/api\//],
        // `/config.js` is the deployment's own file (see the runtime config in technical.md), so it
        // must never be precached: a precached copy would outlive a change of the API URL.
        globIgnores: ['**/config.js'],
        cleanupOutdatedCaches: true,
        runtimeCaching: [
          {
            // Online starts get the current config; offline, or after 3 s without an answer, the last good one.
            urlPattern: ({ url, sameOrigin }) => sameOrigin && url.pathname === '/config.js',
            handler: 'NetworkFirst',
            options: { cacheName: 'runtime-config', networkTimeoutSeconds: 3 },
          },
          {
            // The API lives on its own origin, so match on the path alone. This rule makes sure no
            // API response is ever served from a cache.
            urlPattern: ({ url }) => url.pathname.startsWith('/api/'),
            handler: 'NetworkOnly',
          },
        ],
      },
      devOptions: { enabled: false },
    }),
  ],
  build: { rolldownOptions: { output: { codeSplitting: vendorChunks } } },
  resolve: { tsconfigPaths: true },
})
