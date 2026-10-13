import TranslateIcon from '@mui/icons-material/Translate'
import { languageNames, languages } from '@wattwise/i18n'
import { useTranslation } from 'react-i18next'
import { IconMenu } from '../menu/icon-menu'

export function LanguageSwitch() {
  const { t, i18n } = useTranslation('common')

  return (
    <IconMenu
      label={t('language.label')}
      icon={<TranslateIcon />}
      options={languages.map((language) => ({
        key: language,
        selected: i18n.resolvedLanguage === language,
        content: languageNames[language],
        lang: language,
      }))}
      onSelect={(language) => {
        void i18n.changeLanguage(language)
      }}
    />
  )
}
