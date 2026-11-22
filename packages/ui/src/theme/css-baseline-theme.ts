import type { ThemeOptions } from '@mui/material/styles'

// The glow switch (S5A.2) sets `data-glow="off"` on <html>. The glow tokens are CSS variables that
// hold the glow by default; here the halo variable points at the plain elevation and text and logo
// glow go away. `html[...]` is more specific than the scheme class (`.dark`), which sets the
// values, and the references resolve on <html>, where the scheme class sits too, so no component
// checks the setting and both colour schemes work.
export const cssBaselineTheme: ThemeOptions['components'] = {
  MuiCssBaseline: {
    styleOverrides: (theme) => ({
      'html[data-glow="off"]': {
        [`--${theme.cssVarPrefix}-glow-halo`]: theme.vars.glow.haloOff,
        [`--${theme.cssVarPrefix}-glow-text`]: 'none',
        [`--${theme.cssVarPrefix}-glow-logo`]: 'none',
      },
    }),
  },
}
