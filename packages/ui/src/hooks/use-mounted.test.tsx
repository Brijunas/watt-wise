import { cleanup, render } from '@testing-library/react'
import type { RefObject } from 'react'
import { afterEach, describe, expect, it } from 'vitest'
import useMounted from './use-mounted'

afterEach(cleanup)

describe('useMounted', () => {
  it('is true while mounted and false after unmount', () => {
    let mounted: RefObject<boolean> | undefined
    const Probe = () => {
      mounted = useMounted()
      return null
    }

    const { unmount } = render(<Probe />)
    expect(mounted?.current).toBe(true)

    unmount()
    expect(mounted?.current).toBe(false)
  })
})
