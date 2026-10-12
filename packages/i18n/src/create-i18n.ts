import { createInstance, type i18n } from 'i18next'
import { initReactI18next } from 'react-i18next'
import enCommon from './locales/en/common.json'
import ltCommon from './locales/lt/common.json'

export const languages = ['en', 'lt'] as const

export type Language = (typeof languages)[number]

export function createI18n(options: { lng?: Language } = {}): i18n {
  const instance = createInstance()
  // Init is synchronous because the resources are bundled, so t() works right away.
  void instance.use(initReactI18next).init({
    resources: {
      en: { common: enCommon },
      lt: { common: ltCommon },
    },
    lng: options.lng ?? 'en',
    fallbackLng: 'en',
    defaultNS: 'common',
    ns: ['common'],
    initAsync: false,
    interpolation: { escapeValue: false },
  })
  return instance
}
