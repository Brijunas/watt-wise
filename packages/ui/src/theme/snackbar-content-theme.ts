import { snackbarContentClasses } from '@mui/material/SnackbarContent'
import type { ThemeOptions } from '@mui/material/styles'

// The update prompt's surface: a panel on `background.paper` with `text.primary` text, like a menu,
// not MUI's inverted grey. Its action buttons are normal Buttons ([update-prompt.tsx]).
//
// It must fit the viewport at any text size: MUI sizes the content to its unbreakable content, and
// the Snackbar centres an overflowing child, so a large root font size pushed the panel off both
// edges. The content may shrink (`minWidth: 0`) up to the Snackbar's own width, and the action
// wraps under the message when the buttons don't fit beside it.
export const snackbarContentTheme: ThemeOptions['components'] = {
  MuiSnackbarContent: {
    styleOverrides: {
      root: ({ theme }) => ({
        backgroundColor: theme.vars.palette.background.paper,
        // MUI's dark-mode elevation overlay would lighten the panel above `background.paper`.
        backgroundImage: 'none',
        color: theme.vars.palette.text.primary,
        border: `1px solid ${theme.vars.palette.divider}`,
        borderRadius: theme.vars.shape.radius.panel,
        boxShadow: theme.vars.glow.haloOff,
        boxSizing: 'border-box',
        minWidth: 0,
        maxWidth: '100%',
        // MUI's 288px minimum from 600px, never wider than the Snackbar.
        [theme.breakpoints.up('sm')]: { minWidth: 'min(288px, 100%)' },
        // The buttons sit apart, not touching, and wrap under the message when they don't fit.
        [`& .${snackbarContentClasses.action}`]: {
          flexWrap: 'wrap',
          gap: theme.spacing(1),
          paddingTop: theme.spacing(1),
          paddingBottom: theme.spacing(1),
        },
        '@media (forced-colors: active)': {
          border: '1px solid ButtonBorder',
          boxShadow: 'none',
        },
      }),
    },
  },
}
