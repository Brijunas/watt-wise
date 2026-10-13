import { createI18n } from '@wattwise/i18n'
import en from '@/locales/en.json'
import lt from '@/locales/lt.json'

declare module 'i18next' {
  interface ResourceNamespaceMap {
    app: typeof en
  }
}

export function createAppI18n() {
  return createI18n({ namespaces: { en: { app: en }, lt: { app: lt } } })
}

export const i18n = createAppI18n()
