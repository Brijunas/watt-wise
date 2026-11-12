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

/** `/me` of an account with the Admin role. */
export const adminMeResponse = {
  email: 'admin@example.com',
  language: 'en',
  roles: ['Admin'],
  permissions: ['admin.access'],
}

/** `/me` of an account without roles or permissions. */
export const userMeResponse = {
  email: 'jonas@example.com',
  language: 'lt',
  roles: [],
  permissions: [],
}

const noContent = () => new HttpResponse(null, { status: 204 })

export const authHandlers = [
  http.post(authUrl('/sign-in'), () => HttpResponse.json(signedInResponse)),
  http.post(authUrl('/refresh'), () => HttpResponse.json(signedInResponse)),
  http.post(authUrl('/sign-out'), noContent),
  http.get(meUrl, () => HttpResponse.json(adminMeResponse)),
]
