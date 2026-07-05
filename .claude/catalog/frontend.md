<!-- Last verified: 2026-07-02. Update this file whenever a new public type/function/component is added or removed from the frontend. Check here before adding new code — don't duplicate something that already exists. -->

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
| `academicYearsApi` | `academicYears.ts` | Thin wrappers: `list()`, `create(body)`, `updateSemester(yearId, semId, body)`, `setCurrentYear(id)`, `setCurrentSemester(yearId, semId)`, `archive(id)`. |
| `ACADEMIC_YEAR_KEYS` | `academicYears.ts` | TanStack Query key factory: `{ all: ['academic-years'] }`. All mutations invalidate this key on success. |
| `AcademicYearDto` | `academicYears.ts` | `id`, `name`, `startDate`, `endDate`, `status` (`'Active' \| 'Archived'`), `isCurrent`, `semesters: SemesterDto[]` |
| `SemesterDto` | `academicYears.ts` | `id`, `academicYearId`, `name`, `startDate`, `endDate`, `isCurrent` |
| `CreateAcademicYearRequest` | `academicYears.ts` | `name`, `startDate`, `endDate` |
| `UpdateSemesterRequest` | `academicYears.ts` | `name`, `startDate`, `endDate` |
| `gradesApi` | `grades.ts` | Thin wrappers: `list()`, `create(body)`, `update(id, body)`, `delete(id)`, `addSection(gradeId, body)`, `updateSection(gradeId, sectionId, body)`, `deleteSection(gradeId, sectionId)`. |
| `GRADE_KEYS` | `grades.ts` | TanStack Query key factory: `{ all: ['grades'] }`. All grade/section mutations invalidate this key on success. |
| `GradeDto` | `grades.ts` | `id`, `name`, `displayOrder`, `sections: SectionDto[]` |
| `SectionDto` | `grades.ts` | `id`, `gradeId`, `name` |
| `CreateGradeRequest` | `grades.ts` | `name`, `displayOrder` |
| `UpdateGradeRequest` | `grades.ts` | `name`, `displayOrder` |
| `CreateSectionRequest` | `grades.ts` | `name` |
| `UpdateSectionRequest` | `grades.ts` | `name` |
| `studentsApi` | `students.ts` | Thin wrappers: `list(params)`, `getById(id)`, `create(body)`, `update(id, body)`. |
| `STUDENT_KEYS` | `students.ts` | TanStack Query key factory: `{ list(params), detail(id) }`. All mutations invalidate `['students']` prefix key. |
| `StudentSummaryDto` | `students.ts` | List-view shape: `id`, `studentCode`, `firstName`, `lastName`, `dateOfBirth`, `gender`, `enrollmentDate`, `enrollmentStatus` (all strings) |
| `StudentDto` | `students.ts` | Full shape extending `StudentSummaryDto`: adds `guardianName`, `guardianPhone`, `guardianEmail` (nullable), `createdAt`, `updatedAt`. |
| `PagedResult<T>` | `students.ts` | Generic paged response: `items: T[]`, `totalCount`, `page`, `pageSize`. Defined here for the students feature; move to `src/types/api.ts` when a second consumer appears. |
| `CreateStudentRequest` | `students.ts` | `firstName`, `lastName`, `dateOfBirth`, `gender`, `enrollmentDate`, optional guardian fields. |
| `UpdateStudentRequest` | `students.ts` | Extends `CreateStudentRequest` with `enrollmentStatus`. `studentCode` is NEVER included. |
| `ListStudentsParams` | `students.ts` | Query params for `list()`: `status`, `search`, `page`, `pageSize`. |
| `teachersApi` | `teachers.ts` | Thin wrappers: `list(params)`, `getById(id)`, `create(body)`, `update(id, body)`. `list` omits `?isActive=` when `params.isActive` is null (All tab). |
| `TEACHER_KEYS` | `teachers.ts` | TanStack Query key factory: `{ list(params), detail(id) }`. All mutations invalidate `['teachers']` prefix key. |
| `TeacherSummaryDto` | `teachers.ts` | List-view shape: `id`, `teacherCode`, `firstName`, `lastName`, `phone` (nullable), `joiningDate`, `isActive` (boolean), `email`. |
| `TeacherDto` | `teachers.ts` | Full shape extending `TeacherSummaryDto`: adds `userId`, `createdAt`, `updatedAt`. |
| `CreateTeacherRequest` | `teachers.ts` | `email`, `password`, `firstName`, `lastName`, `joiningDate`, optional `phone`. Creates a User + Teacher atomically. |
| `UpdateTeacherRequest` | `teachers.ts` | `firstName`, `lastName`, `joiningDate`, optional `phone`, `isActive`. Email/password NOT included — auth concern, out of scope. |
| `ListTeachersParams` | `teachers.ts` | Query params for `list()`: `isActive: boolean \| null` (null = all), `search`, `page`, `pageSize`. |

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
| `AppShell` | Persistent authenticated shell. Left sidebar: navy `bg-primary`, `SchoolMS` logo, data-driven `NAV_ITEMS` filtered by `user.role`. Nav items: Dashboard (all roles), Academic Years, Grades & Sections, Students, Teachers (all Admin-only). Right column: topbar with user display name + logout button; `<Outlet>` for page content. Logout calls `authApi.logout()` + `clearUser()` + `navigate('/login')`. |

## Pages (`src/pages/`)

| Component | Location | Purpose |
|---|---|---|
| `LoginPage` | `auth/LoginPage.tsx` | Split-panel login UI matching the "Login Page - Final" design. Left: navy brand panel with marketing copy and feature items. Right: white panel with RHF+Zod form (email + password + remember-me checkbox). Shows `"Invalid email or password."` form-level error on 401; disables submit while in flight. On success: calls `setUser` then navigates to `/dashboard`. |
| `DashboardPage` | `dashboard/DashboardPage.tsx` | Stub — "Welcome, {displayName}" heading. All future role-specific dashboards live here. |
| `AdminRoutes` | `admin/index.tsx` | `<Routes>` wrapper for `/admin/*`. Routes: `academic-years` → `AcademicYearsPage`, `grades` → `GradesPage`, `students` → `StudentsPage`, `teachers` → `TeachersPage`. |
| `AcademicYearsPage` | `admin/academic-years/AcademicYearsPage.tsx` | Admin page at `/admin/academic-years`. Fetches year list via TanStack Query; partitions into current / previous / archived sections; manages `createOpen`, `editingSemester`, `showArchived` state; owns all mutations (setCurrentYear, archive, setCurrentSemester); composes `AcademicYearCard`, `CreateYearModal`, `EditSemesterModal`. |
| `GradesPage` | `admin/grades/GradesPage.tsx` | Admin page at `/admin/grades`. Fetches grade list via TanStack Query; manages `createOpen`, `editingGrade`, `expandedIds` (accordion) state; owns grade delete mutation; auto-expands accordion for newly created grades via `onCreated` callback; composes `GradeAccordionItem`, `CreateGradeModal`, `EditGradeModal`. |
| `GradeAccordionItem` | `admin/grades/components/GradeAccordionItem.tsx` | shadcn Accordion card for one grade. Collapsed header shows grade name + section count Badge. Expanded body shows `SectionChip` row, inline add-section form (input + save/cancel), and Edit Grade / Delete Grade buttons. Delete Grade is disabled (with Tooltip) when `grade.sections.length > 0`. Owns `addSectionMutation`. |
| `SectionChip` | `admin/grades/components/SectionChip.tsx` | Self-contained chip that toggles between display and inline-edit mode. Display: styled button showing section name. Edit: inline input + save/cancel + delete (with `window.confirm`). Owns `renameMutation` and `deleteMutation` locally. Escape key cancels edit; Enter key saves. |
| `CreateGradeModal` | `admin/grades/components/CreateGradeModal.tsx` | shadcn Dialog + RHF + Zod for creating a grade (`name`, `displayOrder: number ≥ 0`). `onCreated(id)` callback used by page to auto-expand the new grade's accordion. 409 → "A grade with this name already exists." toast. |
| `EditGradeModal` | `admin/grades/components/EditGradeModal.tsx` | shadcn Dialog + RHF + Zod for editing a grade. Controlled by `grade: GradeDto \| null` (null = closed). Pre-populates via `useEffect` + `reset` on prop change. |
| `StudentsPage` | `admin/students/StudentsPage.tsx` | Admin page at `/admin/students`. Server-paginated table with enrollment status `Tabs` filter, debounced (300 ms) search input, Prev/Next pagination (`keepPreviousData`). State: `tab`, `search`, `debouncedSearch`, `page`, `createOpen`, `editingId`. Composes `CreateStudentModal` + `EditStudentModal`. Inline `StatusBadge` (color-coded by status). |
| `CreateStudentModal` | `admin/students/components/CreateStudentModal.tsx` | shadcn Dialog + RHF + Zod for creating a student. Two-column layout: First/Last, DOB/Gender, Enrollment Date, guardian section (optional). Gender uses shadcn `Select`. Exports `createSchema` (used by `EditStudentModal`). Empty optional string fields are stripped to `undefined` before POST. |
| `EditStudentModal` | `admin/students/components/EditStudentModal.tsx` | shadcn Dialog + RHF + Zod for editing a student. Controlled by `studentId: string \| null`. Fetches full `StudentDto` on open (`enabled: studentId !== null`). Shows spinner while loading. Displays `studentCode` as read-only text — never in payload. Extends `createSchema` with `enrollmentStatus` Select. |
| `TeachersPage` | `admin/teachers/TeachersPage.tsx` | Admin page at `/admin/teachers`. Server-paginated table with Active/Inactive/All `Tabs` filter (maps to `?isActive=true/false/omit`), debounced (300 ms) search, Prev/Next pagination (`keepPreviousData`). State: `tab`, `search`, `debouncedSearch`, `page`, `createOpen`, `editingId`. Inline `StatusBadge` (green=active, zinc=inactive). Composes `CreateTeacherModal` + `EditTeacherModal`. |
| `CreateTeacherModal` | `admin/teachers/components/CreateTeacherModal.tsx` | shadcn Dialog + RHF + Zod for creating a teacher (also creates their User account). Fields: firstName, lastName, email, password (≥8 chars), joiningDate, phone (optional). Email input is `type="text"` — Zod validates email format (avoids jsdom HTML5 blocking). Exports `createTeacherSchema`. |
| `EditTeacherModal` | `admin/teachers/components/EditTeacherModal.tsx` | shadcn Dialog + RHF + Zod for editing a teacher. Controlled by `teacherId: string \| null`. Fetches full `TeacherDto` on open. Displays `teacherCode` and `email` as read-only text — neither in PUT payload. `isActive` uses native `<input type="checkbox">` via `Controller`. Deactivating also disables the teacher's login (backend syncs `User.IsActive`). |
| `AcademicYearCard` | `admin/academic-years/components/AcademicYearCard.tsx` | Renders one `AcademicYearDto` with its semesters. Highlights current year (`border-l-4` navy + `bg-primary/5`). Shows contextual buttons: Set as Current / Archive on active non-current years; Edit + Set Current on semester rows within current year. Archive requires `window.confirm`. Archived cards show "Archived — read only" and no buttons. |
| `CreateYearModal` | `admin/academic-years/components/CreateYearModal.tsx` | shadcn Dialog + RHF + Zod for creating an academic year (`name`, `startDate`, `endDate`, cross-field `endDate > startDate`). On 409: shows "An academic year with this name already exists." toast. |
| `EditSemesterModal` | `admin/academic-years/components/EditSemesterModal.tsx` | shadcn Dialog + RHF + Zod for editing a semester. Controlled by `semester: SemesterDto \| null` (null = closed). Pre-populates from prop via `useEffect` + `reset`. |
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
| `Dialog` / `DialogContent` / `DialogHeader` / `DialogTitle` / `DialogFooter` / `DialogTrigger` / `DialogClose` | `ui/dialog.tsx` | shadcn-compatible Radix Dialog wrapper. `DialogContent` renders via a portal into `document.body`. Add `showCloseButton={false}` to hide the default X button. |
| `Accordion` / `AccordionItem` / `AccordionTrigger` / `AccordionContent` | `ui/accordion.tsx` | shadcn Radix Accordion. Supports `type="multiple"` (several items open simultaneously) and `type="single" collapsible`. Use controlled `value` + `onValueChange` for programmatic expand (e.g. auto-open after create). |
| `Badge` | `ui/badge.tsx` | shadcn Badge for count/status labels. Variants: `default`, `secondary`, `destructive`, `outline`. |
| `Tooltip` / `TooltipContent` / `TooltipTrigger` / `TooltipProvider` | `ui/tooltip.tsx` | shadcn Radix Tooltip. Wrap `TooltipProvider` at the usage site (or app root). Wrap a disabled `<Button>` in a `<span>` before `TooltipTrigger` — disabled elements don't emit pointer events and won't trigger the tooltip otherwise. |
| `Table` / `TableHeader` / `TableBody` / `TableRow` / `TableHead` / `TableCell` / `TableCaption` / `TableFooter` | `ui/table.tsx` | shadcn-compatible table primitives. `Table` wraps in `overflow-x-auto` container. |
| `Select` / `SelectTrigger` / `SelectValue` / `SelectContent` / `SelectItem` / `SelectGroup` / `SelectLabel` / `SelectSeparator` / `SelectScrollUpButton` / `SelectScrollDownButton` | `ui/select.tsx` | shadcn Radix Select. Use with `<Controller>` from RHF. In tests, mock the module with a native `<select>`+`<option>` to avoid Radix portal/jsdom issues with Dialog scroll-lock. |
| `Tabs` / `TabsList` / `TabsTrigger` / `TabsContent` | `ui/tabs.tsx` | shadcn Radix Tabs. Use `value`+`onValueChange` for controlled tabs. |

## Utilities (`src/lib/`)

| Export | Location | Purpose |
|---|---|---|
| `cn` | `utils.ts` | `clsx` + `tailwind-merge` combiner. Use for all conditional className merging. |

## App Entry (`src/`)

| File | Purpose |
|---|---|
| `App.tsx` | `QueryClientProvider` wraps `AppInner`. `AppInner` calls `useAuthInit()` then renders `RouterProvider`. Also renders `<Toaster richColors position="top-right" />` from `sonner` for all mutations to fire toasts. |
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
| `academic-years/__tests__/AcademicYearsPage.test.tsx` | 8 tests covering: empty state, current year highlighting, Set-as-Current mutation, Archive confirm guard, archived toggle, create modal flow, edit modal pre-population, Set-Current-Semester visibility. `academicYearsApi` mocked via `vi.mock`. |
| `grades/__tests__/GradesPage.test.tsx` | Empty state; grade list with section counts; accordion expand; disabled delete when grade has sections; enabled delete when empty; create modal opens. `gradesApi` mocked via `vi.mock`. |
| `grades/__tests__/CreateGradeModal.test.tsx` | Correct payload on submit; 409 → "already exists" toast; submit disabled while pending. |
| `grades/__tests__/SectionChip.test.tsx` | Renders section name; click enters edit mode; Escape cancels; save calls `updateSection`; delete with confirm calls `deleteSection`. |
| `teachers/__tests__/TeachersPage.test.tsx` | 8 tests: renders rows, empty state, Inactive tab → `isActive: false`, All tab → `isActive: null`, search debounce, pagination, edit opens getById, add button opens modal. `teachersApi` mocked. |
| `teachers/__tests__/CreateTeacherModal.test.tsx` | 6 tests: correct payload, phone omitted when blank, short password blocked, invalid email blocked, submit disabled while pending, closes + calls onCreated on success. |
| `teachers/__tests__/EditTeacherModal.test.tsx` | 7 tests: loading spinner, form pre-population, teacherCode read-only, email read-only, isActive checkbox state, correct PUT payload (no teacherCode/email), "disables login" label visible. |
