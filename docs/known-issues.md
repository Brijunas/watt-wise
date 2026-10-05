# Known issues

Upstream problems the project works around today. Each entry says what the workaround is, where to follow the upstream fix, and what to change once the fix ships. Remove an entry when its workaround is gone.

## TypeScript 7 is not supported by typescript-eslint

_Recorded 2026-10-04 (S1.2). TypeScript 7.0.2, typescript-eslint 8.71.0, @eslint-react/eslint-plugin 5.24.0._

**Problem.** TypeScript 7 is the native (Go) rewrite of the compiler, and 7.0 ships without a JavaScript API. typescript-eslint needs that API to parse TypeScript and to run type-aware rules.

- When the `typescript` module is TS 7, `require('typescript')` exposes only the version, and `@typescript-eslint/typescript-estree` crashes when it loads.
- typescript-eslint's peer range is `typescript >=4.8.4 <6.1.0`, so installing TS 7 under that name also causes peer-dependency errors.
- `@eslint-react/eslint-plugin` is built on `@typescript-eslint/*` packages, so it has the same limit. Its own `typescript: "*"` peer range doesn't change that.

**Workaround in place.** Both majors are installed side by side, as described in Microsoft's TS 7 announcement. In the root `package.json` devDependencies:

```json
"@typescript/native": "npm:typescript@^7",
"typescript": "npm:@typescript/typescript6@^6"
```

| Need                                                                   | Provided by                      |
| ---------------------------------------------------------------------- | -------------------------------- |
| `tsc` binary (type-checking)                                           | TS 7 (`@typescript/native`)      |
| `typescript` module (ESLint, typescript-eslint, @eslint-react, editor) | TS 6 (`@typescript/typescript6`) |
| `tsc6` binary                                                          | TS 6, for comparing results      |

The two compilers can disagree in rare edge cases. If ESLint's type-aware rules and `tsc` report different things about the same code, suspect this.

**Where to follow it.**

- [typescript-eslint#10940](https://github.com/typescript-eslint/typescript-eslint/issues/10940): the tracking issue, "Use TS 7 (tsgo / typescript-go) for type information".
- [typescript-eslint#12803](https://github.com/typescript-eslint/typescript-eslint/pull/12803): the draft PR adding a native backend. It targets TS 7.1 and is opt-in through `parserOptions.projectService: { EXPERIMENTAL_backend: 'native' }`.
- [typescript-eslint#12518](https://github.com/typescript-eslint/typescript-eslint/issues/12518): the maintainer's statement that nothing can be done until TS 7 has an API.
- [Announcing TypeScript 7.0](https://devblogs.microsoft.com/typescript/announcing-typescript-7-0/): says the new API is expected in 7.1, and describes the side-by-side setup.
- Quick check of the current peer range:
  ```bash
  npm view typescript-eslint peerDependencies
  ```

**Resolved when** a stable typescript-eslint release accepts TypeScript 7 in its peer range, and `@eslint-react/eslint-plugin` works on top of it. Then:

1. In the root `package.json`, set `typescript` to the latest TS 7 and remove the `@typescript/native` alias.
2. Turn on whatever parser option typescript-eslint requires for the native backend in `eslint.config.js`, if it is still opt-in.
3. Run `pnpm install`, `pnpm lint` and `pnpm typecheck`.
4. Remove the TS exception from the "Tool versions" bullet in `docs/technical.md`, and delete this entry.

## ESO CSV format around DST changes is unknown

_Recorded 2026-10-05. Blocks the ESO CSV parser in E7._

**Problem.** The ESO CSV gives times as Europe/Vilnius local time. On the last Sunday of October, local 03:00–03:59 occurs twice; on the last Sunday of March, 03:00–03:59 does not exist. We don't know how Mano ESO exports these days: two rows with the same local time, an explicit UTC offset, a 24-hour day with one hour merged or dropped, or something else. If the parser guesses wrong, both October rows map to the same `hour_utc` and the upsert silently overwrites one hour of consumption.

**Workaround in place.** None yet; nothing parses the CSV.

**Resolved when** we have a real ESO export that spans both a March and an October DST change. Then:

1. Check how the repeated and skipped hours appear in it.
2. Choose the NodaTime resolver for `LocalDateTime` → `Instant` that matches (e.g. earlier/later occurrence by row order for the repeated hour; reject or skip for the missing one).
3. Commit trimmed copies of the export as test fixtures and add parser tests for both DST days.
4. Delete this entry.
