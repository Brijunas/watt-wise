import { z } from 'zod'
import { password } from '@wattwise/core'

export const resetPasswordSchema = z.object({ newPassword: password() })

export type ResetPasswordInput = z.input<typeof resetPasswordSchema>
export type ResetPasswordValues = z.output<typeof resetPasswordSchema>
