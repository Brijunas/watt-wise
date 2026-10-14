import { zodResolver } from '@hookform/resolvers/zod'
import { act, cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { email, password, requiredText } from '@wattwise/core'
import { createI18n } from '@wattwise/i18n'
import { useForm } from 'react-hook-form'
import { I18nextProvider } from 'react-i18next'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { z } from 'zod'
import AppThemeProvider from '../theme/app-theme-provider'
import createMatchMedia from '../testing/create-match-media'
import FormPasswordField from './form-password-field'
import FormTextField from './form-text-field'

const schema = z.object({ name: requiredText(), email: email(), password: password() })

type Values = z.input<typeof schema>
type Parsed = z.output<typeof schema>

// The story's demo form: a schema built from the shared helpers, wired to the shared fields.
const DemoForm = ({ onSubmit }: { onSubmit: (values: Parsed) => void }) => {
  const { control, handleSubmit } = useForm<Values, unknown, Parsed>({
    resolver: zodResolver(schema),
    mode: 'onSubmit',
    defaultValues: { name: '', email: '', password: '' },
  })
  return (
    <form noValidate onSubmit={(event) => void handleSubmit(onSubmit)(event)}>
      <FormTextField control={control} name="name" label="Name" />
      <FormTextField control={control} name="email" label="Email" type="email" />
      <FormPasswordField control={control} name="password" label="Password" />
      <button type="submit">Send</button>
    </form>
  )
}

beforeEach(() => {
  vi.stubGlobal('matchMedia', createMatchMedia({ width: 1024 }))
  localStorage.clear()
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

const renderForm = (lng: 'en' | 'lt' = 'en') => {
  const user = userEvent.setup()
  const onSubmit = vi.fn()
  const i18n = createI18n({ lng })
  render(
    <I18nextProvider i18n={i18n}>
      <AppThemeProvider>
        <DemoForm onSubmit={onSubmit} />
      </AppThemeProvider>
    </I18nextProvider>,
  )
  return { user, onSubmit, i18n }
}

const input = (name: string) => screen.getByLabelText(name, { selector: 'input' })

describe('demo form', () => {
  it('shows the required message of every empty field in English', async () => {
    const { user, onSubmit } = renderForm()
    await user.click(screen.getByRole('button', { name: 'Send' }))

    expect(screen.getAllByText('This field is required')).toHaveLength(3)
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('shows the required message of every empty field in Lithuanian', async () => {
    const { user } = renderForm('lt')
    await user.click(screen.getByRole('button', { name: 'Send' }))

    expect(screen.getAllByText('Privalomas laukas')).toHaveLength(3)
  })

  it('shows the email and password messages with the minimum filled in', async () => {
    const { user } = renderForm()
    await user.type(input('Name'), 'Jonas')
    await user.type(input('Email'), 'not-an-email')
    await user.type(input('Password'), 'short')
    await user.click(screen.getByRole('button', { name: 'Send' }))

    expect(screen.getByText('Enter a valid email address')).toBeTruthy()
    expect(screen.getByText('The password must be at least 14 characters long')).toBeTruthy()
  })

  it('shows the Lithuanian password messages with the minimum filled in', async () => {
    const { user } = renderForm('lt')
    await user.type(input('Name'), 'Jonas')
    await user.type(input('Email'), 'jonas@example.com')
    await user.type(input('Password'), 'short')
    await user.click(screen.getByRole('button', { name: 'Send' }))

    expect(screen.getByText('Slaptažodį turi sudaryti bent 14 simbolių')).toBeTruthy()
  })

  it('translates the errors on screen again when the language changes', async () => {
    const { user, i18n } = renderForm()
    await user.click(screen.getByRole('button', { name: 'Send' }))
    expect(screen.getAllByText('This field is required')).toHaveLength(3)

    await act(() => i18n.changeLanguage('lt'))

    expect(screen.queryByText('This field is required')).toBeNull()
    expect(screen.getAllByText('Privalomas laukas')).toHaveLength(3)
  })

  it('submits the parsed values and shows no errors', async () => {
    const { user, onSubmit } = renderForm()
    await user.type(input('Name'), '  Jonas ')
    await user.type(input('Email'), ' jonas@example.com ')
    await user.type(input('Password'), 'Abcdefghijkl1!')
    await user.click(screen.getByRole('button', { name: 'Send' }))

    expect(onSubmit).toHaveBeenCalledTimes(1)
    expect(onSubmit.mock.calls[0]?.[0]).toEqual({
      name: 'Jonas',
      email: 'jonas@example.com',
      password: 'Abcdefghijkl1!',
    })
    expect(screen.queryByText('This field is required')).toBeNull()
    expect(document.querySelector('[aria-invalid="true"]')).toBeNull()
  })

  it('focuses the first invalid field', async () => {
    const { user } = renderForm()
    await user.type(input('Name'), 'Jonas')
    await user.click(screen.getByRole('button', { name: 'Send' }))

    expect(document.activeElement).toBe(input('Email'))
  })
})
