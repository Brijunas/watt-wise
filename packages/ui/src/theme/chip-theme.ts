import CancelRoundedIcon from '@mui/icons-material/CancelRounded'
import { chipClasses } from '@mui/material/Chip'
import type { ThemeOptions } from '@mui/material/styles'
import { createElement } from 'react'

const hoverQuery = '@media (hover: hover) and (pointer: fine)'

// The default colour is a quiet `surface2` chip. `lowestCost` and `pastPrices` are our variants
// (ChipPropsVariantOverrides). The "Lowest cost" chip has no glow: style.md gives glow only to the
// filled primary button and the highlighted card. A clickable variant chip restates its resting
// look on hover and focus, because MUI's own clickable rule would otherwise swap in its grey.
export const chipTheme: ThemeOptions['components'] = {
  MuiChip: {
    defaultProps: { deleteIcon: createElement(CancelRoundedIcon) },
    styleOverrides: {
      root: ({ theme }) => {
        const pastPricesTint = (opacity: number) =>
          `rgba(${theme.vars.palette.primary.mainChannel} / ${String(opacity)})`
        const resting = `&.${chipClasses.clickable}`

        return {
          borderRadius: theme.vars.shape.radius.chip,
          '@media (forced-colors: active)': { border: '1px solid ButtonBorder' },
          // MUI's delete icon is 22px (16px small); a 24px hit area meets WCAG 2.5.8.
          [`& .${chipClasses.deleteIcon}`]: { fontSize: theme.typography.pxToRem(24) },
          variants: [
            {
              props: { color: 'default' },
              style: {
                backgroundColor: theme.vars.palette.surface2,
                color: theme.vars.palette.text.secondary,
                [`& .${chipClasses.deleteIcon}`]: {
                  color: theme.vars.palette.text.secondary,
                  '&:hover': { color: theme.vars.palette.text.primary },
                },
              },
            },
            {
              props: { variant: 'lowestCost' },
              style: {
                backgroundColor: 'transparent',
                backgroundImage: theme.vars.gradient.action,
                color: theme.vars.palette.common.white,
                [resting]: {
                  [`&:hover, &.${chipClasses.focusVisible}`]: { backgroundColor: 'transparent' },
                },
                '@media (forced-colors: active)': { backgroundImage: 'none' },
              },
            },
            {
              props: { variant: 'pastPrices' },
              style: {
                backgroundColor: pastPricesTint(0.12),
                color: theme.vars.palette.primary.main,
                [resting]: {
                  [`&:hover, &.${chipClasses.focusVisible}`]: {
                    backgroundColor: pastPricesTint(0.12),
                  },
                  [hoverQuery]: { '&:hover': { backgroundColor: pastPricesTint(0.2) } },
                },
              },
            },
          ],
        }
      },
    },
  },
}
