import { z } from 'zod'
import { encodeValidationMessage, password } from '@wattwise/core'

// The current password is only required, never checked against the rules: the server compares it.
export const changePasswordSchema = z.object({
  currentPassword: z.string().min(1, encodeValidationMessage('required')),
  newPassword: password(),
})

export type ChangePasswordInput = z.input<typeof changePasswordSchema>
export type ChangePasswordValues = z.output<typeof changePasswordSchema>
