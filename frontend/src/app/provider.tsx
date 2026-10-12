import type { i18n as I18nInstance } from 'i18next'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { AppThemeProvider } from '@wattwise/ui'
import { i18n as appI18n } from '@/lib/i18n'

export function AppProvider({
  children,
  i18n = appI18n,
}: {
  children: ReactNode
  i18n?: I18nInstance
}) {
  return (
    <I18nextProvider i18n={i18n}>
      <AppThemeProvider>{children}</AppThemeProvider>
    </I18nextProvider>
  )
}
