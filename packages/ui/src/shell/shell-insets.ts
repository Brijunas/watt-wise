// The space the shell's fixed bars take, published on <html> by `AppShell` so content that is not
// inside the shell (the update prompt, focused elements through `scroll-padding`) stays clear of
// them (layout.md, "App shell"). `top` is the app bar, `start` the rail or drawer, `bottom` the
// bottom bar.
export const shellInsetNames = {
  top: '--ww-shell-inset-top',
  start: '--ww-shell-inset-start',
  bottom: '--ww-shell-inset-bottom',
} as const

export type ShellInset = keyof typeof shellInsetNames

// A `var()` reference; `fallback` is for readers outside the shell, where nothing publishes it.
export const shellInset = (inset: ShellInset, fallback?: string) =>
  fallback === undefined
    ? `var(${shellInsetNames[inset]})`
    : `var(${shellInsetNames[inset]}, ${fallback})`

// The bottom bar's height before it is measured (and where `ResizeObserver` does not exist): its
// 56px minimum (`MuiBottomNavigation` `minHeight`, in px too) plus the safe area under it.
export const bottomInsetFallback = 'calc(56px + env(safe-area-inset-bottom))'
