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

The app bar is 56px tall under 600 (also in landscape) and 64px from 600, with 44px controls (40px under 400, where its gutters shrink to 8px); the logo's trailing space is a quarter of its height (the cap height of WISE). The app bar's icons stay 24px at any text size, and the Admin chip shrinks with an ellipsis, down to nothing, so the controls always stay on screen. The EN/LT button shows the current language's code and opens the language menu. The side column sits inside the content cap, beside the page from 1600 and after it below. The bottom bar is at least 56px tall and grows when a label wraps; a selected label keeps its size. The shell publishes the space its fixed bars take as `--ww-shell-inset-top`, `-start` and `-bottom` on `html` (the bottom one measured), so the main content, `scroll-padding` and the update prompt stay clear of them.

The logo links to the home page (`/`). The side column sits 24px from the page; the rail's labels use the caption size and may wrap. At 200% text a long Lithuanian bottom-bar label may break mid-word, but it stays inside the bar, which grows. The Settings list and About's data sit in a Card, and About shows `PageLoading` while it loads.

The admin's app bar shows the same logo lockup followed by a small "Admin" chip (`surface2`, `text.secondary`, translated), also on its minimal bar; the mark is untouched. The current item's indicator uses the gradient. Focused content keeps clear of the fixed bars (`scroll-padding`). The theme button opens a menu (Light, Dark, Follow device); the frontend's account menu holds Settings (a page linking to change password and devices) and Sign out at every width and the language below 600px, and for a visitor sign in and sign up; the admin's account menu holds Sign out and the language below 600px.

**Signed-out pages** (sign-up, check email, confirm email, resend confirmation, sign-in, forgot and reset password, the admin's sign-in and no-access) have a minimal app bar (logo, glow switch, theme menu, language) with no account menu (the sign-in and sign-up forms link to each other), no bottom bar, rail or drawer, and centred content. The visitor menu (Sign in, Sign up) stays only on public pages that use the full shell, such as About.

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
