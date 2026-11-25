import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { insideContainer, isFedora, parseOsRelease, rootlessReport } from './fedora.ts'
import type { Runner, RunResult } from './run.ts'

const FEDORA_44 = `NAME="Fedora Linux"
VERSION="44 (Workstation Edition)"
ID=fedora
VERSION_ID=44
# a comment
PRETTY_NAME='Fedora Linux 44'
`

describe('parseOsRelease', () => {
  it('reads plain, double-quoted and single-quoted values', () => {
    const fields = parseOsRelease(FEDORA_44)
    assert.equal(fields.ID, 'fedora')
    assert.equal(fields.NAME, 'Fedora Linux')
    assert.equal(fields.PRETTY_NAME, 'Fedora Linux 44')
  })

  it('skips comments, blank lines and lines without a key', () => {
    assert.deepEqual(parseOsRelease('# x\n\n=oops\nID=debian\n'), { ID: 'debian' })
  })

  it('handles Windows line endings', () => {
    assert.equal(parseOsRelease('ID=fedora\r\nVERSION_ID=99\r\n').ID, 'fedora')
  })
})

describe('isFedora', () => {
  it('is true for any Fedora version', () => {
    assert.equal(isFedora(parseOsRelease(FEDORA_44)), true)
    assert.equal(isFedora(parseOsRelease('ID=fedora\nVERSION_ID=61\n')), true)
  })

  it('is false for other distributions, derivatives and an empty file', () => {
    assert.equal(isFedora(parseOsRelease('ID=debian\n')), false)
    assert.equal(isFedora(parseOsRelease('ID=rhel\nID_LIKE="fedora"\n')), false)
    assert.equal(isFedora({}), false)
  })
})

describe('insideContainer', () => {
  it('is true when Docker or Podman marks the container', () => {
    assert.equal(
      insideContainer((path) => path === '/.dockerenv'),
      true,
    )
    assert.equal(
      insideContainer((path) => path === '/run/.containerenv'),
      true,
    )
  })

  it('is false on a machine with neither marker', () => {
    assert.equal(
      insideContainer(() => false),
      false,
    )
  })
})

// A runner that answers by the command line.
const runnerOf =
  (answers: Record<string, Partial<RunResult>>): Runner =>
  (command, args) => {
    const answer = answers[[command, ...args].join(' ')] ?? { status: 1 }
    return { status: 0, stdout: '', stderr: '', ...answer }
  }

describe('rootlessReport', () => {
  const info = 'docker info --format {{json .SecurityOptions}}'
  const complete = {
    [info]: { stdout: '["name=seccomp,profile=builtin","name=rootless"]' },
    'systemctl --user is-active docker': { stdout: 'active\n' },
    'loginctl show-user dev --property=Linger --value': { stdout: 'yes\n' },
    'docker context ls --format {{.Name}}': { stdout: 'default\nrootless\n' },
  }

  it('reports a complete rootless Docker without problems', () => {
    const report = rootlessReport(runnerOf(complete), 'dev', () => 'dev:100000:65536\n')
    assert.deepEqual(report, { mode: 'rootless', problems: [] })
  })

  it('lists what a rootless Docker lacks', () => {
    const report = rootlessReport(
      runnerOf({
        [info]: complete[info],
        'systemctl --user is-active docker': { status: 3, stdout: 'inactive\n' },
        'loginctl show-user dev --property=Linger --value': { stdout: 'no\n' },
        'docker context ls --format {{.Name}}': { stdout: 'default\n' },
      }),
      'dev',
      () => 'other:100000:65536\n',
    )
    assert.equal(report.mode, 'rootless')
    assert.equal(report.problems.length, 4)
  })

  it('reports a rootful Docker and looks at nothing else', () => {
    const report = rootlessReport(
      runnerOf({ [info]: { stdout: '["name=seccomp,profile=builtin"]' } }),
      'dev',
      () => '',
    )
    assert.deepEqual(report, { mode: 'rootful', problems: [] })
  })

  it('reports nothing when Docker does not answer', () => {
    assert.deepEqual(
      rootlessReport(runnerOf({}), 'dev', () => ''),
      { mode: 'none', problems: [] },
    )
  })
})
