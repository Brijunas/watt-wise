import type { i18n as I18nInstance } from 'i18next'
import type { ReactNode } from 'react'
import { I18nextProvider } from 'react-i18next'
import { Provider } from 'react-redux'
import { AppThemeProvider } from '@wattwise/ui'
import { i18n as appI18n } from '@/lib/i18n'
import type { AppStore } from '@/stores/create-app-store'

const AppProvider = ({
  children,
  store,
  i18n = appI18n,
}: {
  children: ReactNode
  store: AppStore
  i18n?: I18nInstance
}) => {
  return (
    <Provider store={store}>
      <I18nextProvider i18n={i18n}>
        <AppThemeProvider>{children}</AppThemeProvider>
      </I18nextProvider>
    </Provider>
  )
}

export default AppProvider
