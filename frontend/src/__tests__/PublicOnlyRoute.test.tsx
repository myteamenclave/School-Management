import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { PublicOnlyRoute } from '../router/guards/PublicOnlyRoute'
import { useAuthStore } from '../store/auth.store'
import type { AuthUser } from '../store/auth.store'

const mockUser: AuthUser = { id: '1', email: 'a@a.com', displayName: 'A', role: 'Admin' }

function makeRouter() {
  return createMemoryRouter(
    [
      {
        path: '/login',
        element: <PublicOnlyRoute><div>login page</div></PublicOnlyRoute>,
      },
      { path: '/dashboard', element: <div>dashboard</div> },
    ],
    { initialEntries: ['/login'] }
  )
}

beforeEach(() => {
  useAuthStore.setState({ status: 'loading', user: null })
})

describe('PublicOnlyRoute', () => {
  it('renders spinner when status=loading', () => {
    render(<RouterProvider router={makeRouter()} />)
    expect(screen.queryByText('login page')).not.toBeInTheDocument()
    expect(screen.queryByText('dashboard')).not.toBeInTheDocument()
  })

  it('renders children when unauthenticated', () => {
    useAuthStore.setState({ status: 'unauthenticated', user: null })
    render(<RouterProvider router={makeRouter()} />)
    expect(screen.getByText('login page')).toBeInTheDocument()
  })

  it('redirects to /dashboard when authenticated', () => {
    useAuthStore.setState({ status: 'authenticated', user: mockUser })
    render(<RouterProvider router={makeRouter()} />)
    expect(screen.getByText('dashboard')).toBeInTheDocument()
  })
})
