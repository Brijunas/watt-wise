import type { ManifestOptions } from 'vite-plugin-pwa'

const pwaManifest: Partial<ManifestOptions> = {
  name: 'Watt-Wise Admin',
  short_name: 'WW Admin',
  description: 'Watt-Wise administravimas.',
  lang: 'lt',
  start_url: '/',
  scope: '/',
  display: 'standalone',
  theme_color: '#05060E',
  background_color: '#05060E',
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
}

export default pwaManifest
