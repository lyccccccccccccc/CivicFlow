import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export function ProtectedRoute({ staff = false, role }: { staff?: boolean; role?: string }) {
  const { user } = useAuth()
  if (!user) return <Navigate to="/login" replace />
  if (staff && user.roles.includes('Resident')) return <Navigate to="/requests" replace />
  if (role && !user.roles.includes(role)) return <Navigate to="/home" replace />
  return <Outlet />
}
