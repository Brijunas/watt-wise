import fc from 'fast-check'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { appearanceScript, appearanceScriptBody } from './vite'

function stubPrefersDark(dark: boolean) {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: query === '(prefers-color-scheme: dark)' && dark,
    media: query,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
  }))
}

function runScript() {
  // The script is MUI's own, meant to run as an inline <script>; there is no import to test it through.
  // eslint-disable-next-line @typescript-eslint/no-implied-eval, @typescript-eslint/no-unsafe-call
  new Function(appearanceScriptBody())()
}

function stubMedia(matching: string[]) {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: matching.includes(query),
    media: query,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
  }))
}

const html = document.documentElement

beforeEach(() => {
  localStorage.clear()
  html.className = ''
  html.removeAttribute('data-glow')
})

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('appearanceScript', () => {
  it('injects one script into the head', () => {
    const plugin = appearanceScript()
    expect(plugin.name).toBe('wattwise:appearance-script')
    const hook = plugin.transformIndexHtml
    if (typeof hook !== 'function') throw new Error('transformIndexHtml is not a function')
    const tags = hook.call({} as never, '', {} as never) as {
      tag: string
      children: string
      injectTo: string
    }[]
    expect(tags).toHaveLength(1)
    expect(tags[0]?.tag).toBe('script')
    expect(tags[0]?.injectTo).toBe('head')
    expect(tags[0]?.children).toContain('wattwise-mode')
  })

  it('applies a stored dark mode', () => {
    localStorage.setItem('wattwise-mode', 'dark')
    runScript()
    expect(html.classList.contains('dark')).toBe(true)
  })

  it('applies a stored light mode', () => {
    localStorage.setItem('wattwise-mode', 'light')
    stubPrefersDark(true)
    runScript()
    expect(html.classList.contains('light')).toBe(true)
    expect(html.classList.contains('dark')).toBe(false)
  })

  it('follows the OS when nothing is stored', () => {
    stubPrefersDark(true)
    runScript()
    expect(html.classList.contains('dark')).toBe(true)

    html.className = ''
    stubPrefersDark(false)
    runScript()
    expect(html.classList.contains('light')).toBe(true)
  })

  it('injects the mode and the glow rules in that one script', () => {
    const hook = appearanceScript().transformIndexHtml
    if (typeof hook !== 'function') throw new Error('transformIndexHtml is not a function')
    const tags = hook.call({} as never, '', {} as never) as { children: string }[]
    expect(tags[0]?.children).toContain('wattwise-mode')
    expect(tags[0]?.children).toContain('wattwise-glow')
  })

  describe('glow', () => {
    it('leaves data-glow unset when nothing is stored and the device has no preference', () => {
      stubMedia([])
      runScript()
      expect(html.getAttribute('data-glow')).toBeNull()
    })

    it('turns the glow off when nothing is stored and the device asks for more contrast', () => {
      stubMedia(['(prefers-contrast: more)'])
      runScript()
      expect(html.getAttribute('data-glow')).toBe('off')
    })

    it('turns the glow off when nothing is stored and forced colours are active', () => {
      stubMedia(['(forced-colors: active)'])
      runScript()
      expect(html.getAttribute('data-glow')).toBe('off')
    })

    it('turns the glow off for a stored off', () => {
      localStorage.setItem('wattwise-glow', 'off')
      stubMedia([])
      runScript()
      expect(html.getAttribute('data-glow')).toBe('off')
    })

    it('keeps the glow on for a stored on, even with more contrast', () => {
      localStorage.setItem('wattwise-glow', 'on')
      stubMedia(['(prefers-contrast: more)'])
      runScript()
      expect(html.getAttribute('data-glow')).toBeNull()
    })

    it('keeps the glow on for a stored on, even with forced colours', () => {
      localStorage.setItem('wattwise-glow', 'on')
      stubMedia(['(forced-colors: active)'])
      runScript()
      expect(html.getAttribute('data-glow')).toBeNull()
    })

    it('treats an unknown stored value as nothing stored: the glow stays on without a device preference', () => {
      localStorage.setItem('wattwise-glow', 'maybe')
      stubMedia([])
      runScript()
      expect(html.getAttribute('data-glow')).toBeNull()
    })

    it('treats an unknown stored value as nothing stored: the device turns it off', () => {
      localStorage.setItem('wattwise-glow', 'maybe')
      stubMedia(['(prefers-contrast: more)'])
      runScript()
      expect(html.getAttribute('data-glow')).toBe('off')
    })

    it('follows the stored on or off, and the device for anything else, for every input', () => {
      fc.assert(
        fc.property(
          fc.oneof(fc.constantFrom('on', 'off'), fc.string()),
          fc.boolean(),
          fc.boolean(),
          (stored, contrast, forced) => {
            localStorage.clear()
            html.removeAttribute('data-glow')
            localStorage.setItem('wattwise-glow', stored)
            stubMedia([
              ...(contrast ? ['(prefers-contrast: more)'] : []),
              ...(forced ? ['(forced-colors: active)'] : []),
            ])
            runScript()
            const expected =
              stored === 'on' ? null : stored === 'off' || contrast || forced ? 'off' : null
            return html.getAttribute('data-glow') === expected
          },
        ),
      )
    })

    it('does not throw when storage throws, and the device decides', () => {
      vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
        throw new Error('blocked')
      })
      stubMedia(['(forced-colors: active)'])
      expect(runScript).not.toThrow()
      expect(html.getAttribute('data-glow')).toBe('off')
    })
  })
})
