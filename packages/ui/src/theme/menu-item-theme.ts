import { buttonBaseClasses } from '@mui/material/ButtonBase'
import { menuItemClasses } from '@mui/material/MenuItem'
import type { ThemeOptions } from '@mui/material/styles'

// Items are 44px high; the selected one is `primary.main` text on a tint (IconMenu adds its tick).
// The ring sits inside the item so the menu's edge doesn't clip it.
export const menuItemTheme: ThemeOptions['components'] = {
  MuiMenuItem: {
    styleOverrides: {
      root: ({ theme }) => ({
        // `&&` outranks MUI's own `minHeight: 'auto'` from 600px and `minHeight: 32` when dense,
        // so items are 44px at every width.
        '&&': { minHeight: theme.spacing(5.5) },
        [`&.${buttonBaseClasses.focusVisible}`]: { outlineOffset: -2 },
        [`&.${menuItemClasses.selected}`]: {
          color: theme.vars.palette.primary.main,
          backgroundColor: `rgba(${theme.vars.palette.primary.mainChannel} / 0.12)`,
        },
      }),
    },
  },
}
