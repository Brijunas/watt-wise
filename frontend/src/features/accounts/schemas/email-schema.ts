import { z } from 'zod'
import { email } from '@wattwise/core'

// The forms that ask only for an email address: forgot password and "send the link again".
export const emailSchema = z.object({ email: email() })

export type EmailInput = z.input<typeof emailSchema>
export type EmailValues = z.output<typeof emailSchema>
