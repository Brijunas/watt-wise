import type { ValidationKey } from '@wattwise/i18n'

export type ValidationParams = Record<string, string | number>

export interface ValidationMessage {
  key: ValidationKey
  params?: ValidationParams
}

// Every key of the `validation` namespace. A Record makes a key missing from the list, or one the
// namespace lacks, a type error.
const keys: Record<ValidationKey, true> = {
  required: true,
  email: true,
  'password.tooShort': true,
  'password.lowercase': true,
  'password.uppercase': true,
  'password.digit': true,
  'password.symbol': true,
  invalid: true,
}

const separator = '|'

const isKey = (value: string): value is ValidationKey => Object.hasOwn(keys, value)

const isParams = (value: unknown): value is ValidationParams =>
  typeof value === 'object' &&
  value !== null &&
  !Array.isArray(value) &&
  Object.values(value).every((item) => typeof item === 'string' || typeof item === 'number')

/**
 * Encodes a validation key, and its parameters if any, as the text of a zod issue: `key` alone, or
 * `key|{"min":14}` (the key, a bar, the parameters as JSON). Schemas never hold display text; the
 * form fields decode the message and translate the key.
 */
export const encodeValidationMessage = (key: ValidationKey, params?: ValidationParams): string =>
  params === undefined ? key : `${key}${separator}${JSON.stringify(params)}`

/**
 * Decodes a message made by `encodeValidationMessage`. Anything else, such as zod's own English
 * text, an empty string or a broken encoding, decodes to the `invalid` key.
 */
export const decodeValidationMessage = (message: string | undefined): ValidationMessage => {
  if (message === undefined) {
    return { key: 'invalid' }
  }
  const index = message.indexOf(separator)
  const key = index === -1 ? message : message.slice(0, index)
  if (!isKey(key)) {
    return { key: 'invalid' }
  }
  if (index === -1) {
    return { key }
  }
  try {
    const params: unknown = JSON.parse(message.slice(index + 1))
    return isParams(params) ? { key, params } : { key: 'invalid' }
  } catch {
    return { key: 'invalid' }
  }
}
