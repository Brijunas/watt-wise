import { defineConfig } from 'vitest/config'

// Shared Vitest config. Each workspace package merges it in its own vitest.config.js.
export default defineConfig({
  test: {
    environment: 'node',
    include: ['src/**/*.test.{ts,tsx}'],
    restoreMocks: true,
  },
})
