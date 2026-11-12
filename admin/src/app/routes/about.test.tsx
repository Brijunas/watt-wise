import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { renderRoutesAt } from '@/testing/test-utils'
import { routes } from '../router'

describe('About route', () => {
  it('renders the API name and version at /about for a signed-in admin', async () => {
    renderRoutesAt(routes, '/about', { session: 'signedIn', language: 'en' })
    expect(await screen.findByRole('heading', { level: 1, name: 'About' })).toBeTruthy()
    expect(await screen.findByText('API: Watt-Wise API')).toBeTruthy()
    expect(screen.getByText('Version: 1.2.3')).toBeTruthy()
  })
})
