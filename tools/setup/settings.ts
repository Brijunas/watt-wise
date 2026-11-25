// The choices setup remembers between runs, in ${XDG_CONFIG_HOME:-~/.config}/watt-wise/setup.json:
// a file-watching override (`polling`) and a hash of the installed mise tools (`tools`). Any other key
// (an `auth` from an earlier setup) is ignored and dropped at the next write.
// Nothing secret: the token itself lives in op.env (op.ts).
import { existsSync, mkdirSync, readFileSync, renameSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { settingsPath } from './paths.ts'
import type { PollingSetting } from './polling.ts'

export interface Settings {
  polling?: PollingSetting
  // A hash of the installed mise tools at the last run (dependencies.ts).
  tools?: string
}

// Tolerant on purpose: a file that is missing, broken or holds other values reads as "nothing saved".
export const parseSettings = (text: string): Settings => {
  let data: unknown
  try {
    data = JSON.parse(text)
  } catch {
    return {}
  }
  if (typeof data !== 'object' || data === null) return {}
  const record = data as Record<string, unknown>
  const settings: Settings = {}
  if (record.polling === 'on' || record.polling === 'off') settings.polling = record.polling
  if (typeof record.tools === 'string' && record.tools !== '') settings.tools = record.tools
  return settings
}

export const readSettings = (dir: string): Settings => {
  const path = settingsPath(dir)
  return existsSync(path) ? parseSettings(readFileSync(path, 'utf8')) : {}
}

// Returns true when the file changed.
export const writeSettings = (dir: string, settings: Settings): boolean => {
  const path = settingsPath(dir)
  const text = `${JSON.stringify(settings, null, 2)}\n`
  if (existsSync(path) && readFileSync(path, 'utf8') === text) return false
  mkdirSync(dir, { recursive: true, mode: 0o700 })
  const temp = join(dir, '.setup.json.tmp')
  writeFileSync(temp, text, { mode: 0o600 })
  renameSync(temp, path)
  return true
}
