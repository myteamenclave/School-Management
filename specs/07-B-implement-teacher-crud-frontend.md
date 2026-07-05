# Spec: Implement Teacher CRUD — Frontend

## Related docs & specs

- [specs/06-implement-teacher-crud.md](06-implement-teacher-crud.md) — backend spec: Teacher entity, TeacherCode format (`YYYY-NNNNNN`), two-table create (User + Teacher), `UpdateTeacherRequest` fields, IsActive/login-disable coupling
- [specs/06-B-implement-student-crud-frontend.md](06-B-implement-student-crud-frontend.md) — primary frontend pattern reference: API client shape, debounced search, TanStack Query + React Hook Form + Zod conventions, pagination, modal structure, testing approach
- [specs/02-B-scaffold-frontend-and-auth.md](02-B-scaffold-frontend-and-auth.md) — frontend foundation: Axios instance, TanStack Query, React Hook Form + Zod, AppShell + NAV_ITEMS, RoleRoute

## Objective

Build the Admin-only Teachers page (`/admin/teachers`) that lets school admins browse the teacher roster, create new teacher accounts, and edit existing teacher records (including toggling active status). The page is a server-paginated data table filtered by an Active / Inactive status tab and a debounced search. Create and Edit each open a modal dialog — consistent with Academic Years, Grades, and Students pages.

This spec also extends the backend `GET /api/teachers` endpoint with a `?search=` query parameter (ILIKE filter on `FirstName`, `LastName`, `Email`, and `TeacherCode`). That is the only backend change; no new migrations are needed.

**Out of scope:** teacher detail page, email/password update, class/subject assignment, bulk CSV import, any role other than Admin.

## Tech Stack

Same as [specs/02-B-scaffold-frontend-and-auth.md](02-B-scaffold-frontend-and-auth.md). No new shadcn components needed — `Table`, `Select`, and `Tabs` are already installed from the Students spec.

---

## Part 1 — Backend Addition: `?search=` query parameter

Surgical, no-migration change to four existing files.

### `ITeacherRepository` — add `search` parameter

```csharp
// SchoolMgmt.Application/Teachers/ITeacherRepository.cs
Task<(List<Teacher> Items, int TotalCount)> GetPagedAsync(
    bool? isActive, string? search, int page, int pageSize, CancellationToken ct = default);
```

### `TeacherRepository` — ILIKE filter

```csharp
public async Task<(List<Teacher> Items, int TotalCount)> GetPagedAsync(
    bool? isActive, string? search, int page, int pageSize, CancellationToken ct = default)
{
    var query = DbSet.Include(t => t.User).AsQueryable();

    if (isActive.HasValue)
        query = query.Where(t => t.IsActive == isActive.Value);

    if (!string.IsNullOrWhiteSpace(search))
    {
        var pattern = $"%{search.Trim()}%";
        query = query.Where(t =>
            EF.Functions.ILike(t.FirstName + " " + t.LastName, pattern) ||
            EF.Functions.ILike(t.TeacherCode, pattern) ||
            EF.Functions.ILike(t.User.Email, pattern));
    }

    var total = await query.CountAsync(ct);
    var items = await query
        .OrderBy(t => t.LastName).ThenBy(t => t.FirstName)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(ct);

    return (items, total);
}
```

`EF.Functions.ILike` is available via `Npgsql.EntityFrameworkCore.PostgreSQL` (already in the project). No DB index change required for demo scale.

### `TeacherService` — thread `search` through

```csharp
public async Task<PagedResult<TeacherSummaryDto>> GetTeachersAsync(
    bool? isActive, string? search, int page, int pageSize, CancellationToken ct = default)
{
    var (items, total) = await repository.GetPagedAsync(isActive, search, page, pageSize, ct);
    return new PagedResult<TeacherSummaryDto>(items.Select(ToSummaryDto).ToList(), total, page, pageSize);
}
```

### `TeachersController` — expose `?search=` query param

```csharp
[HttpGet]
public async Task<IActionResult> GetAll(
    [FromQuery] bool? isActive,
    [FromQuery] string? search = null,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken ct = default)
{
    pageSize = Math.Min(pageSize, 100);
    var result = await service.GetTeachersAsync(isActive, search, page, pageSize, ct);
    return Ok(result);
}
```

---

## Part 2 — Frontend

### API client — `src/api/teachers.ts`

DTOs mirror the backend response shapes exactly. `DateOnly` fields arrive as `"YYYY-MM-DD"` strings.

```ts
import api from './axios'

export interface TeacherSummaryDto {
  id: string
  teacherCode: string
  firstName: string
  lastName: string
  phone: string | null
  joiningDate: string    // "YYYY-MM-DD"
  isActive: boolean
  email: string
}

export interface TeacherDto extends TeacherSummaryDto {
  userId: string
  createdAt: string
  updatedAt: string | null
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface CreateTeacherRequest {
  email: string
  password: string
  firstName: string
  lastName: string
  phone?: string
  joiningDate: string    // "YYYY-MM-DD"
}

export interface UpdateTeacherRequest {
  firstName: string
  lastName: string
  phone?: string
  joiningDate: string    // "YYYY-MM-DD"
  isActive: boolean
}

export interface ListTeachersParams {
  isActive: boolean | null  // null = all
  search: string
  page: number
  pageSize: number
}

export const TEACHER_KEYS = {
  list: (p: ListTeachersParams) => ['teachers', 'list', p] as const,
  detail: (id: string) => ['teachers', 'detail', id] as const,
}

export const teachersApi = {
  list: (params: ListTeachersParams) => {
    const q: Record<string, unknown> = { page: params.page, pageSize: params.pageSize }
    if (params.isActive !== null) q.isActive = params.isActive
    if (params.search) q.search = params.search
    return api.get<PagedResult<TeacherSummaryDto>>('/teachers', { params: q }).then((r) => r.data)
  },

  getById: (id: string) =>
    api.get<TeacherDto>(`/teachers/${id}`).then((r) => r.data),

  create: (body: CreateTeacherRequest) =>
    api.post<TeacherDto>('/teachers', body).then((r) => r.data),

  update: (id: string, body: UpdateTeacherRequest) =>
    api.put<TeacherDto>(`/teachers/${id}`, body).then((r) => r.data),
}
```

`isActive: null` in `ListTeachersParams` means "all teachers" (no `?isActive=` sent). The default tab is "Active" (`isActive: true`).

`PagedResult<T>` is defined here following the same pattern as `src/api/students.ts` — move both to `src/types/api.ts` when a third consumer appears.

### Error extraction helper

Same pattern as students — defined at the top of `TeachersPage.tsx`:

```ts
function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}
```

### Status tab type

```ts
type StatusTab = 'Active' | 'Inactive' | 'All'
const STATUS_TABS: StatusTab[] = ['Active', 'Inactive', 'All']

function tabToIsActive(tab: StatusTab): boolean | null {
  if (tab === 'Active') return true
  if (tab === 'Inactive') return false
  return null
}
```

### Page component — `src/pages/admin/teachers/TeachersPage.tsx`

**State:**
- `tab: StatusTab` — default `'Active'`
- `search: string` — raw controlled input value
- `debouncedSearch: string` — 300 ms debounced; drives `?search=` param
- `page: number` — default `1`; reset to `1` on tab or debouncedSearch change
- `createOpen: boolean`
- `editingId: string | null`

**Debounce:** identical pattern to `StudentsPage.tsx` (300 ms `setTimeout` in `useEffect`).

**Data fetching:**
```ts
const queryParams: ListTeachersParams = {
  isActive: tabToIsActive(tab),
  search: debouncedSearch,
  page,
  pageSize: 20,
}

const { data, isLoading, isError } = useQuery({
  queryKey: TEACHER_KEYS.list(queryParams),
  queryFn: () => teachersApi.list(queryParams),
  placeholderData: keepPreviousData,
})
```

**Layout:**
```tsx
<div className="px-8 py-8 max-w-6xl mx-auto">
  {/* Page header */}
  <div className="flex items-start justify-between mb-6">
    <div>
      <h1 className="font-heading text-2xl font-semibold text-foreground">Teachers</h1>
      <p className="text-sm text-muted-foreground mt-1">
        Manage teacher accounts and active status.
      </p>
    </div>
    <Button onClick={() => setCreateOpen(true)}>
      <Plus size={16} className="mr-2" /> Add Teacher
    </Button>
  </div>

  {/* Filters row: status tabs + search */}
  <div className="flex items-center justify-between gap-4 mb-4">
    <Tabs value={tab} onValueChange={(v) => { setTab(v as StatusTab); setPage(1) }}>
      <TabsList>
        {STATUS_TABS.map((t) => (
          <TabsTrigger key={t} value={t}>{t}</TabsTrigger>
        ))}
      </TabsList>
    </Tabs>

    <div className="relative w-64">
      <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
      <Input
        placeholder="Search name, code, or email…"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        className="pl-9"
      />
    </div>
  </div>

  {/* Table */}
  {/* ... loading / error / table identical in structure to StudentsPage ... */}

  {/* Table columns: Code | Name | Email | Phone | Joined | Status | (edit button) */}
</div>
```

**Table columns:**

| Column | Source field | Notes |
|---|---|---|
| Code | `teacherCode` | `font-mono text-xs text-muted-foreground` |
| Name | `firstName + ' ' + lastName` | `font-medium` |
| Email | `email` | `text-sm text-muted-foreground` |
| Phone | `phone ?? '—'` | `text-sm text-muted-foreground` |
| Joined | `joiningDate` | `text-sm text-muted-foreground` |
| Status | `isActive` | `<StatusBadge />` |
| — | — | Edit pencil button |

**`StatusBadge` helper (inline in `TeachersPage.tsx`):**

```tsx
function StatusBadge({ isActive }: { isActive: boolean }) {
  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${
      isActive
        ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400'
        : 'bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400'
    }`}>
      {isActive ? 'Active' : 'Inactive'}
    </span>
  )
}
```

**Pagination:** identical to `StudentsPage.tsx` — Prev/Next buttons, "Page X of Y" label, hidden when `totalPages <= 1`.

### Create teacher modal — `src/pages/admin/teachers/components/CreateTeacherModal.tsx`

Creating a teacher also creates their login account (email + password). These fields appear only in the create modal — email/password updates are out of scope for this spec.

**Props:**
```ts
interface CreateTeacherModalProps {
  open: boolean
  onClose: () => void
  onCreated: () => void
}
```

**Zod schema:**
```ts
const schema = z.object({
  firstName:   z.string().min(1, 'Required').max(100),
  lastName:    z.string().min(1, 'Required').max(100),
  email:       z.string().min(1, 'Required').email('Invalid email').max(256),
  password:    z.string().min(8, 'At least 8 characters').max(128),
  joiningDate: z.string().min(1, 'Required'),
  phone:       z.string().max(20).optional(),
})
type FormValues = z.infer<typeof schema>
```

**Form layout:**
```
Row 1: First Name | Last Name          (grid-cols-2)
Row 2: Email                           (full width)
Row 3: Password                        (full width)
Row 4: Joining Date | Phone            (grid-cols-2)
```

**Password field:** `type="password"` native input using the same styled className as date fields (see students spec). No "show password" toggle — keep it simple.

**Mutation:**
```ts
const mutation = useMutation({
  mutationFn: teachersApi.create,
  onSuccess: () => {
    toast.success('Teacher created')
    reset()
    onCreated()
    onClose()
  },
  onError: (err) => toast.error(extractError(err)),
})
```

**Email uniqueness (409):** if `err.response?.status === 409`, the `extractError` will surface the backend's "Email already in use." message from the error body — no special handling needed beyond `extractError`.

**Submit button:** `isSubmitting ? 'Creating…' : 'Create Teacher'`

**Dialog max width:** `sm:max-w-lg`

### Edit teacher modal — `src/pages/admin/teachers/components/EditTeacherModal.tsx`

**Props:**
```ts
interface EditTeacherModalProps {
  teacherId: string | null   // null = closed
  onClose: () => void
  onUpdated: () => void
}
```

`open` is derived: `teacherId !== null`.

**Data fetch:**
```ts
const { data: teacher, isLoading } = useQuery({
  queryKey: TEACHER_KEYS.detail(teacherId ?? ''),
  queryFn: () => teachersApi.getById(teacherId!),
  enabled: teacherId !== null,
})
```

**Zod schema:**
```ts
const schema = z.object({
  firstName:   z.string().min(1, 'Required').max(100),
  lastName:    z.string().min(1, 'Required').max(100),
  joiningDate: z.string().min(1, 'Required'),
  phone:       z.string().max(20).optional(),
  isActive:    z.boolean(),
})
type FormValues = z.infer<typeof schema>
```

**Form population** — `useEffect` on `teacher`:
```ts
useEffect(() => {
  if (teacher) {
    reset({
      firstName:   teacher.firstName,
      lastName:    teacher.lastName,
      joiningDate: teacher.joiningDate,
      phone:       teacher.phone ?? '',
      isActive:    teacher.isActive,
    })
  }
}, [teacher, reset])
```

**Modal header:**
```tsx
<DialogHeader>
  <DialogTitle>Edit Teacher</DialogTitle>
  {teacher && (
    <p className="text-xs text-muted-foreground font-mono mt-0.5">
      {teacher.teacherCode}
    </p>
  )}
</DialogHeader>
```

**Email display** — show the teacher's email as read-only text below the header, above the form fields. It is never a form input:
```tsx
{teacher && (
  <p className="text-sm text-muted-foreground -mt-2 mb-2">{teacher.email}</p>
)}
```

**Form layout:**
```
Row 1: First Name | Last Name          (grid-cols-2)
Row 2: Joining Date | Phone            (grid-cols-2)
Row 3: Active status                   (full width — checkbox or toggle)
```

**`isActive` field** — use a shadcn `Checkbox` wired with `Controller`:
```tsx
<Controller
  name="isActive"
  control={control}
  render={({ field }) => (
    <div className="flex items-center gap-2">
      <Checkbox
        id="isActive"
        checked={field.value}
        onCheckedChange={field.onChange}
      />
      <label htmlFor="isActive" className="text-sm text-foreground cursor-pointer">
        Active (unchecking disables login)
      </label>
    </div>
  )}
/>
```

**Loading state** — spinner while `isLoading`, same as edit student modal.

**Mutation:**
```ts
const mutation = useMutation({
  mutationFn: (data: FormValues) => teachersApi.update(teacherId!, {
    firstName:   data.firstName,
    lastName:    data.lastName,
    joiningDate: data.joiningDate,
    phone:       data.phone || undefined,
    isActive:    data.isActive,
  }),
  onSuccess: () => {
    toast.success('Teacher updated')
    onUpdated()
    onClose()
  },
  onError: (err) => toast.error(extractError(err)),
})
```

**Submit button:** `isSubmitting ? 'Saving…' : 'Save Changes'`

### Route wiring — `src/pages/admin/index.tsx`

```tsx
import { Routes, Route } from 'react-router-dom'
import { AcademicYearsPage } from './academic-years/AcademicYearsPage'
import { GradesPage } from './grades/GradesPage'
import { StudentsPage } from './students/StudentsPage'
import { TeachersPage } from './teachers/TeachersPage'

export function AdminRoutes() {
  return (
    <Routes>
      <Route path="academic-years" element={<AcademicYearsPage />} />
      <Route path="grades" element={<GradesPage />} />
      <Route path="students" element={<StudentsPage />} />
      <Route path="teachers" element={<TeachersPage />} />
    </Routes>
  )
}
```

### Sidebar nav — `src/layouts/AppShell.tsx`

Add a `Teachers` entry to `NAV_ITEMS`. Import `UsersRound` from `lucide-react`:

```ts
{
  label: 'Teachers',
  to: '/admin/teachers',
  icon: <UsersRound size={18} />,
  roles: ['Admin'],
},
```

Position it after Students in the array.

---

## Project Structure

New and modified files introduced by this spec:

```
backend/
  SchoolMgmt.Application/
    Teachers/
      ITeacherRepository.cs          # modified — add `search` param to GetPagedAsync signature
      TeacherService.cs              # modified — thread `search` through GetTeachersAsync

  SchoolMgmt.Infrastructure/
    Persistence/
      Repositories/
        TeacherRepository.cs         # modified — ILIKE filter in GetPagedAsync

  SchoolMgmt.WebApi/
    Controllers/
      TeachersController.cs          # modified — add [FromQuery] string? search param

frontend/src/
  api/
    teachers.ts                      # new — DTOs, TEACHER_KEYS, teachersApi

  pages/
    admin/
      teachers/
        TeachersPage.tsx             # new — table, tabs, search, pagination, modals
        components/
          CreateTeacherModal.tsx     # new — RHF + Zod create form (email + password)
          EditTeacherModal.tsx       # new — RHF + Zod edit form, fetches full TeacherDto
        __tests__/
          TeachersPage.test.tsx      # new
          CreateTeacherModal.test.tsx # new
          EditTeacherModal.test.tsx  # new
      index.tsx                      # modified — add teachers route

  layouts/
    AppShell.tsx                     # modified — add Teachers nav item
```

---

## Testing Strategy

Same stack: Vitest + React Testing Library, `vi.mock` for API modules, per-test `QueryClient`.

### Backend integration tests

Two new cases added to the teacher integration tests:

| Test | Setup | Assert |
|---|---|---|
| `GET /api/teachers?search=nguyen` matches by name | Seed "Nguyen Van A" and "Tran Thi B" teachers | Returns only the Nguyen teacher |
| `GET /api/teachers?search=@school` matches by email | Seed teacher with email `alice@school.edu` | Returns that teacher |
| `GET /api/teachers?search=xyz` no match | Seeded teachers don't match "xyz" | Returns empty `items`, `totalCount: 0` |

### Frontend unit tests

#### `TeachersPage.test.tsx`

| Test | Setup | Assert |
|---|---|---|
| Renders table with teacher rows | `teachersApi.list` returns 2 teachers | Both names visible in rows |
| Empty state when no teachers match | `teachersApi.list` returns `{ items: [], totalCount: 0 }` | "No teachers found." cell visible |
| Clicking "Inactive" tab changes isActive param | Click "Inactive" tab | `teachersApi.list` called with `isActive: false` |
| Clicking "All" tab omits isActive param | Click "All" tab | `teachersApi.list` called with `isActive: null` |
| Search input debounces | Type "nguyen", advance timers 300 ms | `teachersApi.list` called with `search: 'nguyen'` |
| Pagination Prev/Next | `totalCount: 45`, `pageSize: 20` | Next enabled page 1; Prev disabled page 1 |
| Clicking Edit opens edit modal | Click pencil on a row | `EditTeacherModal` rendered with correct `teacherId` |
| Clicking "Add Teacher" opens create modal | Click header button | `CreateTeacherModal` rendered with `open={true}` |

#### `CreateTeacherModal.test.tsx`

| Test | Setup | Assert |
|---|---|---|
| Submits correct payload | Fill all required fields | `teachersApi.create` called with correct shape |
| Phone omitted when blank | Leave phone empty | `phone` absent in payload |
| Validation blocks short password | Submit with 5-char password | Error message shown; `teachersApi.create` not called |
| Validation blocks invalid email | Submit with "notanemail" | Email error shown |
| Disables submit while pending | Mock mutation in-flight | Submit button disabled |
| Closes and resets on success | Mock successful mutation | `onClose` called; fields cleared |

#### `EditTeacherModal.test.tsx`

| Test | Setup | Assert |
|---|---|---|
| Shows loading state while fetching | `teachersApi.getById` pending | Spinner visible |
| Populates form from fetched teacher | `teachersApi.getById` returns `TeacherDto` | Fields pre-filled |
| TeacherCode shown as read-only text | `teacherCode: 'T2025-000001'` returned | Visible as text; no input with that value |
| Email shown as read-only text | `email: 'alice@school.edu'` returned | Visible as text; no input with that value |
| isActive checkbox reflects current status | `isActive: false` | Checkbox unchecked |
| Submits correct UpdateTeacherRequest | Fill form, submit | `teachersApi.update` called; no email/password/teacherCode in payload |
| Deactivating shows label hint | Uncheck isActive | Label "unchecking disables login" visible |

All tests mock `teachersApi` at the module boundary — no real HTTP calls.

---

## Commands

```bash
# Frontend dev server
npm run dev

# Frontend tests
npm run test

# Frontend build (TypeScript check)
npm run build

# Backend build + all tests
dotnet test SchoolMgmt.slnx
```

---

## Boundaries

**Always:**
- Invalidate `['teachers']` prefix after any successful mutation — catches both `list` and `detail` keys
- Keep `TeacherCode` and `Email` out of the `UpdateTeacherRequest` payload — neither is editable via this API
- Use `keepPreviousData` on the list query
- Reset `page` to `1` on tab or search change
- Use `enabled: teacherId !== null` on the `getById` query
- Pass `isActive` only when the tab is "Active" or "Inactive" — omit it entirely for the "All" tab (no `?isActive=` in query string)

**Ask first:**
- Adding email/password update UI (auth concern — currently out of scope)
- Adding a dedicated teacher detail page at `/admin/teachers/:id`
- Adding client-side sorting to table columns (requires backend `?sortBy=` / `?sortDir=` params)

**Never:**
- Send `teacherCode` or `email` in a `PUT /api/teachers/{id}` body
- Add a Delete button — no DELETE route exists and teacher deactivation is the intended lifecycle action
- Skip the `enabled` guard on `getById` — a blank ID resolves to the list endpoint

---

## Success Criteria

- `GET /admin/teachers` (logged in as Admin) renders the Teachers table; unauthenticated requests redirect to `/login`
- "Active" tab (default) shows only active teachers; "Inactive" shows inactive; "All" shows everyone
- Typing in the search box triggers a debounced API call with `?search=`; results update after 300 ms
- Prev/Next pagination works; "Page X of Y" is accurate; Prev disabled on page 1
- Clicking "Add Teacher" opens the create modal; submitting a valid form creates a teacher and the list refreshes
- Duplicate email shows the backend's "Email already in use." error message in a toast
- The create modal has no `isActive` or `TeacherCode` field
- Clicking the edit pencil opens the edit modal pre-populated with the teacher's data
- The edit modal displays `TeacherCode` and `Email` as read-only (not inputs); neither appears in the PUT payload
- Unchecking "Active" and saving causes the teacher to disappear from the Active tab; they appear in Inactive
- All unit tests pass (`npm run test` from `frontend/`)
- All backend integration tests pass (`dotnet test SchoolMgmt.slnx`)
- `npm run build` succeeds with no TypeScript errors
