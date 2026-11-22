import { createTheme, enhanceHighContrast } from '@mui/material/styles'
import { brandedComponents } from './branded-components'
import { brandedTokens } from './branded-tokens'

const baseTheme = createTheme({ ...brandedTokens, components: brandedComponents })

// Composed in a second step because the sizes below 600px need the theme's breakpoints and
// `pxToRem`, which the options don't have yet.
// `enhanceHighContrast` goes last: it adds MUI's forced-colours rules on top of everything above.
export const theme = enhanceHighContrast(
  createTheme(baseTheme, {
    typography: {
      h1: { [baseTheme.breakpoints.down('sm')]: { fontSize: baseTheme.typography.pxToRem(26) } },
      hero: { [baseTheme.breakpoints.down('sm')]: { fontSize: baseTheme.typography.pxToRem(44) } },
    },
  }),
)
