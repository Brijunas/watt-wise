import type { ThemeOptions } from '@mui/material/styles'

// Cards pad 24px, 20px below 600px. MUI's `:last-child` rule gives the last block its own bottom
// padding, so it is set again here.
export const cardContentTheme: ThemeOptions['components'] = {
  MuiCardContent: {
    styleOverrides: {
      root: ({ theme }) => ({
        padding: theme.spacing(2.5),
        '&:last-child': { paddingBottom: theme.spacing(2.5) },
        [theme.breakpoints.up('sm')]: {
          padding: theme.spacing(3),
          '&:last-child': { paddingBottom: theme.spacing(3) },
        },
      }),
    },
  },
}
