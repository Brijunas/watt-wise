import { act, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { selectSessionStatus, sessionEnded, sessionStarted } from '@wattwise/core'
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import { server } from '@/testing/mocks/server'
import { authUrl, sessionsResponse } from '@/testing/mocks/handlers/auth'
import { createGate, problemResponse } from '@wattwise/core/testing'
import { renderRoutesAt } from '@/testing/test-utils'
import { routes } from '../router'

// The dates are formatted in the machine's zone; pin it so the literal texts below hold anywhere.
beforeAll(() => {
  process.env.TZ = 'UTC'
})

const removeListeners: (() => void)[] = []

/** Records unhandled rejections for the test; the listener is removed after it, pass or fail. */
const watchUnhandledRejections = () => {
  const unhandled = vi.fn()
  process.on('unhandledRejection', unhandled)
  removeListeners.push(() => process.off('unhandledRejection', unhandled))
  return unhandled
}

afterEach(() => {
  for (const remove of removeListeners.splice(0)) {
    remove()
  }
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

const firefoxId = '11111111-1111-1111-1111-111111111111'
const safariId = '22222222-2222-2222-2222-222222222222'

const heading = (name: string) => screen.findByRole('heading', { level: 1, name })

// The sessions list is named after the page; the md side navigation is a list too.
const sessionItems = async (title = 'Devices') =>
  within(await screen.findByRole('list', { name: title })).getAllByRole('listitem')

const itemOf = async (label: string, title?: string) => {
  const items = await sessionItems(title)
  const item = items.find((candidate) => within(candidate).queryByText(label) !== null)
  if (!item) {
    throw new Error(`No session item for ${label}`)
  }
  return item
}

describe('Devices page', () => {
  it('lists the sessions with their dates and marks this device', async () => {
    renderRoutesAt(routes, '/devices', { session: 'signedIn', language: 'en' })
    expect(await heading('Devices')).toBeTruthy()

    expect(await sessionItems()).toHaveLength(2)
    const firefox = await itemOf('Firefox on Linux')
    const safari = await itemOf('Safari on iPhone')
    expect(within(firefox).getByText('Firefox on Linux')).toBeTruthy()
    expect(within(firefox).getByText('Signed in: Jan 1, 2026, 8:00 AM')).toBeTruthy()
    expect(within(firefox).getByText('Last used: Jan 1, 2026, 9:30 AM')).toBeTruthy()
    expect(within(firefox).getByText('This device')).toBeTruthy()
    expect(within(safari).getByText('Safari on iPhone')).toBeTruthy()
    expect(within(safari).getByText('Signed in: Dec 30, 2025, 6:00 PM')).toBeTruthy()
    expect(within(safari).getByText('Last used: Dec 31, 2025, 8:15 PM')).toBeTruthy()
    expect(within(safari).queryByText('This device')).toBeNull()
    expect(screen.getAllByRole('button', { name: /^End session on / })).toHaveLength(2)
    expect(screen.getByRole('button', { name: 'Sign out everywhere' })).toBeTruthy()
  })

  it('formats the dates and texts in Lithuanian', async () => {
    renderRoutesAt(routes, '/devices', { session: 'signedIn', language: 'lt' })
    expect(await heading('Įrenginiai')).toBeTruthy()

    const firefox = await itemOf('Firefox on Linux', 'Įrenginiai')
    expect(within(firefox).getByText('Prisijungta: 2026-01-01 08:00')).toBeTruthy()
    expect(within(firefox).getByText('Paskutinį kartą naudota: 2026-01-01 09:30')).toBeTruthy()
    expect(within(firefox).getByText('Šis įrenginys')).toBeTruthy()
    expect(
      within(firefox).getByRole('button', { name: 'Užbaigti seansą įrenginyje Firefox on Linux' }),
    ).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Atsijungti visur' })).toBeTruthy()
  })

  it.each([
    [360, 'en', 'Devices', 'Sign out everywhere'],
    [900, 'en', 'Devices', 'Sign out everywhere'],
    [360, 'lt', 'Įrenginiai', 'Atsijungti visur'],
    [900, 'lt', 'Įrenginiai', 'Atsijungti visur'],
  ] as const)('renders at %i px in %s', async (width, language, title, signOutAll) => {
    renderRoutesAt(routes, '/devices', { session: 'signedIn', language, width })
    expect(await heading(title)).toBeTruthy()
    expect(await sessionItems(title)).toHaveLength(2)
    expect(screen.getByRole('button', { name: signOutAll })).toBeTruthy()
  })

  it('shows a loader while the list loads', async () => {
    const gate = createGate()
    server.use(
      http.get(authUrl('/sessions'), async () => {
        await gate.wait
        return HttpResponse.json(sessionsResponse)
      }),
    )
    renderRoutesAt(routes, '/devices', { session: 'signedIn', language: 'en' })
    await heading('Devices')
    expect(screen.getByRole('progressbar', { name: 'Loading' })).toBeTruthy()
    gate.open()
    expect(await sessionItems()).toHaveLength(2)
    expect(screen.queryByRole('progressbar')).toBeNull()
  })

  it('ends another session and the list is refetched without it', async () => {
    const user = userEvent.setup()
    let remaining = [...sessionsResponse]
    server.use(
      http.get(authUrl('/sessions'), () => HttpResponse.json(remaining)),
      http.delete(authUrl('/sessions/:id'), ({ params }) => {
        remaining = remaining.filter((item) => item.id !== params.id)
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderRoutesAt(routes, '/devices', { session: 'signedIn', language: 'en' })

    await user.click(await screen.findByRole('button', { name: 'End session on Safari on iPhone' }))

    await waitFor(() => {
      expect(screen.queryByText('Safari on iPhone')).toBeNull()
    })
    expect(screen.getByText('Firefox on Linux')).toBeTruthy()
    expect(screen.getByRole('heading', { level: 1, name: 'Devices' })).toBeTruthy()
  })

  it('ending the current session lands on the sign-in page', async () => {
    const user = userEvent.setup()
    const requested: string[] = []
    server.use(
      http.delete(authUrl('/sessions/:id'), ({ params }) => {
        requested.push(String(params.id))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { router, store } = renderRoutesAt(routes, '/devices', {
      session: 'signedIn',
      language: 'en',
    })

    await user.click(await screen.findByRole('button', { name: 'End session on Firefox on Linux' }))

    expect(await heading('Sign in')).toBeTruthy()
    expect(requested).toEqual([firefoxId])
    expect(router.state.location.pathname).toBe('/sign-in')
    expect(selectSessionStatus(store.getState())).toBe('signedOut')
  })

  it('signing out everywhere lands on the sign-in page', async () => {
    const user = userEvent.setup()
    const { router, store } = renderRoutesAt(routes, '/devices', {
      session: 'signedIn',
      language: 'en',
    })

    await user.click(await screen.findByRole('button', { name: 'Sign out everywhere' }))

    expect(await heading('Sign in')).toBeTruthy()
    expect(router.state.location.pathname).toBe('/sign-in')
    expect(selectSessionStatus(store.getState())).toBe('signedOut')
  })

  it('stays on the page with a message when signing out everywhere fails', async () => {
    const user = userEvent.setup()
    server.use(
      http.post(authUrl('/sign-out-everywhere'), () =>
        problemResponse({ status: 500, code: 'General.Unexpected' }),
      ),
    )
    const { router, store } = renderRoutesAt(routes, '/devices', {
      session: 'signedIn',
      language: 'en',
    })

    await user.click(await screen.findByRole('button', { name: 'Sign out everywhere' }))

    expect(
      await screen.findByText('Something went wrong on our side. Try again in a moment.'),
    ).toBeTruthy()
    expect(router.state.location.pathname).toBe('/devices')
    expect(selectSessionStatus(store.getState())).toBe('signedIn')
    expect(await sessionItems()).toHaveLength(2)
  })

  it('shows a message and refetches when the session is already gone', async () => {
    const user = userEvent.setup()
    let remaining = [...sessionsResponse]
    server.use(
      http.get(authUrl('/sessions'), () => HttpResponse.json(remaining)),
      http.delete(authUrl('/sessions/:id'), () => {
        remaining = remaining.filter((item) => item.id !== safariId)
        return problemResponse({ status: 404, code: 'Accounts.SessionNotFound' })
      }),
    )
    renderRoutesAt(routes, '/devices', { session: 'signedIn', language: 'en' })

    await user.click(await screen.findByRole('button', { name: 'End session on Safari on iPhone' }))

    expect(await screen.findByText('This device is no longer signed in.')).toBeTruthy()
    await waitFor(() => {
      expect(screen.queryByText('Safari on iPhone')).toBeNull()
    })
    expect(screen.getByText('Firefox on Linux')).toBeTruthy()
  })

  it('shows a message for another end-session error and keeps the list', async () => {
    const user = userEvent.setup()
    server.use(
      http.delete(authUrl('/sessions/:id'), () =>
        problemResponse({ status: 500, code: 'General.Unexpected' }),
      ),
    )
    renderRoutesAt(routes, '/devices', { session: 'signedIn', language: 'en' })

    await user.click(await screen.findByRole('button', { name: 'End session on Safari on iPhone' }))

    expect(
      await screen.findByText('Something went wrong on our side. Try again in a moment.'),
    ).toBeTruthy()
    expect(await sessionItems()).toHaveLength(2)
  })

  it('shows the error with a retry that loads the list', async () => {
    const user = userEvent.setup()
    let fail = true
    server.use(
      http.get(authUrl('/sessions'), () =>
        fail
          ? problemResponse({ status: 500, code: 'General.Unexpected' })
          : HttpResponse.json(sessionsResponse),
      ),
    )
    renderRoutesAt(routes, '/devices', { session: 'signedIn', language: 'en' })

    expect(
      await screen.findByText('Something went wrong on our side. Try again in a moment.'),
    ).toBeTruthy()
    expect(screen.queryByRole('list', { name: 'Devices' })).toBeNull()

    fail = false
    await user.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await sessionItems()).toHaveLength(2)
    expect(
      screen.queryByText('Something went wrong on our side. Try again in a moment.'),
    ).toBeNull()
  })
})

describe('Devices page and the session guard', () => {
  it('sends a signed-out visitor to the sign-in page', async () => {
    const { router } = renderRoutesAt(routes, '/devices', { session: 'signedOut', language: 'en' })

    expect(await heading('Sign in')).toBeTruthy()
    expect(router.state.location.pathname).toBe('/sign-in')
    expect(screen.queryByText('Firefox on Linux')).toBeNull()
  })

  it('shows the list without bouncing when the restore ends signed in', async () => {
    const { router, store } = renderRoutesAt(routes, '/devices', { language: 'en' })

    expect(await screen.findByRole('progressbar', { name: 'Loading' })).toBeTruthy()
    expect(router.state.location.pathname).toBe('/devices')

    act(() => {
      store.dispatch(sessionStarted({ accessToken: 'access-token' }))
    })

    expect(await sessionItems()).toHaveLength(2)
    expect(router.state.location.pathname).toBe('/devices')
    expect(screen.getByRole('heading', { level: 1, name: 'Devices' })).toBeTruthy()
  })

  it('shows nothing and throws nothing when the list answers after sign-out', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined)
    const gate = createGate()
    let answered = false
    server.use(
      http.get(authUrl('/sessions'), async () => {
        await gate.wait
        answered = true
        return HttpResponse.json(sessionsResponse)
      }),
    )
    const { router, store } = renderRoutesAt(routes, '/devices', {
      session: 'signedIn',
      language: 'en',
    })
    await heading('Devices')

    act(() => {
      store.dispatch(sessionEnded())
    })
    expect(await heading('Sign in')).toBeTruthy()

    gate.open()
    await waitFor(() => {
      expect(answered).toBe(true)
    })
    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 20))
    })

    expect(router.state.location.pathname).toBe('/sign-in')
    expect(screen.queryByText('Firefox on Linux')).toBeNull()
    expect(consoleError).not.toHaveBeenCalled()
  })

  it('changes nothing when an end-session answer arrives after the user left the page', async () => {
    const user = userEvent.setup()
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined)
    const gate = createGate()
    let answered = false
    server.use(
      http.delete(authUrl('/sessions/:id'), async () => {
        await gate.wait
        answered = true
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { router, store } = renderRoutesAt(routes, '/devices', {
      session: 'signedIn',
      language: 'en',
    })
    await user.click(await screen.findByRole('button', { name: 'End session on Safari on iPhone' }))

    await act(async () => {
      await router.navigate('/about')
    })
    gate.open()
    await waitFor(() => {
      expect(answered).toBe(true)
    })
    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 20))
    })

    expect(router.state.location.pathname).toBe('/about')
    expect(selectSessionStatus(store.getState())).toBe('signedIn')
    expect(
      screen.queryByText('Something went wrong on our side. Try again in a moment.'),
    ).toBeNull()
    expect(consoleError).not.toHaveBeenCalled()
  })

  it.each([
    ['an end-session', 'delete', '/sessions/:id', 'End session on Safari on iPhone'],
    ['a sign-out-everywhere', 'post', '/sign-out-everywhere', 'Sign out everywhere'],
  ] as const)(
    'ignores a failed %s answer that arrives after the user left the page',
    async (_name, method, path, button) => {
      const user = userEvent.setup()
      const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined)
      const unhandled = watchUnhandledRejections()
      const gate = createGate()
      let answered = false
      server.use(
        http[method](authUrl(path), async () => {
          await gate.wait
          answered = true
          return problemResponse({ status: 404, code: 'Accounts.SessionNotFound' })
        }),
      )
      const { router, store } = renderRoutesAt(routes, '/devices', {
        session: 'signedIn',
        language: 'en',
      })
      await user.click(await screen.findByRole('button', { name: button }))

      await act(async () => {
        await router.navigate('/about')
      })
      gate.open()
      await waitFor(() => {
        expect(answered).toBe(true)
      })
      await act(async () => {
        await new Promise((resolve) => setTimeout(resolve, 50))
      })

      expect(router.state.location.pathname).toBe('/about')
      expect(selectSessionStatus(store.getState())).toBe('signedIn')
      expect(unhandled).not.toHaveBeenCalled()
      expect(consoleError).not.toHaveBeenCalled()
    },
  )
})

const unexpected = 'Something went wrong on our side. Try again in a moment.'
const settle = () =>
  act(async () => {
    await new Promise((resolve) => setTimeout(resolve, 50))
  })

/** Counts the GET /auth/sessions requests. */
const countSessionLists = () => {
  const count = { value: 0 }
  server.use(
    http.get(authUrl('/sessions'), () => {
      count.value += 1
      return HttpResponse.json(sessionsResponse)
    }),
  )
  return count
}

const operations = [
  ['End session', 'delete', '/sessions/:id', 'End session on Safari on iPhone'],
  ['Sign out everywhere', 'post', '/sign-out-everywhere', 'Sign out everywhere'],
] as const

describe('Devices page texts', () => {
  it.each([
    ['en', 'These are the devices signed in to your account.', 'End session'],
    ['lt', 'Šiuose įrenginiuose esate prisijungę prie savo paskyros.', 'Užbaigti seansą'],
  ] as const)(
    'shows the intro and the visible End session text in %s',
    async (language, intro, end) => {
      renderRoutesAt(routes, '/devices', { session: 'signedIn', language })
      await screen.findByText(intro)

      const item = await itemOf('Safari on iPhone', language === 'en' ? 'Devices' : 'Įrenginiai')
      expect(within(item).getByText(end).tagName).toBe('BUTTON')
    },
  )

  // The devices endpoints don't read `Retry-After`, so a lockout there has no minutes to show.
  it.each([
    ['en', 'Too many failed attempts. Try again later.'],
    ['lt', 'Per daug nesėkmingų bandymų. Bandykite vėliau.'],
  ] as const)('shows the parameterless load failure in %s', async (language, message) => {
    server.use(
      http.get(authUrl('/sessions'), () =>
        problemResponse({ status: 423, code: 'Accounts.LockedOut' }),
      ),
    )
    renderRoutesAt(routes, '/devices', { session: 'signedIn', language })

    expect(await screen.findByText(message)).toBeTruthy()
  })

  it.each([
    ['en', 'Too many failed attempts. Try again later.', 'End session on Safari on iPhone'],
    ['en', 'Too many failed attempts. Try again later.', 'Sign out everywhere'],
    [
      'lt',
      'Per daug nesėkmingų bandymų. Bandykite vėliau.',
      'Užbaigti seansą įrenginyje Safari on iPhone',
    ],
    ['lt', 'Per daug nesėkmingų bandymų. Bandykite vėliau.', 'Atsijungti visur'],
  ] as const)(
    'shows the parameterless action failure in %s after %s',
    async (language, message, button) => {
      const user = userEvent.setup()
      const locked = () => problemResponse({ status: 423, code: 'Accounts.LockedOut' })
      server.use(
        http.delete(authUrl('/sessions/:id'), locked),
        http.post(authUrl('/sign-out-everywhere'), locked),
      )
      renderRoutesAt(routes, '/devices', { session: 'signedIn', language })

      await user.click(await screen.findByRole('button', { name: button }))

      expect(await screen.findByText(message)).toBeTruthy()
    },
  )
})

describe('Devices page actions', () => {
  it.each(operations)(
    'disables every action while %s is pending',
    async (_name, method, path, button) => {
      const user = userEvent.setup()
      const gate = createGate()
      server.use(
        http[method](authUrl(path), async () => {
          await gate.wait
          return new HttpResponse(null, { status: 204 })
        }),
      )
      renderRoutesAt(routes, '/devices', { session: 'signedIn', language: 'en' })
      await user.click(await screen.findByRole('button', { name: button }))

      await waitFor(() => {
        expect(
          [
            ...screen.getAllByRole('button', { name: /^End session on / }),
            screen.getByRole('button', { name: 'Sign out everywhere' }),
          ].map((action) => action.hasAttribute('disabled')),
        ).toEqual([true, true, true])
      })
    },
  )

  it.each([
    ['another end-session', 'End session on Safari on iPhone'],
    ['a sign-out-everywhere', 'Sign out everywhere'],
  ] as const)('clears the old failure when %s starts', async (_name, second) => {
    const user = userEvent.setup()
    const gate = createGate()
    let deletes = 0
    server.use(
      http.delete(authUrl('/sessions/:id'), async () => {
        deletes += 1
        if (deletes === 1) {
          return problemResponse({ status: 500, code: 'General.Unexpected' })
        }
        await gate.wait
        return new HttpResponse(null, { status: 204 })
      }),
      http.post(authUrl('/sign-out-everywhere'), async () => {
        await gate.wait
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderRoutesAt(routes, '/devices', { session: 'signedIn', language: 'en' })
    await user.click(await screen.findByRole('button', { name: 'End session on Safari on iPhone' }))
    await screen.findByText(unexpected)

    await user.click(screen.getByRole('button', { name: second }))

    await waitFor(() => {
      expect(screen.queryByText(unexpected)).toBeNull()
    })
  })

  it.each([
    ['navigates away', '/about'],
    ['signs out', '/sign-in'],
  ] as const)(
    'loads no list and shows no alert for a late "already gone" answer after the user %s',
    async (leave, pathname) => {
      const user = userEvent.setup()
      // A refetch of the unmounted list rejects ("not started yet") instead of sending a request.
      const unhandled = watchUnhandledRejections()
      const lists = countSessionLists()
      const gate = createGate()
      const answered = { value: false }
      server.use(
        http.delete(authUrl('/sessions/:id'), async () => {
          await gate.wait
          answered.value = true
          return problemResponse({ status: 404, code: 'Accounts.SessionNotFound' })
        }),
      )
      const { router, store } = renderRoutesAt(routes, '/devices', {
        session: 'signedIn',
        language: 'en',
      })
      await user.click(
        await screen.findByRole('button', { name: 'End session on Safari on iPhone' }),
      )
      if (leave === 'navigates away') {
        await act(async () => {
          await router.navigate('/about')
        })
      } else {
        act(() => {
          store.dispatch(sessionEnded())
        })
        await heading('Sign in')
      }
      gate.open()
      await waitFor(() => {
        expect(answered.value).toBe(true)
      })
      await settle()

      expect([
        lists.value,
        screen.queryByRole('alert'),
        router.state.location.pathname,
        unhandled.mock.calls.length,
      ]).toEqual([1, null, pathname, 0])
    },
  )

  // A newer sign-in during the request makes the api-client keep the session (its revision check),
  // so the page stays mounted after a successful answer: the one way to see what the component
  // does with a success.
  it('shows no alert when a sign-out-everywhere succeeds and the page stays', async () => {
    const user = userEvent.setup()
    const gate = createGate()
    const answered = { value: false }
    server.use(
      http.post(authUrl('/sign-out-everywhere'), async () => {
        await gate.wait
        answered.value = true
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { router, store } = renderRoutesAt(routes, '/devices', {
      session: 'signedIn',
      language: 'en',
    })
    await user.click(await screen.findByRole('button', { name: 'Sign out everywhere' }))
    act(() => {
      store.dispatch(sessionStarted({ accessToken: 'newer-token' }))
    })
    gate.open()
    await waitFor(() => {
      expect(answered.value).toBe(true)
    })
    await settle()

    expect([router.state.location.pathname, screen.queryByRole('alert')]).toEqual([
      '/devices',
      null,
    ])
  })

  it('does not reload the list after an end-session failure other than "already gone"', async () => {
    const user = userEvent.setup()
    const lists = countSessionLists()
    server.use(
      http.delete(authUrl('/sessions/:id'), () =>
        problemResponse({ status: 500, code: 'General.Unexpected' }),
      ),
    )
    renderRoutesAt(routes, '/devices', { session: 'signedIn', language: 'en' })
    await user.click(await screen.findByRole('button', { name: 'End session on Safari on iPhone' }))
    await screen.findByText(unexpected)
    await settle()

    expect(lists.value).toBe(1)
  })

  describe.each(operations)('an answer to %s that arrives late', (_name, method, path, button) => {
    const startLate = async (leave: 'navigates away' | 'signs out') => {
      const user = userEvent.setup()
      const lists = countSessionLists()
      const gate = createGate()
      const answered = { value: false }
      server.use(
        http[method](authUrl(path), async () => {
          await gate.wait
          answered.value = true
          return new HttpResponse(null, { status: 204 })
        }),
      )
      const { router, store } = renderRoutesAt(routes, '/devices', {
        session: 'signedIn',
        language: 'en',
      })
      await user.click(await screen.findByRole('button', { name: button }))
      if (leave === 'navigates away') {
        await act(async () => {
          await router.navigate('/about')
        })
      } else {
        act(() => {
          store.dispatch(sessionEnded())
        })
        await heading('Sign in')
      }
      gate.open()
      await waitFor(() => {
        expect(answered.value).toBe(true)
      })
      await settle()
      return { lists, router }
    }

    it('changes nothing and loads no list after the user navigates away', async () => {
      const { lists, router } = await startLate('navigates away')

      expect([lists.value, router.state.location.pathname, screen.queryByText(unexpected)]).toEqual(
        [1, '/about', null],
      )
    })

    it('changes nothing and loads no list after the user signs out', async () => {
      const { lists, router } = await startLate('signs out')

      expect([lists.value, router.state.location.pathname, screen.queryByText(unexpected)]).toEqual(
        [1, '/sign-in', null],
      )
    })
  })
})
