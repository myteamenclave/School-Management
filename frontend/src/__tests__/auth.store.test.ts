import { useAuthStore } from '../store/auth.store'
import type { AuthUser } from '../store/auth.store'

const mockUser: AuthUser = {
  id: '1',
  email: 'admin@test.com',
  displayName: 'Admin User',
  role: 'Admin',
}

beforeEach(() => {
  useAuthStore.setState({ status: 'loading', user: null })
})

describe('useAuthStore', () => {
  it('starts with status loading and no user', () => {
    const state = useAuthStore.getState()
    expect(state.status).toBe('loading')
    expect(state.user).toBeNull()
  })

  it('setUser transitions status to authenticated and stores user', () => {
    useAuthStore.getState().setUser(mockUser)
    const state = useAuthStore.getState()
    expect(state.status).toBe('authenticated')
    expect(state.user).toEqual(mockUser)
  })

  it('clearUser resets to unauthenticated with no user', () => {
    useAuthStore.getState().setUser(mockUser)
    useAuthStore.getState().clearUser()
    const state = useAuthStore.getState()
    expect(state.status).toBe('unauthenticated')
    expect(state.user).toBeNull()
  })
})
