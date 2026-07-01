import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { RoleRoute } from '../router/guards/RoleRoute'
import { useAuthStore } from '../store/auth.store'
import type { AuthUser } from '../store/auth.store'

const adminUser: AuthUser = { id: '1', email: 'a@a.com', displayName: 'A', role: 'Admin' }
const teacherUser: AuthUser = { id: '2', email: 'b@b.com', displayName: 'B', role: 'Teacher' }

function makeRouter(user: AuthUser | null) {
  useAuthStore.setState({ status: 'authenticated', user })
  return createMemoryRouter(
    [
      {
        path: '/admin',
        element: <RoleRoute role="Admin"><div>admin content</div></RoleRoute>,
      },
      { path: '/dashboard', element: <div>dashboard</div> },
    ],
    { initialEntries: ['/admin'] }
  )
}

describe('RoleRoute', () => {
  it('renders children when user role matches', () => {
    render(<RouterProvider router={makeRouter(adminUser)} />)
    expect(screen.getByText('admin content')).toBeInTheDocument()
  })

  it('redirects to /dashboard when role does not match', () => {
    render(<RouterProvider router={makeRouter(teacherUser)} />)
    expect(screen.getByText('dashboard')).toBeInTheDocument()
  })

  it('redirects to /dashboard when user is null', () => {
    render(<RouterProvider router={makeRouter(null)} />)
    expect(screen.getByText('dashboard')).toBeInTheDocument()
  })
})
