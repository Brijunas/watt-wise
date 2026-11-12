import * as axeMatchers from 'vitest-axe/matchers'
import { expect } from 'vitest'

// Registers vitest-axe's `toHaveNoViolations` for every workspace (typings: each workspace's src/vitest-axe.d.ts).
expect.extend(axeMatchers)
