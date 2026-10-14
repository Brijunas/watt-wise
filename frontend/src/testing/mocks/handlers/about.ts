import { http, HttpResponse } from 'msw'
import { env } from '@/config/env'

export const aboutResponse = { name: 'Watt-Wise API', version: '1.2.3' }

export const aboutHandlers = [
  http.get(`${env.apiBaseUrl}/api/v1/about`, () => HttpResponse.json(aboutResponse)),
]
