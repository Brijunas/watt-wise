import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { colorSchemeScript, colorSchemeScriptBody } from './vite'

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
  new Function(colorSchemeScriptBody())()
}

const html = document.documentElement

beforeEach(() => {
  localStorage.clear()
  html.className = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('colorSchemeScript', () => {
  it('injects one script into the head', () => {
    const plugin = colorSchemeScript()
    expect(plugin.name).toBe('wattwise:color-scheme-script')
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
})
