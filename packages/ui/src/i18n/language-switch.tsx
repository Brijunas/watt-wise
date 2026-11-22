import { styled, useThemeProps } from '@mui/material/styles'
import type { Language } from '@wattwise/i18n'
import { useTranslation } from 'react-i18next'
import IconMenu from '../menu/icon-menu'
import useLanguageOptions from './use-language-options'

export type LanguageSwitchProps = Record<string, never>

export type LanguageSwitchClassKey = 'root' | 'code'

const name = 'WwLanguageSwitch'

const LanguageSwitchRoot = styled(IconMenu<Language>, { name, slot: 'root' })<{
  ownerState: LanguageSwitchProps
}>({})

const LanguageSwitchCode = styled('span', { name, slot: 'code' })<{
  ownerState: LanguageSwitchProps
}>(({ theme }) => ({
  ...theme.typography.body2,
  fontWeight: theme.typography.fontWeightBold,
  // No theme token is a unitless 1; it keeps the two letters centred in the button.
  lineHeight: 1,
}))

// The button shows the current language's code (EN, LT) and opens the language menu. Its name
// holds the code, so the visible text is part of the accessible name (WCAG 2.5.3).
const LanguageSwitch = (inProps: LanguageSwitchProps) => {
  const props = useThemeProps({ props: inProps, name })
  const { t } = useTranslation('common')
  const { options, select, current } = useLanguageOptions()
  const code = (current ?? '').toUpperCase()

  return (
    <LanguageSwitchRoot
      ownerState={props}
      label={t('language.current', { code })}
      icon={<LanguageSwitchCode ownerState={props}>{code}</LanguageSwitchCode>}
      options={options}
      onSelect={select}
    />
  )
}

export default LanguageSwitch
