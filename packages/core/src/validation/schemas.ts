import { z } from 'zod'
import { encodeValidationMessage } from './validation-message'

/** The minimum password length of the account rules in docs/security.md. */
export const passwordMinLength = 14

const required = encodeValidationMessage('required')

/** A text that is not empty once trimmed. Parses to the trimmed text. */
export const requiredText = () => z.string().trim().min(1, required)

/** An email address, trimmed. An empty value is `required`, a malformed one `email`. */
export const email = () =>
  z
    .string()
    .trim()
    .min(1, required)
    .pipe(z.email({ error: encodeValidationMessage('email') }))

// The same classes ASP.NET Core Identity checks with char.IsLower, IsUpper, IsDigit and
// IsLetterOrDigit: a symbol is any character that is neither a letter nor a decimal digit
// (so ², ½ and Ⅻ are symbols).
const lowercase = /\p{Ll}/u
const uppercase = /\p{Lu}/u
const digit = /\p{Nd}/u
const symbol = /[^\p{L}\p{Nd}]/u

/**
 * A new password by the rules of docs/security.md. Not trimmed, since spaces count as symbols.
 * Each rule is its own issue, so the form can show every rule the value breaks.
 */
export const password = () =>
  z
    .string()
    .min(1, { error: required, abort: true })
    .check(
      z.minLength(
        passwordMinLength,
        encodeValidationMessage('password.tooShort', { min: passwordMinLength }),
      ),
      z.regex(lowercase, encodeValidationMessage('password.lowercase')),
      z.regex(uppercase, encodeValidationMessage('password.uppercase')),
      z.regex(digit, encodeValidationMessage('password.digit')),
      z.regex(symbol, encodeValidationMessage('password.symbol')),
    )
