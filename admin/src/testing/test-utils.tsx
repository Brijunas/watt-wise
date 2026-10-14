import { render } from '@testing-library/react'
import type { ReactElement } from 'react'
import AppProvider from '@/app/provider'
import { createAppI18n } from '@/lib/i18n'
import createAppStore from '@/stores/create-app-store'

const renderWithProviders = (ui: ReactElement) => {
  return render(
    <AppProvider store={createAppStore()} i18n={createAppI18n()}>
      {ui}
    </AppProvider>,
  )
}

export default renderWithProviders
