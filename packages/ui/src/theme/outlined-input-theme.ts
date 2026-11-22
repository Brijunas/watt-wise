import type { ThemeOptions } from '@mui/material/styles'

// `fieldOutline` identifies the control (3:1); focus, error and disabled keep MUI's own rules
// (`primary.main` when focused), which are more specific than the resting colour set here.
export const outlinedInputTheme: ThemeOptions['components'] = {
  MuiOutlinedInput: {
    styleOverrides: {
      root: ({ theme }) => ({ borderRadius: theme.vars.shape.radius.field }),
      notchedOutline: ({ theme }) => ({ borderColor: theme.vars.palette.fieldOutline }),
    },
  },
}
