// Whether the containers poll for file changes. A bind mount delivers no file events to the container
// on macOS and on a Windows folder (Docker Desktop), so `dotnet watch` and Vite must poll there. On Linux
// and inside WSL (a repo in the Linux file system) events arrive, and polling only costs CPU.
// The result is written as WATCH_POLLING="true"|"false" into the rendered deploy/.env.

export type PollingSetting = 'on' | 'off'
export type PollingFlag = PollingSetting | 'auto'

// A Windows path into a WSL distribution: \\wsl$\<distro>\... or \\wsl.localhost\<distro>\...
export const isWslPath = (path: string): boolean =>
  /^[\\/]{2}(wsl\$|wsl\.localhost)[\\/]/i.test(path)

export const defaultPolling = (platform: string, repoPath: string): boolean => {
  if (platform === 'darwin') return true
  if (platform === 'win32') return !isWslPath(repoPath)
  return false
}

export interface PollingChoice {
  enabled: boolean
  source: 'flag' | 'saved' | 'default'
}

// The flag wins (`auto` drops any saved override), then the saved override, then the rule.
export const resolvePolling = (input: {
  flag?: PollingFlag
  saved?: PollingSetting
  platform: string
  repoPath: string
}): PollingChoice => {
  if (input.flag === 'on' || input.flag === 'off') {
    return { enabled: input.flag === 'on', source: 'flag' }
  }
  if (input.flag === undefined && input.saved !== undefined) {
    return { enabled: input.saved === 'on', source: 'saved' }
  }
  return { enabled: defaultPolling(input.platform, input.repoPath), source: 'default' }
}
