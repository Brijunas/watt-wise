import type { ThemeOptions } from '@mui/material/styles'

export const bottomNavigationTheme: ThemeOptions['components'] = {
  MuiBottomNavigation: {
    styleOverrides: {
      root: ({ theme }) => ({
        backgroundColor: theme.vars.palette.background.paper,
        borderTop: `1px solid ${theme.vars.palette.divider}`,
      }),
    },
  },
}
