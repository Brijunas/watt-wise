import { cleanup, render, screen } from '@testing-library/react'
import { encodeValidationMessage } from '@wattwise/core'
import { createI18n } from '@wattwise/i18n'
import { useForm } from 'react-hook-form'
import { I18nextProvider } from 'react-i18next'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import AppThemeProvider from '../theme/app-theme-provider'
import createMatchMedia from '../testing/create-match-media'
import FormTextField from './form-text-field'

beforeEach(() => {
  vi.stubGlobal('matchMedia', createMatchMedia({ width: 1024 }))
  localStorage.clear()
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

interface Values {
  name: string
}

const Harness = ({ message }: { message?: string }) => {
  const { control } = useForm<Values>({
    defaultValues: { name: 'Jonas' },
    errors: message === undefined ? undefined : { name: { type: 'custom', message } },
  })
  return <FormTextField control={control} name="name" label="Name" autoComplete="name" />
}

const renderField = (message?: string, lng: 'en' | 'lt' = 'en') => {
  render(
    <I18nextProvider i18n={createI18n({ lng })}>
      <AppThemeProvider>
        <Harness message={message} />
      </AppThemeProvider>
    </I18nextProvider>,
  )
}

describe('FormTextField', () => {
  it('shows the label and the value without an error', () => {
    renderField()
    const input = screen.getByRole<HTMLInputElement>('textbox', { name: 'Name' })
    expect(input.value).toBe('Jonas')
    expect(input.getAttribute('aria-invalid')).toBe('false')
    expect(input.getAttribute('autocomplete')).toBe('name')
  })

  it('shows the translated message and marks the input invalid', () => {
    renderField(encodeValidationMessage('required'))
    const input = screen.getByRole('textbox', { name: 'Name' })
    expect(input.getAttribute('aria-invalid')).toBe('true')
    expect(screen.getByText('This field is required')).toBeTruthy()
  })

  it('interpolates the parameters of the message', () => {
    renderField(encodeValidationMessage('password.tooShort', { min: 14 }), 'lt')
    expect(screen.getByText('Slaptažodį turi sudaryti bent 14 simbolių')).toBeTruthy()
  })

  it('shows the invalid text for a message that is not an encoded key', () => {
    renderField('Too small: expected string to have >=1 characters')
    expect(screen.getByText('This value is not valid')).toBeTruthy()
  })
})
