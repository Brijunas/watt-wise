import assert from 'node:assert/strict'
import { mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { describe, it } from 'node:test'
import { settingsPath } from './paths.ts'
import { parseSettings, readSettings, writeSettings } from './settings.ts'

describe('parseSettings', () => {
  it('reads the polling key and ignores an auth key from an earlier setup', () => {
    assert.deepEqual(parseSettings('{"auth":"desktop","polling":"on"}'), { polling: 'on' })
  })

  it('reads the remembered tools hash, only as a non-empty string', () => {
    assert.deepEqual(parseSettings('{"tools":"abc"}'), { tools: 'abc' })
    assert.deepEqual(parseSettings('{"tools":""}'), {})
    assert.deepEqual(parseSettings('{"tools":5}'), {})
  })

  it('reads nothing from broken or foreign content', () => {
    assert.deepEqual(parseSettings('not json'), {})
    assert.deepEqual(parseSettings('[]'), {})
    assert.deepEqual(parseSettings('null'), {})
    assert.deepEqual(parseSettings('{"polling":"maybe","other":1}'), {})
  })
})

describe('writeSettings and readSettings', () => {
  const withDir = (action: (dir: string) => void): void => {
    const dir = mkdtempSync(join(tmpdir(), 'setup-settings-'))
    try {
      action(dir)
    } finally {
      rmSync(dir, { recursive: true, force: true })
    }
  }

  it('round-trips, reports a change once and leaves no temp file', () => {
    withDir((dir) => {
      assert.deepEqual(readSettings(dir), {})
      assert.equal(writeSettings(dir, { polling: 'on' }), true)
      assert.equal(writeSettings(dir, { polling: 'on' }), false)
      assert.deepEqual(readSettings(dir), { polling: 'on' })
      assert.equal(writeSettings(dir, { polling: 'off', tools: 'abc' }), true)
      assert.deepEqual(readdirSync(dir), ['setup.json'])
    })
  })

  it('drops an old auth key at the next write', () => {
    withDir((dir) => {
      writeFileSync(settingsPath(dir), '{"auth":"desktop","polling":"on"}')
      assert.equal(writeSettings(dir, readSettings(dir)), true)
      assert.equal(readFileSync(settingsPath(dir), 'utf8'), '{\n  "polling": "on"\n}\n')
    })
  })

  it('replaces a broken file', () => {
    withDir((dir) => {
      writeFileSync(settingsPath(dir), 'garbage')
      assert.deepEqual(readSettings(dir), {})
      assert.equal(writeSettings(dir, { polling: 'on' }), true)
      assert.equal(readFileSync(settingsPath(dir), 'utf8'), '{\n  "polling": "on"\n}\n')
    })
  })
})
