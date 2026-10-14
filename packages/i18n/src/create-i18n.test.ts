import type { i18n } from 'i18next'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import enCommon from './locales/en/common.json'
import ltCommon from './locales/lt/common.json'
import enShell from './locales/en/shell.json'
import ltShell from './locales/lt/shell.json'
import { createI18n, languageNames, languageStorageKey } from './create-i18n'
import flattenKeys from './testing'

const extraNamespaces = {
  en: { demo: { greeting: 'Hello', onlyEnglish: 'Only English' } },
  lt: { demo: { greeting: 'Labas' } },
}

// The demo namespace is not registered in the typed resources, so its keys are untyped here.
function translate(i18n: i18n, key: string): string {
  return (i18n.t as (key: string) => string)(key)
}

function stubBrowserLanguages(languages: string[]) {
  vi.spyOn(navigator, 'languages', 'get').mockReturnValue(languages)
  vi.spyOn(navigator, 'language', 'get').mockReturnValue(languages[0] ?? '')
}

beforeEach(() => {
  vi.restoreAllMocks()
  localStorage.clear()
  document.documentElement.removeAttribute('lang')
})

describe('createI18n', () => {
  it('uses English for an English browser and resolves the appearance keys', () => {
    stubBrowserLanguages(['en-US'])
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

  it('resolves the shell namespace in both languages', () => {
    expect(createI18n().t('navigation.label', { ns: 'shell' })).toBe('Main navigation')
    expect(createI18n({ lng: 'lt' }).t('navigation.label', { ns: 'shell' })).toBe(
      'Pagrindinė navigacija',
    )
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

describe('language detection', () => {
  it('prefers the stored language over the browser language', () => {
    localStorage.setItem(languageStorageKey, 'lt')
    stubBrowserLanguages(['en-US'])

    expect(createI18n().language).toBe('lt')
  })

  it.each([
    [['lt-LT'], 'lt'],
    [['en-GB'], 'en'],
    [['ru', 'pl'], 'lt'],
    [['ru', 'en'], 'en'],
  ])('resolves the browser languages %j to %s', (browser, expected) => {
    stubBrowserLanguages(browser)

    expect(createI18n().language).toBe(expected)
  })

  it('ignores an unsupported stored language', () => {
    localStorage.setItem(languageStorageKey, 'ru')
    stubBrowserLanguages(['lt-LT'])

    expect(createI18n().language).toBe('lt')
  })

  it('lets an explicit language win over detection', () => {
    localStorage.setItem(languageStorageKey, 'en')

    expect(createI18n({ lng: 'lt' }).language).toBe('lt')
  })

  it('sets the html lang at init', () => {
    createI18n({ lng: 'lt' })

    expect(document.documentElement.lang).toBe('lt')
  })

  it('stores the language and updates the html lang when it changes', async () => {
    const i18n = createI18n()

    await i18n.changeLanguage('lt')

    expect(localStorage.getItem(languageStorageKey)).toBe('lt')
    expect(document.documentElement.lang).toBe('lt')
  })
})

describe('translations', () => {
  it('resolves the language keys and names', () => {
    expect(createI18n().t('language.label')).toBe('Language')
    expect(createI18n({ lng: 'lt' }).t('language.label')).toBe('Kalba')
    expect(languageNames).toEqual({ en: 'English', lt: 'Lietuvių' })
  })

  it('resolves extra namespaces in both languages', () => {
    expect(translate(createI18n({ namespaces: extraNamespaces }), 'demo:greeting')).toBe('Hello')
    expect(translate(createI18n({ lng: 'lt', namespaces: extraNamespaces }), 'demo:greeting')).toBe(
      'Labas',
    )
  })

  it('rejects an app namespace that replaces a shared one', () => {
    expect(() => createI18n({ namespaces: { en: { shell: {} }, lt: { shell: {} } } })).toThrow(
      'shared namespace',
    )
  })

  it('rejects languages that register different app namespaces', () => {
    expect(() => createI18n({ namespaces: { en: { demo: {} }, lt: {} } })).toThrow(
      'the same namespaces',
    )
  })

  it('falls back to English for a key missing in Lithuanian', () => {
    const i18n = createI18n({ lng: 'lt', namespaces: extraNamespaces })

    expect(translate(i18n, 'demo:onlyEnglish')).toBe('Only English')
  })

  it('types the keys', () => {
    const i18n = createI18n()

    // @ts-expect-error unknown keys are a type error
    i18n.t('does.not.exist')
  })
})

describe('locale files', () => {
  it.each([
    ['common', enCommon, ltCommon],
    ['shell', enShell, ltShell],
  ])('have the same %s keys in English and Lithuanian', (_name, en, lt) => {
    expect(flattenKeys(lt).sort()).toEqual(flattenKeys(en).sort())
  })
})
