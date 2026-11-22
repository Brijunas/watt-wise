import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import InitColorSchemeScript from '@mui/material/InitColorSchemeScript'
import type { Plugin } from 'vite'
// Vite doesn't bundle this file into the config: Node loads it as is, and Node needs the extension.
import {
  colorSchemeAttribute,
  colorSchemeStorageKey,
  defaultMode,
  modeStorageKey,
} from './theme/color-scheme.ts'
import { glowScriptBody } from './theme/glow.ts'

const colorSchemeScriptBody = (): string => {
  const markup = renderToStaticMarkup(
    createElement(InitColorSchemeScript, {
      attribute: colorSchemeAttribute,
      defaultMode,
      modeStorageKey,
      colorSchemeStorageKey,
    }),
  )
  const match = /^<script[^>]*>([\s\S]*)<\/script>$/.exec(markup)
  const body = match?.[1]
  if (!body) {
    throw new Error(
      `Could not extract the script from MUI's InitColorSchemeScript output: ${markup}`,
    )
  }
  return body
}

// One inline script applies the stored mode and glow (security.md, "Inline script"): its hash is
// the one the apps' policy lists.
export const appearanceScriptBody = (): string => `${colorSchemeScriptBody()}\n${glowScriptBody()}`

export const appearanceScript = (): Plugin => {
  return {
    name: 'wattwise:appearance-script',
    transformIndexHtml() {
      return [{ tag: 'script', children: appearanceScriptBody(), injectTo: 'head' }]
    },
  }
}
