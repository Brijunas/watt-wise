import { defineConfig } from 'vitest/config'

// Tests of the repo's own lint rules (eslint/); run from the repo root by `pnpm test`.
export default defineConfig({
  test: { name: 'eslint-rules', environment: 'node', include: ['eslint/**/*.test.js'] },
})
