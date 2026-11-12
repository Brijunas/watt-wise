import flattenKeys from '@wattwise/i18n/testing'
import { beforeEach, describe, expect, it } from 'vitest'
import aboutEn from '@/features/about/locales/en.json'
import aboutLt from '@/features/about/locales/lt.json'
import accountsEn from '@/features/accounts/locales/en.json'
import accountsLt from '@/features/accounts/locales/lt.json'
import devicesEn from '@/features/devices/locales/en.json'
import devicesLt from '@/features/devices/locales/lt.json'
import en from '@/locales/en.json'
import lt from '@/locales/lt.json'
import { createAppI18n } from './i18n'

beforeEach(() => {
  localStorage.clear()
})

describe('app i18n', () => {
  it('has the same keys in both languages', () => {
    expect(flattenKeys(lt).sort()).toEqual(flattenKeys(en).sort())
  })

  it('has the same keys in both languages for the about namespace', () => {
    expect(flattenKeys(aboutLt).sort()).toEqual(flattenKeys(aboutEn).sort())
  })

  it('has the same keys in both languages for the accounts namespace', () => {
    expect(flattenKeys(accountsLt).sort()).toEqual(flattenKeys(accountsEn).sort())
  })

  it('has the same keys in both languages for the devices namespace', () => {
    expect(flattenKeys(devicesLt).sort()).toEqual(flattenKeys(devicesEn).sort())
  })

  it('resolves the app namespace in English and Lithuanian', async () => {
    const i18n = createAppI18n()
    await i18n.changeLanguage('en')
    expect(i18n.t('nav.home', { ns: 'app' })).toBe('Home')
    await i18n.changeLanguage('lt')
    expect(i18n.t('nav.home', { ns: 'app' })).toBe('Pradžia')
  })

  it('still resolves the shared namespaces', async () => {
    const i18n = createAppI18n()
    await i18n.changeLanguage('en')
    expect(i18n.t('language.label', { ns: 'common' })).toBe('Language')
    await i18n.changeLanguage('lt')
    expect(i18n.t('language.label', { ns: 'common' })).toBe('Kalba')
  })
})
