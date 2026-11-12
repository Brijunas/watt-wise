import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import AccountPage from './account-page'

afterEach(cleanup)

describe('AccountPage', () => {
  it('renders the title as the h1 and the children', () => {
    render(
      <AccountPage title="Sign in">
        <p>Body text</p>
      </AccountPage>,
    )

    expect(screen.getByRole('heading', { level: 1, name: 'Sign in' })).toBeTruthy()
    expect(screen.getByText('Body text')).toBeTruthy()
  })
})
