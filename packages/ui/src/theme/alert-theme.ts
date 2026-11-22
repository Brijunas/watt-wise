import CheckCircleOutlineRoundedIcon from '@mui/icons-material/CheckCircleOutlineRounded'
import ErrorOutlineRoundedIcon from '@mui/icons-material/ErrorOutlineRounded'
import InfoOutlineRoundedIcon from '@mui/icons-material/InfoOutlineRounded'
import WarningAmberRoundedIcon from '@mui/icons-material/WarningAmberRounded'
import type { ThemeOptions } from '@mui/material/styles'
import { createElement } from 'react'

// MUI's default Alert icons are outlined, not Rounded. These are the Rounded outline twins, sized
// like the defaults (`fontSize="inherit"`).
export const alertTheme: ThemeOptions['components'] = {
  MuiAlert: {
    defaultProps: {
      iconMapping: {
        success: createElement(CheckCircleOutlineRoundedIcon, { fontSize: 'inherit' }),
        info: createElement(InfoOutlineRoundedIcon, { fontSize: 'inherit' }),
        warning: createElement(WarningAmberRoundedIcon, { fontSize: 'inherit' }),
        error: createElement(ErrorOutlineRoundedIcon, { fontSize: 'inherit' }),
      },
    },
  },
}
