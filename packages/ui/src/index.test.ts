import { describe, expect, it } from 'vitest'
import { packageName } from './index'

describe('ui', () => {
  it('exports its package name', () => {
    expect(packageName).toBe('@wattwise/ui')
  })
})
