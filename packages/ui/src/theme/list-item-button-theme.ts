import { buttonBaseClasses } from '@mui/material/ButtonBase'
import { listItemButtonClasses } from '@mui/material/ListItemButton'
import { listItemIconClasses } from '@mui/material/ListItemIcon'
import type { ThemeOptions } from '@mui/material/styles'

// The rail's and the drawer's items. The current one: `primary.main` text and a gradient bar on
// its leading edge. The ring sits inside the item so the drawer's edge doesn't clip it.
export const listItemButtonTheme: ThemeOptions['components'] = {
  MuiListItemButton: {
    styleOverrides: {
      root: ({ theme }) => ({
        position: 'relative',
        borderRadius: theme.vars.shape.radius.panel,
        [`&.${buttonBaseClasses.focusVisible}`]: { outlineOffset: -2 },
        [`&.${listItemButtonClasses.selected}`]: {
          color: theme.vars.palette.primary.main,
          [`& .${listItemIconClasses.root}`]: { color: 'inherit' },
          '&::before': {
            content: '""',
            position: 'absolute',
            insetBlock: theme.spacing(1),
            insetInlineStart: 0,
            width: 4,
            borderRadius: theme.vars.shape.radius.chip,
            backgroundImage: theme.vars.gradient.brand,
            '@media (forced-colors: active)': {
              forcedColorAdjust: 'none',
              backgroundImage: 'none',
              backgroundColor: 'Highlight',
            },
          },
        },
      }),
    },
  },
}
