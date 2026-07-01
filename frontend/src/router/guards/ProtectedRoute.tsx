import { Navigate, Outlet } from 'react-router-dom'
import { useAuthStore } from '../../store/auth.store'
import { FullPageSpinner } from '../../components/shared/FullPageSpinner'

export function ProtectedRoute() {
  const status = useAuthStore((s) => s.status)
  if (status === 'loading') return <FullPageSpinner />
  if (status === 'unauthenticated') return <Navigate to="/login" replace />
  return <Outlet />
}
