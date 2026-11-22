import ExpandMoreRoundedIcon from '@mui/icons-material/ExpandMoreRounded'
import type { ThemeOptions } from '@mui/material/styles'

// MUI's default arrow is `ArrowDropDown`; this is the Rounded one, as for `Select`.
export const nativeSelectTheme: ThemeOptions['components'] = {
  MuiNativeSelect: { defaultProps: { IconComponent: ExpandMoreRoundedIcon } },
}
