import HomeIcon from '@mui/icons-material/HomeRounded'
import { zodResolver } from '@hookform/resolvers/zod'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { signInSchema } from '@wattwise/core'
import type { SignInInput, SignInValues } from '@wattwise/core'
import { useForm } from 'react-hook-form'
import type { AnchorHTMLAttributes } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import AccountPage, { type AccountPageProps } from '../auth/account-page'
import SignInFields from '../auth/sign-in-fields'
import FormPasswordField from '../form/form-password-field'
import FormTextField from '../form/form-text-field'
import LanguageSwitch from '../i18n/language-switch'
import IconMenu, { type IconMenuProps } from '../menu/icon-menu'
import AppShell from '../shell/app-shell'
import NotFound, { type NotFoundProps } from '../shell/not-found'
import PageLoading from '../shell/page-loading'
import UpdatePrompt, { type UpdatePromptProps } from '../shell/update-prompt'
import {
  colorModes,
  cssFor,
  renderWithComponents,
  resetThemeState,
} from '../testing/theme-test-utils'
import GlowSwitch from './glow-switch'
import ModeSwitch from './mode-switch'

// Each case sets `theme.components.Ww<Name>` the way a page would and reads the style the browser
// would compute for the rendered element: a config-only check would not notice a slot that is not
// wired to the styled element. The values (`marginTop`) are chosen so the component's own styles
// never set them.

afterEach(() => {
  resetThemeState()
  vi.unstubAllGlobals()
})

function TestLink({ to, ...rest }: { to: string } & AnchorHTMLAttributes<HTMLAnchorElement>) {
  return <a href={to} {...rest} />
}

// A component whose required props the theme's `defaultProps` fill in.
const withoutRequired = <Props,>(props: Partial<Props>) => props as Props

const marginTop = (element: Element | null) => {
  if (element === null) throw new Error('Missing element')
  return getComputedStyle(element).getPropertyValue('margin-top')
}

const closestTo = (element: Element, selector: string) => {
  const found = element.closest(selector)
  if (found === null) throw new Error('Missing ancestor ' + selector)
  return found
}

const languages = ['en', 'lt'] as const

const text = {
  en: {
    navigation: 'Main navigation',
    notFound: 'Page not found',
    home: 'Go to home',
    loading: 'Loading',
    message: 'A new version is available.',
    reload: 'Reload',
    later: 'Later',
    signIn: 'Sign in',
    email: 'Email',
    password: 'Password',
    showPassword: 'Show password',
    hidePassword: 'Hide password',
    language: 'Language',
    appearance: 'Appearance',
  },
  lt: {
    navigation: 'Pagrindinė navigacija',
    notFound: 'Puslapis nerastas',
    home: 'Grįžti į pradžią',
    loading: 'Kraunama',
    message: 'Yra nauja versija.',
    reload: 'Atnaujinti',
    later: 'Vėliau',
    signIn: 'Prisijungti',
    email: 'El. paštas',
    password: 'Slaptažodis',
    showPassword: 'Rodyti slaptažodį',
    hidePassword: 'Slėpti slaptažodį',
    language: 'Kalba',
    appearance: 'Išvaizda',
  },
} as const

interface Values {
  name: string
  password: string
}

const FieldsHarness = ({
  children,
}: {
  children: (control: ReturnType<typeof useForm<Values>>['control']) => React.ReactNode
}) => {
  const { control } = useForm<Values>({ defaultValues: { name: '', password: '' } })
  return <>{children(control)}</>
}

const SignInHarness = ({ busy }: { busy?: boolean }) => {
  const { control, handleSubmit } = useForm<SignInInput, unknown, SignInValues>({
    resolver: zodResolver(signInSchema),
    defaultValues: { email: '', password: '' },
  })
  return (
    <SignInFields
      control={control}
      onSubmit={(event) => void handleSubmit(vi.fn())(event)}
      busy={busy}
    />
  )
}

const shell = (actions?: React.ReactNode) => (
  <AppShell
    title="Watt-Wise"
    items={[{ key: 'home', label: 'Home page', icon: <HomeIcon />, to: '/' }]}
    activeKey="home"
    linkComponent={TestLink}
    actions={actions}
  >
    <p>Page content</p>
  </AppShell>
)

const menu = (label: string) => (
  <IconMenu
    label={label}
    icon={<span>icon</span>}
    options={[{ key: 'a', selected: true, content: 'Alpha' }]}
    onSelect={vi.fn()}
  />
)

describe.each(languages)('theme.components.Ww* in %s', (lng) => {
  const t = text[lng]

  describe('WwAppShell', () => {
    const components = {
      WwAppShell: {
        styleOverrides: {
          root: { marginTop: '11px' },
          appBar: { marginTop: '12px' },
          drawer: { marginTop: '13px' },
          bottomBar: { marginTop: '14px' },
          main: { marginTop: '15px' },
        },
      },
    }

    it.each([
      { slot: 'root', width: 900, expected: '11px' },
      { slot: 'appBar', width: 900, expected: '12px' },
      { slot: 'drawer', width: 900, expected: '13px' },
      { slot: 'main', width: 900, expected: '15px' },
      { slot: 'root', width: 360, expected: '11px' },
      { slot: 'bottomBar', width: 360, expected: '14px' },
      { slot: 'main', width: 360, expected: '15px' },
    ] as const)('applies styleOverrides.$slot at $width px', ({ slot, width, expected }) => {
      renderWithComponents(shell(), { components, lng, width })
      const elements = {
        root: () => screen.getByRole('main').parentElement,
        appBar: () => screen.getByRole('banner'),
        drawer: () =>
          closestTo(screen.getByRole('navigation', { name: t.navigation }), '.MuiDrawer-root'),
        bottomBar: () => screen.getByRole('navigation', { name: t.navigation }),
        main: () => screen.getByRole('main'),
      }
      expect(marginTop(elements[slot]())).toBe(expected)
    })

    it('shows defaultProps.actions when the page passes none', () => {
      renderWithComponents(shell(), {
        components: {
          WwAppShell: { defaultProps: { actions: <button type="button">Default action</button> } },
        },
        lng,
        width: 900,
      })
      expect(screen.queryAllByRole('button', { name: 'Default action' })).toHaveLength(1)
    })
  })

  describe('WwIconMenu', () => {
    const components = {
      WwIconMenu: { styleOverrides: { root: { marginTop: '11px' }, menu: { marginTop: '12px' } } },
    }

    it('applies styleOverrides.root to the button', () => {
      renderWithComponents(menu('Pick'), { components, lng })
      expect(marginTop(screen.getByRole('button', { name: 'Pick' }))).toBe('11px')
    })

    it('applies styleOverrides.menu to the menu', async () => {
      renderWithComponents(menu('Pick'), { components, lng })
      await userEvent.click(screen.getByRole('button', { name: 'Pick' }))
      expect(marginTop(closestTo(screen.getByRole('menu'), '.MuiMenu-root'))).toBe('12px')
    })

    it('names the button from defaultProps.label', () => {
      renderWithComponents(
        <IconMenu
          {...withoutRequired<IconMenuProps<string>>({
            icon: <span>icon</span>,
            options: [],
            onSelect: vi.fn(),
          })}
        />,
        { components: { WwIconMenu: { defaultProps: { label: 'Themed label' } } }, lng },
      )
      expect(screen.queryAllByRole('button', { name: 'Themed label' })).toHaveLength(1)
    })

    it('lets an explicit label win over defaultProps.label', () => {
      renderWithComponents(menu('Explicit label'), {
        components: { WwIconMenu: { defaultProps: { label: 'Themed label' } } },
        lng,
      })
      expect(screen.queryAllByRole('button')).toHaveLength(1)
      expect(screen.queryAllByRole('button', { name: 'Explicit label' })).toHaveLength(1)
    })
  })

  describe.each([
    { name: 'WwLanguageSwitch', node: <LanguageSwitch />, button: t.language },
    { name: 'WwModeSwitch', node: <ModeSwitch />, button: t.appearance },
  ] as const)('$name', ({ name, node, button }) => {
    it('applies styleOverrides.root to the button', () => {
      renderWithComponents(node, {
        components: { [name]: { styleOverrides: { root: { marginTop: '11px' } } } },
        lng,
      })
      expect(marginTop(screen.getByRole('button', { name: button }))).toBe('11px')
    })

    it('also takes the look of WwIconMenu', () => {
      renderWithComponents(node, {
        components: { WwIconMenu: { styleOverrides: { root: { marginTop: '12px' } } } },
        lng,
      })
      expect(marginTop(screen.getByRole('button', { name: button }))).toBe('12px')
    })
  })

  describe('WwGlowSwitch', () => {
    const word = { en: 'Glow', lt: 'Švytėjimas' }[lng]
    const glowName = { en: 'Glow effects', lt: 'Švytėjimas (efektai)' }[lng]
    const overrideFor = (slot: string) => ({
      WwGlowSwitch: { styleOverrides: { [slot]: { marginTop: '11px' } } },
    })

    it('applies styleOverrides.root to the label element around the control', () => {
      renderWithComponents(<GlowSwitch />, { components: overrideFor('root'), lng })
      expect(marginTop(closestTo(screen.getByText(word), 'label'))).toBe('11px')
    })

    it('applies styleOverrides.icon to the bulb', () => {
      const { container } = renderWithComponents(<GlowSwitch />, {
        components: overrideFor('icon'),
        lng,
      })
      expect(marginTop(container.querySelector('svg[data-testid="LightbulbRoundedIcon"]'))).toBe(
        '11px',
      )
    })

    it('applies styleOverrides.label to the visible word', () => {
      renderWithComponents(<GlowSwitch />, { components: overrideFor('label'), lng })
      expect(marginTop(screen.getByText(word))).toBe('11px')
    })

    it('applies styleOverrides.switch to the switch root', () => {
      renderWithComponents(<GlowSwitch />, { components: overrideFor('switch'), lng })
      expect(
        marginTop(closestTo(screen.getByRole('switch', { name: glowName }), '.MuiSwitch-root')),
      ).toBe('11px')
    })
  })

  describe('WwAccountPage', () => {
    const page = (
      <AccountPage title="Sign in">
        <p>Body</p>
      </AccountPage>
    )

    it('applies styleOverrides.root to the container', () => {
      renderWithComponents(page, {
        components: { WwAccountPage: { styleOverrides: { root: { marginTop: '11px' } } } },
        lng,
      })
      expect(
        marginTop(closestTo(screen.getByRole('heading', { level: 1 }), '.MuiContainer-root')),
      ).toBe('11px')
    })

    it('applies styleOverrides.title to the h1', () => {
      renderWithComponents(page, {
        components: { WwAccountPage: { styleOverrides: { title: { paddingTop: '12px' } } } },
        lng,
      })
      // Not margin-top: the page's Stack zeroes its first child's margin with a more specific rule.
      expect(
        getComputedStyle(screen.getByRole('heading', { level: 1 })).getPropertyValue('padding-top'),
      ).toBe('12px')
    })

    it('uses defaultProps.title as the h1', () => {
      renderWithComponents(
        <AccountPage {...withoutRequired<AccountPageProps>({ children: <p>Body</p> })} />,
        { components: { WwAccountPage: { defaultProps: { title: 'Themed title' } } }, lng },
      )
      expect(screen.queryAllByRole('heading', { level: 1, name: 'Themed title' })).toHaveLength(1)
    })
  })

  describe('WwNotFound', () => {
    it('applies styleOverrides.root', () => {
      renderWithComponents(<NotFound linkComponent={TestLink} homeTo="/start" />, {
        components: { WwNotFound: { styleOverrides: { root: { marginTop: '11px' } } } },
        lng,
      })
      expect(marginTop(screen.getByRole('heading', { name: t.notFound }).parentElement)).toBe(
        '11px',
      )
    })

    it('links home to defaultProps.homeTo', () => {
      renderWithComponents(
        <NotFound {...withoutRequired<NotFoundProps>({ linkComponent: TestLink })} />,
        { components: { WwNotFound: { defaultProps: { homeTo: '/themed-home' } } }, lng },
      )
      expect(screen.getByRole('link', { name: t.home }).getAttribute('href')).toBe('/themed-home')
    })

    it('lets an explicit homeTo win over defaultProps.homeTo', () => {
      renderWithComponents(<NotFound linkComponent={TestLink} homeTo="/explicit" />, {
        components: { WwNotFound: { defaultProps: { homeTo: '/themed-home' } } },
        lng,
      })
      expect(screen.getByRole('link', { name: t.home }).getAttribute('href')).toBe('/explicit')
    })
  })

  describe('WwPageLoading', () => {
    it('applies styleOverrides.root', () => {
      renderWithComponents(<PageLoading />, {
        components: { WwPageLoading: { styleOverrides: { root: { marginTop: '11px' } } } },
        lng,
      })
      expect(marginTop(screen.getByRole('progressbar', { name: t.loading }).parentElement)).toBe(
        '11px',
      )
    })
  })

  describe('WwUpdatePrompt', () => {
    const prompt = <UpdatePrompt open onReload={vi.fn()} onClose={vi.fn()} />

    it('applies styleOverrides.root to the snackbar', () => {
      renderWithComponents(prompt, {
        components: { WwUpdatePrompt: { styleOverrides: { root: { marginTop: '11px' } } } },
        lng,
      })
      expect(marginTop(closestTo(screen.getByText(t.message), '.MuiSnackbar-root'))).toBe('11px')
    })

    it('applies styleOverrides.reloadButton to Reload only', () => {
      renderWithComponents(prompt, {
        components: {
          WwUpdatePrompt: { styleOverrides: { reloadButton: { marginTop: '12px' } } },
        },
        lng,
      })
      expect([
        marginTop(screen.getByRole('button', { name: t.reload })),
        marginTop(screen.getByRole('button', { name: t.later })),
      ]).toEqual(['12px', '0px'])
    })

    it('opens from defaultProps.open', () => {
      renderWithComponents(
        <UpdatePrompt
          {...withoutRequired<UpdatePromptProps>({ onReload: vi.fn(), onClose: vi.fn() })}
        />,
        { components: { WwUpdatePrompt: { defaultProps: { open: true } } }, lng },
      )
      expect(screen.queryAllByText(t.message)).toHaveLength(1)
    })
  })

  describe('WwFormTextField', () => {
    const field = (
      <FieldsHarness>
        {(control) => <FormTextField control={control} name="name" label="Name" />}
      </FieldsHarness>
    )

    it('applies styleOverrides.root to the form control', () => {
      renderWithComponents(field, {
        components: { WwFormTextField: { styleOverrides: { root: { marginTop: '11px' } } } },
        lng,
      })
      expect(
        marginTop(closestTo(screen.getByRole('textbox', { name: 'Name' }), '.MuiFormControl-root')),
      ).toBe('11px')
    })

    it('applies defaultProps.size', () => {
      renderWithComponents(field, {
        components: { WwFormTextField: { defaultProps: { size: 'small' } } },
        lng,
      })
      expect(
        closestTo(
          screen.getByRole('textbox', { name: 'Name' }),
          '.MuiInputBase-root',
        ).classList.contains('MuiInputBase-sizeSmall'),
      ).toBe(true)
    })

    it('is full width without defaultProps', () => {
      renderWithComponents(field, { lng })
      expect(
        closestTo(
          screen.getByRole('textbox', { name: 'Name' }),
          '.MuiFormControl-root',
        ).classList.contains('MuiFormControl-fullWidth'),
      ).toBe(true)
    })

    it('applies defaultProps.fullWidth over the built-in default', () => {
      renderWithComponents(field, {
        components: { WwFormTextField: { defaultProps: { fullWidth: false } } },
        lng,
      })
      expect(
        closestTo(
          screen.getByRole('textbox', { name: 'Name' }),
          '.MuiFormControl-root',
        ).classList.contains('MuiFormControl-fullWidth'),
      ).toBe(false)
    })
  })

  describe('WwFormPasswordField', () => {
    const field = (
      <FieldsHarness>
        {(control) => <FormPasswordField control={control} name="password" label={t.password} />}
      </FieldsHarness>
    )
    const input = () => screen.getByLabelText(t.password, { selector: 'input' })

    it('applies styleOverrides.root to the form control', () => {
      renderWithComponents(field, {
        components: { WwFormPasswordField: { styleOverrides: { root: { marginTop: '11px' } } } },
        lng,
      })
      expect(marginTop(closestTo(input(), '.MuiFormControl-root'))).toBe('11px')
    })

    it('applies styleOverrides.toggleButton to the visibility button', () => {
      renderWithComponents(field, {
        components: {
          WwFormPasswordField: { styleOverrides: { toggleButton: { marginTop: '12px' } } },
        },
        lng,
      })
      expect(marginTop(screen.getByRole('button', { name: t.showPassword }))).toBe('12px')
    })

    it('also takes the look of WwFormTextField', () => {
      renderWithComponents(field, {
        components: { WwFormTextField: { styleOverrides: { root: { marginTop: '13px' } } } },
        lng,
      })
      expect(marginTop(closestTo(input(), '.MuiFormControl-root'))).toBe('13px')
    })

    it('applies defaultProps.autoComplete', () => {
      renderWithComponents(field, {
        components: { WwFormPasswordField: { defaultProps: { autoComplete: 'new-password' } } },
        lng,
      })
      expect(input().getAttribute('autocomplete')).toBe('new-password')
    })
  })

  describe('WwSignInFields', () => {
    it('applies styleOverrides.root to the form', () => {
      renderWithComponents(<SignInHarness />, {
        components: { WwSignInFields: { styleOverrides: { root: { marginTop: '11px' } } } },
        lng,
      })
      expect(marginTop(closestTo(screen.getByRole('button', { name: t.signIn }), 'form'))).toBe(
        '11px',
      )
    })

    it('applies defaultProps.busy', () => {
      renderWithComponents(<SignInHarness />, {
        components: { WwSignInFields: { defaultProps: { busy: true } } },
        lng,
      })
      expect(
        closestTo(screen.getByRole('button', { name: t.signIn }), 'form').getAttribute('aria-busy'),
      ).toBe('true')
    })
  })

  describe('explicit props win over defaultProps', () => {
    it('AppShell actions', () => {
      renderWithComponents(shell(<button type="button">Explicit action</button>), {
        components: {
          WwAppShell: { defaultProps: { actions: <button type="button">Default action</button> } },
        },
        lng,
        width: 900,
      })
      expect(screen.getAllByRole('button').map((button) => button.textContent)).toEqual([
        'Explicit action',
      ])
    })

    it('UpdatePrompt open', () => {
      renderWithComponents(<UpdatePrompt open={false} onReload={vi.fn()} onClose={vi.fn()} />, {
        components: { WwUpdatePrompt: { defaultProps: { open: true } } },
        lng,
      })
      expect(screen.queryAllByText(t.message)).toHaveLength(0)
    })

    it('FormTextField size', () => {
      renderWithComponents(
        <FieldsHarness>
          {(control) => <FormTextField control={control} name="name" label="Name" size="medium" />}
        </FieldsHarness>,
        { components: { WwFormTextField: { defaultProps: { size: 'small' } } }, lng },
      )
      expect(
        closestTo(
          screen.getByRole('textbox', { name: 'Name' }),
          '.MuiInputBase-root',
        ).classList.contains('MuiInputBase-sizeSmall'),
      ).toBe(false)
    })
  })

  describe('ownerState reaches the theme overrides', () => {
    it.each([
      { width: 360, expected: '22px' },
      { width: 900, expected: '21px' },
    ])('AppShell main sees isDesktop at $width px', ({ width, expected }) => {
      renderWithComponents(shell(), {
        components: {
          WwAppShell: {
            styleOverrides: {
              main: ({ ownerState }) => ({
                marginTop: ownerState.isDesktop === true ? '21px' : '22px',
              }),
            },
          },
        },
        lng,
        width,
      })
      expect(marginTop(screen.getByRole('main'))).toBe(expected)
    })

    describe('FormPasswordField toggleButton sees visible', () => {
      const field = (
        <FieldsHarness>
          {(control) => <FormPasswordField control={control} name="password" label={t.password} />}
        </FieldsHarness>
      )
      const components = {
        WwFormPasswordField: {
          styleOverrides: {
            toggleButton: ({ ownerState }: { ownerState: Record<string, unknown> }) => ({
              marginTop: ownerState.visible === true ? '32px' : '31px',
            }),
          },
        },
      }

      it('while the password is hidden', () => {
        renderWithComponents(field, { components, lng })
        expect(marginTop(screen.getByRole('button', { name: t.showPassword }))).toBe('31px')
      })

      it('after Show password is clicked', async () => {
        renderWithComponents(field, { components, lng })
        await userEvent.click(screen.getByRole('button', { name: t.showPassword }))
        expect(marginTop(screen.getByRole('button', { name: t.hidePassword }))).toBe('32px')
      })
    })

    describe('FormTextField root sees fullWidth', () => {
      const field = (
        <FieldsHarness>
          {(control) => <FormTextField control={control} name="name" label="Name" />}
        </FieldsHarness>
      )
      const root = () =>
        closestTo(screen.getByRole('textbox', { name: 'Name' }), '.MuiFormControl-root')
      const styleOverrides = {
        root: ({ ownerState }: { ownerState: Record<string, unknown> }) => ({
          marginTop: ownerState.fullWidth === true ? '41px' : '42px',
        }),
      }

      it('by default', () => {
        renderWithComponents(field, { components: { WwFormTextField: { styleOverrides } }, lng })
        expect(marginTop(root())).toBe('41px')
      })

      it('when defaultProps.fullWidth is false', () => {
        renderWithComponents(field, {
          components: {
            WwFormTextField: { styleOverrides, defaultProps: { fullWidth: false } },
          },
          lng,
        })
        expect(marginTop(root())).toBe('42px')
      })
    })
  })

  // The look these components had as `sx` before they became themed components. jsdom's computed
  // style ignores media queries, so the breakpoint rules are read from the emitted CSS; the
  // theme's `md` is 840px.
  describe('keeps the styles that moved out of sx', () => {
    it.each([
      'padding-top:calc(3 * var(--mui-spacing))',
      'padding-bottom:calc(3 * var(--mui-spacing))',
    ])('AccountPage has %s below md', (declaration) => {
      renderWithComponents(
        <AccountPage title="Sign in">
          <p>Body</p>
        </AccountPage>,
        { lng, width: 360 },
      )
      const container = closestTo(screen.getByRole('heading', { level: 1 }), '.MuiContainer-root')
      expect(cssFor(container)).toContain(declaration)
    })

    it.each([
      'padding-top:calc(6 * var(--mui-spacing))',
      'padding-bottom:calc(6 * var(--mui-spacing))',
    ])('AccountPage has %s from md', (declaration) => {
      renderWithComponents(
        <AccountPage title="Sign in">
          <p>Body</p>
        </AccountPage>,
        { lng, width: 900 },
      )
      const container = closestTo(screen.getByRole('heading', { level: 1 }), '.MuiContainer-root')
      expect(cssFor(container, '(min-width:840px)')).toContain(declaration)
    })

    it.each([
      'display:flex',
      'flex-direction:column',
      'align-items:flex-start',
      'gap:calc(2 * var(--mui-spacing))',
    ])('NotFound has %s', (declaration) => {
      renderWithComponents(<NotFound linkComponent={TestLink} homeTo="/start" />, { lng })
      const root = screen.getByRole('heading', { name: t.notFound }).parentElement
      expect(cssFor(root ?? document.body)).toContain(declaration)
    })

    it.each([
      'display:flex',
      'justify-content:center',
      'padding-top:calc(4 * var(--mui-spacing))',
      'padding-bottom:calc(4 * var(--mui-spacing))',
    ])('PageLoading has %s', (declaration) => {
      renderWithComponents(<PageLoading />, { lng })
      const root = screen.getByRole('progressbar', { name: t.loading }).parentElement
      expect(cssFor(root ?? document.body)).toContain(declaration)
    })

    it('UpdatePrompt sits above the bottom bar below md', () => {
      renderWithComponents(<UpdatePrompt open onReload={vi.fn()} onClose={vi.fn()} />, {
        lng,
        width: 360,
      })
      const snackbar = closestTo(screen.getByText(t.message), '.MuiSnackbar-root')
      expect(cssFor(snackbar)).toContain(
        'bottom:calc(56px + env(safe-area-inset-bottom) + var(--mui-spacing))',
      )
    })

    it('UpdatePrompt sits 3 spacing units from the bottom from md', () => {
      renderWithComponents(<UpdatePrompt open onReload={vi.fn()} onClose={vi.fn()} />, {
        lng,
        width: 900,
      })
      const snackbar = closestTo(screen.getByText(t.message), '.MuiSnackbar-root')
      expect(cssFor(snackbar, '(min-width:840px)')).toContain('bottom:calc(3 * var(--mui-spacing))')
    })

    it('UpdatePrompt is anchored bottom centre', () => {
      renderWithComponents(<UpdatePrompt open onReload={vi.fn()} onClose={vi.fn()} />, { lng })
      const snackbar = closestTo(screen.getByText(t.message), '.MuiSnackbar-root')
      expect(snackbar.classList.contains('MuiSnackbar-anchorOriginBottomCenter')).toBe(true)
    })

    it('UpdatePrompt centres horizontally from sm', () => {
      renderWithComponents(<UpdatePrompt open onReload={vi.fn()} onClose={vi.fn()} />, {
        lng,
        width: 900,
      })
      const snackbar = closestTo(screen.getByText(t.message), '.MuiSnackbar-root')
      expect(cssFor(snackbar, '(min-width:600px)')).toContain('left:50%')
    })

    it('UpdatePrompt has a bold Reload button', () => {
      renderWithComponents(<UpdatePrompt open onReload={vi.fn()} onClose={vi.fn()} />, { lng })
      expect(
        getComputedStyle(screen.getByRole('button', { name: t.reload })).getPropertyValue(
          'font-weight',
        ),
      ).toBe('700')
    })

    it('AppShell puts the app bar one layer above the drawer', () => {
      renderWithComponents(shell(), { lng, width: 900 })
      expect(getComputedStyle(screen.getByRole('banner')).getPropertyValue('z-index')).toBe('1201')
    })

    it('AppShell fixes the bottom bar to the screen', () => {
      renderWithComponents(shell(), { lng, width: 360 })
      expect(
        getComputedStyle(screen.getByRole('navigation', { name: t.navigation })).getPropertyValue(
          'position',
        ),
      ).toBe('fixed')
    })
  })
})

describe.each(languages)('accessibility of the themed components in %s', (lng) => {
  const t = text[lng]
  const parts = {
    'account page': () => (
      <AccountPage title="Sign in">
        <p>Body</p>
      </AccountPage>
    ),
    'not-found page': () => <NotFound linkComponent={TestLink} homeTo="/start" />,
    'page loading': () => <PageLoading />,
    'update prompt': () => <UpdatePrompt open onReload={vi.fn()} onClose={vi.fn()} />,
    'text field': () => (
      <FieldsHarness>
        {(control) => <FormTextField control={control} name="name" label="Name" />}
      </FieldsHarness>
    ),
    'password field': () => (
      <FieldsHarness>
        {(control) => <FormPasswordField control={control} name="password" label={t.password} />}
      </FieldsHarness>
    ),
    'sign-in fields': () => <SignInHarness />,
    'sign-in fields while busy': () => <SignInHarness busy />,
    'language switch': () => <LanguageSwitch />,
    'mode switch': () => <ModeSwitch />,
    'glow switch': () => <GlowSwitch />,
    'icon menu': () => menu('Pick'),
  }

  describe.each(colorModes)('%s mode', (mode) => {
    // These are page parts, not pages, so the landmark rule doesn't apply.
    const rules = { region: { enabled: false } }

    it.each(Object.keys(parts) as (keyof typeof parts)[])('has no violations: %s', async (part) => {
      const { container } = renderWithComponents(parts[part](), { lng, mode })
      expect((await axe(container, { rules })).violations).toEqual([])
    })

    it.each([
      { name: 'language switch', node: <LanguageSwitch />, button: t.language },
      { name: 'mode switch', node: <ModeSwitch />, button: t.appearance },
      { name: 'icon menu', node: menu('Pick'), button: 'Pick' },
    ])('has no violations with the $name menu open', async ({ node, button }) => {
      renderWithComponents(node, { lng, mode })
      await userEvent.click(screen.getByRole('button', { name: button }))
      expect((await axe(document.body, { rules })).violations).toEqual([])
    })
  })
})
