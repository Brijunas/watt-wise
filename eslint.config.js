import js from '@eslint/js'
import eslintReact from '@eslint-react/eslint-plugin'
import { defineConfig, globalIgnores } from 'eslint/config'
import prettier from 'eslint-config-prettier/flat'
import { createTypeScriptImportResolver } from 'eslint-import-resolver-typescript'
import importX from 'eslint-plugin-import-x'
import reactHooks from 'eslint-plugin-react-hooks'
import fs from 'node:fs'
import path from 'node:path'
import globals from 'globals'
import tseslint from 'typescript-eslint'

const apps = ['frontend', 'admin']

// Package graph: each package and the packages it must never import.
const forbiddenPackageImports = {
  i18n: ['core', 'api-client', 'ui'],
  core: ['api-client', 'ui'],
  'api-client': ['i18n', 'ui'],
  ui: ['api-client'],
}

const appZones = apps.flatMap((app) => {
  const src = `${app}/src`
  const featuresDir = `${src}/features`
  // One zone per feature, built when the config loads: no features folder, no zones.
  // Read from the config's folder, so the listing doesn't depend on where ESLint is started.
  const featuresPath = path.join(import.meta.dirname, featuresDir)
  const features = fs.existsSync(featuresPath)
    ? fs.readdirSync(featuresPath, { withFileTypes: true }).filter((entry) => entry.isDirectory())
    : []
  return [
    {
      // lib/i18n.ts is excluded here and gets its own zones below.
      target: [`${src}/!(app|features|testing|lib)/**`, `${src}/lib/!(i18n.ts)`, `${src}/lib/*/**`],
      from: [featuresDir, `${src}/app`],
      message: 'Shared folders must not import features/ or app/.',
    },
    {
      target: `${src}/lib/i18n.ts`,
      from: `${src}/app`,
      message: 'Shared folders must not import features/ or app/.',
    },
    {
      // A glob `from` takes its `except` as globs on absolute paths.
      target: `${src}/lib/i18n.ts`,
      from: `${featuresDir}/**`,
      except: [path.join(import.meta.dirname, featuresDir, '*/locales/*.json')],
      message: 'lib/i18n.ts may only import features/<feature>/locales/*.json from features/.',
    },
    {
      target: `${src}/testing`,
      from: featuresDir,
      message: 'testing/ must not import features/.',
    },
    {
      target: `${src}/testing`,
      from: `${src}/app`,
      except: ['./provider.tsx'],
      message: 'testing/ may only import app/provider.tsx from app/.',
    },
    {
      target: featuresDir,
      from: `${src}/app`,
      message: 'features/ must not import app/.',
    },
    ...apps
      .filter((other) => other !== app)
      .map((other) => ({
        target: src,
        from: other,
        message: `${app} must not import ${other}. Move shared code to a @wattwise/* package.`,
      })),
    ...features.map((feature) => ({
      target: `${featuresDir}/${feature.name}`,
      from: featuresDir,
      except: [`./${feature.name}`],
      message: `features/${feature.name} must not import another feature. Compose them in app/routes/.`,
    })),
  ]
})

const packageZones = Object.entries(forbiddenPackageImports).flatMap(([name, forbidden]) => [
  ...forbidden.map((other) => ({
    target: `packages/${name}`,
    from: `packages/${other}`,
    message: `@wattwise/${name} must not import @wattwise/${other}.`,
  })),
  ...apps.map((app) => ({
    target: `packages/${name}`,
    from: app,
    message: 'Packages must not import an app.',
  })),
])

export default defineConfig(
  globalIgnores(['**/dist/', '**/dev-dist/', '**/coverage/']),
  js.configs.recommended,
  {
    extends: [tseslint.configs.strictTypeChecked, tseslint.configs.stylisticTypeChecked],
    languageOptions: {
      parserOptions: { projectService: true, tsconfigRootDir: import.meta.dirname },
    },
  },
  {
    files: ['**/*.{ts,tsx}'],
    extends: [eslintReact.configs['recommended-type-checked'], reactHooks.configs.flat.recommended],
    languageOptions: { globals: globals.browser },
  },
  {
    files: ['**/*.{js,mjs,cjs,jsx}'],
    extends: [tseslint.configs.disableTypeChecked],
    languageOptions: { globals: globals.node },
  },
  {
    files: ['**/*.jsx'],
    extends: [eslintReact.configs.recommended, reactHooks.configs.flat.recommended],
    languageOptions: { globals: globals.browser },
  },
  {
    files: [...apps.map((app) => `${app}/src/**/*.{ts,tsx}`), 'packages/*/src/**/*.{ts,tsx}'],
    plugins: { 'import-x': importX },
    settings: {
      'import-x/extensions': ['.ts', '.tsx'],
      'import-x/resolver-next': [
        createTypeScriptImportResolver({
          noWarnOnMultipleProjects: true,
          project: ['frontend/tsconfig.json', 'admin/tsconfig.json', 'packages/*/tsconfig.json'],
        }),
      ],
    },
    rules: {
      'import-x/no-restricted-paths': [
        'error',
        { basePath: import.meta.dirname, zones: [...appZones, ...packageZones] },
      ],
      'import-x/no-cycle': 'error',
    },
  },
  // A zone silently skips an import the resolver can't follow, which isn't guaranteed for a
  // workspace package that isn't a declared dependency, so the package graph is also enforced by name.
  ...Object.entries(forbiddenPackageImports).map(([name, forbidden]) => ({
    files: [`packages/${name}/**/*.{ts,tsx}`],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: forbidden.map((other) => ({
            group: [`@wattwise/${other}`, `@wattwise/${other}/*`],
            message: `@wattwise/${name} must not import @wattwise/${other}.`,
          })),
        },
      ],
    },
  })),
  {
    files: apps.map((app) => `${app}/src/**/*.{ts,tsx}`),
    ignores: apps.map((app) => `${app}/src/stores/hooks.ts`),
    rules: {
      'no-restricted-imports': [
        'error',
        {
          paths: [
            {
              name: 'react-redux',
              importNames: ['useSelector', 'useDispatch'],
              message: 'Use the typed hooks from @/stores/hooks.',
            },
          ],
        },
      ],
    },
  },
  prettier,
)
