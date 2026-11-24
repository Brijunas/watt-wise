import { checkOrigin, describeOriginProblem } from './origin.ts'

export interface RuntimeConfig {
  apiBaseUrl: string
}

declare global {
  // Set by the `/config.js` script that `index.html` loads before the app.
  var __WATTWISE_CONFIG__: unknown
}

const fail = (reason: string, setting: string, example: string): never => {
  throw new Error(
    `Invalid runtime config from /config.js: ${reason}. ` +
      `The ${setting} setting must be the ${example}.`,
  )
}

const apiSetting = 'API_BASE_URL'
const apiExample = 'API origin, such as https://api.example.test'

// Validates one origin setting: https (http only on loopback), no credentials, path, query or hash.
// `key` is the config key and `setting` the environment variable that feeds it, for the messages.
const readOrigin = (value: unknown, key: string, setting: string, example: string): string => {
  const checked = checkOrigin(value)
  if ('problem' in checked) {
    return fail(describeOriginProblem(checked.problem, key, value), setting, example)
  }
  return checked.origin
}

const readSource = (source: unknown): Record<string, unknown> => {
  if (typeof source !== 'object' || source === null)
    return fail('window.__WATTWISE_CONFIG__ is not set', apiSetting, apiExample)
  return source as Record<string, unknown>
}

// Reads the config the `/config.js` script put on `globalThis`. Nothing is baked into the build.
const readRuntimeConfig = (source: unknown = globalThis.__WATTWISE_CONFIG__): RuntimeConfig => {
  const config = readSource(source)
  return { apiBaseUrl: readOrigin(config.apiBaseUrl, 'apiBaseUrl', apiSetting, apiExample) }
}

/** The admin app also needs the frontend's address, for the forgot-password link. */
export interface AdminRuntimeConfig extends RuntimeConfig {
  frontendUrl: string
}

export const readAdminRuntimeConfig = (
  source: unknown = globalThis.__WATTWISE_CONFIG__,
): AdminRuntimeConfig => {
  const config = readSource(source)
  return {
    apiBaseUrl: readOrigin(config.apiBaseUrl, 'apiBaseUrl', apiSetting, apiExample),
    frontendUrl: readOrigin(
      config.frontendUrl,
      'frontendUrl',
      'FRONTEND_URL',
      'frontend origin, such as https://app.example.test',
    ),
  }
}

export default readRuntimeConfig
