---
paths:
  - '*/src/**/*.tsx'
---

# Accessibility

- Target WCAG 2.2 AA.
- Use semantic elements and MUI components, never a clickable `div`.
- Every field has a label; an error is tied to its field (`aria-describedby` / helper text).
- After a route change focus moves to the page heading; dialogs trap focus and restore it on close.
- Never convey meaning by colour alone; get contrast from theme tokens, not hard-coded colours.
- Touch targets are at least 24px.
- Respect `prefers-reduced-motion`.
- Text for `aria-label` and similar comes from translations, like any other text.

The full rules are in [accessibility.md](../../../frontend/docs/accessibility.md).
