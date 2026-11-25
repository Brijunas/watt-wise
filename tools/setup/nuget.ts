// Did `dotnet tool restore` change anything? It prints the same lines every run, so the answer comes from the
// NuGet package folder: the <package>/<version> entries before and after.
import { existsSync, readdirSync } from 'node:fs'
import { homedir } from 'node:os'
import { join } from 'node:path'
import type { Env } from './run.ts'

export const nugetDir = (env: Env = process.env, home: string = homedir()): string => {
  const set = env.NUGET_PACKAGES
  return set !== undefined && set !== '' ? set : join(home, '.nuget', 'packages')
}

// A sorted text of every <package>/<version> in the folder; empty when the folder doesn't exist.
export const nugetState = (dir: string): string => {
  if (!existsSync(dir)) return ''
  const entries: string[] = []
  for (const name of readdirSync(dir)) {
    const versions = existsSync(join(dir, name)) ? readdirSync(join(dir, name)) : []
    for (const version of versions) entries.push(`${name}/${version}`)
  }
  return entries.sort().join('\n')
}
