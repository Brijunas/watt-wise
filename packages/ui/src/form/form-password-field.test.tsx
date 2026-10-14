import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { encodeValidationMessage } from '@wattwise/core'
import { createI18n } from '@wattwise/i18n'
import { useForm } from 'react-hook-form'
import { I18nextProvider } from 'react-i18next'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import AppThemeProvider from '../theme/app-theme-provider'
import createMatchMedia from '../testing/create-match-media'
import FormPasswordField from './form-password-field'

beforeEach(() => {
  vi.stubGlobal('matchMedia', createMatchMedia({ width: 1024 }))
  localStorage.clear()
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

interface Values {
  password: string
}

const Harness = ({ message, autoComplete }: { message?: string; autoComplete?: string }) => {
  const { control } = useForm<Values>({
    defaultValues: { password: 'secret' },
    errors: message === undefined ? undefined : { password: { type: 'custom', message } },
  })
  return (
    <FormPasswordField
      control={control}
      name="password"
      label="Password"
      autoComplete={autoComplete}
    />
  )
}

const renderField = (
  options: { lng?: 'en' | 'lt'; message?: string; autoComplete?: string } = {},
) => {
  const user = userEvent.setup()
  render(
    <I18nextProvider i18n={createI18n({ lng: options.lng ?? 'en' })}>
      <AppThemeProvider>
        <Harness message={options.message} autoComplete={options.autoComplete} />
      </AppThemeProvider>
    </I18nextProvider>,
  )
  return user
}

describe('FormPasswordField', () => {
  it('hides the text and defaults to the current-password autocomplete', () => {
    renderField()
    const input = screen.getByLabelText<HTMLInputElement>('Password', { selector: 'input' })
    expect(input.type).toBe('password')
    expect(input.value).toBe('secret')
    expect(input.getAttribute('autocomplete')).toBe('current-password')
  })

  it('lets the caller choose the autocomplete', () => {
    renderField({ autoComplete: 'new-password' })
    const input = screen.getByLabelText('Password', { selector: 'input' })
    expect(input.getAttribute('autocomplete')).toBe('new-password')
  })

  it('toggles the visibility and its label in English', async () => {
    const user = renderField()
    const input = screen.getByLabelText<HTMLInputElement>('Password', { selector: 'input' })

    await user.click(screen.getByRole('button', { name: 'Show password' }))
    expect(input.type).toBe('text')

    await user.click(screen.getByRole('button', { name: 'Hide password' }))
    expect(input.type).toBe('password')
  })

  it('toggles the visibility and its label in Lithuanian', async () => {
    const user = renderField({ lng: 'lt' })
    const input = screen.getByLabelText<HTMLInputElement>('Password', { selector: 'input' })

    await user.click(screen.getByRole('button', { name: 'Rodyti slaptažodį' }))
    expect(input.type).toBe('text')
    expect(screen.getByRole('button', { name: 'Slėpti slaptažodį' })).toBeTruthy()
  })

  it('shows the translated error message', () => {
    renderField({ message: encodeValidationMessage('password.digit'), lng: 'lt' })
    expect(screen.getByText('Slaptažodyje turi būti skaitmuo')).toBeTruthy()
    expect(
      screen.getByLabelText('Password', { selector: 'input' }).getAttribute('aria-invalid'),
    ).toBe('true')
  })
})
