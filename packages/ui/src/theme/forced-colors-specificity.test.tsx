import ListItemButton from '@mui/material/ListItemButton'
import Switch from '@mui/material/Switch'
import { cleanup } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { emittedCss, renderThemed } from '../testing/theme-test-utils'

// Under forced colours the real theme runs MUI's `enhanceHighContrast`, which gives every
// ButtonBase a `5px auto` focus outline and tints the focused ListItemButton. Our rules for the
// same elements only win if their selectors out-rank MUI's, so this reads the emitted rules from
// the sheet and compares specificity (jsdom ignores media queries, so it can't be checked by
// computed style).
afterEach(cleanup)

const prefix = '@media (forced-colors: active){'

interface Rule {
  selector: string
  body: string
}

/** The rules Emotion emitted inside `@media (forced-colors: active)`, one per selector. */
const forcedRules = (): Rule[] =>
  emittedCss()
    .split('\n')
    .filter((line) => line.startsWith(prefix))
    .flatMap((line) =>
      Array.from(line.slice(prefix.length).matchAll(/([^{}]+)\{([^{}]*)\}/g)).flatMap((match) => {
        const [, selectors = '', body = ''] = match
        return selectors.split(',').map((selector) => ({ selector: selector.trim(), body }))
      }),
    )

/** [classes, attributes and pseudo-classes; elements and pseudo-elements] of one selector. */
const specificity = (selector: string): [number, number] => {
  let classes = 0
  let elements = 0
  const withoutHas = selector.replace(/:has\(([^()]*)\)/g, (_match, inner: string) => {
    const [innerClasses, innerElements] = specificity(inner)
    classes += innerClasses
    elements += innerElements
    return ''
  })
  const withoutPseudoElements = withoutHas.replace(/::[\w-]+/g, () => {
    elements += 1
    return ''
  })
  classes += (withoutPseudoElements.match(/\.[\w-]+|:[\w-]+|\[[^\]]*\]/g) ?? []).length
  elements += (withoutPseudoElements.match(/(^|[\s>+~])[a-zA-Z][\w-]*/g) ?? []).length
  return [classes, elements]
}

const score = (selector: string) => {
  const [classes, elements] = specificity(selector)
  return classes * 1000 + elements
}

const only = (rules: Rule[], predicate: (rule: Rule) => boolean) => {
  const found = rules.filter(predicate)
  const [rule] = found
  if (found.length !== 1 || rule === undefined) {
    throw new Error('Expected one rule, found ' + String(found.length))
  }
  return rule
}

describe('the specificity helper', () => {
  it.each([
    ['.a', [1, 0]],
    ['.a.b:focus-visible', [3, 0]],
    ['.a .b:focus-visible', [3, 0]],
    ['.a::before', [1, 1]],
    ['.a:focus-within:has(input:focus-visible)', [3, 1]],
    ['.a.b:hover input', [3, 1]],
  ] as const)('counts %s as %j', (selector, expected) => {
    expect(specificity(selector)).toEqual(expected)
  })
})

describe('ListItemButton focus under forced colours', () => {
  const rules = () => {
    renderThemed(<ListItemButton selected>Item</ListItemButton>)
    // The ButtonBase class of the item carries MUI's outline rule and both of ours.
    const all = forcedRules().filter((rule) =>
      rule.selector.includes('-MuiButtonBase-root-MuiListItemButton-root'),
    )
    const base = (suffix: string) =>
      only(
        all,
        (rule) => rule.selector.endsWith(suffix) && rule.body === 'outline:5px auto Highlight;',
      )
    const ours = (suffix: string) =>
      only(all, (rule) => rule.selector.endsWith(suffix) && rule.body.includes('outline:2px'))
    return { all, base, ours }
  }

  it('emits the doubled-class rule for a plain focus-visible item', () => {
    expect(rules().ours('.Mui-focusVisible.Mui-focusVisible').body).toContain(
      'background-color:Highlight;color:HighlightText;outline:2px solid HighlightText;outline-offset:-4px;',
    )
  })

  it('emits the rule for a selected focus-visible item', () => {
    expect(rules().ours('.Mui-selected.Mui-focusVisible').body).toContain(
      'outline:2px solid HighlightText;',
    )
  })

  // MUI's other ring selector, `:focus-within:has(input:focus-visible)`, is left out on purpose: it
  // is one element level above ours (the `input` inside `:has`), but it only matches an item that
  // contains a focus-visible input, which the rail and drawer items never do.
  it.each([
    ['.Mui-focusVisible.Mui-focusVisible', ':focus-visible'],
    ['.Mui-selected.Mui-focusVisible', ':focus-visible'],
  ] as const)('out-ranks the ButtonBase 5px ring: %s over %s', (oursSuffix, muiSuffix) => {
    const { ours, base } = rules()

    expect(score(ours(oursSuffix).selector)).toBeGreaterThan(score(base(muiSuffix).selector))
  })

  it('out-ranks the tinted fill MUI gives a focus-visible item', () => {
    const { all, ours } = rules()
    const fill = only(
      all,
      (rule) =>
        rule.selector.endsWith('.Mui-focusVisible') &&
        rule.body.includes('forced-color-adjust:none;color:HighlightText'),
    )

    expect(score(ours('.Mui-focusVisible.Mui-focusVisible').selector)).toBeGreaterThan(
      score(fill.selector),
    )
  })

  it('ties with the fill MUI gives a selected focus-visible item, which sets the same colours', () => {
    const { all, ours } = rules()
    const fill = only(
      all,
      (rule) =>
        rule.selector.endsWith('.Mui-selected.Mui-focusVisible') &&
        rule.body === 'color:HighlightText;background-color:Highlight;',
    )

    expect(score(ours('.Mui-selected.Mui-focusVisible').selector)).toBeGreaterThanOrEqual(
      score(fill.selector),
    )
  })
})

describe('Switch focus under forced colours', () => {
  const rules = () => {
    renderThemed(<Switch />)
    const all = forcedRules()
    const base = (suffix: string) =>
      only(
        all,
        (rule) =>
          rule.selector.includes('MuiSwitchBase-root-MuiSwitch-switchBase') &&
          rule.selector.endsWith(suffix) &&
          rule.body === 'outline:5px auto Highlight;',
      )
    const ours = (suffix: string) =>
      only(
        all,
        (rule) =>
          rule.selector.includes(' .MuiSwitch-switchBase') &&
          rule.selector.endsWith(suffix) &&
          rule.body === 'outline:none;',
      )
    return { base, ours }
  }

  it.each([':focus-visible', ':focus-within:has(input:focus-visible)'] as const)(
    'out-ranks the ButtonBase ring on %s',
    (suffix) => {
      const { ours, base } = rules()

      expect(score(ours(suffix).selector)).toBeGreaterThan(score(base(suffix).selector))
    },
  )
})
