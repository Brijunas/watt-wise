import { defineProject, mergeConfig } from 'vitest/config'
import base from '../vitest.base.js'
import viteConfig from './vite.config.ts'

export default mergeConfig(
  mergeConfig(base, viteConfig),
  defineProject({
    test: {
      name: 'admin',
      environment: 'jsdom',
      setupFiles: ['./src/testing/setup.ts'],
      env: { VITE_API_BASE_URL: 'http://api.test' },
    },
  }),
)
