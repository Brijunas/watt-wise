import { describe, expect, it } from 'vitest'
import { packageName } from './index'

describe('i18n', () => {
  it('exports its package name', () => {
    expect(packageName).toBe('@wattwise/i18n')
  })
})
