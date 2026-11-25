import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { parseArgs } from './args.ts'

describe('parseArgs', () => {
  it('runs with no options', () => {
    assert.deepEqual(parseArgs([]), {
      kind: 'run',
      polling: undefined,
      skipSecrets: false,
    })
  })

  it('reads the options, with a space or an equals sign', () => {
    assert.deepEqual(parseArgs(['--polling=on', '--skip-secrets']), {
      kind: 'run',
      polling: 'on',
      skipSecrets: true,
    })
  })

  it('refuses a bad value and an unknown option', () => {
    // --auth is gone: there is one sign-in, the token.
    assert.equal(parseArgs(['--auth', 'token']).kind, 'usage-error')
    assert.equal(parseArgs(['--polling']).kind, 'usage-error')
    assert.equal(parseArgs(['--polling', 'sometimes']).kind, 'usage-error')
    assert.equal(parseArgs(['--headless']).kind, 'usage-error')
  })

  it('answers --help and the two checks', () => {
    assert.deepEqual(parseArgs(['--help']), { kind: 'help' })
    assert.deepEqual(parseArgs(['--derive-url', 'API', 'http://localhost:1']), {
      kind: 'derive-url',
      app: 'API',
      url: 'http://localhost:1',
    })
    assert.deepEqual(parseArgs(['--check-bind', 'K', '::1']), {
      kind: 'check-bind',
      key: 'K',
      address: '::1',
    })
  })

  it('needs both arguments of the two checks', () => {
    assert.equal(parseArgs(['--derive-url', 'API']).kind, 'usage-error')
    assert.equal(parseArgs(['--check-bind', 'K']).kind, 'usage-error')
  })
})
