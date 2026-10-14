import { screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { env } from '@/config/env'
import { server } from '@/testing/mocks/server'
import renderWithProviders from '@/testing/test-utils'
import AboutPanel from './about-panel'

describe('AboutPanel', () => {
  it('shows a loading indicator, then the API name and version', async () => {
    renderWithProviders(<AboutPanel />)
    expect(screen.getByRole('progressbar', { name: 'Loading API information' })).toBeTruthy()
    expect(await screen.findByText('API: Watt-Wise API')).toBeTruthy()
    expect(screen.getByText('Version: 1.2.3')).toBeTruthy()
    expect(screen.queryByRole('progressbar')).toBeNull()
  })

  it('shows the error text when the API fails', async () => {
    server.use(
      http.get(`${env.apiBaseUrl}/api/v1/about`, () => new HttpResponse(null, { status: 500 })),
    )
    renderWithProviders(<AboutPanel />)
    expect(
      await screen.findByText('Could not load the API information. Try again later.'),
    ).toBeTruthy()
    expect(screen.queryByText(/^Version:/)).toBeNull()
  })
})
