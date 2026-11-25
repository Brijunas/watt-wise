import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  isAgentSession,
  isMidOperation,
  MID_OPERATION_MARKERS,
  sameCheckout,
} from './follow-stack-rules.ts'

describe('isMidOperation', () => {
  it('sees each marker in the git folder', () => {
    for (const marker of MID_OPERATION_MARKERS) {
      assert.equal(
        isMidOperation('/repo/.git', (path) => path.endsWith(marker)),
        true,
        marker,
      )
    }
  })

  it('is false when none exists', () => {
    assert.equal(
      isMidOperation('/repo/.git', () => false),
      false,
    )
  })

  it('lists the six states of the old script', () => {
    assert.deepEqual(
      [...MID_OPERATION_MARKERS],
      [
        'rebase-merge',
        'rebase-apply',
        'CHERRY_PICK_HEAD',
        'REVERT_HEAD',
        'sequencer',
        'BISECT_LOG',
      ],
    )
  })
})

describe('isAgentSession', () => {
  it('knows Claude Code and Codex', () => {
    assert.equal(isAgentSession({ CLAUDECODE: '1' }), true)
    assert.equal(isAgentSession({ CODEX_THREAD_ID: 'abc' }), true)
  })

  it('is false for a person, and for values that are not the markers', () => {
    assert.equal(isAgentSession({}), false)
    assert.equal(isAgentSession({ CLAUDECODE: '0' }), false)
    assert.equal(isAgentSession({ CODEX_THREAD_ID: '' }), false)
  })
})

describe('sameCheckout', () => {
  const resolve = (path: string): string | undefined =>
    path.startsWith('/missing') ? undefined : path.replace('/link', '/real')

  it('matches after resolving links', () => {
    assert.equal(sameCheckout('/link/repo', '/real/repo', resolve), true)
  })

  it('does not match another checkout, an empty source or a path that cannot be resolved', () => {
    assert.equal(sameCheckout('/other', '/real/repo', resolve), false)
    assert.equal(sameCheckout('', '/real/repo', resolve), false)
    assert.equal(sameCheckout(undefined, '/real/repo', resolve), false)
    assert.equal(sameCheckout('/missing/x', '/real/repo', resolve), false)
    assert.equal(sameCheckout('/real/repo', '/missing/y', resolve), false)
  })
})
