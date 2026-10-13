import { createMatchMedia } from '@wattwise/ui/testing'
import { vi } from 'vitest'

export function stubMatchMedia({ width = 360 }: { width?: number } = {}) {
  vi.stubGlobal('matchMedia', createMatchMedia({ width }))
}
