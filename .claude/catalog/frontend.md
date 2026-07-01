<!-- Last verified: 2026-07-01. Update this file whenever a new public type/function/component is added or removed from the frontend. Check here before adding new code — don't duplicate something that already exists. -->

# Frontend Catalog

## Auth Store (`src/store/auth.store.ts`)

| Export | Purpose |
|---|---|
| `AuthStatus` | `'loading' \| 'authenticated' \| 'unauthenticated'` — the three auth states |
| `AuthUser` | Shape of the authenticated user: `id`, `email`, `displayName`, `role` ('Admin'\|'Teacher'\|'Parent') |
| `useAuthStore` | Zustand store. `status`, `user`, `setUser(user)`, `clearUser()`. Starts at `'loading'` so ProtectedRoute shows a spinner (not a redirect) before `/me` resolves. Use `useAuthStore.getState()` in non-React code (e.g. Axios interceptor). |

## API (`src/api/`)

| Export | Location | Purpose |
|---|---|---|
| `api` (default) | `axios.ts` | Shared Axios instance (`baseURL: '/api'`, `withCredentials: true`). Includes a 401-refresh interceptor with `isRefreshing` + `refreshQueue` guard to prevent concurrent refresh race on one-time-use tokens. On unrecoverable 401: clears auth store and redirects to `/login`. |
| `authApi` | `auth.ts` | Thin wrappers: `login(email, password)`, `logout()`, `me()`. All go through the shared Axios instance — never `fetch` directly. |

## Hooks (`src/hooks/`)

| Export | Location | Purpose |
|---|---|---|
| `useAuthInit` | `useAuthInit.ts` | Called once in `App.tsx` (in `AppInner`, above `RouterProvider`). Calls `GET /api/auth/me` on mount and hydrates the auth store. On failure, calls `clearUser()`. Ensures session rehydration on every URL including deep-links. |

## Route Guards (`src/router/guards/`)

| Component | Purpose |
|---|---|
| `ProtectedRoute` | Renders `<Outlet>` when authenticated; `<FullPageSpinner>` when loading; redirects to `/login` when unauthenticated. Wraps all authenticated routes. |
| `PublicOnlyRoute` | Wraps `/login`. Redirects to `/dashboard` when already authenticated; spins while loading; renders children when unauthenticated. Prevents authenticated users from re-visiting the login page. |
| `RoleRoute` | Wraps role-specific sub-trees (`/admin/*`, `/teacher/*`, `/parent/*`). Nested inside `ProtectedRoute` (user is guaranteed non-null). Redirects to `/dashboard` (not `/login`) when role doesn't match. |

## Router (`src/router/index.tsx`)

| Export | Purpose |
|---|---|
| `router` | `createBrowserRouter` config. `/login` → `PublicOnlyRoute > LoginPage`. `/` → redirect to `/dashboard`. All authenticated routes nested under `ProtectedRoute > AppShell`. Role sub-trees further guarded by `RoleRoute`. |

## Layouts (`src/layouts/`)

| Component | Purpose |
|---|---|
| `AppShell` | Persistent authenticated shell. Left sidebar: navy `bg-primary`, `SchoolMS` logo, data-driven `NAV_ITEMS` filtered by `user.role` (currently only Dashboard for all roles). Right column: topbar with user display name + logout button; `<Outlet>` for page content. Logout calls `authApi.logout()` + `clearUser()` + `navigate('/login')`. |

## Pages (`src/pages/`)

| Component | Location | Purpose |
|---|---|---|
| `LoginPage` | `auth/LoginPage.tsx` | Split-panel login UI matching the "Login Page - Final" design. Left: navy brand panel with marketing copy and feature items. Right: white panel with RHF+Zod form (email + password + remember-me checkbox). Shows `"Invalid email or password."` form-level error on 401; disables submit while in flight. On success: calls `setUser` then navigates to `/dashboard`. |
| `DashboardPage` | `dashboard/DashboardPage.tsx` | Stub — "Welcome, {displayName}" heading. All future role-specific dashboards live here. |
| `AdminRoutes` | `admin/index.tsx` | Stub `<Outlet>` for future `/admin/*` pages. |
| `TeacherRoutes` | `teacher/index.tsx` | Stub `<Outlet>` for future `/teacher/*` pages. |
| `ParentRoutes` | `parent/index.tsx` | Stub `<Outlet>` for future `/parent/*` pages. |

## Shared Components (`src/components/`)

| Component | Location | Purpose |
|---|---|---|
| `FullPageSpinner` | `shared/FullPageSpinner.tsx` | Full-viewport centered spinner. Shown by `ProtectedRoute` and `PublicOnlyRoute` while `status === 'loading'`. |
| `Button` | `ui/button.tsx` | shadcn-compatible button with CVA variants: `default` (navy primary), `destructive`, `outline`, `secondary`, `ghost`, `link`. Sizes: `default`, `sm`, `lg`, `icon`. Supports `asChild` via Radix Slot. |
| `Input` | `ui/input.tsx` | shadcn-compatible text input. `h-11`, `border-border`, `rounded-lg`, focus ring via `focus-visible:ring-ring`. |
| `Label` | `ui/label.tsx` | Radix `LabelPrimitive.Root` wrapper. Accessible — associates with input via `htmlFor`. |
| `Form` / `FormField` / `FormItem` / `FormLabel` / `FormControl` / `FormMessage` | `ui/form.tsx` | shadcn-compatible RHF form primitives. `FormControl` uses Radix `Slot` to forward `id` and `aria-*` to the wrapped input. Not currently used in `LoginPage` (which uses `register` directly) — available for future multi-field forms. |

## Utilities (`src/lib/`)

| Export | Location | Purpose |
|---|---|---|
| `cn` | `utils.ts` | `clsx` + `tailwind-merge` combiner. Use for all conditional className merging. |

## App Entry (`src/`)

| File | Purpose |
|---|---|
| `App.tsx` | `QueryClientProvider` wraps `AppInner`. `AppInner` calls `useAuthInit()` then renders `RouterProvider`. Split so `useAuthInit` runs inside the Query context but above the router. |
| `main.tsx` | `ReactDOM.createRoot` entry. Imports `index.css` (Tailwind v4 entry point). |
| `index.css` | Tailwind v4 (`@import "tailwindcss"`), Google Fonts (Lexend + Source Sans 3), `@theme` block with all design tokens. |

## Tests (`src/__tests__/`)

| File | Covers |
|---|---|
| `auth.store.test.ts` | `useAuthStore`: initial state, `setUser` → authenticated, `clearUser` → unauthenticated |
| `ProtectedRoute.test.tsx` | Spinner on loading, outlet on authenticated, redirect to `/login` on unauthenticated |
| `PublicOnlyRoute.test.tsx` | Spinner on loading, children on unauthenticated, redirect to `/dashboard` on authenticated |
| `RoleRoute.test.tsx` | Children on role match, redirect to `/dashboard` on role mismatch or null user |
| `LoginPage.test.tsx` | Renders fields; calls `authApi.login` on submit; shows `"Invalid email or password."` on 401; disables button while in flight. `authApi` is mocked with `vi.mock`. |
