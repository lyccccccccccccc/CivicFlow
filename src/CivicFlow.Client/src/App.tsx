import { lazy, type ReactNode } from 'react'
import { CssBaseline, ThemeProvider } from '@mui/material'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth/AuthContext'
import { AppShell } from './components/AppShell'
import { LazyRouteBoundary } from './components/LazyRouteBoundary'
import { ProtectedRoute } from './components/ProtectedRoute'
import { civicTheme } from './theme'

const LandingPage = lazy(() => import('./pages/LandingPage').then(module => ({ default: module.LandingPage })))
const LoginPage = lazy(() => import('./pages/AuthPages').then(module => ({ default: module.LoginPage })))
const RegisterPage = lazy(() => import('./pages/AuthPages').then(module => ({ default: module.RegisterPage })))
const CasesPage = lazy(() => import('./pages/CasesPage').then(module => ({ default: module.CasesPage })))
const CaseDetailPage = lazy(() => import('./pages/CaseDetailPage').then(module => ({ default: module.CaseDetailPage })))
const NewRequestPage = lazy(() => import('./pages/NewRequestPage').then(module => ({ default: module.NewRequestPage })))
const NotificationsPage = lazy(() => import('./pages/NotificationsPage').then(module => ({ default: module.NotificationsPage })))
const DashboardPage = lazy(() => import('./pages/DashboardPage').then(module => ({ default: module.DashboardPage })))
const AdminPage = lazy(() => import('./pages/AdminPage').then(module => ({ default: module.AdminPage })))
const AuditLogPage = lazy(() => import('./pages/AuditLogPage').then(module => ({ default: module.AuditLogPage })))
const ProfilePage = lazy(() => import('./pages/ProfilePage').then(module => ({ default: module.ProfilePage })))

function HomeRedirect() {
  const { user } = useAuth()
  if (!user) return <Navigate to="/" replace />
  return <Navigate to={user.roles.includes('Resident') ? '/requests' : '/cases'} replace />
}

const lazyPage = (page: ReactNode) => <LazyRouteBoundary>{page}</LazyRouteBoundary>

export default function App() {
  return <ThemeProvider theme={civicTheme}><CssBaseline /><BrowserRouter><AuthProvider><Routes>
    <Route element={<AppShell />}><Route index element={lazyPage(<LandingPage />)} /><Route path="login" element={lazyPage(<LoginPage />)} /><Route path="register" element={lazyPage(<RegisterPage />)} />
      <Route element={<ProtectedRoute />}><Route path="home" element={<HomeRedirect />} /><Route path="profile" element={lazyPage(<ProfilePage />)} /><Route path="requests" element={lazyPage(<CasesPage />)} /><Route path="cases/:id" element={lazyPage(<CaseDetailPage />)} /><Route path="requests/new" element={lazyPage(<NewRequestPage />)} /><Route path="notifications" element={lazyPage(<NotificationsPage />)} /></Route>
      <Route element={<ProtectedRoute staff />}><Route path="cases" element={lazyPage(<CasesPage />)} /><Route path="dashboard" element={lazyPage(<DashboardPage />)} /></Route>
      <Route element={<ProtectedRoute role="SystemAdministrator" />}><Route path="admin" element={lazyPage(<AdminPage />)} /></Route>
      <Route element={<ProtectedRoute managers />}><Route path="admin/audit-log" element={lazyPage(<AuditLogPage />)} /></Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Route>
  </Routes></AuthProvider></BrowserRouter></ThemeProvider>
}
