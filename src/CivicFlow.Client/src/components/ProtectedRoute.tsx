import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export function ProtectedRoute({ staff = false, role, managers = false }: { staff?: boolean; role?: string; managers?: boolean }) {
  const { user } = useAuth()
  if (!user) return <Navigate to="/login" replace />
  if (staff && user.roles.includes('Resident')) return <Navigate to="/requests" replace />
  if (role && !user.roles.includes(role)) return <Navigate to="/home" replace />
  if (managers && !user.roles.some(x => ['TeamManager', 'SystemAdministrator'].includes(x))) return <Navigate to="/home" replace />
  return <Outlet />
}
