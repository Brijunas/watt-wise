# Frontend style

The visual language of `frontend/`, `admin/` and `@wattwise/ui`: "calm neon", taken from the Watt-Wise logo. A calm, readable base for the general public, with gradient and glow kept for the few things that matter on a screen. This doc owns the logo, colours, type, gradient, glow, hover, shapes and component styles; page layout, navigation, the chart and result states are in [layout.md](layout.md). How the mode and the glow setting are stored and applied is under "Appearance" in [architecture.md](architecture.md#appearance); contrast and motion rules are in [accessibility.md](accessibility.md). Epic E5A brings `packages/ui` to this doc; until it ships, the code still has the earlier Roboto theme.

## Principles

- **Calm base, neon accents.** Quiet near-black or off-white surfaces; gradient and glow mark what to look at first, never decoration.
- **One focus per region.** At most one gradient element per screen region; a list highlights at most one item.
- **Glow is the user's choice.** On by default, off with one switch; the design works without it.
- **Plain and readable.** One friendly font, large text, plain words; the tech flavour comes from colour and the logo.
- **Meaning has its own colours.** Cheaper, pricier and caution use semantic colours, never the brand gradient.

## Logo

- **Mark:** the user's original W with the lightning bolt, used as drawn: never redrawn, recoloured or stretched. The source renders are the reference: [brand/source-mark.png](brand/source-mark.png), [brand/source-lockup.png](brand/source-lockup.png).
- **Lockup:** the mark with "WATT" over "WISE" in Red Hat Display. WATT is weight 800 with 0.14em tracking in the text colour; WISE is weight 700, 84% of WATT's width, in the gradient. WATT starts below the bolt tip; the wordmark is about 1.6 times the mark's width (the original lockup's 1.8 with its wider typeface).
- **Files** in [brand/](brand/), transparent PNG: `logo-lockup-dark.png`, `logo-lockup-light.png` (no glow; the app adds glow as a theme filter so the switch turns it off), `logo-lockup-dark-glow.png` and `logo-mark-glow.png` (marketing and other use outside the app), `logo-mark.png` (edge cleaned for light pages), `app-icon.png` (the original render on a rounded dark tile; favicon and PWA icons).
- **Clear space:** at least the cap height of WISE on every side. **Minimum size:** 32px lockup height.
- Only assets that passed the independent design review are in `brand/`. No vector master exists yet; the PNGs come from the 1254px source render.

## Missing assets

Still to be made by a designer (or a drawing agent), checked by the independent reviewer against the source renders, and approved before they go into `brand/`. Each keeps the mark exactly as drawn in `source-mark.png`: shape, bolt, crossing and shading.

Candidates for each are in [brand-production/](brand-production/), awaiting review.

| Asset                                           | Needed for                               | Requirements                                                                                                    |
| ----------------------------------------------- | ---------------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| `logo-mark.svg`                                 | Vector master; small sizes               | Faithful to `source-mark.png`, no background, no baked glow, under 50 KB                                        |
| `logo-lockup-dark.svg`, `logo-lockup-light.svg` | App bar at any density                   | The lockup above with the wordmark as outlined Red Hat Display paths; WISE gradient in the dark and light stops |
| `favicon.svg`                                   | Browser tab, `generate:icons` input      | The mark legible at 16px on light and dark tab bars                                                             |
| `maskable-icon.png` (1024px)                    | Android adaptive icon                    | The mark inside the 80% safe zone on `#05060E`                                                                  |
| `logo-mark-mono.svg`                            | Forced colours, print, single-colour use | One-colour silhouette of the mark                                                                               |
| `social-card.png` (1200×630)                    | Link previews                            | Lockup on `#05060E`, glow on                                                                                    |

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
| Gradient stops       | `#0891B2` `#6D28D9` `#DB2777` | `#00E5FF` `#7C4DFF` `#FF2BD6` | Brand gradient, 100°                                                                 |
| Action gradient      | `#0E7490` `#5B21B6` `#BE185D` | same                          | Filled primary buttons and the "Lowest cost" chip; white text                        |
| `success`            | `#047857` on `#E3F5EC`        | `#34D399` on 14% of itself    | Cheaper, savings                                                                     |
| `error`              | `#C62828`                     | `#FF6B63`                     | Pricier, errors                                                                      |
| `warning`            | `#A84B00`                     | `#FFB74D`                     | Caution, partial data                                                                |

- Text tokens meet 4.5:1 on `background.default`, `background.paper` and `surface2` in both modes; `fieldOutline` and `primary.main` meet 3:1. The theme's tests check it.
- The gradient stops don't all reach 4.5:1, so gradient text is used only at 24px or larger.
- Green, red and amber always come with a text or icon companion (`↑ €2.00 more a month`).
- Forced colors: gradients and glows give way to system colours; series, chips and the switch keep a visible system-colour fill or border.

## Where the gradient goes

The logo; the filled primary button; the page's one hero figure (gradient text, 24px or larger); the border of the hero card and of the lowest-cost item in a list (1.5px); the current navigation item's indicator; the selected cost series in a chart; the "Lowest cost" chip; the glow switch's track while on. Never on body or small text, section or page backgrounds, every card in a list, semantic states, disabled controls or icons.

## Glow

- **Strength:** medium. Dark: `0 0 28px -6px` cyan `#00E5FF` at 55% plus `0 0 56px -10px` magenta `#FF2BD6` at 45%; light: the same at half the opacity. Gradient text gets `drop-shadow(0 0 18px)` violet at 60%, dark mode only; the logo a soft drop-shadow.
- **Where:** only on gradient elements and the filled primary button; never on chart bars.
- **Off:** halos become ordinary elevation (light `0 1px 2px` and `0 8px 24px -12px` of the text colour at 6% and 14%; dark `0 1px 2px` black at 50%); text and logo glow go away; gradients stay.
- **The switch:** a real switch (`role="switch"`, `aria-checked`, Space toggles) named "Glow effects"; its label never changes, the track's position and colour show the state (no changing On/Off text). Bulb icon at every width, the word "Glow" from 840px. On by default, off by default when the device asks for more contrast or forced colours; stored per browser ([architecture.md](architecture.md#appearance)).
- **Motion:** no pulsing or flicker; changes fade within 0.25s, instantly under `prefers-reduced-motion`.

## Hover and focus

Hover applies only with a real pointer (`@media (hover: hover) and (pointer: fine)`) and never reveals information. A hovered card lifts 2px and gets a 1px `primary.main` outline with a soft one-colour halo (a plain shadow when glow is off) — never the gradient border that marks the lowest-cost item. Keyboard focus shows the same outline plus a 2px `primary.main` focus ring at a 3px offset.

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

| Component                     | Style                                                                                                               |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| Primary button (`contained`)  | Action gradient, white text, glow; one per region                                                                   |
| Secondary button (`outlined`) | Transparent, text colour, 1.5px `fieldOutline`                                                                      |
| Card                          | `background.paper`, 1px `divider`, elevation                                                                        |
| Highlighted card              | 1.5px gradient border, glow; one per group                                                                          |
| Inset panel                   | `surface2`, no border                                                                                               |
| Chips                         | `surface2` with `text.secondary`; "Lowest cost" in the action gradient; "Past prices" in `primary.main` on its tint |
| Fields                        | 12px radius, `fieldOutline`, `primary.main` when focused; disabled fields dimmed                                    |
| Segmented choice              | Radios styled as a pill group; the selected one filled with `primary.main`                                          |
| Menus                         | `background.paper`, 16px radius, items 44px high, the checked item ticked                                           |

## Don'ts

No gradient or glow on more than one thing a region is about; no gradient text under 24px; no coloured page washes, scan lines, flicker or "terminal" fonts; no brand colours for meaning and no meaning by colour alone; no hard-coded colour, shadow or font in a component.
