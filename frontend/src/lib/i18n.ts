import { createI18n } from '@wattwise/i18n'
import aboutEn from '@/features/about/locales/en.json'
import aboutLt from '@/features/about/locales/lt.json'
import en from '@/locales/en.json'
import lt from '@/locales/lt.json'

declare module 'i18next' {
  interface ResourceNamespaceMap {
    app: typeof en
    about: typeof aboutEn
  }
}

export const createAppI18n = () => {
  return createI18n({
    namespaces: { en: { app: en, about: aboutEn }, lt: { app: lt, about: aboutLt } },
  })
}

export const i18n = createAppI18n()
