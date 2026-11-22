import type { Theme } from '@mui/material/styles'
import { cleanup, render } from '@testing-library/react'
import type { ReactElement } from 'react'
import AppThemeProvider from '../theme/app-theme-provider'
import { theme } from '../theme/theme'

export type ColorMode = 'light' | 'dark'

export const colorModes: readonly ColorMode[] = ['light', 'dark']

// Renders inside the real theme provider; the mode is read from the stored one on mount, like
// mode-switch.test.tsx does.
export const renderThemed = (ui: ReactElement, mode: ColorMode = 'light') => {
  localStorage.setItem('wattwise-mode', mode)
  document.documentElement.className = ''
  return render(<AppThemeProvider>{ui}</AppThemeProvider>)
}

export const resetThemeState = () => {
  cleanup()
  localStorage.clear()
  document.documentElement.className = ''
  delete document.documentElement.dataset.glow
}

type Style = Record<string, unknown>

// Emotion applies a list of style objects in order, so a nested selector present in two of them
// keeps the declarations of both.
const mergeStyles = (base: Style, extra: Style): Style => {
  const merged: Style = { ...base }
  for (const [key, value] of Object.entries(extra)) {
    const existing = merged[key]
    const bothObjects =
      typeof value === 'object' &&
      value !== null &&
      !Array.isArray(value) &&
      typeof existing === 'object' &&
      existing !== null &&
      !Array.isArray(existing)
    merged[key] = bothObjects ? mergeStyles(existing as Style, value as Style) : value
  }
  return merged
}

// The style object a component's `styleOverrides.<slot>` (root by default) yields for the theme.
export const rootStyle = (component: string, slot = 'root'): Style => {
  const components = theme.components as Record<
    string,
    { styleOverrides?: Record<string, unknown> } | undefined
  >
  const root = components[component]?.styleOverrides?.[slot]
  // `enhanceHighContrast` turns a component's overrides into [ours, its forced-colours rules].
  const parts = Array.isArray(root) ? (root as unknown[]) : [root]
  return parts.reduce<Style>(
    (merged, part) =>
      mergeStyles(
        merged,
        typeof part === 'function'
          ? (part as (props: { theme: Theme }) => Style)({ theme })
          : (part as Style),
      ),
    {},
  )
}

// A nested object at `path` of keys; throws with the missing key.
export const at = (style: Style, ...path: string[]): Style => {
  let current: unknown = style
  for (const key of path) {
    if (typeof current !== 'object' || current === null || !(key in current)) {
      throw new Error('Missing style key ' + key)
    }
    current = (current as Style)[key]
  }
  return current as Style
}

// The `style` of the entry of `variants` whose `props` equal `props`.
export const variantStyle = (style: Style, props: Record<string, unknown>): Style => {
  const variants = style.variants as { props: Record<string, unknown>; style: Style }[]
  const found = variants.find((v) => JSON.stringify(v.props) === JSON.stringify(props))
  if (!found) throw new Error('Missing variant ' + JSON.stringify(props))
  return found.style
}

// Everything Emotion has put in <style> tags so far.
export const emittedCss = () =>
  Array.from(document.querySelectorAll('style'))
    .map((tag) => tag.textContent)
    .join('\n')
