import { createInstance, type i18n } from 'i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import { initReactI18next } from 'react-i18next'
import enCommon from './locales/en/common.json'
import ltCommon from './locales/lt/common.json'
import enShell from './locales/en/shell.json'
import ltShell from './locales/lt/shell.json'

declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'common'
  }
  interface ResourceNamespaceMap {
    common: typeof enCommon
    shell: typeof enShell
  }
}

export const languages = ['en', 'lt'] as const

export type Language = (typeof languages)[number]

export const languageStorageKey = 'wattwise-language'

// Endonyms: each language is named in itself and never translated.
export const languageNames: Record<Language, string> = { en: 'English', lt: 'Lietuvių' }

export interface CreateI18nOptions {
  lng?: Language
  namespaces?: Record<Language, Record<string, object>>
}

const sharedNamespaces = ['common', 'shell']

export const createI18n = (options: CreateI18nOptions = {}): i18n => {
  const extra = options.namespaces
  const enNames = Object.keys(extra?.en ?? {})
  const ltNames = Object.keys(extra?.lt ?? {})
  if ([...enNames, ...ltNames].some((name) => sharedNamespaces.includes(name))) {
    throw new Error(
      `App namespaces cannot reuse a shared namespace: ${sharedNamespaces.join(', ')}`,
    )
  }
  if (enNames.length !== ltNames.length || enNames.some((name) => !ltNames.includes(name))) {
    throw new Error('Both languages must register the same namespaces')
  }
  const instance = createInstance()

  // The detector returns every code it finds and i18next takes the first supported one, so this
  // last-resort detector makes Lithuanian win only when neither the stored value nor the
  // browser's languages contain a supported language.
  const detector = new LanguageDetector()
  detector.addDetector({ name: 'marketDefault', lookup: () => 'lt' })

  instance.on('languageChanged', (lng) => {
    document.documentElement.lang = lng
  })

  // Init is synchronous because the resources are bundled, so t() works right away.
  void instance
    .use(detector)
    .use(initReactI18next)
    .init({
      resources: {
        en: { common: enCommon, shell: enShell, ...extra?.en },
        lt: { common: ltCommon, shell: ltShell, ...extra?.lt },
      },
      lng: options.lng,
      supportedLngs: [...languages],
      load: 'languageOnly',
      fallbackLng: 'en',
      defaultNS: 'common',
      ns: [...sharedNamespaces, ...enNames],
      detection: {
        order: ['localStorage', 'navigator', 'marketDefault'],
        caches: ['localStorage'],
        lookupLocalStorage: languageStorageKey,
        convertDetectedLanguage: (lng: string) => lng.split('-')[0] ?? lng,
      },
      initAsync: false,
      interpolation: { escapeValue: false },
    })
  return instance
}
