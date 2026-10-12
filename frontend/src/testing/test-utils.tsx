import { render } from '@testing-library/react'
import type { ReactElement } from 'react'
import { createI18n } from '@wattwise/i18n'
import { AppProvider } from '@/app/provider'

export function renderWithProviders(ui: ReactElement) {
  return render(<AppProvider i18n={createI18n()}>{ui}</AppProvider>)
}
