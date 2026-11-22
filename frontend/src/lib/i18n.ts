import { createI18n } from '@wattwise/i18n'
import aboutEn from '@/features/about/locales/en.json'
import aboutLt from '@/features/about/locales/lt.json'
import accountsEn from '@/features/accounts/locales/en.json'
import accountsLt from '@/features/accounts/locales/lt.json'
import devicesEn from '@/features/devices/locales/en.json'
import devicesLt from '@/features/devices/locales/lt.json'
import settingsEn from '@/features/settings/locales/en.json'
import settingsLt from '@/features/settings/locales/lt.json'
import en from '@/locales/en.json'
import lt from '@/locales/lt.json'

declare module 'i18next' {
  interface ResourceNamespaceMap {
    app: typeof en
    about: typeof aboutEn
    accounts: typeof accountsEn
    devices: typeof devicesEn
    settings: typeof settingsEn
  }
}

export const createAppI18n = () => {
  return createI18n({
    namespaces: {
      en: {
        app: en,
        about: aboutEn,
        accounts: accountsEn,
        devices: devicesEn,
        settings: settingsEn,
      },
      lt: {
        app: lt,
        about: aboutLt,
        accounts: accountsLt,
        devices: devicesLt,
        settings: settingsLt,
      },
    },
  })
}

export const i18n = createAppI18n()
