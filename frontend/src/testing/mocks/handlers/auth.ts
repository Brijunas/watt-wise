import { http, HttpResponse } from 'msw'
import { env } from '@/config/env'

/** The URL of an `/api/v1/auth` endpoint, for a test's own `server.use(...)`. */
export const authUrl = (path: string) => `${env.apiBaseUrl}/api/v1/auth${path}`

/** The URL of `/api/v1/me`. */
export const meUrl = `${env.apiBaseUrl}/api/v1/me`

export const signedInResponse = {
  accessToken: 'access-token',
  accessTokenExpiresAt: '2026-01-01T00:15:00Z',
}

export const sessionsResponse = [
  {
    id: '11111111-1111-1111-1111-111111111111',
    deviceLabel: 'Firefox on Linux',
    signedInAt: '2026-01-01T08:00:00Z',
    lastUsedAt: '2026-01-01T09:30:00Z',
    current: true,
  },
  {
    id: '22222222-2222-2222-2222-222222222222',
    deviceLabel: 'Safari on iPhone',
    signedInAt: '2025-12-30T18:00:00Z',
    lastUsedAt: '2025-12-31T20:15:00Z',
    current: false,
  },
]

export const meResponse = {
  email: 'jonas@example.com',
  language: 'lt',
  roles: [],
  permissions: [],
}

const noContent = () => new HttpResponse(null, { status: 204 })

export const authHandlers = [
  http.post(authUrl('/register'), noContent),
  http.post(authUrl('/confirm-email'), noContent),
  http.post(authUrl('/resend-confirmation'), noContent),
  http.post(authUrl('/sign-in'), () => HttpResponse.json(signedInResponse)),
  http.post(authUrl('/refresh'), () => HttpResponse.json(signedInResponse)),
  http.post(authUrl('/sign-out'), noContent),
  http.post(authUrl('/forgot-password'), noContent),
  http.post(authUrl('/reset-password'), noContent),
  http.post(authUrl('/change-password'), () => HttpResponse.json(signedInResponse)),
  http.get(authUrl('/sessions'), () => HttpResponse.json(sessionsResponse)),
  http.delete(authUrl('/sessions/:id'), noContent),
  http.post(authUrl('/sign-out-everywhere'), noContent),
  http.get(meUrl, () => HttpResponse.json(meResponse)),
]
