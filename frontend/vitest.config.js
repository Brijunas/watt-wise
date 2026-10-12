import { defineProject, mergeConfig } from 'vitest/config'
import base from '../vitest.base.js'
import viteConfig from './vite.config.ts'

export default mergeConfig(
  mergeConfig(base, viteConfig),
  defineProject({ test: { name: 'frontend', environment: 'jsdom' } }),
)
