import CheckBoxOutlineBlankRoundedIcon from '@mui/icons-material/CheckBoxOutlineBlankRounded'
import CheckBoxRoundedIcon from '@mui/icons-material/CheckBoxRounded'
import IndeterminateCheckBoxRoundedIcon from '@mui/icons-material/IndeterminateCheckBoxRounded'
import type { ThemeOptions } from '@mui/material/styles'
import { createElement } from 'react'

// The Rounded twins of MUI's default checkbox icons.
export const checkboxTheme: ThemeOptions['components'] = {
  MuiCheckbox: {
    defaultProps: {
      icon: createElement(CheckBoxOutlineBlankRoundedIcon),
      checkedIcon: createElement(CheckBoxRoundedIcon),
      indeterminateIcon: createElement(IndeterminateCheckBoxRoundedIcon),
    },
  },
}
