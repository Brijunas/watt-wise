import { describe, expect, it } from 'vitest'
import readReturnTo from './read-return-to'

describe('readReturnTo', () => {
  it('joins the path, query and hash of a valid location', () => {
    expect(readReturnTo({ from: { pathname: '/devices', search: '?a=1', hash: '#top' } })).toBe(
      '/devices?a=1#top',
    )
  })

  it('accepts a bare path', () => {
    expect(readReturnTo({ from: { pathname: '/devices' } })).toBe('/devices')
  })

  it.each(['//evil.example', 'https://evil.example', 'evil', ''])('rejects %j', (pathname) => {
    expect(readReturnTo({ from: { pathname } })).toBeUndefined()
  })

  it.each([undefined, null, 'x', 42, {}, { from: 'x' }, { from: null }])(
    'rejects state %j',
    (state) => {
      expect(readReturnTo(state)).toBeUndefined()
    },
  )
})
