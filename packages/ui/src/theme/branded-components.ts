import type { ThemeOptions } from '@mui/material/styles'
import { cssBaselineTheme } from './css-baseline-theme'

// One file per component; each adds its `Mui…` key here.
export const brandedComponents: ThemeOptions['components'] = {
  ...cssBaselineTheme,
}
