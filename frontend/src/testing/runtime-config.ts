// Runs before setup.ts: `config/env.ts` reads the config on import, and setup.ts imports it.
globalThis.__WATTWISE_CONFIG__ = { apiBaseUrl: 'https://api.test' }
