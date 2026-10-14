import { setupServer } from 'msw/node'
import { aboutHandlers } from './handlers/about'

export const server = setupServer(...aboutHandlers)
