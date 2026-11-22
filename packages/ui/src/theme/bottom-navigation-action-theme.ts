import { bottomNavigationActionClasses } from '@mui/material/BottomNavigationAction'
import { buttonBaseClasses } from '@mui/material/ButtonBase'
import type { ThemeOptions } from '@mui/material/styles'

// The current action: `primary.main` label and a gradient bar along the top edge. The focus ring
// sits inside the action, so the screen edge doesn't clip it.
export const bottomNavigationActionTheme: ThemeOptions['components'] = {
  MuiBottomNavigationAction: {
    styleOverrides: {
      root: ({ theme }) => ({
        position: 'relative',
        [`&.${buttonBaseClasses.focusVisible}`]: { outlineOffset: -4 },
        [`&.${bottomNavigationActionClasses.selected}`]: {
          color: theme.vars.palette.primary.main,
          '&::before': {
            content: '""',
            position: 'absolute',
            top: 0,
            left: '50%',
            width: theme.spacing(4),
            height: 3,
            transform: 'translateX(-50%)',
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
