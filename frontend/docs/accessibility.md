# Frontend accessibility

The accessibility rules for `frontend/`, `admin/` and the `@wattwise/*` packages. The target is WCAG 2.2 level AA. The short form agents read while editing UI is `frontend/.claude/rules/accessibility.md`; this doc owns the rules. Test mechanics are in [testing.md](testing.md#accessibility-tests).

## Patterns

- **Semantic elements and MUI components.** A button for an action, a link for navigation, real lists and tables, one `h1` per page, no skipped heading levels. Never a clickable `div`.
- **Labelled fields.** Every field has a visible label, and its accessible name contains that label (WCAG 2.5.3).
- **Errors linked to fields.** A field error is tied to its field (`aria-describedby` through the helper text, `aria-invalid`). A form-level or async error is announced (`role="alert"` or a polite live region).
- **Focus after a route change** moves to the page heading.
- **Dialogs** trap focus and give it back to the trigger on close.
- **No meaning by colour alone.** Status colours keep a text or icon companion.
- **Contrast from theme tokens** (4.5:1 for text, 3:1 for large text and UI components), in light and dark. A hard-coded colour bypasses the theme and is a finding. The tokens are in [style.md](style.md#colour-tokens); gradient text only at 24px or larger.
- **Targets** are at least 24 by 24 CSS pixels (2.5.8).
- **Motion.** Animation respects `prefers-reduced-motion`.
- **Reflow.** Content works at a 320px width without horizontal scroll (1.4.10), and text resizes to 200%. Charts follow [layout.md](layout.md#charts): values reachable by keyboard and in a table, and the pinned axis never covers a focused item.
- **Text for assistive technology** (`aria-label`, `aria-description`, live-region text) comes from translations, like any other text ([structure.md](structure.md#translations)).

## Tools

- **Static review.** The `frontend-wcag` agent reads a change against this doc and fixes production UI (components, pages, theme, aria text). It writes no tests; a missing accessibility test goes to `frontend-test-writer` as a finding.
- **Automated check.** A `vitest-axe` test per page and per shared component ([testing.md](testing.md#accessibility-tests)). jsdom has no layout, so axe can't check contrast, reflow or target size; those stay with the static review.
- **Manual check.** The story's `/smoke-check` ([skill](../../.claude/skills/smoke-check/SKILL.md)) drives the real screens in a browser.

## When it runs

The story orchestrator decides whether a story needs `frontend-wcag`: any change to pages, shared components, the theme or aria text. It runs after `frontend-developer` and before the reviewers, alone in the checkout, because it edits production UI.

It fixes what is unambiguous. It asks before a change to the palette, the design, the wording or the behaviour.
