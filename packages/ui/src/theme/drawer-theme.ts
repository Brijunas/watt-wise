import type { ThemeOptions } from '@mui/material/styles'

// The drawer and the rail are both a Drawer; their widths stay with the shell. MUI already draws
// the divider edge of a permanent drawer.
export const drawerTheme: ThemeOptions['components'] = {
  MuiDrawer: {
    styleOverrides: {
      paper: ({ theme }) => ({
        backgroundColor: theme.vars.palette.background.paper,
        backgroundImage: 'none',
      }),
    },
  },
}
