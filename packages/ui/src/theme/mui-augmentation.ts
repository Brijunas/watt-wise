// All of the theme's module augmentation lives here, so every custom token is typed in one place
// (theme.md, "Custom keys and augmentation"). `vars` is reserved by MUI and is never a custom key.
import type {} from '@mui/material/themeCssVarsAugmentation'
import type { ComponentsOverrides, ComponentsVariants } from '@mui/material/styles'
import type { CSSProperties } from 'react'
import type { FieldValues } from 'react-hook-form'
import type { AppShellClassKey, AppShellProps } from '../shell/app-shell'
import type { IconMenuClassKey, IconMenuProps } from '../menu/icon-menu'
import type { LanguageSwitchClassKey, LanguageSwitchProps } from '../i18n/language-switch'
import type { ModeSwitchClassKey, ModeSwitchProps } from './mode-switch'
import type { GlowSwitchClassKey, GlowSwitchProps } from './glow-switch'
import type { AccountPageClassKey, AccountPageProps } from '../auth/account-page'
import type { LogoClassKey, LogoProps } from '../shell/logo'
import type { NotFoundClassKey, NotFoundProps } from '../shell/not-found'
import type { PageLoadingClassKey, PageLoadingProps } from '../shell/page-loading'
import type { UpdatePromptClassKey, UpdatePromptProps } from '../shell/update-prompt'
import type { FormTextFieldClassKey, FormTextFieldProps } from '../form/form-text-field'
import type { FormPasswordFieldClassKey, FormPasswordFieldProps } from '../form/form-password-field'
import type { SignInFieldsClassKey, SignInFieldsProps } from '../auth/sign-in-fields'

export interface Gradient {
  brand: string
  action: string
}

// `halo` and `hover` are box-shadows, `text` and `logo` are drop-shadow filters. `hover` is the
// one-colour halo of a hovered clickable card. `haloOff` is the plain elevation `halo` and `hover`
// fall back to under `data-glow="off"` (see css-baseline-theme.ts).
export interface Glow {
  halo: string
  hover: string
  text: string
  logo: string
  haloOff: string
}

export interface ShapeRadius {
  hero: string
  card: string
  panel: string
  button: string
  field: string
  chip: string
}

declare module '@mui/material/styles' {
  interface CssThemeVariables {
    enabled: true
  }

  interface Palette {
    surface2: string
    fieldOutline: string
  }
  interface PaletteOptions {
    surface2?: string
    fieldOutline?: string
  }
  // Only success, error and warning have a tint.
  interface PaletteColor {
    tint?: string
  }
  interface SimplePaletteColorOptions {
    tint?: string
  }

  // `gradient` and `glow` are keys of a colour scheme (`colorSchemes.<scheme>`), not of the theme
  // root: that is how MUI generates one CSS variable per scheme. `Theme` gets the default scheme's.
  interface ColorSystemOptions {
    gradient?: Gradient
    glow?: Glow
  }
  interface ColorSystem {
    gradient: Gradient
    glow: Glow
  }
  interface ThemeVars {
    gradient: Gradient
    glow: Glow
  }
  interface Theme {
    gradient: Gradient
    glow: Glow
  }

  interface Shape {
    radius: ShapeRadius
  }
  interface ShapeOptions {
    radius?: ShapeRadius
  }

  interface TypographyVariants {
    hero: CSSProperties
    amountCard: CSSProperties
    amountInline: CSSProperties
  }
  interface TypographyVariantsOptions {
    hero?: CSSProperties
    amountCard?: CSSProperties
    amountInline?: CSSProperties
  }

  interface ComponentNameToClassKey {
    WwAppShell: AppShellClassKey
    WwIconMenu: IconMenuClassKey
    WwLanguageSwitch: LanguageSwitchClassKey
    WwModeSwitch: ModeSwitchClassKey
    WwGlowSwitch: GlowSwitchClassKey
    WwLogo: LogoClassKey
    WwAccountPage: AccountPageClassKey
    WwNotFound: NotFoundClassKey
    WwPageLoading: PageLoadingClassKey
    WwUpdatePrompt: UpdatePromptClassKey
    WwFormTextField: FormTextFieldClassKey
    WwFormPasswordField: FormPasswordFieldClassKey
    WwSignInFields: SignInFieldsClassKey
  }

  interface ComponentsPropsList {
    WwAppShell: AppShellProps
    WwIconMenu: IconMenuProps<string>
    WwLanguageSwitch: LanguageSwitchProps
    WwModeSwitch: ModeSwitchProps
    WwGlowSwitch: GlowSwitchProps
    WwLogo: LogoProps
    WwAccountPage: AccountPageProps
    WwNotFound: NotFoundProps
    WwPageLoading: PageLoadingProps
    WwUpdatePrompt: UpdatePromptProps
    WwFormTextField: FormTextFieldProps<FieldValues>
    WwFormPasswordField: FormPasswordFieldProps<FieldValues>
    WwSignInFields: SignInFieldsProps
  }

  interface Components<Theme = unknown> {
    WwAppShell?: {
      defaultProps?: Partial<ComponentsPropsList['WwAppShell']>
      styleOverrides?: ComponentsOverrides<Theme>['WwAppShell']
      variants?: ComponentsVariants<Theme>['WwAppShell']
    }
    WwIconMenu?: {
      defaultProps?: Partial<ComponentsPropsList['WwIconMenu']>
      styleOverrides?: ComponentsOverrides<Theme>['WwIconMenu']
      variants?: ComponentsVariants<Theme>['WwIconMenu']
    }
    WwLanguageSwitch?: {
      defaultProps?: Partial<ComponentsPropsList['WwLanguageSwitch']>
      styleOverrides?: ComponentsOverrides<Theme>['WwLanguageSwitch']
      variants?: ComponentsVariants<Theme>['WwLanguageSwitch']
    }
    WwModeSwitch?: {
      defaultProps?: Partial<ComponentsPropsList['WwModeSwitch']>
      styleOverrides?: ComponentsOverrides<Theme>['WwModeSwitch']
      variants?: ComponentsVariants<Theme>['WwModeSwitch']
    }
    WwGlowSwitch?: {
      defaultProps?: Partial<ComponentsPropsList['WwGlowSwitch']>
      styleOverrides?: ComponentsOverrides<Theme>['WwGlowSwitch']
      variants?: ComponentsVariants<Theme>['WwGlowSwitch']
    }
    WwLogo?: {
      defaultProps?: Partial<ComponentsPropsList['WwLogo']>
      styleOverrides?: ComponentsOverrides<Theme>['WwLogo']
      variants?: ComponentsVariants<Theme>['WwLogo']
    }
    WwAccountPage?: {
      defaultProps?: Partial<ComponentsPropsList['WwAccountPage']>
      styleOverrides?: ComponentsOverrides<Theme>['WwAccountPage']
      variants?: ComponentsVariants<Theme>['WwAccountPage']
    }
    WwNotFound?: {
      defaultProps?: Partial<ComponentsPropsList['WwNotFound']>
      styleOverrides?: ComponentsOverrides<Theme>['WwNotFound']
      variants?: ComponentsVariants<Theme>['WwNotFound']
    }
    WwPageLoading?: {
      defaultProps?: Partial<ComponentsPropsList['WwPageLoading']>
      styleOverrides?: ComponentsOverrides<Theme>['WwPageLoading']
      variants?: ComponentsVariants<Theme>['WwPageLoading']
    }
    WwUpdatePrompt?: {
      defaultProps?: Partial<ComponentsPropsList['WwUpdatePrompt']>
      styleOverrides?: ComponentsOverrides<Theme>['WwUpdatePrompt']
      variants?: ComponentsVariants<Theme>['WwUpdatePrompt']
    }
    WwFormTextField?: {
      defaultProps?: Partial<ComponentsPropsList['WwFormTextField']>
      styleOverrides?: ComponentsOverrides<Theme>['WwFormTextField']
      variants?: ComponentsVariants<Theme>['WwFormTextField']
    }
    WwFormPasswordField?: {
      defaultProps?: Partial<ComponentsPropsList['WwFormPasswordField']>
      styleOverrides?: ComponentsOverrides<Theme>['WwFormPasswordField']
      variants?: ComponentsVariants<Theme>['WwFormPasswordField']
    }
    WwSignInFields?: {
      defaultProps?: Partial<ComponentsPropsList['WwSignInFields']>
      styleOverrides?: ComponentsOverrides<Theme>['WwSignInFields']
      variants?: ComponentsVariants<Theme>['WwSignInFields']
    }
  }

  interface BreakpointOverrides {
    xxl: true
  }
}

// `Card` shares Paper's variant prop, so `highlighted` types both.
declare module '@mui/material/Paper' {
  interface PaperPropsVariantOverrides {
    highlighted: true
  }
}

declare module '@mui/material/Chip' {
  interface ChipPropsVariantOverrides {
    lowestCost: true
    pastPrices: true
  }
}

declare module '@mui/material/Typography' {
  interface TypographyPropsVariantOverrides {
    hero: true
    amountCard: true
    amountInline: true
  }
}
