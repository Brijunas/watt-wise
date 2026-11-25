import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { describe, it } from 'node:test'
import { nugetDir, nugetState } from './nuget.ts'

describe('nugetDir', () => {
  it('uses NUGET_PACKAGES when set, else ~/.nuget/packages', () => {
    assert.equal(nugetDir({ NUGET_PACKAGES: '/cache' }, '/home/dev'), '/cache')
    assert.equal(nugetDir({}, '/home/dev'), join('/home/dev', '.nuget', 'packages'))
    assert.equal(
      nugetDir({ NUGET_PACKAGES: '' }, '/home/dev'),
      join('/home/dev', '.nuget', 'packages'),
    )
  })
})

describe('nugetState', () => {
  it('is empty for a missing folder and changes when a package version appears', () => {
    const dir = mkdtempSync(join(tmpdir(), 'setup-nuget-'))
    try {
      assert.equal(nugetState(join(dir, 'none')), '')
      mkdirSync(join(dir, 'dotnet-ef', '10.0.0'), { recursive: true })
      const before = nugetState(dir)
      assert.equal(before, 'dotnet-ef/10.0.0')
      assert.equal(nugetState(dir), before)
      mkdirSync(join(dir, 'dotnet-stryker', '4.0.0'), { recursive: true })
      assert.notEqual(nugetState(dir), before)
    } finally {
      rmSync(dir, { recursive: true, force: true })
    }
  })
})
