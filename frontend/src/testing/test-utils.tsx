import { render } from '@testing-library/react'
import type { ReactElement } from 'react'
import { createMemoryRouter } from 'react-router'
import type { RouteObject } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { sessionEnded, sessionStarted } from '@wattwise/core'
import type { Language } from '@wattwise/i18n'
import AppProvider from '@/app/provider'
import { createAppI18n } from '@/lib/i18n'
import createAppStore from '@/stores/create-app-store'
import stubMatchMedia from './match-media'

export interface RenderOptions {
  /**
   * The session the store starts with. Omitted, the store starts as a fresh one does: restoring.
   * `signedIn` dispatches `sessionStarted`, `signedOut` dispatches `sessionEnded`.
   */
  session?: 'signedIn' | 'signedOut'
  /** The language of the i18n instance, instead of the detected one. */
  language?: Language
}

const setup = ({ session, language }: RenderOptions) => {
  const store = createAppStore()
  if (session === 'signedIn') {
    store.dispatch(sessionStarted({ accessToken: 'access-token' }))
  } else if (session === 'signedOut') {
    store.dispatch(sessionEnded())
  }
  const i18n = createAppI18n()
  if (language !== undefined) {
    void i18n.changeLanguage(language)
  }
  return { store, i18n }
}

const renderWithProviders = (ui: ReactElement, options: RenderOptions = {}) => {
  const { store, i18n } = setup(options)
  return {
    store,
    ...render(
      <AppProvider store={store} i18n={i18n}>
        {ui}
      </AppProvider>,
    ),
  }
}

export interface RenderRoutesOptions extends RenderOptions {
  /** The screen width in px: 360 is a phone (`xs`), 900 is `md`. Defaults to 360. */
  width?: number
}

/** Renders the given `routes` (the app's real ones, from `app/router.tsx`) at `path` in a memory router. */
export const renderRoutesAt = (
  routes: RouteObject[],
  path: string | { pathname: string; search?: string; state?: unknown },
  { width = 360, ...options }: RenderRoutesOptions = {},
) => {
  stubMatchMedia({ width })
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  return { router, ...renderWithProviders(<RouterProvider router={router} />, options) }
}

export default renderWithProviders
