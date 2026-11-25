import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { defaultPolling, isWslPath, resolvePolling } from './polling.ts'

describe('defaultPolling', () => {
  it('is on for macOS', () => {
    assert.equal(defaultPolling('darwin', '/Users/dev/watt-wise'), true)
  })

  it('is on for Windows when the repo is not inside WSL', () => {
    assert.equal(defaultPolling('win32', 'C:\\Users\\dev\\watt-wise'), true)
  })

  it('is off for Windows when the repo is inside WSL', () => {
    assert.equal(defaultPolling('win32', '\\\\wsl$\\Ubuntu\\home\\dev\\watt-wise'), false)
    assert.equal(defaultPolling('win32', '\\\\wsl.localhost\\Ubuntu\\home\\dev\\watt-wise'), false)
  })

  it('is off for Linux, WSL included', () => {
    assert.equal(defaultPolling('linux', '/home/dev/watt-wise'), false)
    assert.equal(defaultPolling('linux', '/mnt/c/Users/dev/watt-wise'), false)
  })
})

describe('isWslPath', () => {
  it('knows the two WSL share names, in any case', () => {
    assert.equal(isWslPath('\\\\WSL$\\Debian\\home'), true)
    assert.equal(isWslPath('//wsl.localhost/Debian/home'), true)
  })

  it('does not match other shares or drives', () => {
    assert.equal(isWslPath('\\\\server\\share'), false)
    assert.equal(isWslPath('C:\\wsl$\\x'), false)
  })
})

describe('resolvePolling', () => {
  const where = { platform: 'linux', repoPath: '/home/dev/watt-wise' }

  it('uses the rule without a flag or a saved choice', () => {
    assert.deepEqual(resolvePolling(where), { enabled: false, source: 'default' })
    assert.deepEqual(resolvePolling({ platform: 'darwin', repoPath: '/x' }), {
      enabled: true,
      source: 'default',
    })
  })

  it('uses the saved choice over the rule', () => {
    assert.deepEqual(resolvePolling({ ...where, saved: 'on' }), { enabled: true, source: 'saved' })
    assert.deepEqual(resolvePolling({ platform: 'darwin', repoPath: '/x', saved: 'off' }), {
      enabled: false,
      source: 'saved',
    })
  })

  it('uses the flag over the saved choice', () => {
    assert.deepEqual(resolvePolling({ ...where, flag: 'on', saved: 'off' }), {
      enabled: true,
      source: 'flag',
    })
    assert.deepEqual(resolvePolling({ ...where, flag: 'off', saved: 'on' }), {
      enabled: false,
      source: 'flag',
    })
  })

  it('treats auto as the rule, ignoring the saved choice', () => {
    assert.deepEqual(
      resolvePolling({ platform: 'darwin', repoPath: '/x', flag: 'auto', saved: 'off' }),
      {
        enabled: true,
        source: 'default',
      },
    )
  })
})
