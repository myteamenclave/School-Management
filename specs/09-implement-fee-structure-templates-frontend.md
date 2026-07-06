# Spec: Implement Fee Structure Templates — Frontend

## Related docs & specs

- [docs/ideas/08-B-fee-structure-templates-frontend.md](../docs/ideas/08-B-fee-structure-templates-frontend.md) — idea doc: UX decisions, view/edit mode, browser guard, not-doing list
- [docs/ideas/07-fee-structure-templates.md](../docs/ideas/07-fee-structure-templates.md) — source idea: template composition model, discount rule targeting, installment sum rule
- [specs/08-implement-fee-structure-templates.md](08-implement-fee-structure-templates.md) — backend spec: all 7 endpoints, DTO shapes, replace semantics, validation rules
- [specs/07-B-implement-teacher-crud-frontend.md](07-B-implement-teacher-crud-frontend.md) — primary frontend pattern reference: API client shape, TanStack Query, React Hook Form + Zod, pagination, modal structure, testing approach
- [specs/02-B-scaffold-frontend-and-auth.md](02-B-scaffold-frontend-and-auth.md) — frontend foundation: Axios instance, TanStack Query setup, AppShell + NAV_ITEMS, RoleRoute

## Objective

Build the Admin-only Fee Templates section — a list page and a dedicated detail page — that lets school admins create fee structure templates and fully manage their three child collections (line items, installment schedules, discount rules).

This is the first feature with a dedicated detail route (`/admin/fee-templates/:id`). The detail page has two explicit modes:
- **View mode** (row click) — read-only display of header + child collections
- **Edit mode** (edit icon on row, or "Edit" button on view page) — inline header edit + three editable tabs with batch save per collection

**Out of scope:** per-student invoice generation, template duplication, drag-to-reorder, any role other than Admin.

## Tech Stack

Same as [specs/02-B-scaffold-frontend-and-auth.md](02-B-scaffold-frontend-and-auth.md). No new shadcn components needed — `Table`, `Select`, `Tabs`, `Dialog`, `Checkbox` are already installed.

---

## API Client — `src/api/feeTemplates.ts`

### Types

```ts
import api from './axios'
import type { AcademicYearDto } from './academicYears'
import type { GradeDto } from './grades'

// ---- Enum ----
export type DiscountRuleType = 'Percentage' | 'FlatAmount'

// ---- DTOs (mirror backend response shapes) ----
export interface FeeLineItemDto {
  id: string
  name: string
  amount: number
  displayOrder: number
}

export interface FeeInstallmentDto {
  id: string
  name: string
  percentage: number
  displayOrder: number
}

export interface DiscountRuleDto {
  id: string
  name: string
  ruleType: DiscountRuleType
  value: number
  feeLineItemId: string | null
  feeLineItemName: string | null
}

export interface FeeTemplateSummaryDto {
  id: string
  name: string
  academicYearId: string
  academicYearName: string
  gradeId: string
  gradeName: string
  totalAmount: number
  lineItemCount: number
  isActive: boolean
  createdAt: string
}

export interface FeeTemplateDto {
  id: string
  name: string
  academicYearId: string
  academicYearName: string
  gradeId: string
  gradeName: string
  totalAmount: number
  isActive: boolean
  createdAt: string
  updatedAt: string | null
  lineItems: FeeLineItemDto[]
  installments: FeeInstallmentDto[]
  discountRules: DiscountRuleDto[]
}

// ---- Request shapes ----
export interface CreateFeeTemplateRequest {
  name: string
  academicYearId: string
  gradeId: string
}

export interface UpdateFeeTemplateRequest {
  name: string
  isActive: boolean
}

export interface LineItemInput {
  id?: string        // present for existing items; absent for new items
  name: string
  amount: number
  displayOrder: number
}

export interface InstallmentInput {
  name: string
  percentage: number
  displayOrder: number
}

export interface DiscountRuleInput {
  name: string
  ruleType: DiscountRuleType
  value: number
  feeLineItemId?: string   // absent = applies to invoice total
}

// ---- List params ----
export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface ListFeeTemplatesParams {
  isActive: boolean | null   // null = all
  academicYearId: string | null
  gradeId: string | null
  page: number
  pageSize: number
}
```

### Query keys

```ts
export const FEE_TEMPLATE_KEYS = {
  list: (p: ListFeeTemplatesParams) => ['fee-templates', 'list', p] as const,
  detail: (id: string) => ['fee-templates', 'detail', id] as const,
}
```

### API functions

```ts
export const feeTemplatesApi = {
  list: (params: ListFeeTemplatesParams) => {
    const q: Record<string, unknown> = { page: params.page, pageSize: params.pageSize }
    if (params.isActive !== null) q.isActive = params.isActive
    if (params.academicYearId) q.academicYearId = params.academicYearId
    if (params.gradeId) q.gradeId = params.gradeId
    return api
      .get<PagedResult<FeeTemplateSummaryDto>>('/fee-templates', { params: q })
      .then((r) => r.data)
  },

  getById: (id: string) =>
    api.get<FeeTemplateDto>(`/fee-templates/${id}`).then((r) => r.data),

  create: (body: CreateFeeTemplateRequest) =>
    api.post<FeeTemplateDto>('/fee-templates', body).then((r) => r.data),

  updateHeader: (id: string, body: UpdateFeeTemplateRequest) =>
    api.put<FeeTemplateDto>(`/fee-templates/${id}`, body).then((r) => r.data),

  replaceLineItems: (id: string, items: LineItemInput[]) =>
    api
      .put<FeeTemplateDto>(`/fee-templates/${id}/line-items`, { items })
      .then((r) => r.data),

  replaceInstallments: (id: string, items: InstallmentInput[]) =>
    api
      .put<FeeTemplateDto>(`/fee-templates/${id}/installments`, { items })
      .then((r) => r.data),

  replaceDiscountRules: (id: string, items: DiscountRuleInput[]) =>
    api
      .put<FeeTemplateDto>(`/fee-templates/${id}/discount-rules`, { items })
      .then((r) => r.data),
}
```

---

## FeeTemplatesPage — `src/pages/admin/fee-templates/FeeTemplatesPage.tsx`

### State

```ts
type StatusTab = 'Active' | 'Inactive' | 'All'
const STATUS_TABS: StatusTab[] = ['Active', 'Inactive', 'All']

function tabToIsActive(tab: StatusTab): boolean | null {
  if (tab === 'Active') return true
  if (tab === 'Inactive') return false
  return null
}
```

- `tab: StatusTab` — default `'Active'`
- `academicYearFilter: string | null` — default `null` (all years)
- `gradeFilter: string | null` — default `null` (all grades)
- `page: number` — default `1`; reset to `1` on any filter change
- `createOpen: boolean`

No search input — the list is filtered by Academic Year + Grade dropdowns instead.

### Data fetching

```ts
const queryParams: ListFeeTemplatesParams = {
  isActive: tabToIsActive(tab),
  academicYearId: academicYearFilter,
  gradeId: gradeFilter,
  page,
  pageSize: 20,
}

const { data, isLoading, isError } = useQuery({
  queryKey: FEE_TEMPLATE_KEYS.list(queryParams),
  queryFn: () => feeTemplatesApi.list(queryParams),
  placeholderData: keepPreviousData,
})
```

For the filter dropdowns, reuse existing data already cached by other pages:

```ts
const { data: academicYears } = useQuery({
  queryKey: ACADEMIC_YEAR_KEYS.all,
  queryFn: academicYearsApi.list,
  staleTime: Infinity,   // academic years rarely change during a session
})

const { data: grades } = useQuery({
  queryKey: GRADE_KEYS.all,
  queryFn: gradesApi.list,
  staleTime: Infinity,
})
```

### Layout

```tsx
<div className="px-8 py-8 max-w-6xl mx-auto">
  {/* Page header */}
  <div className="flex items-start justify-between mb-6">
    <div>
      <h1 className="font-heading text-2xl font-semibold text-foreground">Fee Templates</h1>
      <p className="text-sm text-muted-foreground mt-1">
        Define reusable fee structures per grade and academic year.
      </p>
    </div>
    <Button onClick={() => setCreateOpen(true)}>
      <Plus size={16} className="mr-2" /> New Template
    </Button>
  </div>

  {/* Filters row: status tabs + year/grade dropdowns */}
  <div className="flex items-center justify-between gap-4 mb-4 flex-wrap">
    <Tabs value={tab} onValueChange={(v) => { setTab(v as StatusTab); setPage(1) }}>
      <TabsList>
        {STATUS_TABS.map((t) => <TabsTrigger key={t} value={t}>{t}</TabsTrigger>)}
      </TabsList>
    </Tabs>

    <div className="flex items-center gap-2">
      {/* Academic Year dropdown */}
      <Select
        value={academicYearFilter ?? 'all'}
        onValueChange={(v) => { setAcademicYearFilter(v === 'all' ? null : v); setPage(1) }}
      >
        <SelectTrigger className="w-44">
          <SelectValue placeholder="All Years" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="all">All Years</SelectItem>
          {academicYears?.map((y) => (
            <SelectItem key={y.id} value={y.id}>{y.name}</SelectItem>
          ))}
        </SelectContent>
      </Select>

      {/* Grade dropdown */}
      <Select
        value={gradeFilter ?? 'all'}
        onValueChange={(v) => { setGradeFilter(v === 'all' ? null : v); setPage(1) }}
      >
        <SelectTrigger className="w-36">
          <SelectValue placeholder="All Grades" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="all">All Grades</SelectItem>
          {grades?.map((g) => (
            <SelectItem key={g.id} value={g.id}>{g.name}</SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  </div>

  {/* Table */}
  {/* ... loading / error / empty states ... */}
</div>
```

### Table columns

| Column | Source | Notes |
|---|---|---|
| Name | `name` | `font-medium`; clicking row navigates to `/admin/fee-templates/${id}` |
| Grade | `gradeName` | `text-sm text-muted-foreground` |
| Academic Year | `academicYearName` | `text-sm text-muted-foreground` |
| Total | `totalAmount` | `font-mono text-sm`; formatted as currency (e.g. `₱12,000.00`) |
| Line Items | `lineItemCount` | `text-sm text-muted-foreground` |
| Status | `isActive` | `<StatusBadge />` |
| — | — | Edit pencil icon; navigates to `/admin/fee-templates/${id}?edit=true` |

**Row click** → `navigate(`/admin/fee-templates/${id}`)` (view mode)
**Edit icon click** → `navigate(`/admin/fee-templates/${id}?edit=true`)` (edit mode)

**`StatusBadge` helper** — same pattern as `TeachersPage.tsx`:

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

**Currency formatter** — inline helper:

```ts
const currencyFmt = new Intl.NumberFormat('en-PH', {
  style: 'currency',
  currency: 'PHP',
  minimumFractionDigits: 2,
})
```

**Pagination** — identical to `TeachersPage.tsx` Prev/Next pattern; hidden when `totalPages <= 1`.

---

## CreateFeeTemplateModal — `src/pages/admin/fee-templates/components/CreateFeeTemplateModal.tsx`

### Props

```ts
interface CreateFeeTemplateModalProps {
  open: boolean
  onClose: () => void
}
```

On success, the modal closes itself and navigates to the new template's detail page in edit mode — `onCreated` callback is not needed because the navigation replaces it.

### Zod schema

```ts
const schema = z.object({
  name:           z.string().min(1, 'Required').max(200),
  academicYearId: z.string().min(1, 'Required'),
  gradeId:        z.string().min(1, 'Required'),
})
type FormValues = z.infer<typeof schema>
```

### Form layout

```
Row 1: Template Name     (full width)
Row 2: Academic Year     (full width — Select dropdown)
Row 3: Grade             (full width — Select dropdown)
```

Both dropdowns fetch from the same cached `useQuery` calls described in `FeeTemplatesPage`.

### Mutation

```ts
const mutation = useMutation({
  mutationFn: feeTemplatesApi.create,
  onSuccess: (created) => {
    queryClient.invalidateQueries({ queryKey: ['fee-templates'] })
    toast.success('Template created')
    reset()
    onClose()
    navigate(`/admin/fee-templates/${created.id}?edit=true`)
  },
  onError: (err) => toast.error(extractError(err)),
})
```

**409 (duplicate name):** `extractError` surfaces the backend message — no special handling.

**Submit button:** `isSubmitting ? 'Creating…' : 'Create Template'`

**Dialog max width:** `sm:max-w-md`

---

## FeeTemplatePage — `src/pages/admin/fee-templates/FeeTemplatePage.tsx`

### Routing and mode

Mode is determined by the `edit` search param:

```ts
const { id } = useParams<{ id: string }>()
const [searchParams, setSearchParams] = useSearchParams()
const isEditMode = searchParams.get('edit') === 'true'
```

- View mode: `searchParams.get('edit')` is absent or `'false'`
- Edit mode: `searchParams.get('edit') === 'true'`

Toggling modes:
```ts
const enterEditMode = () => setSearchParams({ edit: 'true' })
const exitEditMode  = () => setSearchParams({})
```

### Data fetching

```ts
const { data: template, isLoading, isError } = useQuery({
  queryKey: FEE_TEMPLATE_KEYS.detail(id!),
  queryFn: () => feeTemplatesApi.getById(id!),
})
```

### Unsaved changes guard

Track overall dirty state across all three tabs and the header form:

```ts
const [isDirty, setIsDirty] = useState(false)
```

Each child tab calls back `onDirtyChange(boolean)` when their local state diverges from the last saved state.

**In-app navigation block** via `useBlocker` (React Router v6.4+):

```ts
const blocker = useBlocker(
  ({ currentLocation, nextLocation }) =>
    isDirty && currentLocation.pathname !== nextLocation.pathname
)
```

When `blocker.state === 'blocked'`, render a confirmation dialog:

```tsx
{blocker.state === 'blocked' && (
  <ConfirmDiscardDialog
    open
    onConfirm={() => blocker.proceed?.()}
    onCancel={() => blocker.reset?.()}
  />
)}
```

`ConfirmDiscardDialog` is a small inline component using the existing `Dialog` primitives — title "Discard unsaved changes?", body "You have unsaved changes that will be lost.", Cancel + "Discard Changes" buttons.

**Browser-level guard** (tab close / address bar navigation):

```ts
useEffect(() => {
  if (!isDirty) return
  const handler = (e: BeforeUnloadEvent) => { e.preventDefault() }
  window.addEventListener('beforeunload', handler)
  return () => window.removeEventListener('beforeunload', handler)
}, [isDirty])
```

### Page layout

```tsx
<div className="px-8 py-8 max-w-5xl mx-auto">
  {/* Breadcrumb */}
  <nav className="flex items-center gap-2 text-sm text-muted-foreground mb-6">
    <button onClick={() => navigate('/admin/fee-templates')} className="hover:text-foreground transition-colors">
      Fee Templates
    </button>
    <ChevronRight size={14} />
    <span className="text-foreground font-medium">{template?.name ?? '…'}</span>
  </nav>

  {/* Inactive warning banner — shown in edit mode when isActive is false */}
  {isEditMode && template && !template.isActive && (
    <div className="mb-4 flex items-center gap-2 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800 dark:border-amber-800 dark:bg-amber-950/30 dark:text-amber-300">
      <AlertTriangle size={15} className="shrink-0" />
      This template is inactive and will not appear in the default list.
    </div>
  )}

  {/* Header card */}
  <TemplateHeaderSection
    template={template}
    isEditMode={isEditMode}
    onEnterEdit={enterEditMode}
    onDirtyChange={(dirty) => { /* merge into isDirty */ }}
  />

  {/* Child tabs */}
  <Tabs defaultValue="line-items" className="mt-6">
    <TabsList>
      <TabsTrigger value="line-items">
        Line Items {lineItemsDirty && <span className="ml-1 text-amber-500">●</span>}
      </TabsTrigger>
      <TabsTrigger value="installments">
        Installments {installmentsDirty && <span className="ml-1 text-amber-500">●</span>}
      </TabsTrigger>
      <TabsTrigger value="discount-rules">
        Discount Rules {discountRulesDirty && <span className="ml-1 text-amber-500">●</span>}
      </TabsTrigger>
    </TabsList>

    <TabsContent value="line-items">
      <LineItemsTab template={template} isEditMode={isEditMode} onDirtyChange={...} />
    </TabsContent>
    <TabsContent value="installments">
      <InstallmentsTab template={template} isEditMode={isEditMode} onDirtyChange={...} />
    </TabsContent>
    <TabsContent value="discount-rules">
      <DiscountRulesTab template={template} isEditMode={isEditMode} onDirtyChange={...} />
    </TabsContent>
  </Tabs>
</div>
```

### TemplateHeaderSection (inline component or sub-component)

**View mode:**
- Read-only display: Template name (h2), Grade + Academic Year badges, IsActive `StatusBadge`
- "Edit" button → `enterEditMode()`

**Edit mode:**
- Name input (full width)
- IsActive checkbox with label "Active"
- "Save Header" button (calls `feeTemplatesApi.updateHeader`) + "Cancel" button → `exitEditMode()` (with dirty check)
- Grade and Academic Year shown as read-only labels (not inputs — fixed at creation)

Header form uses `react-hook-form` with zod schema:

```ts
const headerSchema = z.object({
  name:     z.string().min(1, 'Required').max(200),
  isActive: z.boolean(),
})
```

On save:
```ts
const headerMutation = useMutation({
  mutationFn: (data: HeaderFormValues) =>
    feeTemplatesApi.updateHeader(id!, { name: data.name, isActive: data.isActive }),
  onSuccess: (updated) => {
    queryClient.setQueryData(FEE_TEMPLATE_KEYS.detail(id!), updated)
    queryClient.invalidateQueries({ queryKey: ['fee-templates', 'list'] })
    toast.success('Template updated')
    onDirtyChange(false)
  },
  onError: (err) => toast.error(extractError(err)),
})
```

---

## LineItemsTab — `src/pages/admin/fee-templates/components/LineItemsTab.tsx`

### Props

```ts
interface LineItemsTabProps {
  template: FeeTemplateDto | undefined
  isEditMode: boolean
  onDirtyChange: (dirty: boolean) => void
}
```

### Local state

```ts
// Local working copy; initialized from template.lineItems on mount / template change
const [localItems, setLocalItems] = useState<LineItemInput[]>([])
const [savedItems, setSavedItems] = useState<LineItemInput[]>([])

// Track dirty
const isDirty = JSON.stringify(localItems) !== JSON.stringify(savedItems)
useEffect(() => onDirtyChange(isDirty), [isDirty])

// Initialize when template loads
useEffect(() => {
  if (!template) return
  const items = template.lineItems.map((li) => ({
    id: li.id,
    name: li.name,
    amount: li.amount,
    displayOrder: li.displayOrder,
  }))
  setLocalItems(items)
  setSavedItems(items)
}, [template])
```

### View mode

Plain read-only table: Name · Amount (formatted as currency) · Display Order

### Edit mode

Editable table — each row has input cells for Name, Amount, Display Order, plus a delete icon button. Add new row button below the table appends a row with empty values (no `id`).

```tsx
{/* Row */}
<TableRow key={idx}>
  <TableCell>
    <Input
      value={item.name}
      onChange={(e) => updateItem(idx, 'name', e.target.value)}
      className="h-8"
    />
  </TableCell>
  <TableCell>
    <Input
      type="number"
      min={0}
      step="0.01"
      value={item.amount}
      onChange={(e) => updateItem(idx, 'amount', parseFloat(e.target.value) || 0)}
      className="h-8 w-28"
    />
  </TableCell>
  <TableCell>
    <Input
      type="number"
      value={item.displayOrder}
      onChange={(e) => updateItem(idx, 'displayOrder', parseInt(e.target.value) || 0)}
      className="h-8 w-16"
    />
  </TableCell>
  <TableCell>
    <Button size="sm" variant="ghost" onClick={() => removeItem(idx)}>
      <Trash2 size={14} className="text-destructive" />
    </Button>
  </TableCell>
</TableRow>
```

**"Add Line Item" button:**

```tsx
<Button size="sm" variant="outline" onClick={addItem}>
  <Plus size={14} className="mr-1" /> Add Line Item
</Button>
```

Appends: `{ name: '', amount: 0, displayOrder: localItems.length + 1 }`

### Save / Discard

```tsx
<div className="flex items-center justify-end gap-2 mt-4">
  <Button variant="outline" size="sm" onClick={handleDiscard}>Discard</Button>
  <Button
    size="sm"
    disabled={!isDirty || saveLineItemsMutation.isPending}
    onClick={handleSave}
  >
    {saveLineItemsMutation.isPending ? 'Saving…' : 'Save Changes'}
  </Button>
</div>
```

**Discard** resets `localItems` to `savedItems`.

**Save mutation:**

```ts
const saveLineItemsMutation = useMutation({
  mutationFn: (items: LineItemInput[]) => feeTemplatesApi.replaceLineItems(id!, items),
  onSuccess: (updated) => {
    queryClient.setQueryData(FEE_TEMPLATE_KEYS.detail(id!), updated)
    queryClient.invalidateQueries({ queryKey: ['fee-templates', 'list'] })
    const newItems = updated.lineItems.map(li => ({ id: li.id, name: li.name, amount: li.amount, displayOrder: li.displayOrder }))
    setLocalItems(newItems)
    setSavedItems(newItems)
    toast.success('Line items saved')
  },
  onError: (err) => toast.error(extractError(err)),
})
```

Shown only in edit mode.

---

## InstallmentsTab — `src/pages/admin/fee-templates/components/InstallmentsTab.tsx`

### Props

```ts
interface InstallmentsTabProps {
  template: FeeTemplateDto | undefined
  isEditMode: boolean
  onDirtyChange: (dirty: boolean) => void
}
```

### Local state

Same initialization pattern as `LineItemsTab`, using `InstallmentInput[]`.

### Percentage sum display

Always visible at the top of the tab (both modes):

```tsx
const totalPct = localItems.reduce((sum, i) => sum + (i.percentage || 0), 0)
const sumOk = localItems.length === 0 || Math.abs(totalPct - 100) < 0.01

<div className={`flex items-center gap-2 text-sm ${sumOk ? 'text-muted-foreground' : 'text-destructive'}`}>
  <span>Total: <strong>{totalPct.toFixed(2)}%</strong> / 100%</span>
  {!sumOk && localItems.length > 0 && (
    <span className="text-xs">(must equal 100%)</span>
  )}
</div>
```

### Edit mode

Editable table: Name · Percentage (%) · Display Order · Delete icon

**Save button disabled** when `!sumOk && localItems.length > 0`:

```tsx
<Button
  size="sm"
  disabled={!isDirty || saveInstallmentsMutation.isPending || (!sumOk && localItems.length > 0)}
  onClick={handleSave}
>
  {saveInstallmentsMutation.isPending ? 'Saving…' : 'Save Changes'}
</Button>
```

**Save mutation:** calls `feeTemplatesApi.replaceInstallments(id!, localItems)`.

---

## DiscountRulesTab — `src/pages/admin/fee-templates/components/DiscountRulesTab.tsx`

### Props

```ts
interface DiscountRulesTabProps {
  template: FeeTemplateDto | undefined
  isEditMode: boolean
  onDirtyChange: (dirty: boolean) => void
}
```

### Local state

Same initialization pattern, using `DiscountRuleInput[]`:

```ts
const [localItems, setLocalItems] = useState<DiscountRuleInput[]>([])
```

### Target Line Item dropdown

Populated from **last-saved** line items on the template (`template.lineItems`) — NOT from any unsaved local state in `LineItemsTab`. This is intentional: admins must save line items first.

```tsx
<Select
  value={item.feeLineItemId ?? 'none'}
  onValueChange={(v) => updateItem(idx, 'feeLineItemId', v === 'none' ? undefined : v)}
>
  <SelectTrigger className="w-44 h-8">
    <SelectValue placeholder="Invoice total" />
  </SelectTrigger>
  <SelectContent>
    <SelectItem value="none">Invoice total</SelectItem>
    {template?.lineItems.map((li) => (
      <SelectItem key={li.id} value={li.id}>{li.name}</SelectItem>
    ))}
  </SelectContent>
</Select>
```

`"none"` → `feeLineItemId: undefined` in the request (applies to invoice total).

### Rule Type + Value

```tsx
{/* Rule Type */}
<Select value={item.ruleType} onValueChange={(v) => updateItem(idx, 'ruleType', v as DiscountRuleType)}>
  <SelectTrigger className="w-32 h-8">
    <SelectValue />
  </SelectTrigger>
  <SelectContent>
    <SelectItem value="Percentage">%</SelectItem>
    <SelectItem value="FlatAmount">Flat</SelectItem>
  </SelectContent>
</Select>

{/* Value */}
<Input
  type="number"
  min={0.01}
  max={item.ruleType === 'Percentage' ? 100 : undefined}
  step="0.01"
  value={item.value}
  onChange={(e) => updateItem(idx, 'value', parseFloat(e.target.value) || 0)}
  className="h-8 w-24"
/>
```

### Save mutation

Calls `feeTemplatesApi.replaceDiscountRules(id!, localItems)`.

### View mode

Read-only table: Name · Type badge (`%` or `Flat`) · Value · Target (line item name or "Invoice total").

---

## Route registration — `src/pages/admin/index.tsx`

```tsx
import { Routes, Route } from 'react-router-dom'
import { AcademicYearsPage }  from './academic-years/AcademicYearsPage'
import { GradesPage }         from './grades/GradesPage'
import { StudentsPage }       from './students/StudentsPage'
import { TeachersPage }       from './teachers/TeachersPage'
import { SubjectsPage }       from './subjects/SubjectsPage'
import { FeeTemplatesPage }   from './fee-templates/FeeTemplatesPage'
import { FeeTemplatePage }    from './fee-templates/FeeTemplatePage'

export function AdminRoutes() {
  return (
    <Routes>
      <Route path="academic-years"        element={<AcademicYearsPage />} />
      <Route path="grades"                element={<GradesPage />} />
      <Route path="students"              element={<StudentsPage />} />
      <Route path="teachers"              element={<TeachersPage />} />
      <Route path="subjects"              element={<SubjectsPage />} />
      <Route path="fee-templates"         element={<FeeTemplatesPage />} />
      <Route path="fee-templates/:id"     element={<FeeTemplatePage />} />
    </Routes>
  )
}
```

## Sidebar nav — `src/layouts/AppShell.tsx`

Add after `Subjects`. Import `Receipt` from `lucide-react`:

```ts
{
  label: 'Fee Templates',
  to: '/admin/fee-templates',
  icon: <Receipt size={18} />,
  roles: ['Admin'],
},
```

---

## Project Structure

```
frontend/src/
  api/
    feeTemplates.ts                              # new

  pages/
    admin/
      fee-templates/
        FeeTemplatesPage.tsx                     # new — list page
        FeeTemplatePage.tsx                      # new — detail/edit page
        components/
          CreateFeeTemplateModal.tsx             # new
          LineItemsTab.tsx                       # new
          InstallmentsTab.tsx                    # new
          DiscountRulesTab.tsx                   # new
        __tests__/
          FeeTemplatesPage.test.tsx              # new
          FeeTemplatePage.test.tsx               # new
          LineItemsTab.test.tsx                  # new
          InstallmentsTab.test.tsx               # new
          DiscountRulesTab.test.tsx              # new
      index.tsx                                  # modified — add fee-templates routes

  layouts/
    AppShell.tsx                                 # modified — add Fee Templates nav item
```

---

## Testing Strategy

Same stack: Vitest + React Testing Library, `vi.mock` for API modules, per-test `QueryClient`. Use `MemoryRouter` wrapping when the component uses `useNavigate` / `useParams` / `useSearchParams`.

### `FeeTemplatesPage.test.tsx`

| Test | Setup | Assert |
|---|---|---|
| Renders table with template rows | `feeTemplatesApi.list` returns 2 templates | Both names visible |
| Empty state | `list` returns `{ items: [], totalCount: 0 }` | "No fee templates found." visible |
| "Active" tab (default) passes `isActive: true` | Render; check call | `list` called with `isActive: true` |
| "Inactive" tab passes `isActive: false` | Click "Inactive" | `list` called with `isActive: false` |
| "All" tab omits isActive | Click "All" | `list` called with `isActive: null` |
| Academic Year dropdown filters | Select year option | `list` called with `academicYearId` set |
| Grade dropdown filters | Select grade option | `list` called with `gradeId` set |
| Row click navigates to view mode | Click row | `navigate('/admin/fee-templates/abc123')` called (no `?edit=true`) |
| Edit icon navigates to edit mode | Click pencil | `navigate('/admin/fee-templates/abc123?edit=true')` called |
| "New Template" button opens modal | Click button | `CreateFeeTemplateModal` with `open={true}` |
| Pagination Prev/Next | `totalCount: 45`, `pageSize: 20` | Next enabled; Prev disabled on page 1 |

### `FeeTemplatePage.test.tsx`

| Test | Setup | Assert |
|---|---|---|
| View mode renders read-only header | No `?edit` param | "Edit" button visible; no input fields in header |
| Edit mode renders editable header | `?edit=true` | Name input + IsActive checkbox visible |
| Inactive banner shown in edit mode for inactive template | `isActive: false` + `?edit=true` | Warning banner visible |
| Inactive banner hidden in view mode | `isActive: false`, no `?edit` | No warning banner |
| "Edit" button enters edit mode | Click in view mode | URL updated to `?edit=true` |
| Breadcrumb "Fee Templates" navigates to list | Click breadcrumb | `navigate('/admin/fee-templates')` called |
| Dirty tab shows `●` indicator | `LineItemsTab` reports dirty | Tab label shows `●` |
| `useBlocker` triggers on navigation when dirty | Set isDirty; trigger navigation | `ConfirmDiscardDialog` rendered |
| Confirming discard proceeds navigation | Click "Discard Changes" | `blocker.proceed` called |

### `LineItemsTab.test.tsx`

| Test | Setup | Assert |
|---|---|---|
| View mode shows read-only table | `isEditMode: false` | No input fields; amounts displayed as currency |
| Edit mode shows input cells | `isEditMode: true` | Input fields for name, amount, display order |
| Add row appends empty row | Click "Add Line Item" | New row visible with empty inputs |
| Delete row removes it | Click trash on row 1 | Row 1 gone; remaining rows present |
| Changing a value marks tab dirty | Edit any cell | `onDirtyChange(true)` called |
| Save sends correct payload — preserves IDs | Template has 2 items; re-send with same IDs | `replaceLineItems` called with IDs intact |
| Save sends correct payload — new row has no ID | Add new row and save | New row object has no `id` field |
| Save updates query cache | `replaceLineItems` returns updated template | `onDirtyChange(false)` called; saved state updated |
| Discard resets to saved state | Edit rows; click Discard | Local state matches original fetched data |

### `InstallmentsTab.test.tsx`

| Test | Setup | Assert |
|---|---|---|
| Sum indicator shows correct total | 3 items at 40%, 40%, 20% | "Total: 100.00% / 100%" in green |
| Sum indicator shows error when sum ≠ 100 | 2 items at 40%, 40% | "Total: 80.00% / 100%" in red |
| Save button disabled when sum ≠ 100 | 2 items totaling 80% | Save button has `disabled` attribute |
| Save button enabled when sum = 100 | 3 items totaling 100% | Save button not disabled |
| Save button enabled when list is empty | Empty list | Save button not disabled (empty schedule allowed) |
| Save sends full installment list | Add 2 items summing to 100%; save | `replaceInstallments` called with both items |

### `DiscountRulesTab.test.tsx`

| Test | Setup | Assert |
|---|---|---|
| View mode shows target line item name | Rule with `feeLineItemName: 'Tuition'` | "Tuition" visible in row |
| View mode shows "Invoice total" when no target | Rule with `feeLineItemId: null` | "Invoice total" visible |
| Target dropdown populated from saved line items | Template has 2 line items | Dropdown has 2 + "Invoice total" options |
| Selecting "Invoice total" sets feeLineItemId to undefined | Select "Invoice total" | Payload has no `feeLineItemId` field |
| Save sends correct payload | Add rule with FlatAmount type; save | `replaceDiscountRules` called with correct `ruleType` and `value` |

---

## Commands

```bash
# Frontend dev server
npm run dev             # from frontend/

# Frontend unit tests
npm run test            # from frontend/

# TypeScript check
npm run build           # from frontend/

# Backend (unchanged by this spec)
dotnet test SchoolMgmt.slnx
```

---

## Boundaries

**Always:**
- Invalidate `['fee-templates', 'list']` after any successful mutation — list TotalAmount and IsActive reflect template changes
- Use `queryClient.setQueryData(FEE_TEMPLATE_KEYS.detail(id), updated)` after child collection saves to avoid a redundant refetch
- Keep `isDirty` accurate across all three tabs — the browser guard fires only when there is genuinely unsaved state
- Pass `isActive` only when a status tab is selected — omit for "All" tab
- Discount Rules tab reads line items from `template.lineItems` (last-saved), never from `LineItemsTab`'s local state

**Ask first:**
- Adding a search bar to the fee templates list (requires backend `?search=` extension)
- Adding template duplication ("Clone Template") — needs a new backend endpoint
- Allowing Grade or Academic Year to be changed after creation — backend `PUT` does not accept these fields

**Never:**
- Send Grade or Academic Year in an `updateHeader` call — they are fixed at creation
- Enable the Save button on `InstallmentsTab` when `localItems.length > 0` and sum ≠ 100%
- Read unsaved `LineItemsTab` state in `DiscountRulesTab` — always use `template.lineItems`
- Use a single global "Save All" button across all three tabs — each collection has independent save/discard

---

## Success Criteria

- `GET /admin/fee-templates` renders the list with status tabs, Academic Year + Grade dropdowns, and pagination
- Row click navigates to view mode (`/admin/fee-templates/:id`, no `?edit`); edit icon navigates to edit mode (`?edit=true`)
- "New Template" modal creates a template and navigates to its detail page in edit mode
- Duplicate (AcademicYearId, GradeId, Name) shows the backend 409 message in a toast
- Detail page view mode is read-only; "Edit" button switches to edit mode
- Detail page edit mode shows the header form; saving updates the template name / IsActive
- Inactive template shows the warning banner in edit mode; not in view mode
- Tab labels show `●` when that tab has unsaved changes
- `useBlocker` fires a confirmation dialog when navigating away with unsaved changes
- `window.beforeunload` fires when refreshing or closing the tab with unsaved changes
- Line Items tab: add/edit/delete rows and save — IDs preserved for existing items, absent for new items
- Installments tab: save disabled when sum ≠ 100% (but not when the list is empty); sum indicator turns red when off
- Discount Rules tab: "Target Line Item" dropdown shows saved line items; "Invoice total" maps to no `feeLineItemId`
- All unit tests pass (`npm run test` from `frontend/`)
- `npm run build` succeeds with no TypeScript errors
