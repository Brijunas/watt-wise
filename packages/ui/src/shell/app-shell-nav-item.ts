import type { ReactNode } from 'react'

export interface AppShellNavItem {
  key: string
  /** Already translated by the app. */
  label: string
  icon: ReactNode
  to: string
}
