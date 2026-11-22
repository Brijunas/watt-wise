import { buttonBaseClasses } from '@mui/material/ButtonBase'
import { switchClasses } from '@mui/material/Switch'
import type { ThemeOptions } from '@mui/material/styles'

const trackPadding = { medium: 12, small: 7 }
const ringOffset = (padding: number) => `${String(3 - padding)}px`

// A disabled checked track keeps MUI's dimmed look: no gradient on a disabled control.
const checkedTrack = `& .${switchClasses.switchBase}.${switchClasses.checked}:not(.${switchClasses.disabled}) + .${switchClasses.track}`

// The checked track is the brand gradient at full opacity, with the thumb in the paper colour so
// it reads on every stop. MUI's own forced-colours rule gives the track a `ButtonBorder` border;
// the thumb keeps a `ButtonText` fill and the gradient and glow give way.
export const switchTheme: ThemeOptions['components'] = {
  MuiSwitch: {
    styleOverrides: {
      root: ({ theme }) => ({
        // MUI's off track is black or white at 38% or 30%, 2.7:1 or lower against the page; the
        // off track is `fieldOutline` (3:1 or more, WCAG 1.4.11). Disabled keeps MUI's own dimming.
        [`& .${switchClasses.track}`]: {
          backgroundColor: theme.vars.palette.fieldOutline,
          opacity: 1,
        },
        [checkedTrack]: {
          backgroundImage: theme.vars.gradient.brand,
          opacity: 1,
        },
        // The ring frames the whole switch, not the thumb: the root draws it and the thumb's own
        // ButtonBase ring is off. An outline survives forced colours.
        [`&:has(.${buttonBaseClasses.focusVisible})`]: {
          borderRadius: theme.vars.shape.radius.chip,
          outline: `2px solid ${theme.vars.palette.primary.main}`,
          // The root pads the track by 12px (7px small, MUI's defaults), so the ring sits 3px
          // outside the track once that padding is taken off the offset.
          outlineOffset: ringOffset(trackPadding.medium),
          [`&.${switchClasses.sizeSmall}`]: { outlineOffset: ringOffset(trackPadding.small) },
        },
        [`& .${switchClasses.switchBase}.${buttonBaseClasses.focusVisible}`]: { outline: 'none' },
        [`& .${switchClasses.switchBase}.${switchClasses.checked}:not(.${switchClasses.disabled})`]:
          {
            color: theme.vars.palette.background.paper,
          },
        '@media (forced-colors: active)': {
          // `enhanceHighContrast` gives every ButtonBase a `5px auto` outline on `:focus-visible` and
          // on `:focus-within:has(input:focus-visible)` (the latter is how a Switch's thumb matches).
          // That ring is clipped by the root's `overflow: hidden`, so the thumb's is turned off with
          // selectors that outrank both, leaving the root's one ring.
          [`& .${switchClasses.switchBase}:focus-visible, & .${switchClasses.switchBase}:focus-within:has(input:focus-visible)`]:
            { outline: 'none' },
          [checkedTrack]: { backgroundImage: 'none', boxShadow: 'none' },
          [`& .${switchClasses.thumb}`]: {
            forcedColorAdjust: 'none',
            backgroundColor: 'ButtonText',
            boxShadow: 'none',
          },
        },
      }),
    },
  },
}
