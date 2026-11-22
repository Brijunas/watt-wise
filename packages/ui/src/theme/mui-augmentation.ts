// All of the theme's module augmentation lives here, so every custom token is typed in one place
// (theme.md, "Custom keys and augmentation"). `vars` is reserved by MUI and is never a custom key.
import type {} from '@mui/material/themeCssVarsAugmentation'
import type { CSSProperties } from 'react'

export interface Gradient {
  brand: string
  action: string
}

// `halo` is a box-shadow, `text` and `logo` are drop-shadow filters. `haloOff` is the plain
// elevation `halo` falls back to under `data-glow="off"` (see css-baseline-theme.ts).
export interface Glow {
  halo: string
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

  interface BreakpointOverrides {
    xxl: true
  }
}

declare module '@mui/material/Typography' {
  interface TypographyPropsVariantOverrides {
    hero: true
    amountCard: true
    amountInline: true
  }
}
