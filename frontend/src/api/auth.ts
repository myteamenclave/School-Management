import api from './axios'
import type { AuthUser } from '../store/auth.store'

export const authApi = {
  login: (email: string, password: string, rememberMe = false) =>
    api.post<{ user: AuthUser }>('/auth/login', { email, password, rememberMe }),
  logout: () => api.post('/auth/logout'),
  me: () => api.get<AuthUser>('/auth/me', { headers: { 'Cache-Control': 'no-cache', Pragma: 'no-cache' } }),
}
