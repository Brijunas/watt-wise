// The one place that knows a Linux distribution's package manager. It runs only when /etc/os-release says
// Fedora (any version): it installs what the tools need (ICU for .NET) and reports,
// without changing anything, whether Docker runs rootless the way this project recommends.
import { existsSync, readFileSync } from 'node:fs'
import { userInfo } from 'node:os'
import { run as defaultRun } from './run.ts'
import type { Runner } from './run.ts'
import { fail, note, step } from './steps.ts'

// KEY=value lines of /etc/os-release; a value may be in single or double quotes.
export const parseOsRelease = (text: string): Record<string, string> => {
  const fields: Record<string, string> = {}
  for (const raw of text.split(/\r?\n/)) {
    const line = raw.trim()
    if (line === '' || line.startsWith('#')) continue
    const separator = line.indexOf('=')
    if (separator < 1) continue
    let value = line.slice(separator + 1).trim()
    const quote = value[0]
    if ((quote === '"' || quote === "'") && value.length >= 2 && value.endsWith(quote)) {
      value = value.slice(1, -1)
    }
    fields[line.slice(0, separator)] = value
  }
  return fields
}

export const isFedora = (osRelease: Record<string, string>): boolean => osRelease.ID === 'fedora'

export const readOsRelease = (path = '/etc/os-release'): Record<string, string> =>
  existsSync(path) ? parseOsRelease(readFileSync(path, 'utf8')) : {}

const ROOTLESS_DOCS = 'https://docs.docker.com/engine/security/rootless/'

export interface RootlessReport {
  // 'none': no Docker CLI or no running daemon to look at.
  mode: 'rootless' | 'rootful' | 'none'
  // What is missing for a rootless Docker that survives a logout and a reboot, each with its fix.
  problems: string[]
}

// Looks at Docker and reports. Changes nothing.
export const rootlessReport = (
  runner: Runner,
  user: string,
  readSubuid: () => string,
): RootlessReport => {
  const info = runner('docker', ['info', '--format', '{{json .SecurityOptions}}'])
  if (info.status !== 0) return { mode: 'none', problems: [] }
  if (!info.stdout.includes('rootless')) return { mode: 'rootful', problems: [] }
  const problems: string[] = []
  if (runner('systemctl', ['--user', 'is-active', 'docker']).stdout.trim() !== 'active') {
    problems.push(`the user's Docker service isn't active (systemctl --user enable --now docker)`)
  }
  const linger = runner('loginctl', ['show-user', user, '--property=Linger', '--value'])
  if (linger.stdout.trim() !== 'yes') {
    problems.push(`linger is off, so Docker stops at logout (sudo loginctl enable-linger ${user})`)
  }
  if (
    !readSubuid()
      .split('\n')
      .some((line) => line.startsWith(`${user}:`))
  ) {
    problems.push(
      `no /etc/subuid entry for ${user} (sudo usermod --add-subuids 100000-165535 ${user})`,
    )
  }
  const contexts = runner('docker', ['context', 'ls', '--format', '{{.Name}}']).stdout.split('\n')
  if (!contexts.map((name) => name.trim()).includes('rootless')) {
    problems.push(`no Docker context named rootless (dockerd-rootless-setuptool.sh install)`)
  }
  return { mode: 'rootless', problems }
}

// Inside a container the Docker socket belongs to the host, so its mode says nothing about this machine.
export const insideContainer = (exists: (path: string) => boolean = existsSync): boolean =>
  exists('/.dockerenv') || exists('/run/.containerenv')

// Installs libicu when missing; then reports on Docker. Runs before the prerequisite check.
// libatomic (Node needs it) is a documented prerequisite, not installed here: without it mise's Node
// can't even start this script.
export const fedoraProfile = (runner: Runner = defaultRun): void => {
  const missing = runner('rpm', ['-q', 'libicu']).status !== 0
  if (missing) {
    if (runner('sudo', ['--version']).status !== 0) {
      fail('sudo is needed to install libicu. Install it yourself: dnf install libicu.')
    }
    // .NET needs ICU.
    const installed = runner('sudo', ['dnf', '-y', 'install', 'libicu'], { inherit: true })
    if (installed.status !== 0) fail('dnf could not install libicu.')
  }
  step('fedora-packages', missing ? 'changed' : 'unchanged')

  if (insideContainer()) {
    step('rootless-docker', 'skipped (inside a container: the Docker socket belongs to the host)')
    return
  }
  const user = userInfo().username
  const report = rootlessReport(runner, user, () =>
    existsSync('/etc/subuid') ? readFileSync('/etc/subuid', 'utf8') : '',
  )
  if (report.mode === 'rootful') {
    note(
      `rootless-docker: Docker runs rootful here; left as it is. Rootless is recommended: ${ROOTLESS_DOCS}`,
    )
  } else if (report.mode === 'rootless' && report.problems.length === 0) {
    note('rootless-docker: ok (user service, linger, subuid and the rootless context)')
  } else if (report.mode === 'rootless') {
    note(
      `rootless-docker: rootless Docker, but not complete (nothing was changed): ${ROOTLESS_DOCS}`,
    )
    for (const problem of report.problems) note(`rootless-docker:   - ${problem}`)
  }
}
