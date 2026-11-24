import fc from 'fast-check'
import { describe, expect, it } from 'vitest'
import { checkOrigin, describeOriginProblem } from './origin'

describe('checkOrigin examples', () => {
  it.each([undefined, null, 42, '', {}])('reports %j as missing', (value) => {
    expect(checkOrigin(value)).toEqual({ problem: 'missing' })
  })

  it('reports text that is not a URL as notUrl', () => {
    expect(checkOrigin('not a url')).toEqual({ problem: 'notUrl' })
  })

  it.each([
    'ftp://example.com',
    'http://example.com',
    'http://localhost.evil.com',
    'http://127.0.0.2:3000',
    'ftp://localhost:3000',
    'ws://127.0.0.1:3000',
  ])('rejects %s with scheme', (value) => {
    expect(checkOrigin(value)).toEqual({ problem: 'scheme' })
  })

  it.each([
    ['https://example.com', 'https://example.com'],
    ['https://example.com/', 'https://example.com'],
    ['https://example.com:8443', 'https://example.com:8443'],
    ['https://localhost:5173', 'https://localhost:5173'],
    ['http://localhost:5173', 'http://localhost:5173'],
    ['http://127.0.0.1:3000', 'http://127.0.0.1:3000'],
    ['http://[::1]:3000', 'http://[::1]:3000'],
    ['HTTP://LOCALHOST:3000', 'http://localhost:3000'],
    ['HTTPS://X.TEST', 'https://x.test'],
    ['https://x.test:443', 'https://x.test'],
    ['http://localhost:80', 'http://localhost'],
  ])('accepts %s as %s', (value, origin) => {
    expect(checkOrigin(value)).toEqual({ origin })
  })

  it('rejects credentials', () => {
    expect(checkOrigin('https://user:pw@example.com')).toEqual({ problem: 'credentials' })
    expect(checkOrigin('https://user@example.com')).toEqual({ problem: 'credentials' })
  })

  it('rejects a query', () => {
    expect(checkOrigin('https://example.com/?a=1')).toEqual({ problem: 'queryOrFragment' })
  })

  it('rejects a fragment', () => {
    expect(checkOrigin('https://example.com/#top')).toEqual({ problem: 'queryOrFragment' })
  })

  it('rejects a path', () => {
    expect(checkOrigin('https://example.com/app')).toEqual({ problem: 'path' })
  })
})

describe('describeOriginProblem', () => {
  it.each([
    ['missing', 'APP_URL is missing'],
    ['notUrl', 'APP_URL "x y" is not a URL'],
    ['scheme', 'APP_URL must use https (http is allowed only for localhost)'],
    ['credentials', 'APP_URL must not contain credentials'],
    ['queryOrFragment', 'APP_URL must not contain a query or a fragment'],
    ['path', 'APP_URL must not contain a path'],
  ] as const)('words %s', (problem, message) => {
    expect(describeOriginProblem(problem, 'APP_URL', 'x y')).toBe(message)
  })
})

describe('checkOrigin properties', () => {
  const host = fc.domain()
  // Non-default ports only: the default ports have their own examples above.
  const port = fc
    .integer({ min: 1, max: 65535 })
    .filter((p) => p !== 80 && p !== 443)
    .map(String)
  const word = fc.stringMatching(/^[a-z0-9]{1,12}$/)

  it('accepts an https origin and returns it', () => {
    fc.assert(
      fc.property(host, port, (h, p) => {
        expect(checkOrigin(`https://${h}:${p}`)).toEqual({ origin: `https://${h}:${p}` })
      }),
    )
  })

  it('rejects any other scheme with scheme, on a loopback or any host', () => {
    fc.assert(
      fc.property(
        fc.constantFrom('ftp', 'ws', 'wss', 'ssh', 'gopher'),
        fc.oneof(host, fc.constantFrom('localhost', '127.0.0.1', '[::1]')),
        port,
        (scheme, h, p) => {
          expect(checkOrigin(`${scheme}://${h}:${p}`)).toEqual({ problem: 'scheme' })
        },
      ),
    )
  })

  it('rejects http on a non-loopback host with scheme', () => {
    fc.assert(
      fc.property(host, port, (h, p) => {
        expect(checkOrigin(`http://${h}:${p}`)).toEqual({ problem: 'scheme' })
      }),
    )
  })

  it('accepts http on each loopback host with any port', () => {
    fc.assert(
      fc.property(fc.constantFrom('localhost', '127.0.0.1', '[::1]'), port, (h, p) => {
        expect(checkOrigin(`http://${h}:${p}`)).toEqual({ origin: `http://${h}:${p}` })
      }),
    )
  })

  it('rejects any added path', () => {
    fc.assert(
      fc.property(host, word, (h, segment) => {
        expect(checkOrigin(`https://${h}/${segment}`)).toEqual({ problem: 'path' })
      }),
    )
  })

  it('rejects any added query', () => {
    fc.assert(
      fc.property(host, word, (h, query) => {
        expect(checkOrigin(`https://${h}/?${query}`)).toEqual({ problem: 'queryOrFragment' })
      }),
    )
  })

  it('rejects any added fragment', () => {
    fc.assert(
      fc.property(host, word, (h, fragment) => {
        expect(checkOrigin(`https://${h}/#${fragment}`)).toEqual({ problem: 'queryOrFragment' })
      }),
    )
  })

  it('rejects any added credentials', () => {
    fc.assert(
      fc.property(host, word, word, (h, user, password) => {
        expect(checkOrigin(`https://${user}:${password}@${h}`)).toEqual({
          problem: 'credentials',
        })
      }),
    )
  })
})
