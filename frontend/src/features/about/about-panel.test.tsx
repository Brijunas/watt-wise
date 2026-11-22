import { screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { env } from '@/config/env'
import { server } from '@/testing/mocks/server'
import renderWithProviders from '@/testing/test-utils'
import AboutPanel from './about-panel'

const text = {
  en: {
    loading: 'Loading',
    name: 'API: Watt-Wise API',
    version: 'Version: 1.2.3',
    error: 'Could not load the API information. Try again later.',
  },
  lt: {
    loading: 'Kraunama',
    name: 'API: Watt-Wise API',
    version: 'Versija: 1.2.3',
    error: 'Nepavyko įkelti API informacijos. Bandykite vėliau.',
  },
} as const

describe.each(['en', 'lt'] as const)('AboutPanel in %s', (language) => {
  const t = text[language]

  it('shows the shared page loader, then the API name and version', async () => {
    renderWithProviders(<AboutPanel />, { language })
    expect(screen.getByRole('progressbar', { name: t.loading })).toBeTruthy()
    expect(await screen.findByText(t.name)).toBeTruthy()
    expect(screen.getByText(t.version)).toBeTruthy()
    expect(screen.queryByRole('progressbar')).toBeNull()
  })

  it('shows no error text while loading', () => {
    renderWithProviders(<AboutPanel />, { language })
    expect(screen.queryByText(t.error)).toBeNull()
  })

  it('shows no error text once the data has loaded', async () => {
    renderWithProviders(<AboutPanel />, { language })
    await screen.findByText(t.name)
    expect(screen.queryByText(t.error)).toBeNull()
  })

  it('shows the error text when the API fails', async () => {
    server.use(
      http.get(`${env.apiBaseUrl}/api/v1/about`, () => new HttpResponse(null, { status: 500 })),
    )
    renderWithProviders(<AboutPanel />, { language })
    expect(await screen.findByText(t.error)).toBeTruthy()
    expect(screen.queryByText(/^(Version|Versija):/)).toBeNull()
  })
})
