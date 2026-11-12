import { z } from 'zod'
import { email, password } from '@wattwise/core'

export const signUpSchema = z.object({ email: email(), password: password() })

export type SignUpInput = z.input<typeof signUpSchema>
export type SignUpValues = z.output<typeof signUpSchema>
