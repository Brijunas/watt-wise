export interface RuntimeConfig {
  apiBaseUrl: string
}

declare global {
  // Set by the `/config.js` script that `index.html` loads before the app.
  var __WATTWISE_CONFIG__: unknown
}

const loopbackHosts = new Set(['localhost', '127.0.0.1', '[::1]'])

const fail = (reason: string): never => {
  throw new Error(
    `Invalid runtime config from /config.js: ${reason}. ` +
      'The API_BASE_URL setting must be the API origin, such as https://api.example.test.',
  )
}

const readApiBaseUrl = (value: unknown): string => {
  if (typeof value !== 'string' || value === '') return fail('apiBaseUrl is missing')
  let url: URL
  try {
    url = new URL(value)
  } catch {
    return fail(`apiBaseUrl "${value}" is not a URL`)
  }
  const secure = url.protocol === 'https:'
  const localHttp = url.protocol === 'http:' && loopbackHosts.has(url.hostname)
  if (!secure && !localHttp) {
    return fail('apiBaseUrl must use https (http is allowed only for localhost)')
  }
  if (url.username || url.password) return fail('apiBaseUrl must not contain credentials')
  if (url.search || url.hash) return fail('apiBaseUrl must not contain a query or a fragment')
  if (url.pathname !== '/') return fail('apiBaseUrl must not contain a path')
  return url.origin
}

// Reads the config the `/config.js` script put on `globalThis`. Nothing is baked into the build.
const readRuntimeConfig = (source: unknown = globalThis.__WATTWISE_CONFIG__): RuntimeConfig => {
  if (typeof source !== 'object' || source === null)
    return fail('window.__WATTWISE_CONFIG__ is not set')
  return { apiBaseUrl: readApiBaseUrl((source as { apiBaseUrl?: unknown }).apiBaseUrl) }
}

export default readRuntimeConfig
