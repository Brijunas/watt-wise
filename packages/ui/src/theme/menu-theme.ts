import type { ThemeOptions } from '@mui/material/styles'

export const menuTheme: ThemeOptions['components'] = {
  MuiMenu: {
    styleOverrides: {
      paper: ({ theme }) => ({
        backgroundColor: theme.vars.palette.background.paper,
        backgroundImage: 'none',
        border: `1px solid ${theme.vars.palette.divider}`,
        borderRadius: theme.vars.shape.radius.panel,
        boxShadow: theme.vars.glow.haloOff,
      }),
    },
  },
}
