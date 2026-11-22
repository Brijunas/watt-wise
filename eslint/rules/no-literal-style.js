// Fails on a literal colour, shadow or font family in component code: they come from the theme's
// tokens (frontend/docs/theme.md, conventions.md "Lint"). The config exempts the theme folder and
// test files.

const colorMessage =
  'No literal colour. Use a palette token (`theme.vars.palette.*`, a `sx` palette name) or a channel token.'
const shadowMessage =
  'No literal shadow. Use `theme.vars.glow.*`, `theme.shadows` or `none`; a new look is a theme token first.'
const fontMessage =
  'No literal font family. The theme sets it through `typography`; use a typography variant or `inherit`.'

const cssNamedColors = new Set(
  (
    'aliceblue antiquewhite aqua aquamarine azure beige bisque black blanchedalmond blue blueviolet ' +
    'brown burlywood cadetblue chartreuse chocolate coral cornflowerblue cornsilk crimson cyan ' +
    'darkblue darkcyan darkgoldenrod darkgray darkgreen darkgrey darkkhaki darkmagenta ' +
    'darkolivegreen darkorange darkorchid darkred darksalmon darkseagreen darkslateblue ' +
    'darkslategray darkslategrey darkturquoise darkviolet deeppink deepskyblue dimgray dimgrey ' +
    'dodgerblue firebrick floralwhite forestgreen fuchsia gainsboro ghostwhite gold goldenrod gray ' +
    'green greenyellow grey honeydew hotpink indianred indigo ivory khaki lavender lavenderblush ' +
    'lawngreen lemonchiffon lightblue lightcoral lightcyan lightgoldenrodyellow lightgray ' +
    'lightgreen lightgrey lightpink lightsalmon lightseagreen lightskyblue lightslategray ' +
    'lightslategrey lightsteelblue lightyellow lime limegreen linen magenta maroon ' +
    'mediumaquamarine mediumblue mediumorchid mediumpurple mediumseagreen mediumslateblue ' +
    'mediumspringgreen mediumturquoise mediumvioletred midnightblue mintcream mistyrose moccasin ' +
    'navajowhite navy oldlace olive olivedrab orange orangered orchid palegoldenrod palegreen ' +
    'paleturquoise palevioletred papayawhip peachpuff peru pink plum powderblue purple ' +
    'rebeccapurple red rosybrown royalblue saddlebrown salmon sandybrown seagreen seashell sienna ' +
    'silver skyblue slateblue slategray slategrey snow springgreen steelblue tan teal thistle ' +
    'tomato turquoise violet wheat white whitesmoke yellow yellowgreen'
  ).split(' '),
)

const hexColor = /(?<![\w&])#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3,4})(?![\w-])/
// Only the channel-token form passes: `rgb(`/`rgba(` ending a template's text, then an expression
// that reads a `…Channel` member (`theme.vars.palette.primary.mainChannel`), then ` / <alpha>)`, as
// in `rgba(${theme.vars.palette.primary.mainChannel} / 0.12)`. Every other colour function fails.
const colorFunction = /\b(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch|color|color-mix|light-dark)\(/gi
const channelFunction = /^rgba?\($/i
const channelAlphaTail = /^ \/ [\d.]+%?\)/
const cssShadowOrFont =
  /(?<![\w-])(?:box-shadow|text-shadow|font-family)\s*:(?!\s*(?:$|none\b|inherit\b))/

const shorthandColorProperties = new Set([
  'background',
  'backgroundImage',
  'border',
  'borderTop',
  'borderRight',
  'borderBottom',
  'borderLeft',
  'borderBlock',
  'borderInline',
  'outline',
  'fill',
  'stroke',
  'columnRule',
])

const isColorProperty = (name) =>
  /color$/i.test(name) || shorthandColorProperties.has(name) || name === 'stopColor'

const shadowProperties = new Set(['boxShadow', 'textShadow'])

// `url(...)` arguments (paths, quoted or not) are not colours.
const urlArgument = /url\(\s*(?:"[^"]*"|'[^']*'|[^)]*)\s*\)/gi

const isNamedColorValue = (value) =>
  value
    .replace(urlArgument, ' ')
    .toLowerCase()
    .split(/[\s,()/]+/)
    .some((token) => cssNamedColors.has(token))

// The key of an object property, or of a JSX attribute, as a string; undefined when computed.
const propertyName = (node) => {
  if (node.type === 'JSXAttribute') {
    return node.name.type === 'JSXIdentifier' ? node.name.name : undefined
  }
  if (node.computed) return undefined
  if (node.key.type === 'Identifier') return node.key.name
  // `'box-shadow'` is `boxShadow`.
  if (node.key.type === 'Literal' && typeof node.key.value === 'string') {
    return node.key.value.replace(/-([a-z])/g, (_, letter) => letter.toUpperCase())
  }
  return undefined
}

const stringValue = (valueNode) => {
  if (valueNode?.type === 'JSXExpressionContainer') return stringValue(valueNode.expression)
  if (valueNode?.type === 'Literal' && typeof valueNode.value === 'string') return valueNode.value
  // A template without expressions is a plain string.
  if (valueNode?.type === 'TemplateLiteral' && valueNode.expressions.length === 0) {
    return valueNode.quasis[0].value.cooked ?? ''
  }
  return undefined
}

// A template literal with text of its own ("0 0 8px ${x}") is a literal; one that is only an
// expression (`${theme.vars.glow.halo}`) is not.
const templateText = (valueNode) => {
  const node = valueNode?.type === 'JSXExpressionContainer' ? valueNode.expression : valueNode
  if (node?.type !== 'TemplateLiteral') return undefined
  const text = node.quasis.map((quasi) => quasi.value.cooked ?? '').join(' ')
  return text.trim() === '' ? undefined : text
}

// `channelExpression` is true when the quasi is followed by a `…Channel` member and a ` / <alpha>)` tail.
const colorFunctionIsLiteral = (text, channelExpression) => {
  for (const match of text.matchAll(colorFunction)) {
    const endsText = match.index + match[0].length === text.length
    if (!(channelExpression && endsText && channelFunction.test(match[0]))) return true
  }
  return false
}

// A plain member chain (`theme.vars.palette.primary.mainChannel`) that goes through `palette`
// and ends in a `…Channel` property.
const isPaletteChannelAccess = (expression) => {
  if (expression.type !== 'MemberExpression' || expression.computed) return false
  if (expression.property.type !== 'Identifier' || !expression.property.name.endsWith('Channel')) {
    return false
  }
  let node = expression.object
  while (node.type === 'MemberExpression') {
    if (!node.computed && node.property.type === 'Identifier' && node.property.name === 'palette') {
      return true
    }
    node = node.object
  }
  // A bare `palette` at the root (`palette.primary.mainChannel`).
  return node.type === 'Identifier' && node.name === 'palette'
}

const isFollowedByChannelToken = (quasi) => {
  const template = quasi.parent
  const index = template.quasis.indexOf(quasi)
  const expression = template.expressions?.[index]
  const next = template.quasis[index + 1]
  if (expression === undefined) return false
  return isPaletteChannelAccess(expression) && channelAlphaTail.test(next.value.cooked ?? '')
}

/** @type {import('eslint').Rule.RuleModule} */
export default {
  meta: {
    type: 'problem',
    schema: [],
    docs: {
      description: 'Disallow literal colours, shadows and font families outside the theme.',
    },
  },
  create(context) {
    const checkText = (node, text, channelExpression) => {
      if (hexColor.test(text) || colorFunctionIsLiteral(text, channelExpression)) {
        context.report({ node, message: colorMessage })
      }
      if (cssShadowOrFont.test(text)) {
        const message = text.includes('font-family') ? fontMessage : shadowMessage
        context.report({ node, message })
      }
    }

    const checkProperty = (node, valueNode) => {
      const name = propertyName(node)
      if (name === undefined) return
      const value = stringValue(valueNode) ?? templateText(valueNode)
      if (value === undefined) return
      const isString = stringValue(valueNode) !== undefined

      if (shadowProperties.has(name) && !(isString && value.trim() === 'none')) {
        context.report({ node: valueNode, message: shadowMessage })
      } else if (name === 'fontFamily' && !(isString && value.trim() === 'inherit')) {
        context.report({ node: valueNode, message: fontMessage })
      } else if (isColorProperty(name) && isNamedColorValue(value)) {
        context.report({ node: valueNode, message: colorMessage })
      }
    }

    return {
      Literal(node) {
        if (typeof node.value === 'string') checkText(node, node.value, false)
      },
      TemplateElement(node) {
        checkText(node, node.value.cooked ?? '', isFollowedByChannelToken(node))
      },
      Property(node) {
        checkProperty(node, node.value)
      },
      JSXAttribute(node) {
        checkProperty(node, node.value)
      },
    }
  },
}
