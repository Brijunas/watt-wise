import RadioButtonCheckedRoundedIcon from '@mui/icons-material/RadioButtonCheckedRounded'
import RadioButtonUncheckedRoundedIcon from '@mui/icons-material/RadioButtonUncheckedRounded'
import type { ThemeOptions } from '@mui/material/styles'
import { createElement } from 'react'

// The Rounded twins of MUI's default radio icons. The segmented look is in form-group-theme.ts.
export const radioTheme: ThemeOptions['components'] = {
  MuiRadio: {
    defaultProps: {
      icon: createElement(RadioButtonUncheckedRoundedIcon),
      checkedIcon: createElement(RadioButtonCheckedRoundedIcon),
    },
  },
}
