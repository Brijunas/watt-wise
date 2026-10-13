import { createI18n } from '@wattwise/i18n'
import { cleanup, render, screen } from '@testing-library/react'
import { I18nextProvider } from 'react-i18next'
import { afterEach, describe, expect, it } from 'vitest'
import { PageLoading } from './page-loading'

afterEach(() => {
  cleanup()
})

describe('PageLoading', () => {
  it.each([
    ['en', 'Loading'],
    ['lt', 'Kraunama'],
  ] as const)('names the progress indicator in %s', (lng, name) => {
    render(
      <I18nextProvider i18n={createI18n({ lng })}>
        <PageLoading />
      </I18nextProvider>,
    )
    expect(screen.getByRole('progressbar', { name })).toBeTruthy()
  })
})
