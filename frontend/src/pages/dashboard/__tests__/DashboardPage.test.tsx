import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, it, expect, vi } from 'vitest'
import { DashboardPage } from '../DashboardPage'
import { useAuthStore } from '../../../store/auth.store'
import type { AuthUser } from '../../../store/auth.store'

// Isolate the dispatch decision — don't render the whole data-fetching overview.
vi.mock('../components/OverviewDashboard', () => ({
  OverviewDashboard: ({ displayName }: { displayName?: string }) => (
    <div>overview-dashboard for {displayName}</div>
  ),
}))

function setUser(user: AuthUser | null) {
  useAuthStore.setState({ user, status: user ? 'authenticated' : 'unauthenticated' })
}

const admin: AuthUser = { id: 'a1', email: 'admin@x.test', displayName: 'Demo Admin', role: 'Admin' }
const teacher: AuthUser = { id: 't1', email: 't@x.test', displayName: 'Tess Teacher', role: 'Teacher' }

function renderPage() {
  return render(
    <MemoryRouter>
      <DashboardPage />
    </MemoryRouter>
  )
}

describe('DashboardPage role dispatch', () => {
  it('renders the overview dashboard for Admin', () => {
    setUser(admin)
    renderPage()
    expect(screen.getByText('overview-dashboard for Demo Admin')).toBeInTheDocument()
  })

  it('renders the plain welcome for Teacher (no overview)', () => {
    setUser(teacher)
    renderPage()
    expect(screen.getByText('Welcome, Tess Teacher')).toBeInTheDocument()
    expect(screen.queryByText(/overview-dashboard/)).not.toBeInTheDocument()
  })
})
