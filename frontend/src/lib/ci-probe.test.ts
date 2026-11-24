import { describe, expect, it } from 'vitest'

// Throwaway (S5B.1): a failing test the PR check must catch. Never merged.
describe('ci probe', () => {
  it('fails on purpose', () => {
    expect(1 + 1).toBe(3)
  })
})
