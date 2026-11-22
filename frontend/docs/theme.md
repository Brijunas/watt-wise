# Frontend theme

How our look from style.md is implemented as the MUI theme in `@wattwise/ui`: `frontend/`, `admin/` and the packages share one theme. What it looks like (colours, type, glow, shapes, the component table) is in [style.md](style.md); where the files go is in [structure.md](structure.md).

## MUI

Epic E5A builds the MUI theme from [style.md](style.md): the colours, gradients, neon glow, type, shapes and component looks become the MUI theme in `packages/ui`, following MUI's own guides ([How the theme is built](#how-the-theme-is-built)). The theme is the single source of the look: each value in style.md exists once, as a typed theme token, and every component gets its look only from tokens and MUI variants.

Every UI piece in `frontend/`, `admin/` and `@wattwise/ui` is an MUI component, styled through the theme: palette, typography, shape, and the `components` `styleOverrides` and `variants`. No other component or CSS library, and no hand-built copy of something MUI already has. `sx` and `styled` use theme tokens only: no literal colours, gradients, shadows or font families. [style.md's components table](style.md#components) maps each piece to its MUI component.

**Rule:** a new look is added as a token or variant in the theme first, then used. Components never compute colours, gradients or shadows themselves.

## MUI reference

MUI's LLM index, <https://mui.com/material-ui/llms.txt>, is the source for how MUI works. Before building or changing a token, variant or component, read the relevant `.md` pages below (every page has a Markdown twin), then Context7. Base: `https://mui.com/material-ui/`.

- Theming and themed components: `customization/theming.md`, `customization/theme-components.md` (`styleOverrides`, `variants`, `defaultProps`), `customization/creating-themed-components.md`.
- CSS variables and colour schemes: `customization/css-theme-variables/overview.md`, `usage.md`, `configuration.md`; `customization/dark-mode.md`; `react-init-color-scheme-script.md`.
- Palette, type, shape, spacing, breakpoints: `customization/palette.md`, `typography.md`, `shape.md`, `spacing.md`, `breakpoints.md`, `default-theme.md`.
- TypeScript augmentation and theme structure: `guides/typescript.md`, `guides/building-extensible-themes.md`.
- Customisation (`sx`, `styled`): `customization/how-to-customize.md`.
- Icons: `icons.md`.
- Components: `react-<name>.md`, e.g. `react-button.md`, `react-card.md`, `react-paper.md`, `react-chip.md`, `react-text-field.md`, `react-radio-button.md`, `react-menu.md`, `react-switch.md`, `react-bottom-navigation.md`, `react-drawer.md`, `react-list.md`.
- Upgrade guide for the installed major (v9): `migration/upgrade-to-v9.md`.

## Tokens

The theme uses MUI's CSS-variables mode (`cssVariables`, `colorSchemes` light and dark). Every token below is typed by module augmentation and read as a CSS variable through `theme.vars`, so it follows the mode without a re-render. The values are in the sections linked.

| Token                                                                  | Holds                                                                                                                                                                                                                                                                                                                              | Values in                                       |
| ---------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------- |
| `palette` standard keys, `surface2`, `fieldOutline`                    | `background`, `text`, `divider`, `primary`, `secondary`, plus the two custom keys (`Palette` and `PaletteOptions`)                                                                                                                                                                                                                 | [Colour tokens](style.md#colour-tokens)         |
| `palette.success`, `error`, `warning`                                  | Each with its tint (the `success` tint is on its row)                                                                                                                                                                                                                                                                              | [Colour tokens](style.md#colour-tokens)         |
| `gradient.brand`, `gradient.action`                                    | Per mode, from the gradient stops rows                                                                                                                                                                                                                                                                                             | [Colour tokens](style.md#colour-tokens)         |
| `glow.halo`, `glow.hover`, `glow.text`, `glow.logo`, `glow.haloOff`    | Box-shadow, the one-colour hover box-shadow of a clickable card, text drop-shadow and logo drop-shadow, plus `haloOff`, the plain elevation `halo` and `hover` fall back to. The first four resolve to the "glow off" values under `data-glow="off"`: CSS variables switched by that attribute, so no component checks the setting | [Glow](style.md#glow)                           |
| `shape.radius`                                                         | `hero` 24, `card` 20, `panel` 16, `button` 14, `field` 12, `chip` round                                                                                                                                                                                                                                                            | [Shape and spacing](style.md#shape-and-spacing) |
| `typography` variants `hero`, `amountCard`, `amountInline`, `overline` | Beside `h1`-`h3`, `body1`, `body2` (secondary) and `button` for the other roles (`TypographyVariants`, `TypographyVariantsOptions`, `TypographyPropsVariantOverrides`)                                                                                                                                                             | [Typography](style.md#typography)               |
| `spacing`                                                              | MUI's 8px unit                                                                                                                                                                                                                                                                                                                     | [Shape and spacing](style.md#shape-and-spacing) |
| `breakpoints`                                                          | `xs`-`xl` plus `xxl`                                                                                                                                                                                                                                                                                                               | [layout.md](layout.md)                          |

Component variants, typed through `ButtonPropsVariantOverrides`, `PaperPropsVariantOverrides` (which `Card` shares), `ChipPropsVariantOverrides` and `TypographyPropsVariantOverrides`: `Button` `contained` (action gradient and glow) and `outlined`; `Card` default and `highlighted`; `Chip` default, `lowestCost` and `pastPrices`; `Typography` `hero` (gradient text, 24px and larger). What each looks like is in [style.md's components table](style.md#components). The segmented choice is the `row` variant of `MuiFormGroup`, scoped with `&.MuiRadioGroup-root` so a checkbox row is untouched: MUI's `RadioGroup` is not a styled component (it renders `FormGroup` and reads only `defaultProps` of `MuiRadioGroup`), so its `styleOverrides` are ignored.

Icons are `@mui/icons-material` in the Rounded style only (`*Rounded`), everywhere: navigation, menus, the glow switch's bulb (`LightbulbRounded`) and status companions.

## How the theme is built

### Structure

Per [Building extensible themes](https://mui.com/material-ui/guides/building-extensible-themes.md), `packages/ui/src/theme/` exports two things:

- `brandedTokens`: a `ThemeOptions` with the palette per colour scheme, typography, shape, spacing, breakpoints and the custom keys.
- `brandedComponents`: a `ThemeOptions['components']`, split into one file per component (e.g. `button-theme.ts`).

The default theme is `createTheme({ ...brandedTokens, components: brandedComponents })`; both apps use it. Options that depend on others are composed in steps (`createTheme(theme, { ... })`), never by passing extra arguments to `createTheme`, and themes are never combined with a deepmerge.

### CSS variables and colour schemes

Per [CSS theme variables](https://mui.com/material-ui/customization/css-theme-variables/usage.md) and [configuration](https://mui.com/material-ui/customization/css-theme-variables/configuration.md):

- `cssVariables` with `colorSchemeSelector` (as now) and `colorSchemes.light` / `colorSchemes.dark`. `InitColorSchemeScript` and the stored mode are under "Appearance" in [architecture.md](architecture.md#appearance).
- Components read `theme.vars.*`, typed through `import type {} from '@mui/material/themeCssVarsAugmentation'`.
- Translucent colours come from channel tokens: `rgba(${theme.vars.palette.primary.mainChannel} / 0.12)`.
- Mode-specific styles use `theme.applyStyles('dark', { ... })` only, never a `theme.palette.mode` check.

### Custom keys and augmentation

- Values that differ per colour scheme (`gradient`, `glow`) are keys of `colorSchemes.<scheme>`, so MUI generates one CSS variable per scheme; they augment `ColorSystemOptions`, `ColorSystem`, `ThemeVars` and `Theme`. The radius scale augments `Shape` and `ShapeOptions`. `vars` is reserved by MUI and never a custom key.
- Custom palette keys augment `Palette` and `PaletteOptions`; typography variants `TypographyVariants`, `TypographyVariantsOptions` and `TypographyPropsVariantOverrides`; breakpoints `BreakpointOverrides`.

### Themed components

Per [Themed components](https://mui.com/material-ui/customization/theme-components.md):

- `defaultProps`, `styleOverrides.<slot>`, and variants inside the slot: `styleOverrides.root.variants: [{ props, style }]`.
- No `ownerState` callbacks (deprecated). Compound class keys removed in v9 (e.g. `containedPrimary`) are written as `variants` with `{ variant, color }` props.
- Every hover style sits under `@media (hover: hover) and (pointer: fine)`. A card is clickable (and reacts) only when it holds a `CardActionArea` or is a link or button (`:has`, `:is`).
- The focus ring lives in two places: `MuiButtonBase` (`.Mui-focusVisible`, covering buttons, menu and list items, chips and bottom-bar actions) and `MuiCssBaseline` (native links). Clickable cards use their own outline instead, and the switch's root draws one ring around the whole switch (`&:has(.Mui-focusVisible)`) while its thumb's own ring is off.
- `MuiCssBaseline` zeroes animation and transition durations and delays (`!important`) under `prefers-reduced-motion`, so no component repeats that rule.
- Built-in MUI icons are replaced by Rounded twins through `defaultProps` (chip delete, select arrow, native select arrow, radio and checkbox icons), like the alert icons.

### Forced colours

`theme.ts` wraps the final theme in `enhanceHighContrast` (from `@mui/material/styles`, MUI 9.1+), which adds the `@media (forced-colors: active)` overrides. Each component file carries its own rules on top where [style.md](style.md#colour-tokens) asks for more: gradients and glows give way to system colours (`backgroundImage: 'none'`, no shadow), and chart series, chips, buttons, the segmented choice, the highlighted card, the glow switch and the navigation indicators keep a visible system-colour fill or border (`ButtonBorder`, `Highlight`, `ButtonText`). The system keywords are the one literal colour allowed in a component file.

### The theme's own components

Components in `@wattwise/ui` that MUI does not have (the glow switch, `Logo`, the app shell and the rest) follow [Creating themed components](https://mui.com/material-ui/customization/creating-themed-components.md), so their look can be set from `theme.components.Ww...` like MUI's own:

- Prefix `Ww`. Each slot is made with `styled(..., { name: 'WwGlowSwitch', slot: 'root' })`.
- Props go through `useThemeProps({ props: inProps, name })`, and every slot gets `ownerState`.
- The component is registered in the theme's types: `ComponentNameToClassKey`, `ComponentsPropsList` and `Components`. All registrations live in `mui-augmentation.ts`; each component's file exports its `<Name>Props` and `<Name>ClassKey`.
- A component's styles are written in its slots from theme values only (no `sx` prop on the component). A page sets a component's look only through `theme.components.Ww<Name>`, never with its own styles.
- Theme `variants` apply only to a component's `root` slot, so a non-root slot (`main`, `toggleButton`...) takes `styleOverrides` only.
- Slots: `WwAppShell` `root`, `appBar`, `drawer`, `bottomBar`, `main`; `WwIconMenu` `root`, `menu`; `WwLogo` `root`, `lightLockup`, `darkLockup`, `mono`, `wordmark`; `WwLanguageSwitch` and `WwModeSwitch` `root` (a styled `IconMenu`); `WwGlowSwitch` `root`, `icon`, `label`, `switch`; `WwAccountPage` `root`, `title`; `WwUpdatePrompt` `root`, `reloadButton`; `WwFormPasswordField` `root` (a styled `FormTextField`, so it also carries `WwFormTextField-root`), `toggleButton`; `WwNotFound`, `WwPageLoading`, `WwFormTextField` and `WwSignInFields` `root`.

## MUI v9

The code follows MUI v9 ([upgrade guide](https://mui.com/material-ui/migration/upgrade-to-v9.md)):

- `slots` and `slotProps`, not `components`, `componentsProps` or the `*Props` props (`PaperProps`, `TransitionProps`, `BackdropProps`).
- No system props on `Box`, `Typography` or `Stack`: use `sx`.
- `nativeButton={false}` when `component` swaps a non-button into a button-based component.
- `MenuItem` only inside `Menu` or `MenuList`.
