import { buttonClasses } from '@mui/material/Button'
import type { ThemeOptions } from '@mui/material/styles'

// Only the primary colour gets the gradient and the outline look; other colours (errors above all)
// keep MUI's. The `&&` raises the specificity above MUI's own hover, focus and active shadows.
export const buttonTheme: ThemeOptions['components'] = {
  MuiButton: {
    styleOverrides: {
      root: ({ theme }) => ({
        borderRadius: theme.vars.shape.radius.button,
        variants: [
          { props: { size: 'medium' }, style: { minHeight: theme.spacing(6) } },
          { props: { size: 'large' }, style: { minHeight: theme.spacing(6) } },
          {
            props: { variant: 'contained', color: 'primary' },
            style: {
              backgroundImage: theme.vars.gradient.action,
              '--variant-containedColor': theme.vars.palette.common.white,
              [`&&:not(.${buttonClasses.disabled})`]: { boxShadow: theme.vars.glow.halo },
              [`&.${buttonClasses.disabled}`]: { backgroundImage: 'none', boxShadow: 'none' },
              '@media (forced-colors: active)': {
                backgroundImage: 'none',
                boxShadow: 'none',
                border: '1px solid ButtonBorder',
                [`&&:not(.${buttonClasses.disabled})`]: { boxShadow: 'none' },
              },
            },
          },
          {
            props: { variant: 'outlined', color: 'primary' },
            style: {
              borderWidth: 1.5,
              '--variant-outlinedColor': theme.vars.palette.text.primary,
              '--variant-outlinedBorder': theme.vars.palette.fieldOutline,
              '--variant-outlinedBg': 'transparent',
            },
          },
        ],
      }),
    },
  },
}
