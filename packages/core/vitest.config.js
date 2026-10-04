import { defineProject, mergeConfig } from 'vitest/config'
import base from '../../vitest.base.js'

export default mergeConfig(base, defineProject({ test: { name: 'core' } }))
