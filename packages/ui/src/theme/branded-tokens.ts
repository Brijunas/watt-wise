import { alpha } from '@mui/material/styles'
import type { ThemeOptions } from '@mui/material/styles'
import { colorSchemeAttribute } from './color-scheme'
import type {} from './mui-augmentation'

// Every value is style.md's. This folder is the one place literal colours, shadows and fonts live.

const fontFamily = "'Red Hat Display Variable', sans-serif"

const rem = (px: number) => `${String(px / 16)}rem`

const cssVarPrefix = 'mui'

// A translucent palette colour from its channel token, which the scheme class defines.
const channelAlpha = (token: string, opacity: number) =>
  `rgba(var(--${cssVarPrefix}-palette-${token}Channel) / ${String(opacity)})`

const cyan = '#00E5FF'
const magenta = '#FF2BD6'
const violet = '#7C4DFF'

const halo = (cyanOpacity: number, magentaOpacity: number) =>
  `0 0 28px -6px ${alpha(cyan, cyanOpacity)}, 0 0 56px -10px ${alpha(magenta, magentaOpacity)}`

// The one-colour halo of a hovered clickable card.
const hoverHalo = (opacity: number) => `0 0 24px -8px ${channelAlpha('primary-main', opacity)}`

// Tabular numerals for every figure that is an amount.
const amount = (size: number, lineHeight: number) => ({
  fontFamily,
  fontSize: rem(size),
  fontWeight: 800,
  lineHeight,
  letterSpacing: 0,
  fontVariantNumeric: 'tabular-nums',
})

export const brandedTokens: ThemeOptions = {
  cssVariables: { cssVarPrefix, colorSchemeSelector: colorSchemeAttribute },
  colorSchemes: {
    light: {
      palette: {
        primary: { main: '#6D28D9' },
        secondary: { main: '#BE185D' },
        success: { main: '#047857', tint: '#E3F5EC' },
        error: { main: '#C62828', tint: channelAlpha('error-main', 0.12) },
        warning: { main: '#A84B00', tint: channelAlpha('warning-main', 0.12) },
        background: { default: '#F7F7FC', paper: '#FFFFFF' },
        surface2: '#F0EFF8',
        text: { primary: '#120F24', secondary: '#56546E' },
        divider: '#E0DEEE',
        fieldOutline: '#7D7A98',
      },
      gradient: {
        brand: 'linear-gradient(100deg, #0891B2, #6D28D9, #DB2777)',
        action: 'linear-gradient(100deg, #0E7490, #5B21B6, #BE185D)',
      },
      glow: {
        halo: halo(0.275, 0.225),
        text: 'none',
        logo: `drop-shadow(0 0 10px ${alpha(violet, 0.25)})`,
        hover: hoverHalo(0.25),
        haloOff: `0 1px 2px ${channelAlpha('text-primary', 0.06)}, 0 8px 24px -12px ${channelAlpha('text-primary', 0.14)}`,
      },
    },
    dark: {
      palette: {
        primary: { main: '#A98BFF' },
        // MUI picks white here (3.2:1, over its threshold of 3), which fails 4.5:1; the page colour is 6.3:1.
        secondary: { main: '#FF2BD6', contrastText: '#05060E' },
        success: { main: '#34D399', tint: channelAlpha('success-main', 0.14) },
        error: { main: '#FF6B63', tint: channelAlpha('error-main', 0.14) },
        warning: { main: '#FFB74D', tint: channelAlpha('warning-main', 0.14) },
        background: { default: '#05060E', paper: '#0D0F1F' },
        surface2: '#151832',
        text: { primary: '#F2F3FA', secondary: '#A9AECB' },
        divider: '#262A4D',
        fieldOutline: '#6B7096',
      },
      gradient: {
        brand: 'linear-gradient(100deg, #00E5FF, #7C4DFF, #FF2BD6)',
        action: 'linear-gradient(100deg, #0E7490, #5B21B6, #BE185D)',
      },
      glow: {
        halo: halo(0.55, 0.45),
        text: `drop-shadow(0 0 18px ${alpha(violet, 0.6)})`,
        logo: `drop-shadow(0 0 10px ${alpha(violet, 0.5)})`,
        hover: hoverHalo(0.5),
        haloOff: `0 1px 2px ${alpha('#000000', 0.5)}`,
      },
    },
  },
  shape: {
    radius: {
      hero: '24px',
      card: '20px',
      panel: '16px',
      button: '14px',
      field: '12px',
      chip: '9999px',
    },
  },
  spacing: 8,
  breakpoints: { values: { xs: 0, sm: 600, md: 840, lg: 1200, xl: 1600, xxl: 2400 } },
  typography: {
    fontFamily,
    h1: { fontSize: rem(34), fontWeight: 800, lineHeight: 1.2, letterSpacing: 0 },
    h2: { fontSize: rem(21), fontWeight: 700, lineHeight: 1.3, letterSpacing: 0 },
    h3: { fontSize: rem(17), fontWeight: 700, lineHeight: 1.35, letterSpacing: 0 },
    body1: { fontSize: rem(16), fontWeight: 400, lineHeight: 1.6, letterSpacing: 0 },
    body2: { fontSize: rem(15), fontWeight: 400, lineHeight: 1.6, letterSpacing: 0 },
    overline: {
      fontSize: rem(13),
      fontWeight: 700,
      lineHeight: 1.5,
      letterSpacing: '0.1em',
      textTransform: 'uppercase',
    },
    button: {
      fontSize: rem(16),
      fontWeight: 700,
      lineHeight: 1.25,
      letterSpacing: 0,
      textTransform: 'none',
    },
    hero: amount(60, 1.05),
    amountCard: amount(28, 1.15),
    amountInline: amount(20, 1.2),
  },
}
