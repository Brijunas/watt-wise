import { cleanup, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { stubMatchMedia } from '@/testing/match-media'
import { renderWithProviders } from '@/testing/test-utils'
import { App } from './app'

beforeEach(() => {
  stubMatchMedia()
  localStorage.clear()
  document.documentElement.className = ''
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

describe('App', () => {
  it('renders the heading', () => {
    renderWithProviders(<App />)
    expect(screen.getByRole('heading', { name: 'Watt-Wise' })).toBeTruthy()
  })

  it('renders the appearance button', () => {
    renderWithProviders(<App />)
    expect(screen.getByRole('button', { name: 'Appearance' })).toBeTruthy()
  })

  it('stores Dark and sets the class on html', async () => {
    const user = userEvent.setup()
    renderWithProviders(<App />)
    await user.click(screen.getByRole('button', { name: 'Appearance' }))
    await user.click(screen.getByRole('menuitemradio', { name: 'Dark' }))
    expect(document.documentElement.classList.contains('dark')).toBe(true)
    expect(localStorage.getItem('wattwise-mode')).toBe('dark')
  })
})
