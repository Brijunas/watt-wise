import { createI18n } from '@wattwise/i18n'
import accountsEn from '@/features/accounts/locales/en.json'
import accountsLt from '@/features/accounts/locales/lt.json'
import aboutEn from '@/features/about/locales/en.json'
import aboutLt from '@/features/about/locales/lt.json'
import en from '@/locales/en.json'
import lt from '@/locales/lt.json'

declare module 'i18next' {
  interface ResourceNamespaceMap {
    app: typeof en
    about: typeof aboutEn
    accounts: typeof accountsEn
  }
}

export const createAppI18n = () => {
  return createI18n({
    namespaces: {
      en: { app: en, about: aboutEn, accounts: accountsEn },
      lt: { app: lt, about: aboutLt, accounts: accountsLt },
    },
  })
}

export const i18n = createAppI18n()
