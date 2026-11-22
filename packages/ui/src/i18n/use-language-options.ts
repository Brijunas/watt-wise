import { languageNames, languages, type Language } from '@wattwise/i18n'
import { useTranslation } from 'react-i18next'

export interface LanguageOption {
  key: Language
  selected: boolean
  content: string
  lang: Language
}

// The languages as menu options, with the current one selected; shared by the language switch and
// the account menu, which holds the language below 600px.
const useLanguageOptions = () => {
  const { i18n } = useTranslation('common')

  const options: readonly LanguageOption[] = languages.map((language) => ({
    key: language,
    selected: i18n.resolvedLanguage === language,
    content: languageNames[language],
    lang: language,
  }))
  const select = (language: Language) => {
    void i18n.changeLanguage(language)
  }

  return { options, select, current: i18n.resolvedLanguage }
}

export default useLanguageOptions
