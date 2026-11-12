import { setupServer } from 'msw/node'
import { aboutHandlers } from './handlers/about'
import { authHandlers } from './handlers/auth'

export const server = setupServer(...aboutHandlers, ...authHandlers)
