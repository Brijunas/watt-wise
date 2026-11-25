import assert from 'node:assert/strict'
import { posix, win32 } from 'node:path'
import { describe, it } from 'node:test'
import { configDir, settingsPath, tokenEnvPath } from './paths.ts'

// configDir joins with the running OS's separator, so the expectations are built the same way.
const expected = (base: string): string =>
  (process.platform === 'win32' ? win32 : posix).join(base, 'watt-wise')

describe('configDir', () => {
  it('uses XDG_CONFIG_HOME when set', () => {
    assert.equal(configDir({ XDG_CONFIG_HOME: '/xdg' }, '/home/dev'), expected('/xdg'))
  })

  it('uses ~/.config otherwise, an empty XDG_CONFIG_HOME included', () => {
    const home = process.platform === 'win32' ? 'C:\\Users\\dev' : '/home/dev'
    const wanted = expected((process.platform === 'win32' ? win32 : posix).join(home, '.config'))
    assert.equal(configDir({}, home), wanted)
    assert.equal(configDir({ XDG_CONFIG_HOME: '' }, home), wanted)
  })

  it('is the same folder on every OS: a Windows home gets .config too', () => {
    assert.match(win32.join('C:\\Users\\dev', '.config', 'watt-wise'), /\.config\\watt-wise$/)
  })
})

describe('the files in it', () => {
  it('are op.env and setup.json', () => {
    assert.match(tokenEnvPath('/d'), /op\.env$/)
    assert.match(settingsPath('/d'), /setup\.json$/)
  })
})
