import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { ProtectedRoute } from '../router/guards/ProtectedRoute'
import { useAuthStore } from '../store/auth.store'
import type { AuthUser } from '../store/auth.store'

const mockUser: AuthUser = { id: '1', email: 'a@a.com', displayName: 'A', role: 'Admin' }

function makeRouter() {
  return createMemoryRouter(
    [
      {
        element: <ProtectedRoute />,
        children: [{ path: '/', element: <div>protected content</div> }],
      },
      { path: '/login', element: <div>login page</div> },
    ],
    { initialEntries: ['/'] }
  )
}

beforeEach(() => {
  useAuthStore.setState({ status: 'loading', user: null })
})

describe('ProtectedRoute', () => {
  it('renders spinner (not content, not redirect) when status=loading', () => {
    render(<RouterProvider router={makeRouter()} />)
    expect(screen.queryByText('protected content')).not.toBeInTheDocument()
    expect(screen.queryByText('login page')).not.toBeInTheDocument()
  })

  it('renders the outlet when authenticated', () => {
    useAuthStore.setState({ status: 'authenticated', user: mockUser })
    render(<RouterProvider router={makeRouter()} />)
    expect(screen.getByText('protected content')).toBeInTheDocument()
  })

  it('redirects to /login when unauthenticated', () => {
    useAuthStore.setState({ status: 'unauthenticated', user: null })
    render(<RouterProvider router={makeRouter()} />)
    expect(screen.getByText('login page')).toBeInTheDocument()
  })
})
