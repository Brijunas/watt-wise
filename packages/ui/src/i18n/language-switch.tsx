import TranslateIcon from '@mui/icons-material/TranslateRounded'
import { styled, useThemeProps } from '@mui/material/styles'
import { languageNames, languages, type Language } from '@wattwise/i18n'
import { useTranslation } from 'react-i18next'
import IconMenu from '../menu/icon-menu'

export type LanguageSwitchProps = Record<string, never>

export type LanguageSwitchClassKey = 'root'

const name = 'WwLanguageSwitch'

const LanguageSwitchRoot = styled(IconMenu<Language>, { name, slot: 'root' })<{
  ownerState: LanguageSwitchProps
}>({})

const LanguageSwitch = (inProps: LanguageSwitchProps) => {
  const props = useThemeProps({ props: inProps, name })
  const { t, i18n } = useTranslation('common')

  return (
    <LanguageSwitchRoot
      ownerState={props}
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

export default LanguageSwitch
