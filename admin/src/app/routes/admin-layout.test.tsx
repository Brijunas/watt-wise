import { act, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { api } from '@wattwise/api-client'
import {
  restoreSession,
  selectAccessToken,
  selectSessionStatus,
  sessionEnded,
  sessionStarted,
} from '@wattwise/core'
import { createGate, problemResponse } from '@wattwise/core/testing'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { adminMeResponse, authUrl, meUrl, userMeResponse } from '@/testing/mocks/handlers/auth'
import { server } from '@/testing/mocks/server'
import { renderRoutesAt } from '@/testing/test-utils'
import { routes } from '../router'

const signedInEn = { session: 'signedIn', language: 'en' } as const
const signedOutEn = { session: 'signedOut', language: 'en' } as const
const tick = () => new Promise((resolve) => setTimeout(resolve, 50))

const home = () => screen.findByRole('heading', { level: 1, name: 'Home' })
const signInHeading = () => screen.findByRole('heading', { level: 1, name: 'Sign in' })

const submit = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.type(screen.getByLabelText('Email', { selector: 'input' }), 'someone@example.com')
  await user.type(screen.getByLabelText('Password', { selector: 'input' }), 'whatever')
  await user.click(screen.getByRole('button', { name: 'Sign in' }))
}

/** Counts the sign-out requests the handler receives. */
const countSignOuts = () => {
  const count = { value: 0 }
  server.use(
    http.post(authUrl('/sign-out'), () => {
      count.value += 1
      return new HttpResponse(null, { status: 204 })
    }),
  )
  return count
}

/** A `/me` that waits for the gate; `answered()` says whether it has answered. */
const gatedMe = (body: unknown) => {
  const gate = createGate()
  const state = { answered: false }
  server.use(
    http.get(meUrl, async () => {
      await gate.wait
      state.answered = true
      return HttpResponse.json(body as Record<string, unknown>)
    }),
  )
  return { gate, state }
}

describe('Admin guard', () => {
  it.each([['/'], ['/about'], ['/nope']])(
    'sends an anonymous visit to %s to sign-in',
    async (path) => {
      const { router } = renderRoutesAt(routes, path, signedOutEn)

      expect(await signInHeading()).toBeTruthy()
      expect(router.state.location.pathname).toBe('/sign-in')
    },
  )

  it('redirects with the requested location in the state, replacing the entry', async () => {
    const { router } = renderRoutesAt(routes, '/about?tab=2#top', signedOutEn)
    await signInHeading()

    expect(router.state.historyAction).toBe('REPLACE')
    expect(router.state.location.state).toMatchObject({
      from: { pathname: '/about', search: '?tab=2', hash: '#top' },
    })
  })

  it('shows the loader and does not redirect while the session restores', async () => {
    const { router } = renderRoutesAt(routes, '/about', { language: 'en' })

    expect(await screen.findByRole('progressbar', { name: 'Loading' })).toBeTruthy()
    expect(router.state.location.pathname).toBe('/about')
    expect(screen.queryByRole('heading', { level: 1, name: 'Sign in' })).toBeNull()
    expect(screen.queryByRole('heading', { level: 1, name: 'About' })).toBeNull()
  })

  it('shows the loader while /me is pending, then the page', async () => {
    const { gate } = gatedMe(adminMeResponse)
    renderRoutesAt(routes, '/', signedInEn)

    expect(await screen.findByRole('progressbar', { name: 'Loading' })).toBeTruthy()
    expect(screen.queryByRole('heading', { level: 1, name: 'Home' })).toBeNull()
    expect(screen.queryByRole('heading', { level: 1, name: 'Sign in' })).toBeNull()
    expect(screen.queryByRole('heading', { level: 1, name: 'No access' })).toBeNull()
    gate.open()

    expect(await home()).toBeTruthy()
    expect(screen.queryByRole('progressbar')).toBeNull()
  })

  it('refetches /me on each mount instead of trusting stale data', async () => {
    let calls = 0
    server.use(
      http.get(meUrl, () => {
        calls += 1
        return HttpResponse.json(adminMeResponse)
      }),
    )
    const { router } = renderRoutesAt(routes, '/', signedInEn)
    await home()
    expect(calls).toBe(1)
    await act(() => router.navigate('/sign-in'))
    await act(() => router.navigate('/about'))
    await screen.findByRole('heading', { level: 1, name: 'About' })

    expect(calls).toBe(2)
  })

  it('shows the page for an account with admin.access', async () => {
    renderRoutesAt(routes, '/about', signedInEn)

    expect(await screen.findByRole('heading', { level: 1, name: 'About' })).toBeTruthy()
  })

  it('signs out once and goes to no-access for an account without admin.access', async () => {
    server.use(http.get(meUrl, () => HttpResponse.json(userMeResponse)))
    const signOuts = countSignOuts()
    const { router, store } = renderRoutesAt(routes, '/about', signedInEn)

    expect(await screen.findByRole('heading', { level: 1, name: 'No access' })).toBeTruthy()
    expect(router.state.location.pathname).toBe('/no-access')
    expect(router.state.historyAction).toBe('REPLACE')
    await waitFor(() => {
      expect(selectSessionStatus(store.getState())).toBe('signedOut')
    })
    await tick()
    expect(signOuts.value).toBe(1)
  })

  it('shows an error with a retry button when /me fails, and the page after a good retry', async () => {
    const user = userEvent.setup()
    let fail = true
    server.use(
      http.get(meUrl, () =>
        fail
          ? problemResponse({ status: 500, code: 'Odd.Thing' })
          : HttpResponse.json(adminMeResponse),
      ),
    )
    renderRoutesAt(routes, '/about', signedInEn)

    expect(await screen.findByRole('alert')).toBeTruthy()
    expect(screen.getByText('Something went wrong. Try again.')).toBeTruthy()
    expect(screen.queryByRole('heading', { level: 1, name: 'About' })).toBeNull()
    fail = false
    await user.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'About' })).toBeTruthy()
    expect(screen.queryByRole('alert')).toBeNull()
  })
})

describe('Admin guard with the sign-in form', () => {
  it('sends a non-admin who signs in to no-access, signed out, with one sign-out request', async () => {
    const user = userEvent.setup()
    server.use(http.get(meUrl, () => HttpResponse.json(userMeResponse)))
    const signOuts = countSignOuts()
    const { router, store } = renderRoutesAt(routes, '/', signedOutEn)
    await signInHeading()
    await submit(user)

    expect(await screen.findByRole('heading', { level: 1, name: 'No access' })).toBeTruthy()
    expect(router.state.location.pathname).toBe('/no-access')
    await waitFor(() => {
      expect(selectSessionStatus(store.getState())).toBe('signedOut')
    })
    expect(selectAccessToken(store.getState())).toBeNull()
    await tick()
    expect(signOuts.value).toBe(1)
  })

  it('shows the shell to an admin who signs in and returns to the requested page', async () => {
    const user = userEvent.setup()
    const { router } = renderRoutesAt(routes, '/about', signedOutEn)
    await signInHeading()
    await submit(user)

    expect(await screen.findByRole('heading', { level: 1, name: 'About' })).toBeTruthy()
    expect(router.state.location.pathname).toBe('/about')
    expect(screen.getByRole('navigation', { name: 'Main navigation' })).toBeTruthy()
  })

  it('shows Home with the navigation to an admin who signs in at /', async () => {
    const user = userEvent.setup()
    renderRoutesAt(routes, '/', signedOutEn)
    await signInHeading()
    await submit(user)

    expect(await home()).toBeTruthy()
    expect(screen.getByRole('navigation', { name: 'Main navigation' })).toBeTruthy()
  })
})

describe('Admin guard session restore', () => {
  it('keeps an admin on the page across a reload, never passing through signed out or sign-in', async () => {
    const { store } = renderRoutesAt(routes, '/about', { language: 'en' })
    const statuses: string[] = [selectSessionStatus(store.getState())]
    store.subscribe(() => {
      statuses.push(selectSessionStatus(store.getState()))
    })
    let signInSeen = false
    const observer = new MutationObserver(() => {
      if (document.querySelector('h1')?.textContent === 'Sign in') {
        signInSeen = true
      }
    })
    observer.observe(document.body, { subtree: true, childList: true, characterData: true })

    expect(await screen.findByRole('progressbar', { name: 'Loading' })).toBeTruthy()
    expect(screen.queryByRole('heading', { level: 1, name: 'Sign in' })).toBeNull()
    await act(async () => {
      await store.dispatch(restoreSession())
    })

    expect(await screen.findByRole('heading', { level: 1, name: 'About' })).toBeTruthy()
    observer.disconnect()
    expect(statuses).toContain('signedIn')
    expect(statuses).not.toContain('signedOut')
    expect(signInSeen).toBe(false)
    expect(screen.queryByRole('heading', { level: 1, name: 'Sign in' })).toBeNull()
  })
})

describe('Admin guard deferred answers', () => {
  it('ignores a non-admin /me that answers after the user signed out', async () => {
    const user = userEvent.setup()
    const { gate, state } = gatedMe(userMeResponse)
    const signOuts = countSignOuts()
    const { router } = renderRoutesAt(routes, '/', signedInEn)
    await screen.findByRole('progressbar', { name: 'Loading' })
    await user.click(screen.getByRole('button', { name: 'Sign out' }))
    expect(await signInHeading()).toBeTruthy()
    gate.open()
    await waitFor(() => {
      expect(state.answered).toBe(true)
    })
    await tick()

    expect(screen.queryByRole('heading', { level: 1, name: 'No access' })).toBeNull()
    expect(router.state.location.pathname).toBe('/sign-in')
    expect(signOuts.value).toBe(1)
  })

  it('never shows the shell for an admin /me that answers after the user signed out', async () => {
    const user = userEvent.setup()
    const { gate, state } = gatedMe(adminMeResponse)
    const { router, store } = renderRoutesAt(routes, '/', signedInEn)
    await screen.findByRole('progressbar', { name: 'Loading' })
    await user.click(screen.getByRole('button', { name: 'Sign out' }))
    expect(await signInHeading()).toBeTruthy()
    gate.open()
    await waitFor(() => {
      expect(state.answered).toBe(true)
    })
    await tick()

    expect(screen.queryByRole('heading', { level: 1, name: 'Home' })).toBeNull()
    expect(router.state.location.pathname).toBe('/sign-in')
    expect(selectSessionStatus(store.getState())).toBe('signedOut')
  })
})

describe('Sign-out button', () => {
  it('signs out and sends the user to sign-in', async () => {
    const user = userEvent.setup()
    const signOuts = countSignOuts()
    const { router, store } = renderRoutesAt(routes, '/about', signedInEn)
    await screen.findByRole('heading', { level: 1, name: 'About' })
    await user.click(screen.getByRole('button', { name: 'Sign out' }))

    expect(await signInHeading()).toBeTruthy()
    expect(router.state.location.pathname).toBe('/sign-in')
    expect(selectSessionStatus(store.getState())).toBe('signedOut')
    expect(signOuts.value).toBe(1)
  })

  it('is not shown after the session ends', async () => {
    const { store } = renderRoutesAt(routes, '/about', signedInEn)
    await screen.findByRole('heading', { level: 1, name: 'About' })
    act(() => {
      store.dispatch(sessionEnded())
    })

    await signInHeading()
    expect(screen.queryByRole('button', { name: 'Sign out' })).toBeNull()
    act(() => {
      store.dispatch(sessionStarted({ accessToken: 't' }))
    })
    expect(await screen.findByRole('button', { name: 'Sign out' })).toBeTruthy()
  })
})

describe('Admin guard in StrictMode', () => {
  it('sends one sign-out request and shows the link only after it answers', async () => {
    server.use(http.get(meUrl, () => HttpResponse.json(userMeResponse)))
    const gate = createGate()
    const signOuts = { value: 0 }
    server.use(
      http.post(authUrl('/sign-out'), async () => {
        signOuts.value += 1
        await gate.wait
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { router, store } = renderRoutesAt(routes, '/about', { ...signedInEn, strict: true })

    expect(await screen.findByRole('heading', { level: 1, name: 'No access' })).toBeTruthy()
    await waitFor(() => {
      expect(signOuts.value).toBe(1)
    })
    await tick()
    expect(signOuts.value).toBe(1)
    expect(router.state.location.pathname).toBe('/no-access')
    expect(screen.queryByRole('link', { name: 'Sign in' })).toBeNull()
    gate.open()

    expect(await screen.findByRole('link', { name: 'Sign in' })).toBeTruthy()
    await tick()
    expect(signOuts.value).toBe(1)
    expect(router.state.location.pathname).toBe('/no-access')
    expect(selectSessionStatus(store.getState())).toBe('signedOut')
  })

  it('shows the page to an admin without any sign-out request', async () => {
    const signOuts = countSignOuts()
    renderRoutesAt(routes, '/about', { ...signedInEn, strict: true })

    expect(await screen.findByRole('heading', { level: 1, name: 'About' })).toBeTruthy()
    await tick()
    expect(signOuts.value).toBe(0)
  })
})

describe('Admin guard freshness within one millisecond', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  /** Records whether a protected page's h1 ever appeared in the DOM. */
  const watchHeading = (text: string) => {
    const seen = { value: false }
    const observer = new MutationObserver(() => {
      if (document.querySelector('h1')?.textContent === text) {
        seen.value = true
      }
    })
    observer.observe(document.body, { subtree: true, childList: true, characterData: true })
    return {
      seen,
      stop: () => {
        observer.disconnect()
      },
    }
  }

  it('does not show a cached allowed answer while the fresh one is pending, and the fresh denial decides', async () => {
    vi.spyOn(Date, 'now').mockReturnValue(Date.now())
    const { router, store } = renderRoutesAt(routes, '/', signedInEn)
    await home()
    await act(() => router.navigate('/no-access'))
    await screen.findByRole('heading', { level: 1, name: 'No access' })
    const { gate, state } = gatedMe(userMeResponse)
    const signOuts = countSignOuts()
    const watch = watchHeading('About')
    await act(() => router.navigate('/about'))

    expect(await screen.findByRole('progressbar', { name: 'Loading' })).toBeTruthy()
    await tick()
    expect(watch.seen.value).toBe(false)
    expect(signOuts.value).toBe(0)
    gate.open()
    await waitFor(() => {
      expect(state.answered).toBe(true)
    })

    expect(await screen.findByRole('heading', { level: 1, name: 'No access' })).toBeTruthy()
    await waitFor(() => {
      expect(selectSessionStatus(store.getState())).toBe('signedOut')
    })
    await tick()
    watch.stop()
    expect(watch.seen.value).toBe(false)
    expect(signOuts.value).toBe(1)
  })

  it('does not deny on a cached denied answer while the fresh one is pending, and the fresh admin answer decides', async () => {
    vi.spyOn(Date, 'now').mockReturnValue(Date.now())
    const { router, store } = renderRoutesAt(routes, '/no-access', signedInEn)
    await screen.findByRole('heading', { level: 1, name: 'No access' })
    await act(async () => {
      await store.dispatch(api.util.upsertQueryData('getMe', undefined, userMeResponse))
    })
    const { gate, state } = gatedMe(adminMeResponse)
    const signOuts = countSignOuts()
    await act(() => router.navigate('/about'))

    expect(await screen.findByRole('progressbar', { name: 'Loading' })).toBeTruthy()
    await tick()
    expect(signOuts.value).toBe(0)
    expect(router.state.location.pathname).toBe('/about')
    gate.open()
    await waitFor(() => {
      expect(state.answered).toBe(true)
    })

    expect(await screen.findByRole('heading', { level: 1, name: 'About' })).toBeTruthy()
    expect(signOuts.value).toBe(0)
    expect(selectSessionStatus(store.getState())).toBe('signedIn')
  })
})

describe('Admin guard leaving and returning while /me is pending', () => {
  const leaveAndReturn = async (body: unknown) => {
    const { gate, state } = gatedMe(body)
    const signOuts = countSignOuts()
    const { router, store } = renderRoutesAt(routes, '/about', signedInEn)
    expect(await screen.findByRole('progressbar', { name: 'Loading' })).toBeTruthy()
    await act(() => router.navigate('/no-access'))
    await screen.findByRole('heading', { level: 1, name: 'No access' })
    await act(() => router.navigate('/about'))
    expect(await screen.findByRole('progressbar', { name: 'Loading' })).toBeTruthy()
    gate.open()
    await waitFor(() => {
      expect(state.answered).toBe(true)
    })
    return { router, store, signOuts }
  }

  it('shows the page for an admin answer, with no sign-out request', async () => {
    const { signOuts } = await leaveAndReturn(adminMeResponse)

    expect(await screen.findByRole('heading', { level: 1, name: 'About' })).toBeTruthy()
    expect(screen.queryByRole('progressbar', { name: 'Loading' })).toBeNull()
    await tick()
    expect(signOuts.value).toBe(0)
  })

  it('ends on no-access, signed out, after one sign-out request for a non-admin answer', async () => {
    const { router, store, signOuts } = await leaveAndReturn(userMeResponse)

    expect(await screen.findByRole('heading', { level: 1, name: 'No access' })).toBeTruthy()
    await waitFor(() => {
      expect(selectSessionStatus(store.getState())).toBe('signedOut')
    })
    await tick()
    expect(router.state.location.pathname).toBe('/no-access')
    expect(signOuts.value).toBe(1)
  })
})
