# Spec: Implement Class / Section Structure — Frontend

## Related docs & specs

- [docs/ideas/04-class-section-frontend.md](../docs/ideas/04-class-section-frontend.md) — UX decisions: accordion layout, inline section editing, two-phase grade+section creation, proactive delete guard
- [docs/ideas/02-class-section-structure.md](../docs/ideas/02-class-section-structure.md) — domain rationale: persistent catalog, grade-empty delete guard, per-grade section uniqueness
- [specs/04-implement-class-section-structure.md](04-implement-class-section-structure.md) — backend spec: API routes, DTOs, error codes (`409` name conflict, `404` not found, `400` grade has sections)
- [specs/02-B-scaffold-frontend-and-auth.md](02-B-scaffold-frontend-and-auth.md) — frontend foundation: Axios instance, TanStack Query, React Hook Form + Zod, AppShell + NAV_ITEMS, RoleRoute

## Objective

Build the Admin-only Grades page (`/admin/grades`) that lets school admins manage their Grade/Section catalog: create and edit grades, add/rename/delete sections inline, and delete empty grades. The design is an accordion list — one card per grade, sorted by `DisplayOrder` — with sections managed inline inside the expanded card via clickable chips. All data operations follow the same React Query + Axios patterns already established by the Academic Year page.

**Out of scope:** drag-and-drop reordering, bulk section creation, section ordering within a grade, any role other than Admin.

## Tech Stack

Same as [specs/02-B-scaffold-frontend-and-auth.md](02-B-scaffold-frontend-and-auth.md). Two additional shadcn/ui components are needed:

```bash
# Run from frontend/
npx shadcn@latest add accordion badge tooltip
```

| Component | Used for |
|---|---|
| `Accordion` | Expandable grade cards |
| `Badge` | Section count chip in collapsed accordion header |
| `Tooltip` | Explain why the Delete Grade button is disabled when sections exist |

## Design

### API client — `src/api/grades.ts`

DTOs mirror the backend `GradeDto` / `SectionDto` response shapes exactly:

```ts
import api from './axios'

export interface SectionDto {
  id: string
  gradeId: string
  name: string
}

export interface GradeDto {
  id: string
  name: string
  displayOrder: number
  sections: SectionDto[]
}

export interface CreateGradeRequest {
  name: string
  displayOrder: number
}

export interface UpdateGradeRequest {
  name: string
  displayOrder: number
}

export interface CreateSectionRequest {
  name: string
}

export interface UpdateSectionRequest {
  name: string
}

export const GRADE_KEYS = {
  all: ['grades'] as const,
}

export const gradesApi = {
  list: () =>
    api.get<GradeDto[]>('/grades').then((r) => r.data),

  create: (body: CreateGradeRequest) =>
    api.post<GradeDto>('/grades', body).then((r) => r.data),

  update: (id: string, body: UpdateGradeRequest) =>
    api.put<GradeDto>(`/grades/${id}`, body).then((r) => r.data),

  delete: (id: string) =>
    api.delete(`/grades/${id}`),

  addSection: (gradeId: string, body: CreateSectionRequest) =>
    api.post<SectionDto>(`/grades/${gradeId}/sections`, body).then((r) => r.data),

  updateSection: (gradeId: string, sectionId: string, body: UpdateSectionRequest) =>
    api.put<SectionDto>(`/grades/${gradeId}/sections/${sectionId}`, body).then((r) => r.data),

  deleteSection: (gradeId: string, sectionId: string) =>
    api.delete(`/grades/${gradeId}/sections/${sectionId}`),
}
```

### Error extraction helper

Define this once in `GradesPage.tsx` and pass `extractError` to child components via props, or duplicate it locally in components that need it (same pattern as `AcademicYearsPage.tsx`):

```ts
function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}
```

### Page component — `src/pages/admin/grades/GradesPage.tsx`

**State:**
- `createOpen: boolean` — controls `CreateGradeModal` visibility
- `editingGrade: GradeDto | null` — non-null when `EditGradeModal` is open
- `expandedIds: string[]` — which grade accordions are currently open; controls the shadcn `Accordion value` prop

**Data fetching:**
```ts
const { data: grades = [], isLoading, isError } = useQuery({
  queryKey: GRADE_KEYS.all,
  queryFn: gradesApi.list,
})
```

**Invalidation helper:** `const invalidate = () => queryClient.invalidateQueries({ queryKey: GRADE_KEYS.all })`

**Grade mutations (owned by the page):**
- `deleteMutation` — calls `gradesApi.delete(id)`, invalidates on success, toasts error
- Grade create/update mutations live inside their respective modal components (same pattern as `CreateYearModal`)

**Auto-expand after create:** `CreateGradeModal` calls `onCreated(grade.id)` on success. The page appends the new grade's id to `expandedIds`:
```ts
const handleCreated = (id: string) => setExpandedIds((prev) => [...prev, id])
```

**Layout:**
```tsx
<div className="px-8 py-8 max-w-4xl mx-auto">
  {/* Page header */}
  <div className="flex items-start justify-between mb-8">
    <div>
      <h1 className="font-heading text-2xl font-semibold text-foreground">Grades & Sections</h1>
      <p className="text-sm text-muted-foreground mt-1">
        Define the grade and section structure for your school.
      </p>
    </div>
    <Button onClick={() => setCreateOpen(true)}>
      <Plus size={16} className="mr-2" /> Add Grade
    </Button>
  </div>

  {/* Empty state */}
  {grades.length === 0 && !isLoading && (
    <div className="flex flex-col items-center justify-center py-20 text-center">
      <p className="text-muted-foreground text-sm mb-4">
        No grades yet. Add your first grade to get started.
      </p>
      <Button variant="outline" onClick={() => setCreateOpen(true)}>
        <Plus size={15} className="mr-2" /> Add Grade
      </Button>
    </div>
  )}

  {/* Accordion list */}
  <Accordion
    type="multiple"
    value={expandedIds}
    onValueChange={setExpandedIds}
    className="flex flex-col gap-2"
  >
    {grades.map((grade) => (
      <GradeAccordionItem
        key={grade.id}
        grade={grade}
        onEdit={() => setEditingGrade(grade)}
        onDelete={() => {
          if (window.confirm(`Delete grade "${grade.name}"? This cannot be undone.`))
            deleteMutation.mutate(grade.id)
        }}
      />
    ))}
  </Accordion>

  <CreateGradeModal open={createOpen} onClose={() => setCreateOpen(false)} onCreated={handleCreated} />
  <EditGradeModal grade={editingGrade} onClose={() => setEditingGrade(null)} />
</div>
```

**Loading and error states:** same inline pattern as `AcademicYearsPage.tsx` — centered text in a `h-48` container.

### Grade accordion item — `src/pages/admin/grades/components/GradeAccordionItem.tsx`

Uses shadcn `AccordionItem`, `AccordionTrigger`, `AccordionContent`. Manages its own section-level state.

**Props:**
```ts
interface GradeAccordionItemProps {
  grade: GradeDto
  onEdit: () => void
  onDelete: () => void
}
```

**Local state:**
- `addingSectionOpen: boolean` — whether the inline "add section" input is visible
- `newSectionName: string` — controlled value for the add-section input

**Section mutations (local to this component):**
- `addSectionMutation` — calls `gradesApi.addSection(grade.id, { name })`, invalidates `GRADE_KEYS.all`, resets `newSectionName` and closes the input on success
- Individual section rename/delete are handled inside `SectionChip` (see below)

**Accordion trigger (collapsed header):**
```tsx
<AccordionTrigger className="hover:no-underline">
  <div className="flex items-center gap-3">
    <span className="font-medium text-foreground">{grade.name}</span>
    <Badge variant="secondary">{grade.sections.length} section{grade.sections.length !== 1 ? 's' : ''}</Badge>
  </div>
</AccordionTrigger>
```

**Accordion content (expanded body):**
```tsx
<AccordionContent>
  <div className="pt-3 pb-4 px-1 flex flex-col gap-4">
    {/* Section chips row */}
    <div className="flex flex-wrap items-center gap-2">
      {grade.sections.map((section) => (
        <SectionChip key={section.id} section={section} gradeId={grade.id} />
      ))}

      {/* Inline add section */}
      {addingSectionOpen ? (
        <form
          onSubmit={(e) => {
            e.preventDefault()
            if (newSectionName.trim()) addSectionMutation.mutate(newSectionName.trim())
          }}
          className="flex items-center gap-1"
        >
          <input
            autoFocus
            value={newSectionName}
            onChange={(e) => setNewSectionName(e.target.value)}
            placeholder="Name"
            maxLength={50}
            className="h-7 w-24 rounded-md border border-border bg-background px-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
          />
          <Button type="submit" size="sm" variant="ghost" disabled={addSectionMutation.isPending}>
            <Check size={14} />
          </Button>
          <Button
            type="button"
            size="sm"
            variant="ghost"
            onClick={() => { setAddingSectionOpen(false); setNewSectionName('') }}
          >
            <X size={14} />
          </Button>
        </form>
      ) : (
        <Button
          type="button"
          size="sm"
          variant="ghost"
          className="h-7 text-muted-foreground"
          onClick={() => setAddingSectionOpen(true)}
        >
          <Plus size={13} className="mr-1" /> Add Section
        </Button>
      )}
    </div>

    {/* Grade actions row */}
    <div className="flex items-center gap-2">
      <Button size="sm" variant="outline" onClick={onEdit}>
        <Pencil size={13} className="mr-1.5" /> Edit Grade
      </Button>

      {/* Delete grade — disabled with tooltip when grade has sections */}
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <span>
              <Button
                size="sm"
                variant="ghost"
                className="text-destructive hover:text-destructive"
                disabled={grade.sections.length > 0}
                onClick={onDelete}
              >
                <Trash2 size={13} className="mr-1.5" /> Delete Grade
              </Button>
            </span>
          </TooltipTrigger>
          {grade.sections.length > 0 && (
            <TooltipContent>Delete all sections first</TooltipContent>
          )}
        </Tooltip>
      </TooltipProvider>
    </div>
  </div>
</AccordionContent>
```

### Section chip — `src/pages/admin/grades/components/SectionChip.tsx`

Self-contained chip that toggles between display and inline-edit mode. Owns its own mutations.

**Props:**
```ts
interface SectionChipProps {
  section: SectionDto
  gradeId: string
}
```

**Local state:**
- `editing: boolean`
- `value: string` — initialized to `section.name`; reset to `section.name` on cancel

**Mutations:**
- `renameMutation` — `gradesApi.updateSection(gradeId, section.id, { name: value })` → invalidate `GRADE_KEYS.all`, close edit mode, toast success
- `deleteMutation` — `gradesApi.deleteSection(gradeId, section.id)` → invalidate `GRADE_KEYS.all`, toast success

**Render — display mode:**
```tsx
<button
  onClick={() => setEditing(true)}
  className="inline-flex h-7 items-center rounded-full border border-border bg-muted px-3 text-sm text-foreground hover:bg-accent transition-colors"
>
  {section.name}
</button>
```

**Render — edit mode:**
```tsx
<span className="inline-flex items-center gap-1">
  <input
    autoFocus
    value={value}
    onChange={(e) => setValue(e.target.value)}
    maxLength={50}
    className="h-7 w-20 rounded-md border border-ring bg-background px-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
    onKeyDown={(e) => {
      if (e.key === 'Enter') handleSave()
      if (e.key === 'Escape') handleCancel()
    }}
  />
  <Button size="sm" variant="ghost" className="h-7 w-7 p-0" onClick={handleSave} disabled={renameMutation.isPending}>
    <Check size={13} />
  </Button>
  <Button size="sm" variant="ghost" className="h-7 w-7 p-0" onClick={handleCancel}>
    <X size={13} />
  </Button>
  <Button
    size="sm"
    variant="ghost"
    className="h-7 w-7 p-0 text-destructive hover:text-destructive"
    onClick={() => {
      if (window.confirm(`Delete section "${section.name}"?`)) deleteMutation.mutate()
    }}
    disabled={deleteMutation.isPending}
  >
    <Trash2 size={13} />
  </Button>
</span>
```

`handleSave`: if `value.trim()` is empty or unchanged, call `handleCancel`. Otherwise call `renameMutation.mutate()`.
`handleCancel`: set `editing` to false, reset `value` to `section.name`.

### Create grade modal — `src/pages/admin/grades/components/CreateGradeModal.tsx`

Follows the exact structure of `CreateYearModal.tsx`.

**Props:**
```ts
interface CreateGradeModalProps {
  open: boolean
  onClose: () => void
  onCreated: (id: string) => void
}
```

**Zod schema:**
```ts
const schema = z.object({
  name: z.string().min(1, 'Required').max(100),
  displayOrder: z.coerce.number().int().min(0, 'Must be 0 or greater'),
})
```

**Fields:** Name (`<Input>`) + Display Order (`<Input type="number" min="0">`).

**`onSuccess`:** call `queryClient.invalidateQueries`, `toast.success('Grade created')`, `reset()`, `onCreated(grade.id)`, `onClose()`.

**`onError`:** 409 → `toast.error('A grade with this name already exists.')`, otherwise `toast.error(extractError(err))`.

**Submit button label:** `isSubmitting ? 'Creating…' : 'Create Grade'`

### Edit grade modal — `src/pages/admin/grades/components/EditGradeModal.tsx`

Mirrors `CreateGradeModal` but receives the existing `GradeDto` and calls `gradesApi.update`.

**Props:**
```ts
interface EditGradeModalProps {
  grade: GradeDto | null
  onClose: () => void
}
```

`open` is derived: `grade !== null`. Reset form via `useEffect` on `grade` change (same pattern as `EditSemesterModal`):
```ts
useEffect(() => {
  if (grade) reset({ name: grade.name, displayOrder: grade.displayOrder })
}, [grade, reset])
```

**`onSuccess`:** invalidate, `toast.success('Grade updated')`, `onClose()`.

**`onError`:** same 409 / generic handling as create modal.

**Submit button label:** `isSubmitting ? 'Saving…' : 'Save Changes'`

### Route wiring — `src/pages/admin/index.tsx`

Add the grades route alongside `academic-years`:

```tsx
import { Routes, Route } from 'react-router-dom'
import { AcademicYearsPage } from './academic-years/AcademicYearsPage'
import { GradesPage } from './grades/GradesPage'

export function AdminRoutes() {
  return (
    <Routes>
      <Route path="academic-years" element={<AcademicYearsPage />} />
      <Route path="grades" element={<GradesPage />} />
    </Routes>
  )
}
```

### Sidebar nav — `src/layouts/AppShell.tsx`

Add a `Grades & Sections` entry to `NAV_ITEMS`. Import `Layers` from `lucide-react`:

```ts
{
  label: 'Grades & Sections',
  to: '/admin/grades',
  icon: <Layers size={18} />,
  roles: ['Admin'],
},
```

Position it after Academic Years in the array.

## Project Structure

New files introduced by this spec:

```
frontend/src/
  api/
    grades.ts                                     # new — DTOs, GRADE_KEYS, gradesApi
  pages/
    admin/
      grades/
        GradesPage.tsx                            # new — page: query, mutations, accordion, modals
        components/
          GradeAccordionItem.tsx                  # new — accordion card with inline section management
          SectionChip.tsx                         # new — display/edit chip with inline rename + delete
          CreateGradeModal.tsx                    # new — create grade form modal
          EditGradeModal.tsx                      # new — edit grade form modal
      index.tsx                                   # modified — add grades route

layouts/
  AppShell.tsx                                    # modified — add Grades & Sections nav item
```

No new dependencies beyond `npx shadcn@latest add accordion badge tooltip`.

## Testing Strategy

Same stack: Vitest + React Testing Library, `vi.mock` for API module, per-test `QueryClient`.

### Unit tests — `src/pages/admin/grades/__tests__/GradesPage.test.tsx`

| Test | Setup | Assert |
|---|---|---|
| Renders empty state when no grades | `gradesApi.list` returns `[]` | "No grades yet." message visible |
| Renders grade list with section counts | `gradesApi.list` returns two grades | Both grade names and their section counts appear |
| Accordion expands on click | Click a grade accordion trigger | Section chips become visible |
| Disabled delete button when grade has sections | Grade with 2 sections | Delete Grade button has `disabled` attribute |
| Enabled delete button when grade is empty | Grade with 0 sections | Delete Grade button is not disabled |
| Opens create modal on "Add Grade" click | Click header button | `CreateGradeModal` is rendered with `open={true}` |

### Unit tests — `src/pages/admin/grades/__tests__/CreateGradeModal.test.tsx`

| Test | Setup | Assert |
|---|---|---|
| Submits correct payload | Fill name + displayOrder, submit | `gradesApi.create` called with `{ name, displayOrder }` |
| Shows error on 409 | `gradesApi.create` rejects with 409 | Toast contains "already exists" |
| Disables submit while pending | Mock mutation in-flight | Submit button is disabled |

### Unit tests — `src/pages/admin/grades/__tests__/SectionChip.test.tsx`

| Test | Setup | Assert |
|---|---|---|
| Renders section name as button | Render chip | Button with section name visible |
| Click chip enters edit mode | Click chip button | Input becomes visible with section name pre-filled |
| Escape key cancels edit | Enter edit mode, press Escape | Input hidden, chip restored |
| Save calls updateSection | Edit name, click save | `gradesApi.updateSection` called with new name |
| Delete with confirm calls deleteSection | Click delete, confirm dialog | `gradesApi.deleteSection` called |

All tests mock `gradesApi` at the module boundary — no real HTTP calls.

## Boundaries

**Always:**
- Call all mutations through `gradesApi` on the shared Axios instance — never `fetch` directly
- Invalidate `GRADE_KEYS.all` on every successful mutation — a single cache key covers the whole list
- Disable the Delete Grade button (not just hide it) when `grade.sections.length > 0` — proactive guard, not a reliance on the 400 response
- Reset the edit-grade form via `useEffect` on the `grade` prop change — prevents stale values when switching between grades

**Ask first:**
- Adding drag-and-drop reordering of grades (requires a dnd library and a bulk `PATCH` backend endpoint)
- Adding section ordering within a grade (sections have no `displayOrder` on the backend — needs a schema change)
- Using an `AlertDialog` instead of `window.confirm` for delete confirmations (consistent with the existing codebase pattern; upgrade both if upgrading either)

**Never:**
- Add a standalone section page or route — sections are always managed inline within the grade accordion
- Call `gradesApi` endpoints with a `gradeId` that isn't confirmed to exist client-side (always read `grade.id` from the already-fetched `GradeDto`)
- Let a 400 response from the backend be the first indication to the admin that grade delete is blocked — the UI must prevent the attempt entirely via the disabled button

## Success Criteria

- `GET /admin/grades` (logged in as Admin) renders the Grades & Sections page with no console errors; unauthenticated requests redirect to `/login`
- When no grades exist, the empty state is shown with an "Add Grade" prompt
- Creating a grade via the modal adds it to the list and auto-expands its accordion
- Clicking "+ Add Section" inside an expanded grade shows an inline input; submitting adds the section chip
- Clicking a section chip enters edit mode; saving renames the chip; Escape cancels; the delete button inside edit mode removes the section after `window.confirm`
- The Delete Grade button is disabled (and shows a tooltip) when the grade has sections; clicking it after sections are removed shows a `window.confirm` and then removes the grade
- Toast messages appear on all successful and failed mutations
- All unit tests pass (`npm run test` from `frontend/`)
- `npm run build` succeeds with no TypeScript errors
