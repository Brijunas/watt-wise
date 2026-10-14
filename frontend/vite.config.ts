import { colorSchemeScript } from '@wattwise/ui/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import { VitePWA } from 'vite-plugin-pwa'

export default defineConfig({
  plugins: [
    react(),
    colorSchemeScript(),
    VitePWA({
      // The user decides when to switch to a new version: `PwaUpdatePrompt` registers the worker.
      registerType: 'prompt',
      injectRegister: false,
      manifest: {
        name: 'Watt-Wise',
        short_name: 'Watt-Wise',
        description:
          'Supraskite ir sumažinkite elektros sąskaitas pagal valandines kainas ir išmaniuosius planus.',
        lang: 'lt',
        start_url: '/',
        scope: '/',
        display: 'standalone',
        theme_color: '#5235D8',
        // The light theme's page background (packages/ui/src/theme/theme.ts).
        background_color: '#F8F7FD',
        icons: [
          { src: 'pwa-64x64.png', sizes: '64x64', type: 'image/png' },
          { src: 'pwa-192x192.png', sizes: '192x192', type: 'image/png' },
          { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'any' },
          {
            src: 'maskable-icon-512x512.png',
            sizes: '512x512',
            type: 'image/png',
            purpose: 'maskable',
          },
        ],
      },
      workbox: {
        // The app shell: scripts, styles, the HTML, icons and the bundled Roboto font files.
        globPatterns: ['**/*.{js,css,html,ico,png,svg,woff2}'],
        navigateFallback: 'index.html',
        navigateFallbackDenylist: [/^\/api\//],
        cleanupOutdatedCaches: true,
        runtimeCaching: [
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
  resolve: { tsconfigPaths: true },
})
