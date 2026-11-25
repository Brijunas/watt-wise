// The decisions of follow-stack.ts, apart from the commands it runs, so they can be tested.
import { join } from 'node:path'

// Files in the git folder that say a rebase, cherry-pick, revert or bisect is in progress.
export const MID_OPERATION_MARKERS = [
  'rebase-merge',
  'rebase-apply',
  'CHERRY_PICK_HEAD',
  'REVERT_HEAD',
  'sequencer',
  'BISECT_LOG',
] as const

export const isMidOperation = (gitDir: string, exists: (path: string) => boolean): boolean =>
  MID_OPERATION_MARKERS.some((marker) => exists(join(gitDir, marker)))

// Claude Code sets CLAUDECODE=1 and Codex sets CODEX_THREAD_ID in the commands they run. Such a session
// must not run `op`: it would put a 1Password prompt in front of the user.
export const isAgentSession = (env: Record<string, string | undefined>): boolean =>
  env.CLAUDECODE === '1' || (env.CODEX_THREAD_ID ?? '') !== ''

// Does the stack's bind source (/src) name this checkout? Both are resolved first; one that can't be is no match.
export const sameCheckout = (
  source: string | undefined,
  top: string,
  resolve: (path: string) => string | undefined,
): boolean => {
  if (source === undefined || source === '') return false
  const sourceReal = resolve(source)
  const topReal = resolve(top)
  return sourceReal !== undefined && topReal !== undefined && sourceReal === topReal
}
