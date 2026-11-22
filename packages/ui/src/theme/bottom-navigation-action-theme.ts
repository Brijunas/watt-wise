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
        // MUI's 80px minimum would overflow four actions at 320px; the label wraps instead.
        minWidth: 0,
        paddingBlock: theme.spacing(1),
        paddingInline: theme.spacing(1),
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
      // The label wraps inside the bar and keeps 12px when selected (MUI grows it to 14px), so
      // choosing an item does not reflow the bar.
      label: ({ theme }) => ({
        textAlign: 'center',
        overflowWrap: 'anywhere',
        [`&.${bottomNavigationActionClasses.selected}`]: {
          fontSize: theme.typography.pxToRem(12),
        },
      }),
    },
  },
}
