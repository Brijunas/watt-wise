import { createTheme } from '@mui/material/styles'
import { colorSchemeAttribute } from './color-scheme'

declare module '@mui/material/styles' {
  interface CssThemeVariables {
    enabled: true
  }
}

export const theme = createTheme({
  cssVariables: { colorSchemeSelector: colorSchemeAttribute },
  colorSchemes: {
    light: {
      palette: {
        primary: { main: '#5235D8' },
        secondary: { main: '#3A4252' },
        success: { main: '#1F7A2E' },
        error: { main: '#C62828' },
        warning: { main: '#A84B00' },
        info: { main: '#0369A1' },
        background: { default: '#F8F7FD', paper: '#FFFFFF' },
        text: { primary: '#0D0E14', secondary: '#4A4D5C' },
        divider: '#DEDBEE',
      },
    },
    dark: {
      palette: {
        primary: { main: '#B0A6FF' },
        secondary: { main: '#D5DAE5' },
        success: { main: '#6FD67A' },
        error: { main: '#FF6B63' },
        warning: { main: '#FFB74D' },
        info: { main: '#4FC3F7' },
        background: { default: '#0D0C15', paper: '#171521' },
        text: { primary: '#F5F6FA', secondary: '#B4B8C8' },
        divider: '#2F2C42',
      },
    },
  },
  typography: {
    fontFamily: "'Roboto Variable', 'Roboto', 'Helvetica', 'Arial', sans-serif",
  },
})
