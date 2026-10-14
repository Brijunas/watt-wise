import { describe, expect, it } from 'vitest'
import { email, password, passwordMinLength, requiredText } from './schemas'

const messages = (
  schema: { safeParse: (value: unknown) => { error?: { issues: { message: string }[] } } },
  value: unknown,
) => schema.safeParse(value).error?.issues.map((issue) => issue.message) ?? []

describe('requiredText', () => {
  it('trims the value', () => {
    expect(requiredText().parse('  Jonas  ')).toBe('Jonas')
  })

  it.each(['', '   '])('rejects %j as required', (value) => {
    expect(messages(requiredText(), value)).toEqual(['required'])
  })
})

describe('email', () => {
  it('accepts an address and trims it', () => {
    expect(email().parse('  jonas@example.com ')).toBe('jonas@example.com')
  })

  it.each(['', '   '])('rejects %j as required only', (value) => {
    expect(messages(email(), value)).toEqual(['required'])
  })

  it.each(['jonas', 'jonas@', '@example.com', 'jo nas@example.com'])(
    'rejects %j as an invalid email',
    (value) => {
      expect(messages(email(), value)).toEqual(['email'])
    },
  )
})

describe('password', () => {
  const valid = 'Abcdefghijkl1!'

  it('has a minimum length of 14', () => {
    expect(passwordMinLength).toBe(14)
  })

  it('accepts a value that meets every rule, untrimmed', () => {
    expect(password().parse(valid)).toBe(valid)
    expect(password().parse(` ${valid} `)).toBe(` ${valid} `)
  })

  it('rejects an empty value as required only', () => {
    expect(messages(password(), '')).toEqual(['required'])
  })

  it('rejects a short value with the minimum', () => {
    expect(messages(password(), 'Abcdefgh1!')).toEqual(['password.tooShort|{"min":14}'])
  })

  it.each([
    ['ABCDEFGHIJKL1!', 'password.lowercase'],
    ['abcdefghijkl1!', 'password.uppercase'],
    ['Abcdefghijklm!', 'password.digit'],
    ['Abcdefghijkl12', 'password.symbol'],
  ])('rejects %j with only %s', (value, key) => {
    expect(messages(password(), value)).toEqual([key])
  })

  it('reports every broken rule', () => {
    expect(messages(password(), 'abc')).toEqual([
      'password.tooShort|{"min":14}',
      'password.uppercase',
      'password.digit',
      'password.symbol',
    ])
  })

  it('counts Lithuanian letters as lowercase and uppercase', () => {
    expect(password().safeParse('ąčęėįšųūž1!ĄČ').success).toBe(false)
    expect(password().safeParse('ąčęėįšųūžĄČĘ1!').success).toBe(true)
    expect(messages(password(), 'ŠŠŠŠŠŠŠŠŠŠŠŠ1!')).toEqual(['password.lowercase'])
    expect(messages(password(), 'ąąąąąąąąąąąą1!')).toEqual(['password.uppercase'])
  })

  it('counts a space as a symbol and a non-ASCII digit as a digit', () => {
    expect(password().safeParse('Abcdefghijkl 1').success).toBe(true)
    expect(password().safeParse('Abcdefghijkl٣!').success).toBe(true)
  })

  it('counts a non-decimal number such as ² as a symbol', () => {
    expect(messages(password(), 'Abcdefghijkl1²')).toEqual([])
  })
})
