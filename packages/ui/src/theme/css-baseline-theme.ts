import { buttonBaseClasses } from '@mui/material/ButtonBase'
import type { ThemeOptions } from '@mui/material/styles'

// The glow switch (S5A.2) sets `data-glow="off"` on <html>. The glow tokens are CSS variables that
// hold the glow by default; here the halo variables point at the plain elevation and text and logo
// glow go away. `html[...]` is more specific than the scheme class (`.dark`), which sets the
// values, and the references resolve on <html>, where the scheme class sits too, so no component
// checks the setting and both colour schemes work.
//
// Native links and other focusables that are not a ButtonBase get the focus ring here; ButtonBase
// has its own (button-base-theme.ts).
export const cssBaselineTheme: ThemeOptions['components'] = {
  MuiCssBaseline: {
    styleOverrides: (theme) => ({
      'html[data-glow="off"]': {
        [`--${theme.cssVarPrefix}-glow-halo`]: theme.vars.glow.haloOff,
        [`--${theme.cssVarPrefix}-glow-hover`]: theme.vars.glow.haloOff,
        [`--${theme.cssVarPrefix}-glow-text`]: 'none',
        [`--${theme.cssVarPrefix}-glow-logo`]: 'none',
      },
      // Changes are instant when the device asks for less motion (style.md, "Motion").
      '@media (prefers-reduced-motion: reduce)': {
        '*, *::before, *::after': {
          animationDuration: '0s !important',
          animationDelay: '0s !important',
          transitionDuration: '0s !important',
          transitionDelay: '0s !important',
        },
      },
      [`a:not(.${buttonBaseClasses.root}):focus-visible`]: {
        outline: `2px solid ${theme.vars.palette.primary.main}`,
        outlineOffset: 3,
      },
    }),
  },
}
