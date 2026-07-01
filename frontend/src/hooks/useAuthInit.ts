import { useEffect } from 'react'
import { authApi } from '../api/auth'
import { useAuthStore } from '../store/auth.store'

export function useAuthInit() {
  const { setUser, clearUser } = useAuthStore()

  useEffect(() => {
    authApi
      .me()
      .then((res) => setUser(res.data))
      .catch(() => clearUser())
  }, []) // eslint-disable-line react-hooks/exhaustive-deps
}
