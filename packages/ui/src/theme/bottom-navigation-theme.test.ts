import { expect, it } from 'vitest'
import { rootStyle } from '../testing/theme-test-utils'

// The bar's own height: labels that wrap (a long Lithuanian label, text at 200%) grow it instead of
// overflowing, and the shell measures it.
it('grows the bottom bar from a 56px minimum', () => {
  const style = rootStyle('MuiBottomNavigation')
  expect([style.height, style.minHeight]).toEqual(['auto', 56])
})

it('keeps the bottom bar on the paper surface with a top divider', () => {
  const style = rootStyle('MuiBottomNavigation')
  expect([style.backgroundColor, style.borderTop]).toEqual([
    expect.stringMatching(/^var\(--mui-palette-background-paper[,)]/) as unknown,
    expect.stringMatching(/^1px solid var\(--mui-palette-divider[,)]/) as unknown,
  ])
})
