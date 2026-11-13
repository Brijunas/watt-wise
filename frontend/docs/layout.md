# Frontend layout

How screens are laid out in `frontend/` and `admin/`: breakpoints, the app shell, content width, and the patterns for comparison results, charts and result states. Colours, type and components are in [style.md](style.md); accessibility rules in [accessibility.md](accessibility.md).

## Breakpoints

The MUI theme uses Material Design 3's window classes:

| Key   | From | Class       | Typical screens        |
| ----- | ---- | ----------- | ---------------------- |
| `xs`  | 0    | compact     | Phones (320–430)       |
| `sm`  | 600  | medium      | Small tablets          |
| `md`  | 840  | expanded    | Tablets, small laptops |
| `lg`  | 1200 | large       | Laptops (1280–1536)    |
| `xl`  | 1600 | extra-large | FHD (1920)             |
| `xxl` | 2400 | —           | 2K and wider           |

Every screen works from 320px wide and at 200% text without horizontal page scroll; only charts and tables scroll inside their own box.

## App shell

| Width       | Navigation                  | App bar                                                                                          | Content                             |
| ----------- | --------------------------- | ------------------------------------------------------------------------------------------------ | ----------------------------------- |
| under 600   | Bottom bar, icon over label | Logo 32px (36 from 360, 40 from 400), glow switch, theme menu, account menu (holds the language) | One column, 16px gutters            |
| 600–1199    | Rail, 88px, icon over label | Logo 48px, + EN/LT button                                                                        | Max 1120px, 24px gutters            |
| 1200–1599   | Drawer, 256px               | Glow switch shows its "Glow" label from 840px                                                    | Max 1120px, 40px gutters            |
| 1600–2399   | Drawer                      | Same                                                                                             | Max 1440px with a 340px side column |
| 2400 and up | Drawer                      | Same                                                                                             | Max 1600px with a 380px side column |

The current item's indicator uses the gradient. Focused content keeps clear of the fixed bars (`scroll-padding`). The theme button opens a menu (Light, Dark, Follow device); the account menu holds the language below 600px and Settings and Sign out at every width.

## Comparison results

The results screen (E14) is built to these rules; the reviewed prototype is the reference.

- **Context bar** at the top: the place, the analysis period with its data coverage and a "Change period" dialog, and the cost basis (per month by default, per year) as a segmented choice that recalculates every amount.
- **Hero:** the lowest-cost grid + supplier combination, labelled as such, with the saving per the cost basis, one primary action, and today versus lowest cost.
- **Baseline:** the current setup as its own strip, not inside the ranking; every saving is relative to it.
- **Ranking:** tabs for grid + supplier, supplier plans and grid plans; "Lowest cost" (never "Best for you") marks the cheapest; pricier options say so in red with an arrow and text; exchange-price options carry "Past prices" and "What you would have paid at past exchange prices. Not a forecast."
- **Cost breakdown:** choosing a ranked option shows that option's own total, difference, terms and monthly series, and moves focus to the breakdown heading. Its note follows the data (where it saves most, or in how many months it costs more).
- **Phones:** a "Jump to the ranked plans" link under the hero.

## Charts

- Current setup in `fieldOutline` against the selected option in the vertical gradient; no glow.
- A visible € axis with gridlines; the axis grows with text, stays pinned while the chart scrolls, and never covers a focused month.
- Every month (or bucket) is a button whose accessible name holds its values. A persistent values panel above the chart shows the focused, tapped or hovered month — no floating tooltip.
- A "Show the numbers as a table" disclosure with the same data.
- Forced colors: series in `CanvasText` and `Highlight`.
- Below 840px, a hint says to swipe; focusing a month scrolls it fully into view in both directions.

## Result states

Each state says what happened and what to do next, with one main action: loading (skeleton, `aria-busy`); no usage uploaded; only part of the period has data (amber); current plans not entered (ranking still available); exchange prices unavailable (fixed-price plans still ranked); already on the cheapest setup (green); a tie (both share first place and the "Lowest cost" mark; cards stack when they don't fit); calculation error. The reviewed prototype has each in English and Lithuanian.
