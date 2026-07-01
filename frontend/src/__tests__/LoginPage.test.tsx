import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { vi } from 'vitest'
import { LoginPage } from '../pages/auth/LoginPage'
import { authApi } from '../api/auth'
import { useAuthStore } from '../store/auth.store'

vi.mock('../api/auth', () => ({
  authApi: {
    login: vi.fn(),
    logout: vi.fn(),
    me: vi.fn(),
  },
}))

const mockLogin = vi.mocked(authApi.login)

const mockUser = { id: '1', email: 'admin@test.com', displayName: 'Admin User', role: 'Admin' as const }

function makeRouter() {
  return createMemoryRouter(
    [
      { path: '/login', element: <LoginPage /> },
      { path: '/dashboard', element: <div>dashboard</div> },
    ],
    { initialEntries: ['/login'] }
  )
}

beforeEach(() => {
  vi.clearAllMocks()
  useAuthStore.setState({ status: 'unauthenticated', user: null })
})

describe('LoginPage', () => {
  it('renders email and password fields', () => {
    render(<RouterProvider router={makeRouter()} />)
    expect(screen.getByLabelText('Email address')).toBeInTheDocument()
    expect(screen.getByLabelText('Password')).toBeInTheDocument()
  })

  it('calls authApi.login with email and password on valid submit', async () => {
    mockLogin.mockResolvedValueOnce({ data: { user: mockUser } } as any)
    const user = userEvent.setup()

    render(<RouterProvider router={makeRouter()} />)

    await user.type(screen.getByLabelText('Email address'), 'admin@test.com')
    await user.type(screen.getByLabelText('Password'), 'Passw0rd!')
    await user.click(screen.getByRole('button', { name: 'Sign In' }))

    await waitFor(() => {
      expect(mockLogin).toHaveBeenCalledWith('admin@test.com', 'Passw0rd!')
    })
  })

  it('shows "Invalid email or password." on 401 response', async () => {
    mockLogin.mockRejectedValueOnce({ response: { status: 401 } })
    const user = userEvent.setup()

    render(<RouterProvider router={makeRouter()} />)

    await user.type(screen.getByLabelText('Email address'), 'wrong@test.com')
    await user.type(screen.getByLabelText('Password'), 'wrongpass')
    await user.click(screen.getByRole('button', { name: 'Sign In' }))

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Invalid email or password.')
    })
  })

  it('disables the submit button while the request is in flight', async () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    let resolve: (v: any) => void
    mockLogin.mockReturnValueOnce(new Promise((r) => { resolve = r }))
    const user = userEvent.setup()

    render(<RouterProvider router={makeRouter()} />)

    await user.type(screen.getByLabelText('Email address'), 'admin@test.com')
    await user.type(screen.getByLabelText('Password'), 'Passw0rd!')
    await user.click(screen.getByRole('button', { name: 'Sign In' }))

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /signing in/i })).toBeDisabled()
    })

    resolve!({ data: { user: mockUser } })
  })
})
