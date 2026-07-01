import { useAuthStore } from '../../store/auth.store'

export function DashboardPage() {
  const user = useAuthStore((s) => s.user)
  return (
    <div className="p-6">
      <h1 className="font-heading text-2xl font-semibold text-foreground">
        Welcome, {user?.displayName}
      </h1>
    </div>
  )
}
