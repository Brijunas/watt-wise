import ExpandMoreRoundedIcon from '@mui/icons-material/ExpandMoreRounded'
import type { ThemeOptions } from '@mui/material/styles'

// MUI's default arrow is `ArrowDropDown`; this is the Rounded one.
export const selectTheme: ThemeOptions['components'] = {
  MuiSelect: { defaultProps: { IconComponent: ExpandMoreRoundedIcon } },
}
