# Spec: Academic Year / Term Configuration UI

## Claude Design Reference

The visual design for this page was authored in Claude Design and must be followed during implementation.

| | |
|---|---|
| **Project ID** | `2ee7d4a1-a721-42d8-b77e-737609059f37` |
| **Design file** | `Academic Years.dc.html` |
| **View link** | https://claude.ai/design/p/2ee7d4a1-a721-42d8-b77e-737609059f37?file=Academic+Years.dc.html |

The design is the authoritative source for visual details (colors, spacing, component variants, animation timings). Where the design and this spec conflict, the spec's **Boundaries** section takes precedence — see the "Ask first / Never" items for elements in the design that are deliberately excluded from implementation scope.

## Related docs & specs

- [docs/ideas/01-academic-year-term-configuration.md](../docs/ideas/01-academic-year-term-configuration.md) — idea doc: problem statement, two-level calendar structure, domain guard, MVP scope
- [specs/03-implement-academic-year-term-configuration.md](03-implement-academic-year-term-configuration.md) — backend spec: all seven API endpoints, DTO shapes, domain rules — fully implemented; this spec consumes it
- [specs/02-B-scaffold-frontend-and-auth.md](02-B-scaffold-frontend-and-auth.md) — frontend scaffold spec: Axios instance, TanStack Query setup, Zustand auth store, AppShell, route guards — all implemented; this spec extends the existing frontend
- [docs/design-system.md](../docs/design-system.md) — color palette (navy `#1E3A5F`, blue `#2563EB`, green `#059669`), typography (Lexend headings, Source Sans 3 body)
- [.claude/rules/backend.md](../.claude/rules/backend.md) — GET-must-be-side-effect-free rule; all state-mutating actions must use POST/PUT

## Objective

Build the Admin-only Academic Years page (`/admin/academic-years`) that lets school admins create academic years, edit semester dates, set the current year and current semester, and archive completed years. Wire the page into the existing sidebar and router. The backend API is fully implemented — this spec is purely frontend.

**Out of scope:** any read-only display of the current year/semester on other pages (e.g. dashboard badge); downstream modules (fees, grades) enforcing archived-year guards; E2E tests (Playwright tests will follow when E2E coverage begins).

## Tech Stack

All libraries are already installed from spec 02-B. No new dependencies are required.

| Concern | Library | Notes |
|---|---|---|
| Server state | TanStack Query v5 | `useQuery` for list, `useMutation` per action |
| Forms | React Hook Form v7 + Zod v3 | Create Year modal + Edit Semester modal |
| HTTP | Axios | Shared instance from `src/api/axios.ts` — `withCredentials: true` already set |
| UI primitives | shadcn/ui + Tailwind CSS | `Button`, `Input`, `Label`, `Form` already installed; `Dialog` needed — add with `npx shadcn@latest add dialog` |
| Notifications | sonner (toast library) | Add: `npm install sonner` + `<Toaster />` in `App.tsx` |

**Why `<input type="date">` instead of a dedicated date picker:** no extra dependency, and the admin-tool audience is on desktop where native date inputs work well. Styled to match shadcn/ui `Input` via Tailwind (`border border-input bg-background rounded-md px-3 py-2 text-sm`). Dates are passed to the API as strings in `YYYY-MM-DD` format, which is what `DateOnly` serializes to on the backend.

## Commands

```bash
# From the frontend/ directory

# New shadcn component needed
npx shadcn@latest add dialog

# Toast library
npm install sonner

# Dev server
npm run dev

# Tests
npm run test

# Build
npm run build
```

## Design

### API client

`src/api/academicYears.ts` — thin wrappers over the shared Axios instance. Dates are typed as `string` (ISO `YYYY-MM-DD`) on the frontend; the backend serializes `DateOnly` to this format automatically.

```ts
import api from './axios'

export interface SemesterDto {
  id: string
  academicYearId: string
  name: string
  startDate: string   // "YYYY-MM-DD"
  endDate: string
  isCurrent: boolean
}

export interface AcademicYearDto {
  id: string
  name: string
  startDate: string
  endDate: string
  status: 'Active' | 'Archived'
  isCurrent: boolean
  semesters: SemesterDto[]
}

export interface CreateAcademicYearRequest {
  name: string
  startDate: string
  endDate: string
}

export interface UpdateSemesterRequest {
  name: string
  startDate: string
  endDate: string
}

export const academicYearsApi = {
  list: () =>
    api.get<AcademicYearDto[]>('/academic-years').then((r) => r.data),

  create: (body: CreateAcademicYearRequest) =>
    api.post<AcademicYearDto>('/academic-years', body).then((r) => r.data),

  updateSemester: (yearId: string, semesterId: string, body: UpdateSemesterRequest) =>
    api
      .put<SemesterDto>(`/academic-years/${yearId}/semesters/${semesterId}`, body)
      .then((r) => r.data),

  setCurrentYear: (id: string) =>
    api.post(`/academic-years/${id}/set-current`).then((r) => r.data),

  setCurrentSemester: (yearId: string, semesterId: string) =>
    api
      .post(`/academic-years/${yearId}/semesters/${semesterId}/set-current`)
      .then((r) => r.data),

  archive: (id: string) =>
    api.post(`/academic-years/${id}/archive`).then((r) => r.data),
}
```

### Query key convention

```ts
export const ACADEMIC_YEAR_KEYS = {
  all: ['academic-years'] as const,
}
```

All mutations call `queryClient.invalidateQueries({ queryKey: ACADEMIC_YEAR_KEYS.all })` on success to refetch the full list. No optimistic updates — the list is small and the refetch is fast.

### Toast helper

Wire sonner's `<Toaster />` into `src/App.tsx` once (before `<RouterProvider>`):

```tsx
import { Toaster } from 'sonner'
// ...
return (
  <>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
    <Toaster richColors position="top-right" />
  </>
)
```

Mutations use `toast.success(...)` on settlement and `toast.error(message)` to surface backend 400 error messages. Extract the message from `AxiosError.response.data.error` (the shape `DomainExceptionFilter` returns).

### Router wiring

**`src/router/index.tsx`** — add a nested route under the existing `/admin/*` branch:

```tsx
import { AcademicYearsPage } from '../pages/admin/academic-years/AcademicYearsPage'

// inside the /admin/* children:
{ path: 'academic-years', element: <AcademicYearsPage /> }
```

**`src/pages/admin/index.tsx`** — replace the bare `<Outlet />` with a proper `<Routes>` so nested paths resolve:

```tsx
import { Routes, Route } from 'react-router-dom'
import { AcademicYearsPage } from './academic-years/AcademicYearsPage'

export function AdminRoutes() {
  return (
    <Routes>
      <Route path="academic-years" element={<AcademicYearsPage />} />
    </Routes>
  )
}
```

**`src/layouts/AppShell.tsx`** — add to `NAV_ITEMS`:

```tsx
import { CalendarDays } from 'lucide-react'

{
  label: 'Academic Years',
  to: '/admin/academic-years',
  icon: <CalendarDays size={18} />,
  roles: ['Admin'],
},
```

### Page: `AcademicYearsPage`

`src/pages/admin/academic-years/AcademicYearsPage.tsx`

Responsibilities:
- Fetch the full year list via `useQuery({ queryKey: ACADEMIC_YEAR_KEYS.all, queryFn: academicYearsApi.list })`
- Partition the list into `currentYear` (single item where `isCurrent === true`), `previousYears` (active, not current), and `archivedYears` (status === `'Archived'`)
- Manage `showArchived: boolean` toggle state (default `false`)
- Manage `createModalOpen: boolean` state
- Pass `queryClient` and mutation callbacks down to cards

```
AcademicYearsPage
  ├── Page header: "Academic Years" heading + subtitle "Manage academic years, semesters, and set the current active period." + "New Academic Year" button
  ├── Loading state: skeleton or spinner while query is pending
  ├── Error state: error message if query fails
  ├── Empty state: prompt to create the first year (when list is empty)
  ├── "Current Year" section heading (only if currentYear exists)
  │     └── AcademicYearCard (highlighted variant)
  ├── "Previous Years" section heading (only if previousYears.length > 0)
  │     └── AcademicYearCard × N
  ├── "Show archived (N)" toggle button (only if archivedYears.length > 0)
  │     └── AcademicYearCard × N (rendered when showArchived is true)
  └── CreateYearModal (controlled by createModalOpen)
```

### Component: `AcademicYearCard`

`src/pages/admin/academic-years/components/AcademicYearCard.tsx`

Props:
```ts
interface AcademicYearCardProps {
  year: AcademicYearDto
  onSetCurrent: (id: string) => void
  onArchive: (id: string) => void
  onEditSemester: (semester: SemesterDto) => void
  onSetCurrentSemester: (yearId: string, semesterId: string) => void
}
```

Layout:
```
┌─ [left border accent if isCurrent] ─────────────────────────────┐
│  Header row:                                                      │
│    [Year name]  [Status badge]  [IsCurrent badge if current]     │
│    [Date range]                                                   │
│                                                                   │
│  Actions (right-aligned, shown contextually):                    │
│    [Set as Current]  — only if !isCurrent && status === 'Active' │
│    [Archive]         — only if !isCurrent && status === 'Active' │
│                                                                   │
│  Semesters section:                                               │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │  Semester 1  |  01 Aug 2025 – 31 Jan 2026  | [CURRENT]    │  │
│  │              |                               | [Edit]       │  │
│  │              |                               | [Set Current]│  │
│  ├────────────────────────────────────────────────────────────┤  │
│  │  Semester 2  |  01 Feb 2026 – 30 Jun 2026  |              │  │
│  │              |                               | [Edit]       │  │
│  │              |                               | [Set Current]│  │
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

**Visual treatment:**
- Current year card: left border `border-l-4 border-l-primary` (navy) + card header background `bg-primary/5` (`#EEF2F7`)
- Non-current / previous year card: standard white card, no left border accent
- Archived year card: `opacity-75`, no action buttons, "Archived — read only" label in card header instead of actions
- Status badge: `Active` → green pill (`bg-accent/10 text-accent`); `Archived` → muted pill (`bg-muted text-muted-foreground`)
- `Current Year` badge on current year header: navy pill with checkmark icon
- `Current` semester badge: small blue pill with checkmark icon (`bg-secondary/10 text-secondary`)
- Each semester row has a **3px vertical left accent bar**: navy (`#1E3A5F`) when that semester is current, grey (`#E4E7EB`) otherwise
- Semester status sub-label (below semester name): `Current` → no extra label (badge already communicates this); non-current semester in current year → `"Upcoming"`; semesters in non-current years → `"Completed"`
- Current year card header right side: shows a "Protected — cannot archive" chip (lock icon + text) instead of Archive/Set-Current buttons — cleaner than a disabled button
- "Set Current Semester" button: only shown on semester rows within the current year (`year.isCurrent === true`) and only when `!semester.isCurrent`
- Card entrance: `fadeIn` CSS animation (opacity 0→1, translateY 6px→0, 200ms ease)
- Modal entrance: `modalIn` CSS animation (opacity 0→1, scale 0.97→1, translateY 8px→0, 180ms ease)
- Archive action: clicking it opens a `window.confirm` confirmation dialog before calling the mutation (no separate modal needed for this simple irreversible action)

### Component: `CreateYearModal`

`src/pages/admin/academic-years/components/CreateYearModal.tsx`

Wraps shadcn `Dialog`. Form fields: `name` (text), `startDate` (date), `endDate` (date).

Zod schema:
```ts
const schema = z.object({
  name: z.string().min(1, 'Required').max(100),
  startDate: z.string().min(1, 'Required'),
  endDate: z.string().min(1, 'Required'),
}).refine((d) => d.endDate > d.startDate, {
  message: 'End date must be after start date',
  path: ['endDate'],
})
```

Modal body includes an informational note below the date fields: *"Two semesters will be created automatically — each spanning half the year. You can adjust semester dates after creation."* This sets admin expectations before submission.

On submit: call `createMutation.mutateAsync(data)` → close modal on success → `toast.success('Academic year created')`. On 409 from the server: `toast.error('An academic year with this name already exists.')`.

### Component: `EditSemesterModal`

`src/pages/admin/academic-years/components/EditSemesterModal.tsx`

Wraps shadcn `Dialog`. Controlled by `editingSemester: SemesterDto | null` in the parent page (non-null = open). Pre-populates form with the semester's current values.

Props:
```ts
interface EditSemesterModalProps {
  semester: SemesterDto | null   // null = closed
  onClose: () => void
}
```

Zod schema: same shape as `CreateYearModal` minus `name` constraint differences:
```ts
const schema = z.object({
  name: z.string().min(1, 'Required').max(100),
  startDate: z.string().min(1, 'Required'),
  endDate: z.string().min(1, 'Required'),
}).refine((d) => d.endDate > d.startDate, {
  message: 'End date must be after start date',
  path: ['endDate'],
})
```

On submit: `updateSemesterMutation.mutateAsync({ yearId: semester.academicYearId, semesterId: semester.id, body: data })` → close on success → `toast.success('Semester updated')`.

## Project Structure

New and modified files introduced by this spec:

```
frontend/
  src/
    api/
      academicYears.ts              # new — API client + DTOs + query key

    pages/
      admin/
        index.tsx                   # modify — add Routes/Route for academic-years
        academic-years/
          AcademicYearsPage.tsx     # new — main page
          components/
            AcademicYearCard.tsx    # new
            CreateYearModal.tsx     # new
            EditSemesterModal.tsx   # new

    layouts/
      AppShell.tsx                  # modify — add "Academic Years" to NAV_ITEMS

    router/
      index.tsx                     # modify — add { path: 'academic-years', element: <AcademicYearsPage /> }

    App.tsx                         # modify — add <Toaster /> from sonner
```

## Testing Strategy

### Unit / component tests (Vitest + React Testing Library)

`src/pages/admin/academic-years/__tests__/AcademicYearsPage.test.tsx`

Test cases (mock `academicYearsApi` via `vi.mock`):

- **Empty state**: when `list` returns `[]`, renders the "no academic years yet" prompt and a "New Academic Year" button.
- **Current year highlighted**: when the list includes a year with `isCurrent: true`, that card renders with the current-year visual treatment and the "Set as Current" and "Archive" buttons are absent from it.
- **Set Current Year**: clicking "Set as Current" on a non-current year calls `academicYearsApi.setCurrentYear` with the correct id and invalidates the query.
- **Archive confirmation**: clicking "Archive" triggers a confirm dialog; if the user cancels, `archive` is not called.
- **"Show archived" toggle**: archived years are hidden by default; clicking the toggle shows them.
- **Create modal opens/closes**: clicking "New Academic Year" opens the modal; submitting the form calls `academicYearsApi.create` and closes the modal on success.
- **Edit Semester modal pre-populates**: clicking "Edit" on a semester row opens the modal with the semester's current name and dates.

Test cases for `AcademicYearCard.tsx` directly:

- **"Set Current Semester" visibility**: the button appears on non-current semester rows within a current year, and does not appear on semesters within a non-current year.

### What is NOT tested here

- E2E flows (Playwright) — deferred to a later spec
- API client functions (`academicYears.ts`) — thin wrappers with no logic; covered by integration tests on the backend side

## Boundaries

- **Always:** invalidate `ACADEMIC_YEAR_KEYS.all` on every successful mutation (create year, update semester, set current year, set current semester, archive). Never update the query cache manually — a refetch is the source of truth.
- **Always:** surface the backend's `error` field from 400 responses as a toast — domain exceptions carry user-readable messages (e.g. "Cannot archive the current academic year").
- **Ask first:** introducing a dedicated date-picker component (e.g. `react-day-picker`) — it adds a dependency and changes the date input pattern across the app.
- **Ask first:** adding pagination or filtering to the year list — the list is expected to stay small (one new year per calendar year), but if the school has 10+ years it may warrant reconsideration.
- **Ask first:** restructuring the `AppShell` sidebar/topbar (e.g. moving user profile to the sidebar bottom, adding a notification bell, adding future nav items with count badges) — the Claude Design prototype shows a richer shell, but that is a separate AppShell-wide change outside this spec's scope.
- **Never:** add a search input to the page header — filtering/searching years is explicitly out of scope.
- **Never:** add nav items for pages that don't exist yet (Students, Staff, Fees, Reports) — dead links in the sidebar undermine trust in an admin tool.
- **Never:** trigger a mutation from a GET request or a side-effect on page load — all state transitions must be user-initiated.
- **Never:** store the year list in Zustand — server state lives in TanStack Query, not the client store.
- **Never:** import `AppDbContext`, backend services, or any backend project reference into the frontend.

## Success Criteria

- `GET /api/academic-years` data renders as stacked cards with correct sectioning (current / previous / archived).
- The current year card is visually distinct from non-current cards (left accent border, highlighted header).
- "New Academic Year" opens a modal; a valid submission creates a year with auto-scaffolded semesters and the list refreshes.
- Clicking "Edit" on a semester row opens the edit modal pre-populated with the semester's current values; saving updates the data and the list refreshes.
- "Set as Current" on a non-current year atomically updates the list (previously-current year loses the highlight; new year gains it; Semester 1 of the new year shows the current badge).
- "Archive" requires user confirmation, is absent from the current year's card, and results in the year moving to the archived section.
- Archived years are hidden by default behind a "Show archived" toggle.
- All mutation errors (400 domain exceptions) display a toast with the backend's error message.
- "Academic Years" nav item appears in the sidebar for Admin users only and navigates to `/admin/academic-years`.
- `npm run test` passes with all new component tests green.
