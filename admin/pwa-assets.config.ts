import { defineConfig, minimal2023Preset } from '@vite-pwa/assets-generator/config'

// The source is the favicon, and the icons are written next to it in public/. The logo already sits
// inside the maskable safe zone, so the maskable and Apple icons need no extra padding; the corners
// the rounded logo leaves transparent are filled with the app colour.
const background = '#3A4252'

export default defineConfig({
  preset: {
    ...minimal2023Preset,
    maskable: { ...minimal2023Preset.maskable, padding: 0, resizeOptions: { background } },
    apple: { ...minimal2023Preset.apple, padding: 0, resizeOptions: { background } },
  },
  images: ['public/favicon.svg'],
})
