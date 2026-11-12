import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { zodResolver } from '@hookform/resolvers/zod'
import { signInSchema } from '@wattwise/core'
import type { SignInInput, SignInValues } from '@wattwise/core'
import { createI18n } from '@wattwise/i18n'
import { useForm } from 'react-hook-form'
import { I18nextProvider } from 'react-i18next'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import createMatchMedia from '../testing/create-match-media'
import AppThemeProvider from '../theme/app-theme-provider'
import SignInFields from './sign-in-fields'

beforeEach(() => {
  vi.stubGlobal('matchMedia', createMatchMedia({ width: 1024 }))
  localStorage.clear()
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

const Harness = ({
  onValid,
  busy,
  withSlots,
}: {
  onValid: (values: SignInValues) => void
  busy?: boolean
  withSlots?: boolean
}) => {
  const { control, handleSubmit } = useForm<SignInInput, unknown, SignInValues>({
    resolver: zodResolver(signInSchema),
    defaultValues: { email: '', password: '' },
  })
  return (
    <SignInFields
      control={control}
      onSubmit={(event) => void handleSubmit(onValid)(event)}
      busy={busy}
      error={withSlots ? <p>Server said no</p> : undefined}
      forgotPasswordLink={withSlots ? <a href="/forgot">Forgot link</a> : undefined}
    />
  )
}

const renderFields = (options: { lng?: 'en' | 'lt'; busy?: boolean; withSlots?: boolean } = {}) => {
  const onValid = vi.fn()
  const user = userEvent.setup()
  render(
    <I18nextProvider i18n={createI18n({ lng: options.lng ?? 'en' })}>
      <AppThemeProvider>
        <Harness onValid={onValid} busy={options.busy} withSlots={options.withSlots} />
      </AppThemeProvider>
    </I18nextProvider>,
  )
  return { onValid, user }
}

describe('SignInFields', () => {
  it('shows the English labels with the right autocomplete', () => {
    renderFields()

    expect(screen.getByLabelText('Email', { selector: 'input' }).getAttribute('autocomplete')).toBe(
      'email',
    )
    expect(
      screen.getByLabelText('Password', { selector: 'input' }).getAttribute('autocomplete'),
    ).toBe('current-password')
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeTruthy()
  })

  it('shows the Lithuanian texts', () => {
    renderFields({ lng: 'lt' })

    expect(screen.getByLabelText('El. paštas', { selector: 'input' })).toBeTruthy()
    expect(screen.getByLabelText('Slaptažodis', { selector: 'input' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Prisijungti' })).toBeTruthy()
  })

  it('submits the typed values', async () => {
    const { onValid, user } = renderFields()
    await user.type(screen.getByLabelText('Email', { selector: 'input' }), 'a@b.test')
    await user.type(screen.getByLabelText('Password', { selector: 'input' }), 'short')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(onValid).toHaveBeenCalledTimes(1)
    expect(onValid.mock.calls[0]?.[0]).toEqual({ email: 'a@b.test', password: 'short' })
  })

  it('shows the required messages and does not submit empty fields', async () => {
    const { onValid, user } = renderFields({ lng: 'lt' })
    await user.click(screen.getByRole('button', { name: 'Prisijungti' }))

    expect(screen.getAllByText('Privalomas laukas')).toHaveLength(2)
    expect(onValid).not.toHaveBeenCalled()
  })

  it('shows the error and the forgot-password slots', () => {
    renderFields({ withSlots: true })

    expect(screen.getByText('Server said no')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Forgot link' })).toBeTruthy()
  })

  it('disables the button while busy', () => {
    renderFields({ busy: true })

    expect(screen.getByRole('button', { name: /Sign in/ }).hasAttribute('disabled')).toBe(true)
  })
})
