import { describe, expect, it } from 'vitest'
import { signInSchema } from './sign-in-schema'

describe('signInSchema', () => {
  it('accepts a short password without the password rules and keeps it as typed', () => {
    expect(signInSchema.parse({ email: ' a@b.test ', password: ' abc ' })).toEqual({
      email: 'a@b.test',
      password: ' abc ',
    })
  })

  it('requires both fields', () => {
    const result = signInSchema.safeParse({ email: '', password: '' })
    expect(result.error?.issues.map((i) => [i.path.join('.'), i.message])).toEqual([
      ['email', 'required'],
      ['password', 'required'],
    ])
  })

  it('rejects a malformed email', () => {
    const result = signInSchema.safeParse({ email: 'nope', password: 'x' })
    expect(result.error?.issues.map((i) => i.message)).toEqual(['email'])
  })
})
