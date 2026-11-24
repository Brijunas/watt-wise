// The rule for an origin setting (API_BASE_URL, FRONTEND_URL, APP_URL): https, or http only on a
// loopback host, and no credentials, path, query or fragment. `readRuntimeConfig` applies it in the
// browser and the `appUrl()` Vite plugin at build tooling time. It imports nothing, because Node
// loads `vite.config.ts` and its imports without a bundler (the file is imported with `.ts`).
// The container's start guard (`deploy/nginx/05-check-settings.sh`) applies the same rule in shell.

export type OriginProblem =
  'missing' | 'notUrl' | 'scheme' | 'credentials' | 'queryOrFragment' | 'path'

export type OriginCheck = { origin: string } | { problem: OriginProblem }

const loopbackHosts = new Set(['localhost', '127.0.0.1', '[::1]'])

export const checkOrigin = (value: unknown): OriginCheck => {
  if (typeof value !== 'string' || value === '') return { problem: 'missing' }
  let url: URL
  try {
    url = new URL(value)
  } catch {
    return { problem: 'notUrl' }
  }
  const secure = url.protocol === 'https:'
  const localHttp = url.protocol === 'http:' && loopbackHosts.has(url.hostname)
  if (!secure && !localHttp) return { problem: 'scheme' }
  if (url.username || url.password) return { problem: 'credentials' }
  if (url.search || url.hash) return { problem: 'queryOrFragment' }
  if (url.pathname !== '/') return { problem: 'path' }
  return { origin: url.origin }
}

// The wording of a problem, for the messages of both callers. `key` names the setting.
export const describeOriginProblem = (
  problem: OriginProblem,
  key: string,
  value: unknown,
): string => {
  switch (problem) {
    case 'missing':
      return `${key} is missing`
    case 'notUrl':
      return `${key} "${String(value)}" is not a URL`
    case 'scheme':
      return `${key} must use https (http is allowed only for localhost)`
    case 'credentials':
      return `${key} must not contain credentials`
    case 'queryOrFragment':
      return `${key} must not contain a query or a fragment`
    case 'path':
      return `${key} must not contain a path`
  }
}
