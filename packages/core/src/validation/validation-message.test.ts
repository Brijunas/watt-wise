import { describe, expect, it } from 'vitest'
import { decodeValidationMessage, encodeValidationMessage } from './validation-message'

describe('encodeValidationMessage', () => {
  it('encodes a bare key as itself', () => {
    expect(encodeValidationMessage('required')).toBe('required')
  })

  it('appends the parameters as JSON after a bar', () => {
    expect(encodeValidationMessage('password.tooShort', { min: 14 })).toBe(
      'password.tooShort|{"min":14}',
    )
  })

  it('does not accept a key the namespace lacks', () => {
    // @ts-expect-error unknown validation keys are a type error
    encodeValidationMessage('does.not.exist')
  })
})

describe('decodeValidationMessage', () => {
  it('decodes a bare key', () => {
    expect(decodeValidationMessage('email')).toEqual({ key: 'email' })
  })

  it('decodes a key with parameters', () => {
    expect(decodeValidationMessage('password.tooShort|{"min":14}')).toEqual({
      key: 'password.tooShort',
      params: { min: 14 },
    })
  })

  it('round-trips string and number parameters', () => {
    const encoded = encodeValidationMessage('invalid', { a: 'x|y', b: 2 })

    expect(decodeValidationMessage(encoded)).toEqual({ key: 'invalid', params: { a: 'x|y', b: 2 } })
  })

  it.each([
    ['undefined', undefined],
    ['an empty string', ''],
    ['zod default text', 'Too small: expected string to have >=1 characters'],
    ['an unknown key', 'nope'],
    ['malformed JSON', 'password.tooShort|{min:'],
    ['parameters that are not an object', 'password.tooShort|14'],
    ['parameters with nested values', 'password.tooShort|{"min":{"a":1}}'],
    ['an array of parameters', 'password.tooShort|[14]'],
  ])('decodes %s to the invalid key', (_name, message) => {
    expect(decodeValidationMessage(message)).toEqual({ key: 'invalid' })
  })
})
