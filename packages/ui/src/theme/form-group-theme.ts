import { formControlLabelClasses } from '@mui/material/FormControlLabel'
import { buttonBaseClasses } from '@mui/material/ButtonBase'
import { radioClasses } from '@mui/material/Radio'
import { radioGroupClasses } from '@mui/material/RadioGroup'
import type { ThemeOptions } from '@mui/material/styles'

const hoverQuery = '@media (hover: hover) and (pointer: fine)'

// The segmented choice: a `RadioGroup row` as a group of pills. The variant sits on MuiFormGroup
// because MUI's RadioGroup is not a styled component (it renders FormGroup and reads only
// `defaultProps` of MuiRadioGroup), and `&.MuiRadioGroup-root` leaves a checkbox FormGroup row
// alone. Each pill is a FormControlLabel; the radio is stretched over it and made transparent, so
// the input stays focusable and clickable. The checked pill is filled; a vertical RadioGroup stays
// a plain radio list.
export const formGroupTheme: ThemeOptions['components'] = {
  MuiFormGroup: {
    styleOverrides: {
      root: ({ theme }) => ({
        variants: [
          {
            props: { row: true },
            style: {
              [`&.${radioGroupClasses.root}`]: {
                display: 'inline-flex',
                maxWidth: '100%',
                flexWrap: 'nowrap',
                gap: theme.spacing(0.5),
                padding: theme.spacing(0.5),
                backgroundColor: theme.vars.palette.surface2,
                borderRadius: theme.vars.shape.radius.chip,
                [`& .${formControlLabelClasses.root}`]: {
                  position: 'relative',
                  flex: '1 1 auto',
                  justifyContent: 'center',
                  minHeight: theme.spacing(5.5),
                  margin: 0,
                  padding: theme.spacing(0, 2),
                  borderRadius: theme.vars.shape.radius.chip,
                  color: theme.vars.palette.text.secondary,
                  [`& .${formControlLabelClasses.label}`]: { fontWeight: 700 },
                  [`& .${radioClasses.root}`]: {
                    position: 'absolute',
                    inset: 0,
                    width: '100%',
                    height: '100%',
                    padding: 0,
                    opacity: 0,
                  },
                  [hoverQuery]: {
                    [`&:hover:not(:has(.${radioClasses.checked}))`]: {
                      backgroundColor: `rgba(${theme.vars.palette.primary.mainChannel} / 0.12)`,
                    },
                  },
                  [`&:has(.${radioClasses.checked})`]: {
                    backgroundColor: theme.vars.palette.primary.main,
                    color: theme.vars.palette.primary.contrastText,
                  },
                  [`&:has(.${radioClasses.root}.${buttonBaseClasses.focusVisible})`]: {
                    outline: `2px solid ${theme.vars.palette.primary.main}`,
                    outlineOffset: 3,
                  },
                },
                '@media (forced-colors: active)': {
                  border: '1px solid ButtonBorder',
                  [`& .${formControlLabelClasses.root}:has(.${radioClasses.checked})`]: {
                    forcedColorAdjust: 'none',
                    backgroundColor: 'Highlight',
                    color: 'HighlightText',
                  },
                },
              },
            },
          },
        ],
      }),
    },
  },
}
