# Frontend style

The visual language of `frontend/`, `admin/` and `@wattwise/ui`: "calm neon", taken from the Watt-Wise logo. A calm, readable base for the general public, with gradient and glow kept for the few things that matter on a screen. This doc owns the logo, colours, type, gradient, glow, hover, shapes and component styles; page layout, navigation, the chart and result states are in [layout.md](layout.md). How the mode and the glow setting are stored and applied is under "Appearance" in [architecture.md](architecture.md#appearance); contrast and motion rules are in [accessibility.md](accessibility.md).

The MUI theme that implements this look (tokens, theme structure, variants, rules) is in [theme.md](theme.md).

## Principles

- **Calm base, neon accents.** Quiet near-black or off-white surfaces; gradient and glow mark what to look at first, never decoration.
- **One focus per region.** At most one gradient element per screen region; a list highlights at most one item.
- **Glow is the user's choice.** On by default, off with one switch; the design works without it.
- **Plain and readable.** One friendly font, large text, plain words; the tech flavour comes from colour and the logo.
- **Meaning has its own colours.** Cheaper, pricier and caution use semantic colours, never the brand gradient.

## Logo

- **Mark:** the user's original W with the lightning bolt, used as drawn: never redrawn, recoloured or stretched. The source renders are the reference: [brand/source-mark.png](brand/source-mark.png), [brand/source-lockup.png](brand/source-lockup.png).
- **Lockup:** the mark with "WATT" over "WISE" in Red Hat Display. WATT is weight 800 with 0.14em tracking in the text colour; WISE is weight 700, 84% of WATT's width, in the gradient. WATT starts below the bolt tip; the wordmark is about 1.6 times the mark's width (the original lockup's 1.8 with its wider typeface).
- **Originals** in [brand/](brand/): only the user's own artwork, `source-mark.png` (the original logo) and `source-lockup.png` (the banner). Nothing the apps use lives there.
- **Files the apps use** are in each app's `public/` ([frontend](../../frontend/public/), [admin](../../admin/public/)), hand-made and final: the icons (`favicon.svg`, `favicon.ico`, `pwa-64x64.png`, `pwa-192x192.png`, `pwa-512x512.png`, `maskable-icon-512x512.png`, `apple-touch-icon-180x180.png`; the frontend puts the mark on a dark tile, the admin inside a gradient gear) and the app bar logo (`logo-lockup-dark.png` and `logo-lockup-light.png`, transparent PNGs with no glow, as the app adds it through the theme, and `logo-mark-mono.svg`, the `currentColor` vector for forced colours). There is no icon generator: changing an icon means replacing its files. The rule: every file a web app uses lives in that app's `public/`.
- **Clear space:** at least the cap height of WISE on every side. **Minimum size:** 32px lockup height.
- The logo set is final: nothing is open or pending.

## Colour tokens

Every colour comes from the MUI theme in `packages/ui`; a component never hard-codes one ([accessibility.md](accessibility.md#patterns)).

| Token                | Light                         | Dark                          | Use                                                                                  |
| -------------------- | ----------------------------- | ----------------------------- | ------------------------------------------------------------------------------------ |
| `background.default` | `#F7F7FC`                     | `#05060E`                     | Page                                                                                 |
| `background.paper`   | `#FFFFFF`                     | `#0D0F1F`                     | Cards, app bar, menus                                                                |
| `surface2`           | `#F0EFF8`                     | `#151832`                     | Inset panels, context bar, chips, readouts                                           |
| `divider`            | `#E0DEEE`                     | `#262A4D`                     | Card borders, rules, chart grid; decorative only                                     |
| `fieldOutline`       | `#7D7A98`                     | `#6B7096`                     | Borders that identify a control; also the chart's current-setup series (3:1 or more) |
| `text.primary`       | `#120F24`                     | `#F2F3FA`                     | Text                                                                                 |
| `text.secondary`     | `#56546E`                     | `#A9AECB`                     | Secondary text, labels, units                                                        |
| `primary.main`       | `#6D28D9`                     | `#A98BFF`                     | Links, selected states, focus ring                                                   |
| `secondary.main`     | `#BE185D`                     | `#FF2BD6`                     | MUI's second accent, for a component that needs one; never for meaning               |
| Gradient stops       | `#0891B2` `#6D28D9` `#DB2777` | `#00E5FF` `#7C4DFF` `#FF2BD6` | Brand gradient, 100°                                                                 |
| Action gradient      | `#0E7490` `#5B21B6` `#BE185D` | same                          | Filled primary buttons and the "Lowest cost" chip; white text                        |
| `success`            | `#047857` on `#E3F5EC`        | `#34D399` on 14% of itself    | Cheaper, savings                                                                     |
| `error`              | `#C62828` on 12% of itself    | `#FF6B63` on 14% of itself    | Pricier, errors                                                                      |
| `warning`            | `#A84B00` on 12% of itself    | `#FFB74D` on 14% of itself    | Caution, partial data                                                                |

- Text tokens meet 4.5:1 on `background.default`, `background.paper` and `surface2` in both modes; `fieldOutline`, `primary.main` and `secondary.main` meet 3:1. The theme's tests check it.
- The gradient stops don't all reach 4.5:1, so gradient text is used only at 24px or larger.
- Green, red and amber always come with a text or icon companion (`↑ €2.00 more a month`).
- Forced colors: gradients and glows give way to system colours; series, chips and the switch keep a visible system-colour fill or border.

## Where the gradient goes

The logo; the filled primary button; the page's one hero figure (gradient text, 24px or larger); the border of the hero card and of the lowest-cost item in a list (1.5px); the current navigation item's indicator; the selected cost series in a chart; the "Lowest cost" chip; the glow switch's track while on. Never on body or small text, section or page backgrounds, every card in a list, semantic states, disabled controls or icons.

## Glow

- **Strength:** medium. Dark: `0 0 28px -6px` cyan `#00E5FF` at 55% plus `0 0 56px -10px` magenta `#FF2BD6` at 45%; light: the same at half the opacity. Gradient text gets `drop-shadow(0 0 18px)` violet `#7C4DFF` at 60%, dark mode only; the logo gets `drop-shadow(0 0 10px)` in the same violet, 50% in dark and 25% in light.
- **Hover halo:** a hovered clickable card gets `0 0 24px -8px` `primary.main` at 50% in dark and 25% in light (`glow.hover`); it follows the glow switch like the halo.
- **Where:** only on gradient elements, the filled primary button and the hover halo of a clickable card; never on chart bars.
- **Off:** halos become ordinary elevation (light `0 1px 2px` and `0 8px 24px -12px` of the text colour at 6% and 14%; dark `0 1px 2px` black at 50%); text and logo glow go away; gradients stay.
- **The switch:** a real switch (a native checkbox with `role="switch"` that exposes its checked state as `aria-checked`, Space toggles) named "Glow effects"; its label never changes, the track's position and colour show the state (no changing On/Off text). Bulb icon at every width, the word "Glow" from 840px. On by default, off by default when the device asks for more contrast or forced colours; stored per browser ([architecture.md](architecture.md#appearance)).
- **Motion:** no pulsing or flicker; changes fade within 0.25s, instantly under `prefers-reduced-motion`.

## Hover and focus

Hover applies only with a real pointer (`@media (hover: hover) and (pointer: fine)`) and never reveals information. Only a clickable card (a `Card` holding a `CardActionArea`, or a `Card` that is a link or button) reacts: it lifts 2px and gets a 1px `primary.main` outline with the soft one-colour `glow.hover` halo (a plain shadow when glow is off), never the gradient border that marks the lowest-cost item. A static card never changes on hover. Keyboard focus on a clickable card shows the same outline; every other focusable shows a 2px `primary.main` focus ring at a 3px offset (inset inside menus, lists and the bottom bar so an edge doesn't clip it).

## Typography

One family everywhere: **Red Hat Display**, variable, bundled with the apps (`@fontsource-variable/red-hat-display`), so no request goes to a font CDN. It covers € and every Lithuanian letter. Amounts use tabular numerals and the locale's format (`€11.25`, `11,25 €`).

| Role                        | Size / weight                                              |
| --------------------------- | ---------------------------------------------------------- |
| Page title (`h1`)           | 34px / 800 (26px below 600px)                              |
| Section title (`h2`)        | 21px / 700                                                 |
| Card title (`h3`)           | 17px / 700                                                 |
| Body                        | 16px / 400, line height 1.6; running text never below 15px |
| Secondary                   | 15px / 400–600                                             |
| Overline                    | 13px / 700, uppercase, 0.1em tracking                      |
| Hero figure                 | 60px / 800 (44px below 600px), gradient                    |
| Card amount                 | 28px / 800                                                 |
| Inline amount               | 19–20px / 800                                              |
| Button                      | 16px / 700                                                 |
| Chart axis and month labels | 12px / 600; the smallest size allowed                      |

Weight 800 only for the page title and amounts.

## Shape and spacing

Radius 24px hero card, 20px cards, 16px inset panels, 14px buttons, 12px fields, fully round chips. MUI's 8px unit; cards pad 24px (20px below 600px); sections 24–28px apart. Buttons 48px high, app-bar controls 44px (40px below 400px).

## Components

Each component is the MUI one in the second column, styled by its theme override or variant ([theme.md](theme.md)).

| Component                     | MUI component                                           | Style                                                                                                                                |
| ----------------------------- | ------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| Primary button (`contained`)  | `Button` `variant="contained"`                          | Action gradient, white text, glow; one per region                                                                                    |
| Secondary button (`outlined`) | `Button` `variant="outlined"`                           | Transparent, text colour, 1.5px `fieldOutline`                                                                                       |
| Card                          | `Card`                                                  | `background.paper`, 1px `divider`, elevation                                                                                         |
| Highlighted card              | `Card` custom variant `highlighted`                     | 1.5px gradient border, glow; one per group                                                                                           |
| Inset panel                   | `Paper` `elevation={0}`, on `surface2`                  | `surface2`, no border                                                                                                                |
| Chips                         | `Chip`; custom variants `lowestCost` and `pastPrices`   | `surface2` with `text.secondary`; "Lowest cost" in the action gradient; "Past prices" in `primary.main` on its tint                  |
| Fields                        | `TextField` `variant="outlined"`                        | 12px radius, `fieldOutline`, `primary.main` when focused; disabled fields dimmed                                                     |
| Segmented choice              | `RadioGroup row` with `Radio`s in `FormControlLabel`s   | Pill group; the selected one filled with `primary.main`. The variant sits on `MuiFormGroup` ([theme.md](theme.md#themed-components)) |
| Menus                         | `Menu` with `MenuItem`s                                 | `background.paper`, 16px radius, items 44px high, the checked item ticked (`IconMenu` adds the `CheckRounded`)                       |
| Glow switch                   | `Switch`                                                | Track gradient while on ([Glow](#glow))                                                                                              |
| Bottom bar                    | `BottomNavigation` with `BottomNavigationAction`s       | Under 600px ([layout.md](layout.md))                                                                                                 |
| Drawer                        | `Drawer` `variant="permanent"`                          | From 1200px ([layout.md](layout.md))                                                                                                 |
| Rail                          | Narrow (88px) permanent `Drawer` with `ListItemButton`s | 600-1199px; MUI has no rail component                                                                                                |

## Don'ts

No gradient or glow on more than one thing a region is about; no gradient text under 24px; no coloured page washes, scan lines, flicker or "terminal" fonts; no brand colours for meaning and no meaning by colour alone; no hard-coded colour, shadow or font in a component and no non-Rounded icon (a lint check enforces both: [conventions.md](conventions.md#lint)).
