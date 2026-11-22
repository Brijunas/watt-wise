import { buttonBaseClasses } from '@mui/material/ButtonBase'
import type { ThemeOptions } from '@mui/material/styles'

// The focus ring of everything built on ButtonBase: buttons, icon buttons, menu and list items,
// bottom navigation actions, card action areas, chips and the switch and radio bases. Native
// links get it from css-baseline-theme.ts. Under forced colours enhanceHighContrast draws it.
export const buttonBaseTheme: ThemeOptions['components'] = {
  MuiButtonBase: {
    styleOverrides: {
      root: ({ theme }) => ({
        [`&.${buttonBaseClasses.focusVisible}`]: {
          outline: `2px solid ${theme.vars.palette.primary.main}`,
          outlineOffset: 3,
        },
      }),
    },
  },
}
