import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { BookOpen, CalendarDays, GraduationCap, LayoutDashboard, Layers, LogOut } from 'lucide-react'
import { authApi } from '../api/auth'
import { useAuthStore } from '../store/auth.store'
import type { AuthUser } from '../store/auth.store'

interface NavItem {
  label: string
  to: string
  icon: React.ReactNode
  roles: AuthUser['role'][]
}

const NAV_ITEMS: NavItem[] = [
  {
    label: 'Dashboard',
    to: '/dashboard',
    icon: <LayoutDashboard size={18} />,
    roles: ['Admin', 'Teacher', 'Parent'],
  },
  {
    label: 'Academic Years',
    to: '/admin/academic-years',
    icon: <CalendarDays size={18} />,
    roles: ['Admin'],
  },
  {
    label: 'Grades & Sections',
    to: '/admin/grades',
    icon: <Layers size={18} />,
    roles: ['Admin'],
  },
  {
    label: 'Students',
    to: '/admin/students',
    icon: <GraduationCap size={18} />,
    roles: ['Admin'],
  },
]

export function AppShell() {
  const user = useAuthStore((s) => s.user)
  const clearUser = useAuthStore((s) => s.clearUser)
  const navigate = useNavigate()

  const visibleNav = NAV_ITEMS.filter((item) => user && item.roles.includes(user.role))

  const handleLogout = async () => {
    try {
      await authApi.logout()
    } finally {
      clearUser()
      navigate('/login', { replace: true })
    }
  }

  return (
    <div className="flex h-screen bg-background">
      {/* Sidebar */}
      <aside className="flex w-60 flex-shrink-0 flex-col bg-primary text-white">
        {/* Logo */}
        <div className="flex items-center gap-3 px-5 py-5 border-b border-white/10">
          <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-white/15 flex-shrink-0">
            <BookOpen size={18} />
          </div>
          <div>
            <div className="font-heading text-base font-700 leading-tight">SchoolMS</div>
            <div className="text-xs text-white/40">Management System</div>
          </div>
        </div>

        {/* Nav */}
        <nav className="flex-1 px-3 py-4 flex flex-col gap-1">
          {visibleNav.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                `flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm transition-colors ${
                  isActive
                    ? 'bg-white/15 text-white font-medium'
                    : 'text-white/70 hover:bg-white/10 hover:text-white'
                }`
              }
            >
              {item.icon}
              {item.label}
            </NavLink>
          ))}
        </nav>

        {/* Footer */}
        <div className="px-3 py-4 border-t border-white/10">
          <p className="px-3 text-xs text-white/25">Powered by Enclave</p>
        </div>
      </aside>

      {/* Main column */}
      <div className="flex flex-1 flex-col overflow-hidden">
        {/* Topbar */}
        <header className="flex h-14 items-center justify-between border-b border-border bg-card px-6">
          <div />
          <div className="flex items-center gap-4">
            <span className="text-sm font-medium text-foreground">{user?.displayName}</span>
            <button
              onClick={handleLogout}
              className="flex items-center gap-2 rounded-md px-3 py-1.5 text-sm text-muted-foreground hover:bg-muted hover:text-foreground transition-colors"
            >
              <LogOut size={15} />
              Sign out
            </button>
          </div>
        </header>

        {/* Page content */}
        <main className="flex-1 overflow-auto">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
