import api from './axios'
import type { AuthUser } from '../store/auth.store'

export const authApi = {
  login: (email: string, password: string) =>
    api.post<{ user: AuthUser }>('/auth/login', { email, password }),
  logout: () => api.post('/auth/logout'),
  me: () => api.get<AuthUser>('/auth/me', { headers: { 'Cache-Control': 'no-cache', Pragma: 'no-cache' } }),
}
