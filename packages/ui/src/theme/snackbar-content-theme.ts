import { snackbarContentClasses } from '@mui/material/SnackbarContent'
import type { ThemeOptions } from '@mui/material/styles'

// The update prompt's surface: a panel on `background.paper` with `text.primary` text, like a menu,
// not MUI's inverted grey. Its action buttons are normal Buttons ([update-prompt.tsx]).
export const snackbarContentTheme: ThemeOptions['components'] = {
  MuiSnackbarContent: {
    styleOverrides: {
      root: ({ theme }) => ({
        backgroundColor: theme.vars.palette.background.paper,
        color: theme.vars.palette.text.primary,
        border: `1px solid ${theme.vars.palette.divider}`,
        borderRadius: theme.vars.shape.radius.panel,
        boxShadow: theme.vars.glow.haloOff,
        // The two buttons sit apart, not touching.
        [`& .${snackbarContentClasses.action}`]: { gap: theme.spacing(1) },
        '@media (forced-colors: active)': {
          border: '1px solid ButtonBorder',
          boxShadow: 'none',
        },
      }),
    },
  },
}
