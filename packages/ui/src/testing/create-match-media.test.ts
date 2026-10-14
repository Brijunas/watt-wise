import { describe, expect, it } from 'vitest'
import createMatchMedia from './create-match-media'

describe('createMatchMedia', () => {
  it('evaluates min-width against the width', () => {
    const matchMedia = createMatchMedia({ width: 900 })
    expect(matchMedia('(min-width:900px)').matches).toBe(true)
    expect(matchMedia('(min-width: 901px)').matches).toBe(false)
    expect(matchMedia('(min-width:600.5px)').matches).toBe(true)
  })

  it('evaluates max-width against the width', () => {
    const matchMedia = createMatchMedia({ width: 360 })
    expect(matchMedia('(max-width:360px)').matches).toBe(true)
    expect(matchMedia('(max-width: 359.95px)').matches).toBe(false)
  })

  it('requires every part of a combined query', () => {
    const matchMedia = createMatchMedia({ width: 700 })
    expect(matchMedia('(min-width:600px) and (max-width:899.95px)').matches).toBe(true)
    expect(matchMedia('(min-width:800px) and (max-width:899.95px)').matches).toBe(false)
  })

  it('strips a leading @media', () => {
    const matchMedia = createMatchMedia({ width: 900 })
    expect(matchMedia('@media (min-width:900px)').matches).toBe(true)
  })

  it('answers prefers-color-scheme', () => {
    expect(createMatchMedia({ width: 900 })('(prefers-color-scheme: dark)').matches).toBe(false)
    expect(
      createMatchMedia({ width: 900, prefersDark: true })('(prefers-color-scheme: dark)').matches,
    ).toBe(true)
  })

  it('does not match unknown queries', () => {
    expect(createMatchMedia({ width: 900 })('(orientation: portrait)').matches).toBe(false)
  })

  it('keeps the query as media and ignores listeners', () => {
    const list = createMatchMedia({ width: 900 })('(min-width:900px)')
    expect(list.media).toBe('(min-width:900px)')
    expect(list.onchange).toBe(null)
    expect(list.dispatchEvent(new Event('change'))).toBe(false)
  })
})
