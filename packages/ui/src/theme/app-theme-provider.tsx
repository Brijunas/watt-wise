import '@fontsource-variable/roboto'
import CssBaseline from '@mui/material/CssBaseline'
import { ThemeProvider } from '@mui/material/styles'
import type { ReactNode } from 'react'
import { colorSchemeStorageKey, defaultMode, modeStorageKey } from './color-scheme'
import { theme } from './theme'

const AppThemeProvider = ({ children }: { children: ReactNode }) => {
  return (
    <ThemeProvider
      theme={theme}
      defaultMode={defaultMode}
      modeStorageKey={modeStorageKey}
      colorSchemeStorageKey={colorSchemeStorageKey}
      noSsr
    >
      <CssBaseline enableColorScheme />
      {children}
    </ThemeProvider>
  )
}

export default AppThemeProvider
