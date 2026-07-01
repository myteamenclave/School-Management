# Spec: Scaffold Frontend & Auth Layer

## Related docs & specs

- [.claude/context/architecture.md](../.claude/context/architecture.md) — tech stack choices (React 19, Vite, shadcn/ui + Tailwind, TanStack Query, Zustand, React Hook Form + Zod), authentication design (JWT-in-httpOnly-cookie, `/me` hydration pattern)
- [specs/02-implement-auth.md](02-implement-auth.md) — backend auth endpoints this spec consumes: `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`, `GET /api/auth/me`
- [docs/design-system.md](../docs/design-system.md) — color palette and typography; the login page and app shell must follow this
- [docs/ideas/school-management-system.md](../docs/ideas/school-management-system.md) — role definitions (Admin, Teacher, Parent) and per-role surface areas

## Objective

Scaffold the React 19 + Vite frontend from an empty directory, wire the full auth layer (login page, Zustand session store, Axios interceptor for silent token refresh, route guards), and produce the persistent app shell (sidebar + topbar) that all future admin/teacher/parent screens will live inside. No functional pages beyond the login page and a stub dashboard exist at the end of this spec — the deliverable is a solid foundation that every subsequent frontend spec builds on top of.

**Out of scope:** any admin, teacher, or parent page content beyond stub placeholders; the student CRUD UI; role-specific nav content (nav items are stubbed); Playwright E2E tests (first E2E tests will be written when the first functional page lands).

## Tech Stack

| Concern | Library | Notes |
|---|---|---|
| Framework | React 19 + TypeScript | Vite build tool (`react-ts` template) |
| UI components | shadcn/ui + Tailwind CSS | Initialized via `npx shadcn@latest init` |
| Routing | React Router v7 | Data router (`createBrowserRouter`) |
| Server state | TanStack Query v5 | `QueryClientProvider` at app root |
| Client state (auth) | Zustand v5 | Auth store only — no other Zustand stores in this spec |
| HTTP | Axios | Single shared instance with `withCredentials: true` and 401-refresh interceptor |
| Forms | React Hook Form v7 + Zod v3 | Login form only in this spec |
| Testing | Vitest + React Testing Library | Replaces Jest — Vitest is the standard Vite-native test runner |

**Why Vitest over Jest:** Vitest runs in the same Vite pipeline (no separate Babel/Jest transformer), shares `vite.config.ts`, and has a Jest-compatible API — no migration cost for test style. Jest's Node-based transformer is awkward with ESM packages (shadcn/ui, TanStack Query) that skip CommonJS builds.

## Commands

```bash
# Bootstrap (run from repo root)
npm create vite@latest frontend -- --template react-ts
cd frontend

# Core runtime dependencies
npm install react-router-dom @tanstack/react-query zustand axios react-hook-form @hookform/resolvers zod

# shadcn/ui (prompts for style/color — answer "New York" / "Zinc" to match design-system.md)
npx shadcn@latest init
# Components used in this spec:
npx shadcn@latest add button input label card form

# Dev dependencies
npm install -D vitest @vitest/coverage-v8 @testing-library/react @testing-library/user-event @testing-library/jest-dom jsdom

# Dev server (proxies /api to the backend)
npm run dev

# Build
npm run build

# Tests
npm run test

# Tests with coverage
npm run test:coverage
```

## Design

### Vite proxy configuration

`vite.config.ts` — proxies all `/api` requests to the local backend so the dev server (`:5173`) and the API (`:5000`) can run on different ports without triggering CORS:

```ts
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
  },
})
```

`target` must match the Kestrel port from `backend/SchoolMgmt.WebApi/Properties/launchSettings.json` (`applicationUrl`). Update if the backend port changes.

### Auth store (Zustand)

`src/store/auth.store.ts`

```ts
type AuthStatus = 'loading' | 'authenticated' | 'unauthenticated'

interface AuthUser {
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
  status: 'loading',   // initial — before /me resolves
  user: null,
  setUser: (user) => set({ user, status: 'authenticated' }),
  clearUser: () => set({ user: null, status: 'unauthenticated' }),
}))
```

**Why `'loading'` as the initial status:** the JWT lives in an httpOnly cookie — JS cannot read it. On page refresh, the app has no idea whether the user is authenticated until `GET /api/auth/me` resolves. Starting at `'unauthenticated'` causes a flash-redirect to `/login` on every page load before the `/me` request completes. `'loading'` lets `ProtectedRoute` show a spinner instead.

### Axios instance + interceptor

`src/api/axios.ts`

```ts
const api = axios.create({
  baseURL: '/api',
  withCredentials: true,  // send httpOnly cookies on every request
})

let isRefreshing = false
let refreshQueue: Array<() => void> = []

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as AxiosRequestConfig & { _retry?: boolean }

    if (error.response?.status !== 401 || originalRequest._retry) {
      return Promise.reject(error)
    }

    if (isRefreshing) {
      // Park this request until the in-flight refresh completes
      return new Promise((resolve) => {
        refreshQueue.push(() => resolve(api(originalRequest)))
      })
    }

    originalRequest._retry = true
    isRefreshing = true

    try {
      await api.post('/auth/refresh')
      refreshQueue.forEach((cb) => cb())
      refreshQueue = []
      return api(originalRequest)
    } catch {
      refreshQueue = []
      useAuthStore.getState().clearUser()
      window.location.href = '/login'
      return Promise.reject(error)
    } finally {
      isRefreshing = false
    }
  }
)

export default api
```

**The `isRefreshing` + `refreshQueue` pattern** prevents a refresh race: if three requests fire concurrently and all return 401, without this guard all three would call `/api/auth/refresh` in parallel — two would get 401 back (the first rotation consumes the one-time-use refresh token) and the interceptor would incorrectly signal theft. With the queue, the first 401 starts the refresh; the others park, then retry automatically once the new cookie is issued.

**`useAuthStore.getState()`** — the escape hatch for calling Zustand outside React components. React hooks (`useAuthStore()`) only work inside components; the interceptor is plain JS.

### Auth API calls

`src/api/auth.ts` — thin wrappers over the Axios instance:

```ts
export const authApi = {
  login:  (email: string, password: string) =>
    api.post<{ user: AuthUser }>('/auth/login', { email, password }),
  logout: () => api.post('/auth/logout'),
  me:     () => api.get<AuthUser>('/auth/me'),
}
```

### Session hydration

`src/hooks/useAuthInit.ts` — called once in `App.tsx` before any routes render:

```ts
export function useAuthInit() {
  const { setUser, clearUser } = useAuthStore()

  useEffect(() => {
    authApi.me()
      .then((res) => setUser(res.data))
      .catch(() => clearUser())
  }, [])
}
```

Calling this at the app root (not inside a route component) ensures it fires on every URL, including deep-links, not just on `/`. `ProtectedRoute` shows a spinner while `status === 'loading'`.

### Route structure

```
/login              ← PublicOnlyRoute (redirects to /dashboard if already authenticated)
/                   ← redirect to /dashboard
/* (catch-all)      ← ProtectedRoute → AppShell
  /dashboard        ← shared landing page (stub for now)
  /admin/*          ← RoleRoute role="Admin"
  /teacher/*        ← RoleRoute role="Teacher"
  /parent/*         ← RoleRoute role="Parent"
```

`src/router/index.tsx` — built with `createBrowserRouter`:

```tsx
export const router = createBrowserRouter([
  {
    path: '/login',
    element: <PublicOnlyRoute><LoginPage /></PublicOnlyRoute>,
  },
  { path: '/', element: <Navigate to="/dashboard" replace /> },
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppShell />,
        children: [
          { path: '/dashboard', element: <DashboardPage /> },
          { path: '/admin/*',   element: <RoleRoute role="Admin"><AdminRoutes /></RoleRoute> },
          { path: '/teacher/*', element: <RoleRoute role="Teacher"><TeacherRoutes /></RoleRoute> },
          { path: '/parent/*',  element: <RoleRoute role="Parent"><ParentRoutes /></RoleRoute> },
        ],
      },
    ],
  },
])
```

### Route guard components

**`ProtectedRoute`** — renders `<Outlet>` for authenticated users, spins while loading, redirects to `/login` if unauthenticated:

```tsx
export function ProtectedRoute() {
  const status = useAuthStore((s) => s.status)
  if (status === 'loading')        return <FullPageSpinner />
  if (status === 'unauthenticated') return <Navigate to="/login" replace />
  return <Outlet />
}
```

**`PublicOnlyRoute`** — wraps `/login`; prevents authenticated users from reaching the login page:

```tsx
export function PublicOnlyRoute({ children }: { children: ReactNode }) {
  const status = useAuthStore((s) => s.status)
  if (status === 'loading')       return <FullPageSpinner />
  if (status === 'authenticated') return <Navigate to="/dashboard" replace />
  return <>{children}</>
}
```

**`RoleRoute`** — nested inside `ProtectedRoute` (user is guaranteed authenticated); redirects to `/dashboard` (not `/login`) if the role doesn't match:

```tsx
export function RoleRoute({ role, children }: { role: AuthUser['role']; children: ReactNode }) {
  const user = useAuthStore((s) => s.user)
  if (user?.role !== role) return <Navigate to="/dashboard" replace />
  return <>{children}</>
}
```

### Login page

`src/pages/auth/LoginPage.tsx` — centered card, React Hook Form + Zod:

```ts
const loginSchema = z.object({
  email:    z.string().email('Enter a valid email'),
  password: z.string().min(1, 'Password is required'),
})
```

On submit: calls `authApi.login(email, password)`, then `setUser(res.data.user)`, then `navigate('/dashboard', { replace: true })`.

On 401: sets a **form-level** error `"Invalid email or password."` — no field-level errors that reveal whether the email or password is wrong (matches backend's no-user-enumeration behavior from spec #2).

Submit button is disabled while the request is in flight (prevent double-submit). Uses shadcn/ui `<Card>`, `<Form>`, `<Input>`, `<Button>` — no bespoke layout beyond what shadcn provides.

### App shell

`src/layouts/AppShell.tsx` — persistent layout wrapping all authenticated pages:

- **Sidebar** — school name at the top; nav items are data-driven (`NAV_ITEMS` array filtered by `user.role`), currently stubbed with a "Dashboard" link only. Adding a nav item for a future page is one array entry.
- **Topbar** — user display name on the right; logout button calls `authApi.logout()`, then `clearUser()`, then `navigate('/login', { replace: true })`.
- **Main content area** — `<Outlet>` renders the active page.

## Project Structure

```
frontend/
  src/
    api/
      axios.ts            # Axios instance + 401-refresh interceptor
      auth.ts             # authApi: login, logout, me
    store/
      auth.store.ts       # Zustand: status, user, setUser, clearUser
    router/
      index.tsx           # createBrowserRouter config
      guards/
        ProtectedRoute.tsx
        PublicOnlyRoute.tsx
        RoleRoute.tsx
    layouts/
      AppShell.tsx        # sidebar + topbar + <Outlet>
    pages/
      auth/
        LoginPage.tsx
      dashboard/
        DashboardPage.tsx     # stub — "Welcome, {displayName}"
      admin/
        index.tsx             # AdminRoutes — stub <Outlet>
      teacher/
        index.tsx             # TeacherRoutes — stub <Outlet>
      parent/
        index.tsx             # ParentRoutes — stub <Outlet>
    components/
      shared/
        FullPageSpinner.tsx
    hooks/
      useAuthInit.ts      # calls /me on mount, hydrates auth store
    lib/
      utils.ts            # shadcn/ui cn() utility (generated by init)
    test/
      setup.ts            # import '@testing-library/jest-dom'
    App.tsx               # QueryClientProvider + RouterProvider + useAuthInit
    main.tsx              # ReactDOM.createRoot
  vite.config.ts
  tailwind.config.ts
  tsconfig.json
  index.html
  package.json
```

## Testing Strategy

Per [.claude/context/architecture.md](../.claude/context/architecture.md):

**Unit (Vitest + React Testing Library):**

- `useAuthStore` — `setUser` transitions `status` to `authenticated` and stores the user; `clearUser` resets to `status: unauthenticated, user: null`
- `ProtectedRoute` — renders spinner when `status=loading`; renders outlet when `authenticated`; redirects to `/login` when `unauthenticated`
- `PublicOnlyRoute` — renders children when `unauthenticated`; redirects to `/dashboard` when `authenticated`; renders spinner when `loading`
- `RoleRoute` — renders children when the user's role matches; redirects to `/dashboard` when it doesn't
- `LoginPage` — renders email and password fields; calls `authApi.login` on valid submit; shows `"Invalid email or password."` on a 401 response; disables the submit button while the request is in flight

All unit tests mock `authApi` (Vitest `vi.mock`) — they never make real HTTP calls.

**No E2E tests in this spec** — Playwright E2E will be authored when the first functional end-to-end flow exists (e.g. Admin creates a student). Wiring Playwright on a login page and stubs doesn't add meaningful coverage yet.

## Boundaries

**Always:**
- Set `withCredentials: true` on the Axios instance — without it, browsers don't send httpOnly cookies on cross-origin requests in dev
- Start the auth store at `status: 'loading'` — never `'unauthenticated'` — to prevent the login-flash on page refresh before `/me` resolves
- Use the `isRefreshing` + `refreshQueue` guard in the interceptor — concurrent 401s without it race to call `/api/auth/refresh` multiple times, consuming the one-time-use refresh token and triggering theft detection on the backend
- Follow [docs/design-system.md](../docs/design-system.md) for all colors, fonts, and spacing on the login page and app shell
- Route all API calls through the shared Axios instance — never use `fetch` directly in components

**Ask first:**
- Changing the backend port the Vite proxy targets (must coordinate with whoever starts the backend locally)
- Adding a "remember me" / persistent session toggle to the login form (affects refresh token TTL — a backend concern)
- Switching from React Router v7 `createBrowserRouter` to file-based routing

**Never:**
- Store the JWT, user id, or any auth token in `localStorage` or `sessionStorage` — cookies are httpOnly for XSS protection; JS storage is not
- Attempt to read or decode the `access_token` cookie from JavaScript — it is intentionally inaccessible to JS
- Call `/api/auth/refresh` from a component directly — only the Axios interceptor does this; components that receive 401s let the interceptor handle the retry transparently
- Call `useAuthStore()` (the React hook) inside the Axios interceptor — hooks only work inside React components; use `useAuthStore.getState()` instead

## Success Criteria

- `npm run dev` starts the frontend; navigating to `http://localhost:5173/login` renders the login form with no console errors
- Logging in with `admin@demoschool.test` / `Passw0rd!` sets the auth store, redirects to `/dashboard`, and shows the app shell with the user's display name in the topbar
- Refreshing the browser on any authenticated route rehydrates the session via `/api/auth/me` and does not redirect to `/login`
- Logging out calls `POST /api/auth/logout`, clears the auth store, and redirects to `/login`; navigating back is blocked by `ProtectedRoute`
- Visiting `/login` while already authenticated redirects to `/dashboard` (`PublicOnlyRoute`)
- Visiting `/admin/*` while authenticated as a Teacher redirects to `/dashboard` (`RoleRoute`)
- Wrong credentials show a single form-level `"Invalid email or password."` error — no field-level discrimination
- All unit tests listed above pass (`npm run test`)
- `npm run build` succeeds with no TypeScript errors
