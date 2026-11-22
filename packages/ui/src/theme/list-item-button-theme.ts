import { buttonBaseClasses } from '@mui/material/ButtonBase'
import { listItemButtonClasses } from '@mui/material/ListItemButton'
import { listItemIconClasses } from '@mui/material/ListItemIcon'
import type { ThemeOptions } from '@mui/material/styles'

// List items: the rail's, the drawer's and the Settings list's. The current one: `primary.main`
// text and a gradient bar on its leading edge. The ring sits inside the item so the drawer's edge
// or a card's `overflow: hidden` doesn't clip it.
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
        // Forced colours: a focused item is a `Highlight` fill with `HighlightText` text, icon and
        // bar, a pair the system keeps; `forcedColorAdjust: none` stops the browser mixing in the
        // item's own colours. The ring sits 4px inside the item, so a card's `overflow: hidden`
        // (or the drawer's edge) can't clip it.
        '@media (forced-colors: active)': {
          // Two classes (or the selected rule's three) outrank MUI's tinted focus fills.
          [`&.${buttonBaseClasses.focusVisible}.${buttonBaseClasses.focusVisible}, &.${listItemButtonClasses.selected}.${buttonBaseClasses.focusVisible}`]:
            {
              forcedColorAdjust: 'none',
              backgroundColor: 'Highlight',
              color: 'HighlightText',
              outline: '2px solid HighlightText',
              outlineOffset: -4,
              [`& .${listItemIconClasses.root}`]: { color: 'inherit' },
              '&::before': { backgroundColor: 'HighlightText' },
            },
        },
      }),
    },
  },
}
