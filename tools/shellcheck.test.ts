import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { selectScripts, SHELL_SHEBANG } from './shellcheck.ts'

describe('SHELL_SHEBANG', () => {
  it('matches sh and bash shebangs, direct or through env', () => {
    for (const line of [
      '#!/bin/sh',
      '#!/usr/bin/env bash',
      '#!/bin/bash -e',
      '#!/usr/bin/env sh',
    ]) {
      assert.match(line, SHELL_SHEBANG)
    }
  })

  it('does not match other interpreters', () => {
    for (const line of ['#!/usr/bin/env node', '#!/usr/bin/python3', '# shellcheck shell=sh', '']) {
      assert.doesNotMatch(line, SHELL_SHEBANG)
    }
  })
})

describe('selectScripts', () => {
  it('takes *.sh, the husky hooks and shebang files, and nothing else', () => {
    const lines: Record<string, string> = {
      'a/run.sh': '',
      '.husky/pre-commit': '# shellcheck shell=sh',
      '.husky/post-merge': '',
      '.husky/post-checkout': '',
      '.husky/post-rewrite': '',
      '.husky/_/husky.sh': '',
      'bin/tool': '#!/usr/bin/env bash',
      'tools/shellcheck.ts': '// comment',
      'README.md': '# Readme',
    }
    assert.deepEqual(
      selectScripts(Object.keys(lines), (file) => lines[file] ?? ''),
      [
        'a/run.sh',
        '.husky/pre-commit',
        '.husky/post-merge',
        '.husky/post-checkout',
        '.husky/post-rewrite',
        '.husky/_/husky.sh',
        'bin/tool',
      ],
    )
  })
})
