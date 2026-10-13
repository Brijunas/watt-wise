import { render } from '@testing-library/react'
import type { ReactElement } from 'react'
import { AppProvider } from '@/app/provider'
import { createAppI18n } from '@/lib/i18n'

export function renderWithProviders(ui: ReactElement) {
  return render(<AppProvider i18n={createAppI18n()}>{ui}</AppProvider>)
}
