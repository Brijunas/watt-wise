// Where setup keeps its per-user files. The same folder on every OS: `${XDG_CONFIG_HOME:-~/.config}/watt-wise`,
// which is also what mise.toml's `{{ xdg_config_home }}` resolves to.
import { homedir } from 'node:os'
import { join } from 'node:path'
import type { Env } from './run.ts'

export const configDir = (env: Env = process.env, home: string = homedir()): string => {
  const xdg = env.XDG_CONFIG_HOME
  return join(xdg !== undefined && xdg !== '' ? xdg : join(home, '.config'), 'watt-wise')
}

// The headless 1Password token as a dotenv file: mise.toml loads it into every `mise` environment.
export const tokenEnvPath = (dir: string): string => join(dir, 'op.env')

// The saved choices (polling override, installed tools).
export const settingsPath = (dir: string): string => join(dir, 'setup.json')
