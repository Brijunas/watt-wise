import type { ThemeOptions } from '@mui/material/styles'

// The app bar is a surface like the cards and menus (`background.paper`, style.md), not a coloured
// band. MUI's default `color="primary"` fills it with the primary colour in light mode (and, with
// `enableColorOnDark` off, falls back to it in dark mode too). `color="inherit"` drops MUI's
// `--AppBar-background` fill and leaves the Paper's own paper background, which the override
// states outright and clears the dark-mode elevation overlay from. A bottom divider replaces the
// shadow.
export const appBarTheme: ThemeOptions['components'] = {
  MuiAppBar: {
    defaultProps: { color: 'inherit' },
    styleOverrides: {
      root: ({ theme }) => ({
        backgroundColor: theme.vars.palette.background.paper,
        backgroundImage: 'none',
        color: theme.vars.palette.text.primary,
        boxShadow: 'none',
        borderBottom: `1px solid ${theme.vars.palette.divider}`,
      }),
    },
  },
}
