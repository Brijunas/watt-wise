import { cleanup, render, screen } from '@testing-library/react'
import { createI18n } from '@wattwise/i18n'
import { I18nextProvider } from 'react-i18next'
import { afterEach, describe, expect, it } from 'vitest'
import useErrorText from './use-error-text'

afterEach(cleanup)

const Probe = ({ error }: { error: unknown }) => {
  const errorText = useErrorText()
  return <p>{errorText(error)}</p>
}

const renderProbe = (error: unknown, lng: 'en' | 'lt' = 'en') =>
  render(
    <I18nextProvider i18n={createI18n({ lng })}>
      <Probe error={error} />
    </I18nextProvider>,
  )

describe('useErrorText', () => {
  it('translates a known ProblemDetails code', () => {
    renderProbe({ status: 401, data: { code: 'Accounts.InvalidCredentials' } })

    expect(screen.getByText('The email or password is wrong.')).toBeTruthy()
  })

  it('uses the current language', () => {
    renderProbe({ status: 401, data: { code: 'Accounts.InvalidCredentials' } }, 'lt')

    expect(screen.getByText('El. paštas arba slaptažodis neteisingas.')).toBeTruthy()
  })

  it('shows the network text for a failed fetch', () => {
    renderProbe({ status: 'FETCH_ERROR', error: 'TypeError: Failed to fetch' })

    expect(
      screen.getByText('Could not reach the server. Check your connection and try again.'),
    ).toBeTruthy()
  })
})
