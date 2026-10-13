import { createI18n } from '@wattwise/i18n'
import { cleanup, render, screen } from '@testing-library/react'
import type { AnchorHTMLAttributes } from 'react'
import { I18nextProvider } from 'react-i18next'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AppThemeProvider } from '../theme/app-theme-provider'
import { createMatchMedia } from '../testing/create-match-media'
import { NotFound } from './not-found'

function TestLink({ to, ...rest }: { to: string } & AnchorHTMLAttributes<HTMLAnchorElement>) {
  return <a href={to} {...rest} />
}

beforeEach(() => {
  vi.stubGlobal('matchMedia', createMatchMedia({ width: 360 }))
  localStorage.clear()
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function renderNotFound(lng?: 'en' | 'lt') {
  render(
    <I18nextProvider i18n={createI18n({ lng })}>
      <AppThemeProvider>
        <NotFound linkComponent={TestLink} homeTo="/start" />
      </AppThemeProvider>
    </I18nextProvider>,
  )
}

describe('NotFound', () => {
  it('shows the heading, the message and a link home', () => {
    renderNotFound()
    expect(screen.getByRole('heading', { level: 1, name: 'Page not found' })).toBeTruthy()
    expect(screen.getByText('The page you are looking for does not exist.')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Go to home' }).getAttribute('href')).toBe('/start')
  })

  it('is translated to Lithuanian', () => {
    renderNotFound('lt')
    expect(screen.getByRole('heading', { level: 1, name: 'Puslapis nerastas' })).toBeTruthy()
    expect(screen.getByText('Puslapio, kurio ieškote, nėra.')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Grįžti į pradžią' }).getAttribute('href')).toBe(
      '/start',
    )
  })
})
