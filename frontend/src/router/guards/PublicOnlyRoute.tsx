import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import { useAuthStore } from '../../store/auth.store'
import { FullPageSpinner } from '../../components/shared/FullPageSpinner'

export function PublicOnlyRoute({ children }: { children: ReactNode }) {
  const status = useAuthStore((s) => s.status)
  if (status === 'loading') return <FullPageSpinner />
  if (status === 'authenticated') return <Navigate to="/dashboard" replace />
  return <>{children}</>
}
