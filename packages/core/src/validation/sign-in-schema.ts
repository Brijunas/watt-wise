import { z } from 'zod'
import { email } from './schemas'
import { encodeValidationMessage } from './validation-message'

/**
 * The sign-in form. The password is only required, never checked against the password rules: an
 * existing account must be able to sign in with whatever its password is. Not trimmed either.
 */
export const signInSchema = z.object({
  email: email(),
  password: z.string().min(1, encodeValidationMessage('required')),
})

export type SignInInput = z.input<typeof signInSchema>
export type SignInValues = z.output<typeof signInSchema>
