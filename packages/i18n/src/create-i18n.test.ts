import { describe, expect, it } from 'vitest'
import enCommon from './locales/en/common.json'
import ltCommon from './locales/lt/common.json'
import { createI18n } from './create-i18n'

function flattenKeys(value: object, prefix = ''): string[] {
  return Object.entries(value).flatMap(([key, child]: [string, unknown]) => {
    const path = prefix === '' ? key : `${prefix}.${key}`
    return typeof child === 'object' && child !== null ? flattenKeys(child, path) : [path]
  })
}

describe('createI18n', () => {
  it('defaults to English and resolves the appearance keys', () => {
    const i18n = createI18n()

    expect(i18n.language).toBe('en')
    expect(i18n.t('appearance.label')).toBe('Appearance')
    expect(i18n.t('appearance.light')).toBe('Light')
    expect(i18n.t('appearance.dark')).toBe('Dark')
    expect(i18n.t('appearance.auto')).toBe('Auto')
  })

  it('resolves the Lithuanian texts', () => {
    const i18n = createI18n({ lng: 'lt' })

    expect(i18n.t('appearance.label')).toBe('Išvaizda')
    expect(i18n.t('appearance.light')).toBe('Šviesi')
    expect(i18n.t('appearance.dark')).toBe('Tamsi')
    expect(i18n.t('appearance.auto')).toBe('Automatinė')
  })

  it('returns independent instances', async () => {
    const first = createI18n()
    const second = createI18n()

    await first.changeLanguage('lt')

    expect(first).not.toBe(second)
    expect(first.t('appearance.label')).toBe('Išvaizda')
    expect(second.t('appearance.label')).toBe('Appearance')
  })
})

describe('locale files', () => {
  it('have the same keys in English and Lithuanian', () => {
    expect(flattenKeys(ltCommon).sort()).toEqual(flattenKeys(enCommon).sort())
  })
})
