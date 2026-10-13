import { createInstance, type i18n } from 'i18next'
import { initReactI18next } from 'react-i18next'
import enCommon from './locales/en/common.json'
import ltCommon from './locales/lt/common.json'
import enShell from './locales/en/shell.json'
import ltShell from './locales/lt/shell.json'

export const languages = ['en', 'lt'] as const

export type Language = (typeof languages)[number]

export function createI18n(options: { lng?: Language } = {}): i18n {
  const instance = createInstance()
  // Init is synchronous because the resources are bundled, so t() works right away.
  void instance.use(initReactI18next).init({
    resources: {
      en: { common: enCommon, shell: enShell },
      lt: { common: ltCommon, shell: ltShell },
    },
    lng: options.lng ?? 'en',
    fallbackLng: 'en',
    defaultNS: 'common',
    ns: ['common', 'shell'],
    initAsync: false,
    interpolation: { escapeValue: false },
  })
  return instance
}
