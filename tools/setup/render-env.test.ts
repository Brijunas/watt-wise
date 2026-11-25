// The URL derivation and the bind guard, ported from the table of .github/scripts/check-setup.sh.
import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  BIND_ITEMS,
  checkBind,
  composeEnv,
  derivePublished,
  formatPublished,
} from './render-env.ts'
import { SetupError } from './steps.ts'

const FORMAT_MESSAGE = 'is not exactly an http origin'
const PORT_MESSAGE = 'is not a number from 1 to 65535'

const messageOf = (action: () => unknown): string => {
  try {
    action()
  } catch (error) {
    assert.ok(error instanceof SetupError, 'a SetupError, not another error')
    return error.message
  }
  assert.fail('expected it to stop')
}

describe('derivePublished', () => {
  const accepted: [string, string, string][] = [
    ['http://localhost:5062', '127.0.0.1', '5062'],
    ['http://127.0.0.1:5173', '127.0.0.1', '5173'],
    ['http://[::1]:5174', '::1', '5174'],
    ['http://localhost:1', '127.0.0.1', '1'],
    ['http://localhost:65535', '127.0.0.1', '65535'],
    ['http://127.0.0.1:65535', '127.0.0.1', '65535'],
    ['http://[::1]:1', '::1', '1'],
    ['http://[::1]:65535', '::1', '65535'],
  ]
  for (const [url, bindAddress, port] of accepted) {
    it(`accepts ${url}`, () => {
      assert.deepEqual(derivePublished('API', url), { bindAddress, port })
    })
  }

  it('derives the lines under the app name', () => {
    assert.equal(
      formatPublished('ADMIN', derivePublished('ADMIN', 'http://localhost:5174')),
      'ADMIN_BIND_ADDRESS="127.0.0.1"\nADMIN_PORT="5174"',
    )
  })

  const formatRejects: [string, string][] = [
    ['https', 'https://localhost:5062'],
    ['a path', 'http://localhost:5062/api'],
    ['a trailing slash', 'http://localhost:5062/'],
    ['two trailing slashes', 'http://localhost:5062//'],
    ['a public host', 'http://example.com:5062'],
    ['all interfaces', 'http://0.0.0.0:5062'],
    ['a host that only starts with localhost', 'http://localhost.example.com:5062'],
    ['a host that only starts with localhost, evil', 'http://localhost.evil:5062'],
    ['a host that only starts with 127.0.0.1', 'http://127.0.0.1.evil:5062'],
    ['credentials', 'http://localhost@evil.com:5062'],
    ['localhost in the path', 'http://evil.com/localhost:5062'],
    ['an uppercase host', 'http://LOCALHOST:5062'],
    ['a prefix before the scheme', 'xhttp://localhost:5062'],
    ['a leading space', ' http://localhost:5062'],
    ['a trailing space', 'http://localhost:5062 '],
    ['a trailing newline', 'http://localhost:5062\n'],
    ["dots that aren't dots", 'http://127x0x0x1:5062'],
    ['an empty port', 'http://[::1]:'],
    ['no port, [::1]', 'http://[::1]'],
    ['no port', 'http://localhost'],
    ['an empty value', ''],
  ]
  for (const [why, url] of formatRejects) {
    it(`rejects ${why}, naming FRONTEND_URL and the 'frontend' item`, () => {
      const message = messageOf(() => derivePublished('FRONTEND', url))
      assert.match(message, /FRONTEND_URL/)
      assert.match(message, /'frontend' item/)
      assert.ok(message.includes(FORMAT_MESSAGE), message)
    })
  }

  const portRejects: [string, string][] = [
    ['port 0', 'http://localhost:0'],
    ['port 65536', 'http://localhost:65536'],
    ['port 99999', 'http://localhost:99999'],
    ['port 100000', 'http://localhost:100000'],
    ['a leading zero', 'http://localhost:05062'],
    ['a leading zero on 1', 'http://localhost:00001'],
  ]
  for (const [why, url] of portRejects) {
    it(`rejects ${why} with the port message`, () => {
      const message = messageOf(() => derivePublished('FRONTEND', url))
      assert.match(message, /FRONTEND_URL/)
      assert.match(message, /'frontend' item/)
      assert.ok(message.includes(PORT_MESSAGE), message)
    })
  }

  it("names the app's own item", () => {
    assert.match(
      messageOf(() => derivePublished('JOBS', 'https://localhost:5062')),
      /'jobs' item/,
    )
    assert.match(
      messageOf(() => derivePublished('ADMIN', 'https://localhost:5174')),
      /'admin' item/,
    )
  })

  it('refuses an app other than API, JOBS, FRONTEND and ADMIN', () => {
    assert.match(
      messageOf(() => derivePublished('BOGUS', 'http://localhost:5062')),
      /unknown app/,
    )
  })
})

describe('checkBind', () => {
  const keys = Object.keys(BIND_ITEMS)
  const accepted = ['127.0.0.1', '127.0.0.2', '127.255.255.255', '127.0.0.0', '::1']
  const rejected: [string, string][] = [
    ['all interfaces', '0.0.0.0'],
    ['all IPv6 interfaces', '::'],
    ['a documentation address', '192.0.2.10'],
    ['another documentation address', '198.51.100.7'],
    ['a name', 'localhost'],
    ['a bracketed IPv6 loopback', '[::1]'],
    ['another IPv6 address', '::2'],
    ['an IPv4-mapped loopback', '::ffff:127.0.0.1'],
    ['just outside 127/8', '128.0.0.1'],
    ['an octet above 255', '127.0.0.256'],
    ['a leading zero', '127.0.0.01'],
    ['three octets', '127.0.0'],
    ['a host that only starts with 127.0.0.1', '127.0.0.1.evil'],
    ['a prefix length', '127.0.0.1/8'],
    ['a port', '127.0.0.1:5432'],
    ['a leading space', ' 127.0.0.1'],
    ['a trailing newline', '127.0.0.1\n'],
    ['an extra digit', '1270.0.0.1'],
    ['an empty value', ''],
  ]
  for (const key of keys) {
    const item = BIND_ITEMS[key] ?? ''
    for (const address of accepted) {
      it(`${key} accepts ${address}`, () => {
        assert.doesNotThrow(() => {
          checkBind(key, address)
        })
      })
    }
    for (const [why, address] of rejected) {
      it(`${key} rejects ${why}, naming the key and the '${item}' item`, () => {
        const message = messageOf(() => {
          checkBind(key, address)
        })
        assert.ok(message.includes(key), message)
        assert.ok(message.includes(`'${item}' item`), message)
        assert.ok(message.includes('not a loopback address'), message)
      })
    }
  }

  it('refuses a key that is not one of the five bind addresses', () => {
    assert.match(
      messageOf(() => {
        checkBind('BOGUS_BIND_ADDRESS', '127.0.0.1')
      }),
      /unknown key/,
    )
  })
})

describe('composeEnv', () => {
  const rendered = [
    '# a comment',
    'POSTGRES_BIND_ADDRESS="127.0.0.1"',
    'GRAFANA_BIND_ADDRESS="127.0.0.1"',
    'OTLP_BIND_ADDRESS="::1"',
    'MAILPIT_SMTP_BIND_ADDRESS="127.0.0.2"',
    'MAILPIT_BIND_ADDRESS="127.0.0.1"',
    'API_URL="http://localhost:5062"',
    'JOBS_URL="http://127.0.0.1:5063"',
    'FRONTEND_URL="http://localhost:5173"',
    'ADMIN_URL="http://[::1]:5174"',
    '',
  ].join('\n')

  it('appends the published lines and the polling mode', () => {
    const text = composeEnv(rendered, true)
    assert.ok(text.startsWith(rendered))
    assert.ok(text.includes('API_BIND_ADDRESS="127.0.0.1"\nAPI_PORT="5062"\n'))
    assert.ok(text.includes('ADMIN_BIND_ADDRESS="::1"\nADMIN_PORT="5174"\n'))
    assert.ok(text.endsWith('WATCH_POLLING="true"\n'))
    assert.ok(composeEnv(rendered, false).endsWith('WATCH_POLLING="false"\n'))
  })

  it('gives the same text for the same input', () => {
    assert.equal(composeEnv(rendered, false), composeEnv(rendered, false))
  })

  it('stops on a non-loopback bind address', () => {
    const bad = rendered.replace('OTLP_BIND_ADDRESS="::1"', 'OTLP_BIND_ADDRESS="0.0.0.0"')
    assert.match(
      messageOf(() => composeEnv(bad, false)),
      /OTLP_BIND_ADDRESS.*'otlp' item/,
    )
  })

  it('stops on a missing key or URL', () => {
    const noUrl = rendered.replace('ADMIN_URL="http://[::1]:5174"\n', '')
    assert.match(
      messageOf(() => composeEnv(noUrl, false)),
      /rendered no ADMIN_URL/,
    )
    const noBind = rendered.replace('MAILPIT_BIND_ADDRESS="127.0.0.1"\n', '')
    assert.match(
      messageOf(() => composeEnv(noBind, false)),
      /rendered no MAILPIT_BIND_ADDRESS/,
    )
  })
})
