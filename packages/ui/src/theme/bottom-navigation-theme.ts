import type { ThemeOptions } from '@mui/material/styles'

// MUI fixes the bar at 56px; here 56px is the minimum, so labels that wrap (a long Lithuanian
// label, or text at 200%) grow the bar instead of overflowing it.
export const bottomNavigationTheme: ThemeOptions['components'] = {
  MuiBottomNavigation: {
    styleOverrides: {
      root: ({ theme }) => ({
        height: 'auto',
        minHeight: 56,
        backgroundColor: theme.vars.palette.background.paper,
        borderTop: `1px solid ${theme.vars.palette.divider}`,
      }),
    },
  },
}
