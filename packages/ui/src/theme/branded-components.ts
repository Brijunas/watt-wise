import type { ThemeOptions } from '@mui/material/styles'
import { alertTheme } from './alert-theme'
import { appBarTheme } from './app-bar-theme'
import { bottomNavigationActionTheme } from './bottom-navigation-action-theme'
import { bottomNavigationTheme } from './bottom-navigation-theme'
import { buttonBaseTheme } from './button-base-theme'
import { buttonTheme } from './button-theme'
import { cardContentTheme } from './card-content-theme'
import { cardTheme } from './card-theme'
import { checkboxTheme } from './checkbox-theme'
import { chipTheme } from './chip-theme'
import { cssBaselineTheme } from './css-baseline-theme'
import { drawerTheme } from './drawer-theme'
import { formGroupTheme } from './form-group-theme'
import { listItemButtonTheme } from './list-item-button-theme'
import { menuItemTheme } from './menu-item-theme'
import { menuTheme } from './menu-theme'
import { nativeSelectTheme } from './native-select-theme'
import { outlinedInputTheme } from './outlined-input-theme'
import { radioTheme } from './radio-theme'
import { selectTheme } from './select-theme'
import { snackbarContentTheme } from './snackbar-content-theme'
import { switchTheme } from './switch-theme'
import { textFieldTheme } from './text-field-theme'

// One file per component; each adds its `Mui…` key here.
export const brandedComponents: ThemeOptions['components'] = {
  ...alertTheme,
  ...appBarTheme,
  ...bottomNavigationActionTheme,
  ...bottomNavigationTheme,
  ...buttonBaseTheme,
  ...buttonTheme,
  ...cardContentTheme,
  ...cardTheme,
  ...checkboxTheme,
  ...chipTheme,
  ...cssBaselineTheme,
  ...drawerTheme,
  ...formGroupTheme,
  ...listItemButtonTheme,
  ...menuItemTheme,
  ...menuTheme,
  ...nativeSelectTheme,
  ...outlinedInputTheme,
  ...radioTheme,
  ...selectTheme,
  ...snackbarContentTheme,
  ...switchTheme,
  ...textFieldTheme,
}
