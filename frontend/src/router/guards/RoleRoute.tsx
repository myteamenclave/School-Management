import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import type { AuthUser } from '../../store/auth.store'
import { useAuthStore } from '../../store/auth.store'

interface RoleRouteProps {
  role: AuthUser['role']
  children: ReactNode
}

export function RoleRoute({ role, children }: RoleRouteProps) {
  const user = useAuthStore((s) => s.user)
  if (user?.role !== role) return <Navigate to="/dashboard" replace />
  return <>{children}</>
}
