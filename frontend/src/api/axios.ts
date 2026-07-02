import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { useAuthStore } from '../store/auth.store'

const api = axios.create({
  baseURL: '/api',
  withCredentials: true,
})

// Separate instance with no interceptors — used only for the token
// refresh call so a 401 from /auth/refresh doesn't re-enter the retry
// logic and cause the queue to hang indefinitely.
const refreshApi = axios.create({
  baseURL: '/api',
  withCredentials: true,
})

let isRefreshing = false
let refreshQueue: Array<() => void> = []

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean }

    if (error.response?.status !== 401 || originalRequest._retry) {
      return Promise.reject(error)
    }

    if (isRefreshing) {
      return new Promise((resolve) => {
        refreshQueue.push(() => resolve(api(originalRequest)))
      })
    }

    originalRequest._retry = true
    isRefreshing = true

    try {
      await refreshApi.post('/auth/refresh')
      refreshQueue.forEach((cb) => cb())
      refreshQueue = []
      return api(originalRequest)
    } catch {
      refreshQueue = []
      useAuthStore.getState().clearUser()
      return Promise.reject(error)
    } finally {
      isRefreshing = false
    }
  }
)

export default api
