# Spec 13 — Implement Fee Invoicing Frontend

## Related

- **Idea doc**: [docs/ideas/08-fee-invoicing.md](../docs/ideas/08-fee-invoicing.md)
- **Backend spec**: [specs/12-implement-fee-invoicing.md](12-implement-fee-invoicing.md) (implemented)
- **Fee template frontend**: `FeeTemplatePage.tsx` / `FeeTemplatesPage.tsx` (existing)
- **Student Detail page**: `StudentDetailPage.tsx` (existing)

---

## Objective

Four surfaces:

1. **Fee Template → Invoicing tab** — broadcast to grade, enter installment due dates, generate Draft invoices.
2. **Fee Invoices page** (`/admin/fee-invoices`) — paginated invoice list, status/grade/year filters, single-issue + bulk-issue actions.
3. **Fee Invoice detail page** (`/admin/fee-invoices/:id`) — full invoice with line items + installments, issue and cancel lifecycle buttons.
4. **Student Detail → Fee Assignment tab** — view/override assigned template, add/remove per-student discount rules.

---

## Part A — API Clients

### A1 — `frontend/src/api/feeAssignments.ts` (new file)

```ts
import api from './axios'

export interface StudentFeeAssignmentDto {
  id: string
  studentId: string
  studentName: string
  studentCode: string
  feeTemplateId: string
  templateName: string
  academicYearId: string
  academicYearName: string
}

export interface StudentDiscountAssignmentDto {
  id: string
  studentId: string
  discountRuleId: string
  discountRuleName: string
  ruleType: 'Percentage' | 'FlatAmount'
  value: number
  academicYearId: string
}

export interface BroadcastAssignmentResult {
  assigned: number
  skipped: number
}

export interface SetStudentAssignmentRequest {
  feeTemplateId: string
  academicYearId: string
}

export interface AddStudentDiscountRequest {
  discountRuleId: string
  academicYearId: string
}

export const FEE_ASSIGNMENT_KEYS = {
  studentAssignment: (studentId: string, academicYearId: string) =>
    ['fee-assignments', 'student', studentId, academicYearId] as const,
  studentDiscounts: (studentId: string, academicYearId: string) =>
    ['fee-assignments', 'discounts', studentId, academicYearId] as const,
}

export const feeAssignmentsApi = {
  broadcast: (templateId: string) =>
    api
      .post<BroadcastAssignmentResult>('/fee-assignments/broadcast', { templateId })
      .then((r) => r.data),

  getStudentAssignment: (studentId: string, academicYearId: string) =>
    api
      .get<StudentFeeAssignmentDto | null>('/fee-assignments', {
        params: { studentId, academicYearId },
      })
      .then((r) => r.data),

  setStudentAssignment: (studentId: string, body: SetStudentAssignmentRequest) =>
    api
      .put<StudentFeeAssignmentDto>('/fee-assignments', body, { params: { studentId } })
      .then((r) => r.data),

  removeStudentAssignment: (studentId: string, academicYearId: string) =>
    api.delete('/fee-assignments', { params: { studentId, academicYearId } }),

  getStudentDiscounts: (studentId: string, academicYearId: string) =>
    api
      .get<StudentDiscountAssignmentDto[]>('/fee-assignments/discounts', {
        params: { studentId, academicYearId },
      })
      .then((r) => r.data),

  addStudentDiscount: (studentId: string, body: AddStudentDiscountRequest) =>
    api
      .post<StudentDiscountAssignmentDto>('/fee-assignments/discounts', body, {
        params: { studentId },
      })
      .then((r) => r.data),

  removeStudentDiscount: (id: string) =>
    api.delete(`/fee-assignments/discounts/${id}`),
}
```

### A2 — `frontend/src/api/feeInvoices.ts` (new file)

```ts
import api from './axios'

export type InvoiceStatus = 'Draft' | 'Issued' | 'Cancelled'
export type InstallmentStatus = 'Pending' | 'Paid' | 'Overdue'

export interface FeeInvoiceLineItemDto {
  id: string
  name: string
  originalAmount: number
  discountAmount: number
  finalAmount: number
  displayOrder: number
}

export interface FeeInvoiceInstallmentDto {
  id: string
  name: string
  percentage: number
  dueDate: string | null   // ISO date string (DateOnly serialised as "YYYY-MM-DD")
  amount: number
  status: InstallmentStatus
  displayOrder: number
}

export interface FeeInvoiceSummaryDto {
  id: string
  invoiceCode: string
  studentId: string
  studentName: string
  studentCode: string
  academicYearId: string
  academicYearName: string
  feeTemplateId: string
  templateName: string
  totalAmount: number
  status: InvoiceStatus
  issuedAt: string | null
  createdAt: string
}

export interface FeeInvoiceDto extends FeeInvoiceSummaryDto {
  cancelledAt: string | null
  updatedAt: string | null
  lineItems: FeeInvoiceLineItemDto[]
  installments: FeeInvoiceInstallmentDto[]
}

export interface InstallmentDueDateInput {
  templateInstallmentId: string
  dueDate: string   // "YYYY-MM-DD"
}

export interface GenerateInvoicesRequest {
  gradeId: string
  academicYearId: string
  installmentDueDates: InstallmentDueDateInput[]
}

export interface GenerateInvoicesResult {
  generated: number
  skipped: number
}

export interface BulkIssueResult {
  issued: number
  skipped: number
}

export interface ListFeeInvoicesParams {
  status?: InvoiceStatus | null
  gradeId?: string | null
  academicYearId?: string | null
  studentId?: string | null
  page: number
  pageSize: number
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export const FEE_INVOICE_KEYS = {
  list: (p: ListFeeInvoicesParams) => ['fee-invoices', 'list', p] as const,
  detail: (id: string) => ['fee-invoices', 'detail', id] as const,
}

export const feeInvoicesApi = {
  generate: (body: GenerateInvoicesRequest) =>
    api.post<GenerateInvoicesResult>('/fee-invoices/generate', body).then((r) => r.data),

  list: (params: ListFeeInvoicesParams) => {
    const q: Record<string, unknown> = { page: params.page, pageSize: params.pageSize }
    if (params.status) q.status = params.status
    if (params.gradeId) q.gradeId = params.gradeId
    if (params.academicYearId) q.academicYearId = params.academicYearId
    if (params.studentId) q.studentId = params.studentId
    return api.get<PagedResult<FeeInvoiceSummaryDto>>('/fee-invoices', { params: q }).then((r) => r.data)
  },

  getById: (id: string) =>
    api.get<FeeInvoiceDto>(`/fee-invoices/${id}`).then((r) => r.data),

  issue: (id: string) =>
    api.post<FeeInvoiceDto>(`/fee-invoices/${id}/issue`).then((r) => r.data),

  bulkIssue: (ids: string[]) =>
    api.post<BulkIssueResult>('/fee-invoices/bulk-issue', { ids }).then((r) => r.data),

  cancel: (id: string) =>
    api.post<FeeInvoiceDto>(`/fee-invoices/${id}/cancel`).then((r) => r.data),
}
```

---

## Part B — Fee Template "Invoicing" tab

### B1 — Add `isFrozen` to `FeeTemplateDto`

Edit `frontend/src/api/feeTemplates.ts` — add `isFrozen: boolean` to `FeeTemplateDto`.

### B2 — Modify `FeeTemplatePage.tsx`

Two changes:

**1. Frozen banner** — add after the existing inactive banner:
```tsx
{template.isFrozen && (
  <div className="mb-4 flex items-center gap-2 rounded-lg border border-blue-200 bg-blue-50 px-4 py-3 text-sm text-blue-800 dark:border-blue-800 dark:bg-blue-950/30 dark:text-blue-300">
    <Lock size={15} className="shrink-0" />
    This template is frozen. At least one invoice has been issued — line items, installments, and discount rules can no longer be modified.
  </div>
)}
```
Import `Lock` from `lucide-react`.

**2. Invoicing tab** — add a fourth `TabsTrigger` and `TabsContent`:
```tsx
<TabsTrigger value="invoicing">Invoicing</TabsTrigger>
...
<TabsContent value="invoicing">
  <InvoicingTab template={template} />
</TabsContent>
```
Import `InvoicingTab` from `./components/InvoicingTab`.

### B3 — `InvoicingTab.tsx` (new file)

**Location:** `frontend/src/pages/admin/fee-templates/components/InvoicingTab.tsx`

**Props:** `{ template: FeeTemplateDto }`

**Sections:**

**Broadcast section:**
- Card with heading "Grade Assignment" and description "Assign this template to all students enrolled in {template.gradeName} for {template.academicYearName} who don't yet have a fee assignment for this year."
- "Broadcast to Grade" `Button` (disabled while pending).
- On success: Sonner toast `"Assigned {result.assigned} students. {result.skipped} already had an assignment and were skipped."`.
- On error: Sonner error toast.
- Mutation: `feeAssignmentsApi.broadcast(template.id)`. No query to invalidate (this is fire-and-forget).

**Generate Invoices section:**
- Card with heading "Generate Draft Invoices".
- "Generate Invoices" Button → opens `GenerateInvoicesDialog`.
- Description: "Generate a Draft invoice for each student assigned to this template. Enter due dates for each installment below."

```tsx
const [generateOpen, setGenerateOpen] = useState(false)
...
<GenerateInvoicesDialog
  open={generateOpen}
  template={template}
  onClose={() => setGenerateOpen(false)}
/>
```

### B4 — `GenerateInvoicesDialog.tsx` (new file)

**Location:** `frontend/src/pages/admin/fee-templates/components/GenerateInvoicesDialog.tsx`

**Props:**
```ts
interface GenerateInvoicesDialogProps {
  open: boolean
  template: FeeTemplateDto
  onClose: () => void
}
```

**Behaviour:**
- shadcn/ui `Dialog` (width `sm:max-w-lg`).
- Heading: "Generate Invoices — {template.name}".
- Subtext showing grade + academic year.
- **Installment due dates section**: for each `template.installments` item render a row:
  - Installment name label + percentage badge
  - Date `Input` (type `"date"`) for due date — label `"Due Date"`.
  - If `template.installments` is empty, show message "This template has no installments. Invoices will be generated without an installment schedule."
- "Generate" button (submits) + "Cancel" button.
- **Form:** React Hook Form + Zod.
  - Schema: `z.object({ dueDates: z.array(z.object({ templateInstallmentId: z.string(), dueDate: z.string().min(1, "Required") })) })` when installments exist; empty schema when none.
- On submit, calls `feeInvoicesApi.generate({ gradeId: template.gradeId, academicYearId: template.academicYearId, installmentDueDates: dueDates })`.
- On success: close dialog, Sonner toast `"Generated {result.generated} invoices. {result.skipped} skipped (already had active invoices)."`, then `navigate('/admin/fee-invoices?academicYearId=' + template.academicYearId + '&gradeId=' + template.gradeId)`.
- On error: Sonner error toast, dialog stays open.

---

## Part C — Fee Invoices list page

### C1 — New `FeeInvoicesPage.tsx`

**Location:** `frontend/src/pages/admin/fee-invoices/FeeInvoicesPage.tsx`

**Route:** `/admin/fee-invoices`

**Filters (top bar):**
- Status tabs: `All | Draft | Issued | Cancelled` (same `Tabs` pattern as other list pages).
- Grade `Select` (fetches `gradesApi.list()`, staleTime Infinity).
- Academic Year `Select` (fetches `academicYearsApi.list()`, staleTime Infinity).

**Table columns:** `Code` (monospace) | `Student` | `Template` | `Year` | `Total` (PHP currency) | `Status` badge | actions

**Status badge colours:**
- Draft: muted/zinc
- Issued: green
- Cancelled: red/destructive

**Row actions:**
- Eye button (`Eye` icon) → `navigate('/admin/fee-invoices/' + invoice.id)`
- Issue button (`CheckCircle` icon, only shown when `status === 'Draft'`) → calls `feeInvoicesApi.issue(id)`, invalidates list, Sonner toast.
- Cancel button (`XCircle` icon, only shown when `status !== 'Cancelled'`) → `window.confirm` → calls `feeInvoicesApi.cancel(id)`, invalidates list.

**Bulk issue:**
- Each row has a checkbox. Checkbox only enabled when `status === 'Draft'`.
- "Issue Selected ({n})" `Button` in header area (visible when `selectedIds.size > 0`).
- On click: calls `feeInvoicesApi.bulkIssue([...selectedIds])`, Sonner toast `"Issued {result.issued} invoices. {result.skipped} skipped."`, clears selection, invalidates list.

**Pagination:** same Prev/Next pattern as other list pages, 20 per page.

**Empty state:** "No invoices found." centered text in table body.

**React Query:** `FEE_INVOICE_KEYS.list(params)`, `keepPreviousData`.

**URL-based filter initialisation:** Read `gradeId` and `academicYearId` from `useSearchParams` to pre-populate the dropdowns. This allows `GenerateInvoicesDialog` to navigate here with filters pre-set.

### C2 — Add nav item and routes

**AppShell nav item** (`frontend/src/layouts/AppShell.tsx`):
```ts
{
  label: 'Fee Invoices',
  to: '/admin/fee-invoices',
  icon: <FileText size={18} />,
  roles: ['Admin'],
}
```
Add after "Fee Templates". Import `FileText` from `lucide-react`.

**AdminRoutes** (`frontend/src/pages/admin/index.tsx`):
```tsx
import { FeeInvoicesPage } from './fee-invoices/FeeInvoicesPage'
import { FeeInvoicePage } from './fee-invoices/FeeInvoicePage'
...
<Route path="fee-invoices" element={<FeeInvoicesPage />} />
<Route path="fee-invoices/:id" element={<FeeInvoicePage />} />
```

---

## Part D — Fee Invoice detail page

### D1 — New `FeeInvoicePage.tsx`

**Location:** `frontend/src/pages/admin/fee-invoices/FeeInvoicePage.tsx`

**Route:** `/admin/fee-invoices/:id`

**Layout:**
```
← Fee Invoices               [breadcrumb nav]

INV-2025-000001  [Draft badge]        [Issue] [Cancel]

Student: Nguyen Van A (2025-000001)
Template: Grade 1 Standard Fee
Year: 2025-2026   Total: ₱25,000.00
```

**Line Items card** — table: `Name` | `Original` | `Discount` | `Final`; totals row in `tfoot`.

**Installments card** — table: `Name` | `%` | `Due Date` | `Amount` | `Status` badge; if no installments, show "No installment schedule."

**Actions:**
- `Issue` Button (primary, only when `status === 'Draft'`) → calls `feeInvoicesApi.issue(id)` → invalidates `FEE_INVOICE_KEYS.detail(id)` and `['fee-invoices', 'list']`.
- `Cancel` Button (destructive variant, only when `status !== 'Cancelled'`) → `window.confirm` → calls `feeInvoicesApi.cancel(id)` → same invalidation.
- Both show Sonner toast on success/error.

**React Query:** `FEE_INVOICE_KEYS.detail(id)` via `feeInvoicesApi.getById(id)`.

**Currency helper:** reuse the `Intl.NumberFormat` PHP currency pattern from `FeeTemplatesPage`.

---

## Part E — Student Detail "Fee Assignment" tab

### E1 — Modify `StudentDetailPage.tsx`

Add a third `Tabs` item:
```tsx
<TabsTrigger value="fee-assignment">Fee Assignment</TabsTrigger>
...
<TabsContent value="fee-assignment">
  <FeeAssignmentTab studentId={id!} />
</TabsContent>
```
Import `FeeAssignmentTab` from `./components/FeeAssignmentTab`.

### E2 — `FeeAssignmentTab.tsx` (new file)

**Location:** `frontend/src/pages/admin/students/components/FeeAssignmentTab.tsx`

**Props:** `{ studentId: string }`

**Behaviour:**
- Year selector (same pattern as `StudentSectionAssignmentsTab` — fetches all academic years, defaults to first non-archived).
- For the selected year, queries `feeAssignmentsApi.getStudentAssignment(studentId, yearId)`.
- **No assignment state:** grey card saying "No fee template assigned for this year." with "Assign Template" button.
- **Assignment card:** shows template name, grade, year. Buttons: "Override" (replace with different template) + "Remove" (with `window.confirm`).
  - Remove: calls `feeAssignmentsApi.removeStudentAssignment`, invalidates `FEE_ASSIGNMENT_KEYS.studentAssignment`.
  - Override: opens `SetFeeAssignmentModal`.
- **Discount Rules section** (below assignment card, only shown when assignment exists):
  - Heading "Discount Rules" + "Add Discount" button.
  - Fetches `feeAssignmentsApi.getStudentDiscounts(studentId, yearId)` via `FEE_ASSIGNMENT_KEYS.studentDiscounts`.
  - Table: `Rule Name` | `Type` | `Value` | remove button.
  - Type badge: `Percentage` (blue) / `FlatAmount` (orange).
  - Value display: `{value}%` for Percentage, `₱{value.toLocaleString()}` for FlatAmount.
  - Remove: calls `feeAssignmentsApi.removeStudentDiscount(id)`, invalidates `FEE_ASSIGNMENT_KEYS.studentDiscounts`.
  - "Add Discount" → opens `AddDiscountModal`.

**React Query keys used:**
- `FEE_ASSIGNMENT_KEYS.studentAssignment(studentId, yearId)`
- `FEE_ASSIGNMENT_KEYS.studentDiscounts(studentId, yearId)`

### E3 — `SetFeeAssignmentModal.tsx` (new file)

**Location:** `frontend/src/pages/admin/students/components/SetFeeAssignmentModal.tsx`

**Props:**
```ts
interface SetFeeAssignmentModalProps {
  open: boolean
  studentId: string
  academicYearId: string
  currentTemplateId?: string
  onClose: () => void
  onSaved: () => void
}
```

**Behaviour:**
- shadcn/ui `Dialog`.
- Fetches all fee templates for the selected year: `feeTemplatesApi.list({ isActive: true, academicYearId, gradeId: null, page: 1, pageSize: 100 })`.
- `Select` of template options showing `"{template.name} — {template.gradeName}"`.
- Pre-selects `currentTemplateId` if provided.
- Submit calls `feeAssignmentsApi.setStudentAssignment(studentId, { feeTemplateId, academicYearId })`.
- On success: Sonner toast "Fee assignment saved.", `onSaved()`, `onClose()`.

**Form:** React Hook Form + Zod: `z.object({ feeTemplateId: z.string().uuid() })`.

### E4 — `AddDiscountModal.tsx` (new file)

**Location:** `frontend/src/pages/admin/students/components/AddDiscountModal.tsx`

**Props:**
```ts
interface AddDiscountModalProps {
  open: boolean
  studentId: string
  academicYearId: string
  assignedTemplateId: string
  alreadyAssignedRuleIds: Set<string>
  onClose: () => void
  onAdded: () => void
}
```

**Behaviour:**
- shadcn/ui `Dialog`.
- Fetches template details via `feeTemplatesApi.getById(assignedTemplateId)` (enabled while open).
- Derives available rules: `template.discountRules.filter(dr => !alreadyAssignedRuleIds.has(dr.id))`.
- `Select` listing available rules as `"{rule.name} ({ruleType} — {value})"`.
- If no available rules: shows "All discount rules from this template have already been assigned."
- Submit calls `feeAssignmentsApi.addStudentDiscount(studentId, { discountRuleId, academicYearId })`.
- On 409: Sonner error "This discount is already assigned."
- On success: Sonner toast "Discount added.", `onAdded()`, `onClose()`.

**Form:** React Hook Form + Zod: `z.object({ discountRuleId: z.string().uuid() })`.

---

## Project Structure — New Files

```
frontend/src/api/
  feeAssignments.ts
  feeInvoices.ts

frontend/src/pages/admin/fee-templates/components/
  InvoicingTab.tsx
  GenerateInvoicesDialog.tsx

frontend/src/pages/admin/fee-invoices/          ← new directory
  FeeInvoicesPage.tsx
  FeeInvoicePage.tsx

frontend/src/pages/admin/students/components/
  FeeAssignmentTab.tsx
  SetFeeAssignmentModal.tsx
  AddDiscountModal.tsx
```

## Project Structure — Modified Files

```
frontend/src/api/
  feeTemplates.ts                 (+ isFrozen on FeeTemplateDto)

frontend/src/layouts/
  AppShell.tsx                    (+ Fee Invoices nav item)

frontend/src/pages/admin/
  index.tsx                       (+ fee-invoices routes)
  fee-templates/
    FeeTemplatePage.tsx           (+ Invoicing tab, frozen banner)
  students/
    StudentDetailPage.tsx         (+ Fee Assignment tab)
```

---

## Shared Patterns

- **Currency formatting:** `new Intl.NumberFormat('en-PH', { style: 'currency', currency: 'PHP', minimumFractionDigits: 2 })` — same as `FeeTemplatesPage`.
- **Year defaulting:** fetch all years, default `selectedYearId` to first year where `isArchived === false`, fallback to `years[0]`. Reuse pattern from `AssignmentsTab` / `StudentSectionAssignmentsTab`.
- **Error extraction:** `isAxiosError(err) && err.response?.data?.error` — same as `FeeTemplatePage`.
- **No new shadcn components needed** — `Dialog`, `Select`, `Table`, `Tabs`, `Button`, `Input`, `Badge` are all already installed.

---

## Success Criteria

1. "Broadcast to Grade" on a fee template assigns StudentFeeAssignments for all enrolled students and shows accurate assigned/skipped counts in a toast.
2. "Generate Invoices" dialog shows one date input per installment; submitting creates Draft invoices and navigates to the invoice list pre-filtered by grade + year.
3. Frozen template banner appears on the template detail page after the first invoice is Issued.
4. `/admin/fee-invoices` lists invoices with correct filters; status badge colour matches lifecycle state.
5. "Issue" action on a Draft row transitions it to Issued immediately (optimistic invalidation acceptable).
6. "Cancel" action on an Issued row prompts confirmation, then transitions to Cancelled.
7. Bulk-issue selects only Draft rows; shows issued/skipped summary toast.
8. `/admin/fee-invoices/:id` shows line items with discount breakdown and installments with due dates.
9. Student Detail "Fee Assignment" tab shows current template for selected year; override and remove work correctly.
10. "Add Discount" shows only unassigned rules from the student's current template; adding a rule appears in the list immediately.

---

## Out of Scope

- Installment status updates (Paid/Overdue) — payment gateway phase.
- Invoice PDF export.
- Parent portal visibility.
- Searching invoices by student name/code (only ID filter for now).
- Pagination inside AddDiscountModal / SetFeeAssignmentModal dropdowns.
