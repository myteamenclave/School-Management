import { useAuthStore } from '../../store/auth.store'
import { OverviewDashboard } from './components/OverviewDashboard'

export function DashboardPage() {
  const user = useAuthStore((s) => s.user)

  // Admin landing screen is the overview dashboard. Teacher/Parent keep the
  // simple welcome until their own dashboards exist (spec 16 — Admin-only).
  if (user?.role === 'Admin') {
    return <OverviewDashboard displayName={user.displayName} />
  }

  return (
    <div className="p-6">
      <h1 className="font-heading text-2xl font-semibold text-foreground">
        Welcome, {user?.displayName}
      </h1>
    </div>
  )
}
