import { create } from 'zustand/react'

export type AuthStatus = 'loading' | 'authenticated' | 'unauthenticated'

export interface AuthUser {
  id: string
  email: string
  displayName: string
  role: 'Admin' | 'Teacher' | 'Parent'
}

interface AuthState {
  status: AuthStatus
  user: AuthUser | null
  setUser: (user: AuthUser) => void
  clearUser: () => void
}

export const useAuthStore = create<AuthState>((set) => ({
  status: 'loading',
  user: null,
  setUser: (user) => set({ user, status: 'authenticated' }),
  clearUser: () => set({ user: null, status: 'unauthenticated' }),
}))
