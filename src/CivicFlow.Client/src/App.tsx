import { CssBaseline, ThemeProvider, createTheme } from '@mui/material'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth/AuthContext'
import { AppShell } from './components/AppShell'
import { ProtectedRoute } from './components/ProtectedRoute'
import { LoginPage, RegisterPage } from './pages/AuthPages'
import { CaseDetailPage } from './pages/CaseDetailPage'
import { CasesPage } from './pages/CasesPage'
import { DashboardPage } from './pages/DashboardPage'
import { LandingPage } from './pages/LandingPage'
import { NewRequestPage } from './pages/NewRequestPage'
import { AdminPage } from './pages/AdminPage'
import { AuditLogPage } from './pages/AuditLogPage'
import { NotificationsPage } from './pages/NotificationsPage'

const theme = createTheme({
  palette: { mode: 'light', primary: { main: '#075985', dark: '#0c4a6e' }, secondary: { main: '#d97706' }, background: { default: '#f3f6f8', paper: '#fff' } },
  typography: { fontFamily: 'Inter, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif', h1: { fontWeight: 850, letterSpacing: '-.045em' }, h3: { fontWeight: 800, letterSpacing: '-.025em' } },
  shape: { borderRadius: 12 },
  components: { MuiButton: { styleOverrides: { root: { textTransform: 'none', fontWeight: 750, borderRadius: 8 } } }, MuiPaper: { defaultProps: { elevation: 0 }, styleOverrides: { root: { border: '1px solid #dbe3e8' } } }, MuiTableHead: { styleOverrides: { root: { background: '#eef3f6' } } } },
})

function HomeRedirect() {
  const { user } = useAuth()
  if (!user) return <Navigate to="/" replace />
  return <Navigate to={user.roles.includes('Resident') ? '/requests' : '/cases'} replace />
}

export default function App() {
  return <ThemeProvider theme={theme}><CssBaseline /><BrowserRouter><AuthProvider><Routes>
    <Route element={<AppShell />}><Route index element={<LandingPage />} /><Route path="login" element={<LoginPage />} /><Route path="register" element={<RegisterPage />} />
      <Route element={<ProtectedRoute />}><Route path="home" element={<HomeRedirect />} /><Route path="requests" element={<CasesPage />} /><Route path="cases/:id" element={<CaseDetailPage />} /><Route path="requests/new" element={<NewRequestPage />} /><Route path="notifications" element={<NotificationsPage />} /></Route>
      <Route element={<ProtectedRoute staff />}><Route path="cases" element={<CasesPage />} /><Route path="dashboard" element={<DashboardPage />} /></Route>
      <Route element={<ProtectedRoute role="SystemAdministrator" />}><Route path="admin" element={<AdminPage />} /><Route path="admin/audit-log" element={<AuditLogPage />} /></Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Route>
  </Routes></AuthProvider></BrowserRouter></ThemeProvider>
}
