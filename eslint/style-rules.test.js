import { ESLint } from 'eslint'
import { describe, expect, it } from 'vitest'

const eslint = new ESLint()
const literalRule = 'wattwise/no-literal-style'
const syntaxRule = 'no-restricted-syntax'
const rulesUnderTest = [literalRule, syntaxRule]
const app = 'frontend/src/main.tsx'
const component = 'packages/ui/src/shell/app-shell.tsx'
const testFile = 'packages/ui/src/shell/app-shell.test.tsx'
const themeFixture = "import type {Theme} from '@mui/material/styles'; declare const t: Theme; "

async function diagnostics(filePath, source) {
  const [result] = await eslint.lintText(source, { filePath })
  return [...new Set(result.messages.map(({ ruleId }) => ruleId))].filter((ruleId) =>
    rulesUnderTest.includes(ruleId),
  )
}

it.each([
  [app, "export const color = '#abc'"],
  ['admin/src/main.tsx', "export const color = 'rgb(1, 2, 3)'"],
  [component, "export const color = 'hsl(120 20% 40%)'"],
  [app, 'export const border = (width: number) => `${String(width)}px solid #abcf`'],
  [component, "export const sx = { color: 'rebeccapurple' }"],
  [app, 'export default <path fill="navy" />'],
  [app, "export const sx = {boxShadow:'0 1px 2px currentColor'}"],
  [app, "export const sx = {fontFamily:'Arial'}"],
  [app, "export const css = 'box-shadow: 0 1px 2px currentColor;'"],
  [app, "export const sx = {textShadow:'1px 2px currentColor'}"],
  [app, 'declare const x: string; export const sx = {fontFamily:`Foo, ${x}`}'],
  [app, 'declare const x: string; export const sx = {boxShadow:`0 1px ${x}`}'],
  [app, 'declare const x: string; export const sx = {color:`rgb(1,2,3) ${x}`}'],
  [app, 'declare const x: string; export const sx = {color:`rgb(${x}, 0.5)`}'],
  [app, 'declare const x: string; export const sx = {color:`hsl(${x} 50% 50%)`}'],
  [app, "export const sx = {color:'hwb(0 0% 0%)'}"],
  [app, "export const sx = {color:'oklch(70% 0.2 150)'}"],
  [app, "export const sx = {color:'color-mix(in srgb, red, white)'}"],
  [app, "export const sx = {color:'RGB(1 2 3)'}"],
  [app, 'export const sx = {color:`red`}'],
  [app, "export const sx = {bgcolor:'red'}"],
  [app, "export const sx = {'box-shadow':'0 1px 2px currentColor'}"],
  [app, "export const sx = {border:'1px solid red'}"],
  [app, 'export const color = `rgba(${String(255)} 0 0 / 0.12)`'],
  [app, themeFixture + 'export const color = `rgba(${t.vars.palette.primary.main} / 0.12)`'],
  [
    app,
    themeFixture +
      'export const color = `rgba(${t.vars.palette.primary.mainChannel} / 0.12), #fff`',
  ],
  [app, "export const sx = {background:'linear-gradient(red, blue)'}"],
  [app, "export const sx = {backgroundImage:'linear-gradient(to right, #fff, transparent)'}"],
  [app, "export const sx = {background:'url(/a.png) red'}"],
  [app, `export const sx = {background:"url( '/i/(m)/red/a.svg' ) red"}`],
  [
    app,
    'declare const o: {fakeChannel: string}; export const color = `rgba(${o.fakeChannel} / 0.5)`',
  ],
  [
    app,
    'declare const t: {vars: {other: {mainChannel: string}}}; export const color = `rgba(${t.vars.other.mainChannel} / 0.5)`',
  ],
  [app, "export const sx = {background:'linear-gradient(100deg, rgb(1 2 3))'}"],
  [app, 'declare const x: string; export const sx = {background:`linear-gradient(red, ${x})`}'],
])('rejects literal styling in %s: %s', { timeout: 30_000 }, async (file, source) => {
  expect(await diagnostics(file, source)).toEqual([literalRule])
})

describe.each([app, testFile])('MUI restrictions in %s', (file) => {
  it.each([
    "import '@mui/icons-material/Home'",
    "import '@mui/icons-material'",
    "export const icon = import('@mui/icons-material/Home')",
    'export const icon = import(`@mui/icons-material/Home`)',
    'declare const n: string; export const icon = import(`@mui/icons-material/${n}`)',
    "export * from '@mui/icons-material'",
    "export { Home } from '@mui/icons-material'",
    "import '@mui/icons-material/Home/foo'",
    'declare const theme: {palette: {mode: string}}; export const { mode } = theme.palette',
    'declare const theme: {palette: {mode: string}}; export const { palette: { mode } } = theme',
    "declare const palette: {mode: string}; export const mode = palette['mode']",
    "import {createTheme} from '@mui/material/styles'; export const {'mode': m} = createTheme().palette",
    themeFixture + "export const {'palette': {'mode': m}} = t",
    themeFixture + "export const {palette: {'mode': m}} = t",
    themeFixture + "export const mode = t['palette'].mode",
    ...['components', 'componentsProps', 'PaperProps', 'TransitionProps', 'BackdropProps'].map(
      (prop) => 'export default <div ' + prop + '={{}} />',
    ),
    "import {createTheme} from '@mui/material/styles'; export const mode = createTheme().palette.mode",
  ])('rejects %s', async (source) => {
    expect(await diagnostics(file, source)).toEqual([syntaxRule])
  })
})

it.each([
  "import '@mui/icons-material/HomeRounded'",
  'export const icon = import(`@mui/icons-material/HomeRounded`)',
  themeFixture + 'export const color = `rgb(${t.vars.palette.primary.mainChannel} / 12%)`',
  themeFixture + 'export const sx = {background:t.vars.gradient.brand}',
  "export const sx = {backgroundImage:'none'}",
  "export const sx = {backgroundImage:'url(/images/blue/hero.png)'}",
  `export const sx = {background: 'url( "/images/(mobile)/blue/hero.svg" )'}`,
  `export const sx = {background:'url("/img/red/a.png") no-repeat'}`,
  themeFixture +
    'const {palette} = t.vars; export const color = `rgba(${palette.primary.mainChannel} / 0.5)`',
  "import type {Theme} from '@mui/material/styles'; export const sx = (theme:Theme) => ({color:theme.vars.palette.primary.main})",
  "import type {Theme} from '@mui/material/styles'; export const sx = (theme:Theme) => ({boxShadow:theme.vars.glow.halo})",
  "export const sx = {color:'text.secondary'}",
  'export const sx = {boxShadow:0}',
  'export const sx = {boxShadow:2}',
  "export const sx = {boxShadow:'none'}",
  "export const sx = {textShadow:'none'}",
  "export const sx = {color:'currentColor'}",
  "export const sx = {color:'transparent'}",
  "export const sx = {color:'inherit'}",
  "export const sx = {fontFamily:'inherit'}",
  "import type {Theme} from '@mui/material/styles'; export const color = (theme: Theme) => `rgba(${theme.vars.palette.primary.mainChannel} / 0.12)`",
  'import {useColorScheme} from "@mui/material/styles"; export const useMode = () => useColorScheme().mode',
  "export const text = '/about#top'",
  "export const text = '&#8212;'",
  "export const text = '#main'",
  'declare const Chip: React.FC<{name:string}>; export default <Chip name="red" />',
  'export const css = (x: string) => `font-family: ${x}`',
  'export type LocaleKey<K extends string> = `validation.${K}`',
])('accepts %s', async (source) => {
  expect(await diagnostics(component, source)).toEqual([])
})

it.each([
  'packages/ui/src/theme/mode-switch.tsx',
  testFile,
  'frontend/src/testing/setup.ts',
  'packages/ui/src/vite.test.ts',
  'packages/ui/src/testing.ts',
])('allows literal colours in %s', async (file) => {
  expect(await diagnostics(file, "export const color = '#abcdef'")).toEqual([])
})
