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

  it.each([
    { lng: 'en', show: 'Show password', hide: 'Hide password' },
    { lng: 'lt', show: 'Rodyti slaptažodį', hide: 'Slėpti slaptažodį' },
  ] as const)(
    'shows the text on $show and hides it again on $hide',
    async ({ lng, show, hide }) => {
      const user = renderField({ lng })
      const input = screen.getByLabelText<HTMLInputElement>('Password', { selector: 'input' })

      await user.click(screen.getByRole('button', { name: show }))
      expect(input.type).toBe('text')

      await user.click(screen.getByRole('button', { name: hide }))
      expect(input.type).toBe('password')
    },
  )

  it.each([
    { lng: 'en', show: 'Show password' },
    { lng: 'lt', show: 'Rodyti slaptažodį' },
  ] as const)('keeps the focus in the input when the toggle is pressed ($lng)', async (c) => {
    const user = renderField({ lng: c.lng })
    const input = screen.getByLabelText('Password', { selector: 'input' })
    await user.click(input)

    await user.click(screen.getByRole('button', { name: c.show }))

    expect(document.activeElement).toBe(input)
  })

  it.each([
    { lng: 'en', text: 'This field is required' },
    { lng: 'lt', text: 'Privalomas laukas' },
  ] as const)('describes the input by its error text in $lng', ({ lng, text }) => {
    renderField({ lng, message: encodeValidationMessage('required') })
    const input = screen.getByLabelText('Password', { selector: 'input' })
    const describedBy = input.getAttribute('aria-describedby') ?? ''
    expect(document.getElementById(describedBy)?.textContent).toBe(text)
  })

  it('shows the translated error message', () => {
    renderField({ message: encodeValidationMessage('password.digit'), lng: 'lt' })
    expect(screen.getByText('Slaptažodyje turi būti skaitmuo')).toBeTruthy()
    expect(
      screen.getByLabelText('Password', { selector: 'input' }).getAttribute('aria-invalid'),
    ).toBe('true')
  })
})
