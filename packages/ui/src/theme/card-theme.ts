import { buttonBaseClasses } from '@mui/material/ButtonBase'
import { cardActionAreaClasses } from '@mui/material/CardActionArea'
import type { ThemeOptions } from '@mui/material/styles'

const hoverQuery = '@media (hover: hover) and (pointer: fine)'

// A card is clickable when it holds a CardActionArea or is itself a link or a button. Only a
// clickable card reacts to the pointer; a static card never changes on hover.
const clickable = `&:is(a, button), &:has(.${cardActionAreaClasses.root})`

export const cardTheme: ThemeOptions['components'] = {
  MuiCard: {
    styleOverrides: {
      root: ({ theme }) => {
        const outline = `1px solid ${theme.vars.palette.primary.main}`

        return {
          backgroundColor: theme.vars.palette.background.paper,
          backgroundImage: 'none',
          border: `1px solid ${theme.vars.palette.divider}`,
          borderRadius: theme.vars.shape.radius.card,
          boxShadow: theme.vars.glow.haloOff,
          // The card's own ring replaces the one on the action area inside it.
          [`& .${cardActionAreaClasses.root}.${buttonBaseClasses.focusVisible}`]: {
            outline: 'none',
          },
          [clickable]: {
            transition: theme.transitions.create(['transform', 'box-shadow'], {
              duration: theme.transitions.duration.short,
            }),
            [`&:focus-visible, &:has(.${buttonBaseClasses.focusVisible})`]: {
              outline,
              outlineOffset: -1,
            },
            [hoverQuery]: {
              '&:hover': {
                transform: 'translateY(-2px)',
                outline,
                outlineOffset: -1,
                boxShadow: theme.vars.glow.hover,
              },
            },
          },
          variants: [
            {
              props: { variant: 'highlighted' },
              style: {
                border: '1.5px solid transparent',
                backgroundImage: `linear-gradient(${theme.vars.palette.background.paper}, ${theme.vars.palette.background.paper}), ${theme.vars.gradient.brand}`,
                backgroundOrigin: 'padding-box, border-box',
                backgroundClip: 'padding-box, border-box',
                boxShadow: theme.vars.glow.halo,
                '@media (forced-colors: active)': {
                  backgroundImage: 'none',
                  boxShadow: 'none',
                  border: '1.5px solid Highlight',
                },
              },
            },
          ],
          '@media (forced-colors: active)': {
            boxShadow: 'none',
            [clickable]: { '&:hover': { boxShadow: 'none' } },
          },
        }
      },
    },
  },
}
