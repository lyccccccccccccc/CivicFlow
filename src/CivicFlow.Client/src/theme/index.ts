import { alpha, createTheme } from '@mui/material/styles'
import { civicTokens } from './tokens'

export const civicTheme = createTheme({
  palette: {
    mode: 'light',
    primary: { main: civicTokens.colors.brand, dark: civicTokens.colors.brandDark },
    secondary: { main: civicTokens.colors.warning },
    success: { main: civicTokens.colors.success },
    warning: { main: civicTokens.colors.warning },
    error: { main: civicTokens.colors.error },
    info: { main: civicTokens.colors.info },
    background: { default: civicTokens.colors.surfaceMuted, paper: civicTokens.colors.surface },
    text: { primary: civicTokens.colors.textPrimary, secondary: civicTokens.colors.textSecondary },
    divider: civicTokens.colors.border,
  },
  spacing: 4,
  shape: { borderRadius: civicTokens.radius.card },
  typography: {
    fontFamily: 'Inter, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
    h1: { fontSize: 'clamp(2.5rem, 6vw, 4.5rem)', lineHeight: 1.05, fontWeight: 850, letterSpacing: '-.045em' },
    h2: { fontSize: 'clamp(2rem, 4vw, 3rem)', lineHeight: 1.12, fontWeight: 825, letterSpacing: '-.035em' },
    h3: { fontSize: 'clamp(1.75rem, 3vw, 2.5rem)', lineHeight: 1.18, fontWeight: 800, letterSpacing: '-.025em' },
    h4: { fontSize: '1.75rem', lineHeight: 1.25, fontWeight: 800 },
    h5: { fontSize: '1.25rem', lineHeight: 1.35, fontWeight: 800 },
    h6: { fontSize: '1.0625rem', lineHeight: 1.4, fontWeight: 800 },
    body1: { lineHeight: 1.6 },
    body2: { lineHeight: 1.55 },
    button: { fontWeight: 750, textTransform: 'none' },
  },
  components: {
    MuiCssBaseline: { styleOverrides: { ':focus-visible': { outline: `3px solid ${alpha(civicTokens.colors.info, .7)}`, outlineOffset: 3 }, body: { minWidth: 320 } } },
    MuiButton: { defaultProps: { disableElevation: true }, styleOverrides: { root: { minHeight: 44, borderRadius: civicTokens.radius.form, paddingInline: 16 } } },
    MuiIconButton: { styleOverrides: { root: { minWidth: 44, minHeight: 44 } } },
    MuiTextField: { defaultProps: { variant: 'outlined' } },
    MuiOutlinedInput: { styleOverrides: { root: { borderRadius: civicTokens.radius.form } } },
    MuiPaper: { defaultProps: { elevation: 0 }, styleOverrides: { root: { border: `1px solid ${civicTokens.colors.border}` } } },
    MuiTableHead: { styleOverrides: { root: { background: '#eef3f6' } } },
    MuiLink: { styleOverrides: { root: { textUnderlineOffset: 3 } } },
  },
})

export { civicTokens }
