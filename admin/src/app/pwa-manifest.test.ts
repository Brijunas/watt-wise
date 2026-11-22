import { describe, expect, it } from 'vitest'
import pwaManifest from './pwa-manifest'

describe('admin PWA manifest', () => {
  it('uses the dark page background for the theme and background colours', () => {
    expect([pwaManifest.theme_color, pwaManifest.background_color]).toEqual(['#05060E', '#05060E'])
  })

  it('keeps the installability fields', () => {
    expect({
      name: pwaManifest.name,
      short_name: pwaManifest.short_name,
      start_url: pwaManifest.start_url,
      display: pwaManifest.display,
    }).toEqual({
      name: 'Watt-Wise Admin',
      short_name: 'WW Admin',
      start_url: '/',
      display: 'standalone',
    })
  })

  it('has 192 and 512 icons and a maskable 512 icon', () => {
    expect(pwaManifest.icons).toEqual(
      expect.arrayContaining([
        { src: 'pwa-192x192.png', sizes: '192x192', type: 'image/png' },
        { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'any' },
        {
          src: 'maskable-icon-512x512.png',
          sizes: '512x512',
          type: 'image/png',
          purpose: 'maskable',
        },
      ]) as unknown,
    )
  })
})
