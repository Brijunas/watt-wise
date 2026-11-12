---
name: frontend-translations
description: Add, change, move or remove user-facing text in frontend/, admin/ or packages/ (locale JSON, translation keys, namespaces, plurals, Lithuanian wording). Use whenever such text changes, even when only one language is mentioned.
---

# Frontend translations

The rules for translations are in [frontend/docs/structure.md](../../../frontend/docs/structure.md#translations) and [frontend/docs/architecture.md](../../../frontend/docs/architecture.md#i18n). This skill is the procedure for a text change. Every text exists in both `en` and `lt` with the same keys.

## 1. Pick the level

| Who shows the text          | Where it lives                                                                                             |
| --------------------------- | ---------------------------------------------------------------------------------------------------------- |
| One feature                 | `<app>/src/features/<f>/locales/{en,lt}.json`, namespace named after the feature                           |
| One app, no feature owns it | `<app>/src/locales/{en,lt}.json`, the `app` namespace                                                      |
| Both apps                   | `packages/i18n/src/locales/<lang>/<ns>.json`, namespaces `auth`, `common`, `errors`, `shell`, `validation` |

When a second app starts using a key, move it down to the package namespace (with its key test) and remove it from the app, in the same change.

## 2. Write the keys

1. Reuse first: grep the locales for the concept and take the existing wording and key rather than a synonym.
2. **Plurals.** Any text with a count uses i18next plural keys. LT: `_one`, `_few`, `_many`, `_other` (1 minutė, 2 minutės, 10 minučių, 21 minutė; `_other` for fractions). EN: `_one`, `_other`. The key-parity check ignores plural suffixes, so complete every form yourself. Existing `{{count}} min.` texts stay unless the change touches them.
3. **Lithuanian register.** Polite "jūs". Body text and instructions in the polite imperative ("Prisijunkite", "Patikrinkite el. paštą"); buttons and menu items in the infinitive ("Prisijungti", "Sukurti paskyrą").
4. **New namespace.** In that app's `src/lib/i18n.ts` add the two imports, the `ResourceNamespaceMap` entry and both languages in `namespaces`; add its key-parity case to `src/lib/i18n.test.ts`. A new package namespace goes into `packages/i18n` the same way (its own test).
5. **Removal.** Before deleting or renaming a key, grep for its uses (`t('key'`, the namespace). Delete keys no code uses any more from both languages in the same change.
6. **Changed Lithuanian value under the same key.** Make the change without stopping, and list it in your final report as `namespace:key: old → new`, so it goes into the PR description.

## 3. Done when

- No literal text is left in components, and `pnpm typecheck` passes.
- The key tests of every touched workspace pass: `pnpm --filter <ws> test src/lib/i18n.test.ts` for an app, the package's own tests for `@wattwise/i18n`.
- The final report lists every changed Lithuanian value as above, or says there were none.
