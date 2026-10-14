import { screen } from '@testing-library/react'
import { createMemoryRouter } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { describe, expect, it } from 'vitest'
import renderWithProviders from '@/testing/test-utils'
import { routes } from '../router'

describe('About route', () => {
  it('renders the API name and version at /about', async () => {
    const router = createMemoryRouter(routes, { initialEntries: ['/about'] })
    renderWithProviders(<RouterProvider router={router} />)
    expect(await screen.findByRole('heading', { level: 1, name: 'About' })).toBeTruthy()
    expect(await screen.findByText('API: Watt-Wise API')).toBeTruthy()
    expect(screen.getByText('Version: 1.2.3')).toBeTruthy()
  })
})
