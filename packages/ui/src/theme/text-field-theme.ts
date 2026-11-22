import type { ThemeOptions } from '@mui/material/styles'

export const textFieldTheme: ThemeOptions['components'] = {
  MuiTextField: { defaultProps: { variant: 'outlined' } },
}
