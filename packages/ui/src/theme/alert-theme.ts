import CheckCircleOutlineRoundedIcon from '@mui/icons-material/CheckCircleOutlineRounded'
import ErrorOutlineRoundedIcon from '@mui/icons-material/ErrorOutlineRounded'
import InfoOutlineRoundedIcon from '@mui/icons-material/InfoOutlineRounded'
import WarningAmberRoundedIcon from '@mui/icons-material/WarningAmberRounded'
import { alertClasses } from '@mui/material/Alert'
import type { ThemeOptions } from '@mui/material/styles'
import { createElement } from 'react'

// MUI's default Alert icons are outlined, not Rounded. These are the Rounded outline twins, sized
// like the defaults (`fontSize="inherit"`).
//
// A standard Alert is style.md's status look: `text.primary` text on the severity's tint, with the
// severity colour on the icon only. Info has no tint, so it sits on `surface2` with a
// `primary.main` icon. MUI's own severity variants set the same properties first; these come after
// them in the same rule, so they win.
export const alertTheme: ThemeOptions['components'] = {
  MuiAlert: {
    defaultProps: {
      iconMapping: {
        success: createElement(CheckCircleOutlineRoundedIcon, { fontSize: 'inherit' }),
        info: createElement(InfoOutlineRoundedIcon, { fontSize: 'inherit' }),
        warning: createElement(WarningAmberRoundedIcon, { fontSize: 'inherit' }),
        error: createElement(ErrorOutlineRoundedIcon, { fontSize: 'inherit' }),
      },
    },
    styleOverrides: {
      root: ({ theme }) => {
        const tinted = (severity: 'success' | 'error' | 'warning') => ({
          props: { severity, variant: 'standard' as const },
          style: {
            backgroundColor: theme.vars.palette[severity].tint,
            [`& .${alertClasses.icon}`]: { color: theme.vars.palette[severity].main },
          },
        })

        return {
          borderRadius: theme.vars.shape.radius.panel,
          '@media (forced-colors: active)': { border: '1px solid ButtonBorder' },
          variants: [
            // Only the standard look has a light surface; MUI's filled variant keeps its own text colour.
            { props: { variant: 'standard' }, style: { color: theme.vars.palette.text.primary } },
            tinted('success'),
            tinted('error'),
            tinted('warning'),
            {
              props: { severity: 'info', variant: 'standard' },
              style: {
                backgroundColor: theme.vars.palette.surface2,
                [`& .${alertClasses.icon}`]: { color: theme.vars.palette.primary.main },
              },
            },
          ],
        }
      },
    },
  },
}
