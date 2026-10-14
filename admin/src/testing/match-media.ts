import { createMatchMedia } from '@wattwise/ui/testing'
import { vi } from 'vitest'

const stubMatchMedia = ({ width = 360 }: { width?: number } = {}) => {
  vi.stubGlobal('matchMedia', createMatchMedia({ width }))
}

export default stubMatchMedia
