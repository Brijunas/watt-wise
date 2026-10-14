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

export const colorSchemeScriptBody = (): string => {
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

export const colorSchemeScript = (): Plugin => {
  return {
    name: 'wattwise:color-scheme-script',
    transformIndexHtml() {
      return [{ tag: 'script', children: colorSchemeScriptBody(), injectTo: 'head' }]
    },
  }
}
