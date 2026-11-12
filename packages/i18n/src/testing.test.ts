import fc from 'fast-check'
import { describe, expect, it } from 'vitest'
import flattenKeys from './testing'

describe('flattenKeys', () => {
  it('lists nested keys as dotted paths', () => {
    expect(flattenKeys({ a: { b: 'x', c: { d: 'y' } }, e: 'z' }).sort()).toEqual([
      'a.b',
      'a.c.d',
      'e',
    ])
  })

  it('reduces plural forms to one base key', () => {
    expect(
      flattenKeys({ items: { count_one: '1', count_few: '2', count_many: '3', count_other: '4' } }),
    ).toEqual(['items.count'])
  })

  it('matches Lithuanian and English plural sets', () => {
    const lt = { n_one: 'a', n_few: 'b', n_many: 'c', n_other: 'd', keep: 'e' }
    const en = { n_one: 'a', n_other: 'b', keep: 'e' }
    expect(flattenKeys(lt).sort()).toEqual(flattenKeys(en).sort())
  })

  it('treats a plural form in one language and a plain key in the other as equal', () => {
    expect(flattenKeys({ n_other: 'a' })).toEqual(flattenKeys({ n: 'a' }))
  })

  it('only strips a plural suffix at the end of a key', () => {
    expect(flattenKeys({ one_thing: 'a', x_one: { y: 'b' } }).sort()).toEqual([
      'one_thing',
      'x_one.y',
    ])
  })

  it('keeps keys whose value is null', () => {
    expect(flattenKeys({ a: null, b: { c: null } }).sort()).toEqual(['a', 'b.c'])
  })

  const word = fc.stringMatching(/^[a-z]{1,6}$/)
  const suffix = fc.constantFrom('_zero', '_one', '_two', '_few', '_many', '_other')

  it('property: adding plural suffixes never changes the reduced keys', () => {
    fc.assert(
      fc.property(
        fc.dictionary(word, fc.string(), { minKeys: 1 }),
        fc.array(suffix, { minLength: 1 }),
        (base, suffixes) => {
          const plural = Object.fromEntries(
            Object.entries(base).flatMap(([key, text]) =>
              suffixes.map((s) => [`${key}${s}`, text] as const),
            ),
          )
          expect(flattenKeys(plural).sort()).toEqual(flattenKeys(base).sort())
        },
      ),
    )
  })
})
