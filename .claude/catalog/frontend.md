<!-- Last verified: 2026-07-15. Update this file whenever a new public type/function/component is added or removed from the frontend. Check here before adding new code — don't duplicate something that already exists. -->

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
| `subjectsApi` | `subjects.ts` | Thin wrappers: `list(params)`, `getById(id)`, `create(body)`, `update(id, body)`. `list` omits `?isActive=` when `params.isActive` is null (All tab). |
| `SUBJECT_KEYS` | `subjects.ts` | TanStack Query key factory: `{ list(params), detail(id) }`. All mutations invalidate `['subjects']` prefix key. |
| `SubjectSummaryDto` | `subjects.ts` | List-view shape: `id`, `name`, `code`, `description` (nullable), `isActive` (boolean), `createdAt`. |
| `SubjectDto` | `subjects.ts` | Full shape extending `SubjectSummaryDto`: adds `updatedAt`. |
| `CreateSubjectRequest` | `subjects.ts` | `name`, `code`, optional `description`. Code is immutable after create. |
| `UpdateSubjectRequest` | `subjects.ts` | `name`, optional `description`, `isActive`. Code NOT included — immutable. |
| `ListSubjectsParams` | `subjects.ts` | Query params for `list()`: `isActive: boolean \| null` (null = all), `search`, `page`, `pageSize`. |
| `feeTemplatesApi` | `feeTemplates.ts` | Thin wrappers: `list(params)`, `getById(id)`, `create(body)`, `updateHeader(id, body)`, `replaceLineItems(id, items)`, `replaceInstallments(id, items)`, `replaceDiscountRules(id, items)`. `list` omits `?isActive=` when null. |
| `enrollmentsApi` | `enrollments.ts` | Thin wrappers: `getBySectionAndYear(sectionId, academicYearId)`, `getEnrolledIds(academicYearId)` (`GET /api/enrollments/enrolled-ids` — returns `string[]` of already-placed student IDs for a year), `getByStudentId(studentId)` (`GET /api/enrollments?studentId=` — returns all `EnrollmentDto[]` for one student across all years), `enroll(sectionId, body)` (`POST /api/sections/{id}/enrollments`), `transfer(id, body)` (`PUT /api/enrollments/{id}`), `remove(id)` (`DELETE /api/enrollments/{id}`). |
| `ENROLLMENT_KEYS` | `enrollments.ts` | TanStack Query key factory: `{ bySectionAndYear(sectionId, academicYearId), enrolledIds(academicYearId), byStudent(studentId) }`. |
| `EnrollmentDto` | `enrollments.ts` | `id`, `studentId`, `studentCode`, `studentFirstName`, `studentLastName`, `sectionId`, `sectionName`, `gradeId`, `gradeName`, `academicYearId`, `academicYearName`, `createdAt`, `updatedAt`. |
| `CreateEnrollmentRequest` | `enrollments.ts` | `studentId`, `academicYearId` — sectionId is a route param. |
| `TransferEnrollmentRequest` | `enrollments.ts` | `sectionId` (the destination section). |
| `teacherAssignmentsApi` | `teacherAssignments.ts` | Thin wrappers: `getByTeacherAndYear(teacherId, academicYearId)`, `assign(teacherId, body)` (`POST /api/teachers/{id}/assignments`), `remove(teacherId, assignmentId)` (`DELETE /api/teachers/{id}/assignments/{assignmentId}`). |
| `ASSIGNMENT_KEYS` | `teacherAssignments.ts` | TanStack Query key factory: `{ byTeacherAndYear(teacherId, academicYearId) }`. |
| `TeacherAssignmentDto` | `teacherAssignments.ts` | `id`, `teacherId`, `subjectId`, `subjectName`, `subjectCode`, `sectionId`, `sectionName`, `gradeId`, `gradeName`, `academicYearId`, `academicYearName`, `createdAt`. |
| `CreateTeacherAssignmentRequest` | `teacherAssignments.ts` | `subjectId`, `sectionId`, `academicYearId` — teacherId is a route param. |
| `feeAssignmentsApi` | `feeAssignments.ts` | Thin wrappers: `broadcast(templateId)`, `getStudentAssignment(studentId, academicYearId)` (returns null on 404), `setStudentAssignment(studentId, body)` (upsert via PUT), `removeStudentAssignment(studentId, academicYearId)`, `getStudentDiscounts(studentId, academicYearId)`, `addStudentDiscount(studentId, body)`, `removeStudentDiscount(id)`. |
| `FEE_ASSIGNMENT_KEYS` | `feeAssignments.ts` | TanStack Query key factory: `{ studentAssignment(studentId, academicYearId), studentDiscounts(studentId, academicYearId) }`. |
| `StudentFeeAssignmentDto` | `feeAssignments.ts` | `id`, `studentId`, `studentName`, `studentCode`, `feeTemplateId`, `templateName`, `academicYearId`, `academicYearName`. |
| `StudentDiscountAssignmentDto` | `feeAssignments.ts` | `id`, `studentId`, `discountRuleId`, `discountRuleName`, `ruleType`, `value`, `academicYearId`. |
| `BroadcastAssignmentResult` | `feeAssignments.ts` | `assigned`, `skipped` counts. |
| `SetStudentAssignmentRequest` | `feeAssignments.ts` | `feeTemplateId`, `academicYearId`. |
| `AddStudentDiscountRequest` | `feeAssignments.ts` | `discountRuleId`, `academicYearId`. |
| `feeInvoicesApi` | `feeInvoices.ts` | Thin wrappers: `list(params)`, `getById(id)`, `generate(body)`, `issue(id)`, `cancel(id)`, `bulkIssue(ids)`. |
| `FEE_INVOICE_KEYS` | `feeInvoices.ts` | TanStack Query key factory: `{ list(params), detail(id) }`. |
| `InvoiceStatus` | `feeInvoices.ts` | `'Draft' \| 'Issued' \| 'Cancelled'` |
| `InstallmentStatus` | `feeInvoices.ts` | `'Pending' \| 'Paid' \| 'Overdue'` |
| `FeeInvoiceSummaryDto` | `feeInvoices.ts` | List-view shape: `id`, `invoiceCode`, `studentId/Name/Code`, `academicYearId/Name`, `feeTemplateId`, `templateName`, `totalAmount`, `status`, `issuedAt`, `createdAt`. |
| `FeeInvoiceDto` | `feeInvoices.ts` | Detail shape extending summary: adds `cancelledAt`, `updatedAt`, `lineItems: FeeInvoiceLineItemDto[]`, `installments: FeeInvoiceInstallmentDto[]`. |
| `FeeInvoiceLineItemDto` | `feeInvoices.ts` | `id`, `name`, `originalAmount`, `discountAmount`, `finalAmount`, `displayOrder`. |
| `FeeInvoiceInstallmentDto` | `feeInvoices.ts` | `id`, `name`, `percentage`, `dueDate` (nullable string), `amount`, `status`, `displayOrder`. |
| `ListFeeInvoicesParams` | `feeInvoices.ts` | `status: InvoiceStatus\|null`, `gradeId\|null`, `academicYearId\|null`, `page`, `pageSize`. |
| `GenerateInvoicesRequest` | `feeInvoices.ts` | `gradeId`, `academicYearId`, `installmentDueDates: InstallmentDueDateInput[]`. |
| `GenerateInvoicesResult` | `feeInvoices.ts` | `generated`, `skipped` counts. |
| `BulkIssueResult` | `feeInvoices.ts` | `issued`, `skipped` counts. |
| `FEE_TEMPLATE_KEYS` | `feeTemplates.ts` | TanStack Query key factory: `{ list(params), detail(id) }`. Mutations invalidate `['fee-templates', 'list']` and setQueryData on detail. |
| `FeeTemplateSummaryDto` | `feeTemplates.ts` | List-view shape: `id`, `name`, `academicYearId/Name`, `gradeId/Name`, `totalAmount`, `lineItemCount`, `isActive`, `createdAt`. |
| `FeeTemplateDto` | `feeTemplates.ts` | Full shape with `lineItems: FeeLineItemDto[]`, `installments: FeeInstallmentDto[]`, `discountRules: DiscountRuleDto[]`. Added `isFrozen: boolean` (spec 13). |
| `FeeLineItemDto` | `feeTemplates.ts` | `id`, `name`, `amount`, `displayOrder` |
| `FeeInstallmentDto` | `feeTemplates.ts` | `id`, `name`, `percentage`, `displayOrder` |
| `DiscountRuleDto` | `feeTemplates.ts` | `id`, `name`, `ruleType` (`'Percentage'\|'FlatAmount'`), `value`, `feeLineItemId` (null = invoice total), `feeLineItemName` |
| `LineItemInput` | `feeTemplates.ts` | `id?` (absent for new items), `name`, `amount`, `displayOrder` |
| `InstallmentInput` | `feeTemplates.ts` | `name`, `percentage`, `displayOrder` |
| `DiscountRuleInput` | `feeTemplates.ts` | `name`, `ruleType`, `value`, `feeLineItemId?` (absent = invoice total) |
| `ListFeeTemplatesParams` | `feeTemplates.ts` | `isActive: boolean\|null`, `academicYearId\|null`, `gradeId\|null`, `page`, `pageSize` |

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
| `AppShell` | Persistent authenticated shell. Left sidebar: navy `bg-primary`, `SchoolMS` logo, data-driven `NAV_ITEMS` filtered by `user.role`. Nav items: Dashboard (all roles), Academic Years, Grades & Sections, Students, Teachers, Subjects, Fee Templates, Fee Invoices (all Admin-only). Right column: topbar with user display name + logout button; `<Outlet>` for page content. Logout calls `authApi.logout()` + `clearUser()` + `navigate('/login')`. |

## Pages (`src/pages/`)

| Component | Location | Purpose |
|---|---|---|
| `LoginPage` | `auth/LoginPage.tsx` | Split-panel login UI matching the "Login Page - Final" design. Left: navy brand panel with marketing copy and feature items. Right: white panel with RHF+Zod form (email + password + remember-me checkbox). Shows `"Invalid email or password."` form-level error on 401; disables submit while in flight. On success: calls `setUser` then navigates to `/dashboard`. |
| `DashboardPage` | `dashboard/DashboardPage.tsx` | Stub — "Welcome, {displayName}" heading. All future role-specific dashboards live here. |
| `AdminRoutes` | `admin/index.tsx` | `<Routes>` wrapper for `/admin/*`. Routes: `academic-years`, `grades`, `students`, `students/:id`, `teachers`, `teachers/:id`, `subjects`, `fee-templates`, `fee-templates/:id`, `fee-invoices`, `fee-invoices/:id`. |
| `AcademicYearsPage` | `admin/academic-years/AcademicYearsPage.tsx` | Admin page at `/admin/academic-years`. Fetches year list via TanStack Query; partitions into current / previous / archived sections; manages `createOpen`, `editingSemester`, `showArchived` state; owns all mutations (setCurrentYear, archive, setCurrentSemester); composes `AcademicYearCard`, `CreateYearModal`, `EditSemesterModal`. |
| `GradesPage` | `admin/grades/GradesPage.tsx` | Admin page at `/admin/grades`. Fetches grade list via TanStack Query; manages `createOpen`, `editingGrade`, `expandedIds` (accordion) state; owns grade delete mutation; auto-expands accordion for newly created grades via `onCreated` callback; composes `GradeAccordionItem`, `CreateGradeModal`, `EditGradeModal`. |
| `GradeAccordionItem` | `admin/grades/components/GradeAccordionItem.tsx` | shadcn Accordion card for one grade. Collapsed header shows grade name + section count Badge. Expanded body shows `SectionChip` row (each with `onRoster` callback), inline add-section form (input + save/cancel), and Edit Grade / Delete Grade buttons. Delete Grade is disabled (with Tooltip) when `grade.sections.length > 0`. Owns `addSectionMutation` and `rosterSection` state (drives `SectionRosterSheet`). |
| `SectionChip` | `admin/grades/components/SectionChip.tsx` | Self-contained chip that toggles between display and inline-edit mode. Display: styled button showing section name + optional `Users` icon button (shown on hover) when `onRoster` prop is provided. Edit: inline input + save/cancel + delete (with `window.confirm`). Owns `renameMutation` and `deleteMutation` locally. Escape key cancels edit; Enter key saves. |
| `SectionRosterSheet` | `admin/grades/components/SectionRosterSheet.tsx` | shadcn `Sheet` slide-over for managing a section's enrollment roster. Opens when `section` prop is non-null. Year dropdown (defaults to first active year). Table: StudentCode, Name, Transfer button, Remove button. "Enroll Student" button opens `EnrollStudentModal`. Invalidates `ENROLLMENT_KEYS.bySectionAndYear` on enroll/transfer/remove. |
| `EnrollStudentModal` | `admin/grades/components/EnrollStudentModal.tsx` | shadcn Dialog for enrolling a student in a section. Debounced search (300 ms) queries active students. Fetches `enrollmentsApi.getEnrolledIds(academicYearId)` to grey out / exclude already-placed students. RHF + Zod (`studentId: uuid`). 409 → Sonner error toast. |
| `TransferStudentModal` | `admin/grades/components/TransferStudentModal.tsx` | shadcn Dialog for transferring a student to another section. Shows current section read-only. Section `Select` built from `gradesApi.list()` (excludes current section). RHF + Zod (`sectionId: uuid`). Calls `enrollmentsApi.transfer`. |
| `CreateGradeModal` | `admin/grades/components/CreateGradeModal.tsx` | shadcn Dialog + RHF + Zod for creating a grade (`name`, `displayOrder: number ≥ 0`). `onCreated(id)` callback used by page to auto-expand the new grade's accordion. 409 → "A grade with this name already exists." toast. |
| `EditGradeModal` | `admin/grades/components/EditGradeModal.tsx` | shadcn Dialog + RHF + Zod for editing a grade. Controlled by `grade: GradeDto \| null` (null = closed). Pre-populates via `useEffect` + `reset` on prop change. |
| `StudentsPage` | `admin/students/StudentsPage.tsx` | Admin page at `/admin/students`. Server-paginated table with enrollment status `Tabs` filter, debounced (300 ms) search input, Prev/Next pagination (`keepPreviousData`). Each row has an `Eye` icon (navigates to `/admin/students/:id`). Composes `CreateStudentModal`. Inline `StatusBadge` (color-coded by status). |
| `StudentDetailPage` | `admin/students/StudentDetailPage.tsx` | Admin detail page at `/admin/students/:id`. Back button row, then name + studentCode heading. Three shadcn `Tabs`: **Details** (edit form), **Section Assignments** (`StudentSectionAssignmentsTab`), **Fee Assignment** (`FeeAssignmentTab`). |
| `StudentSectionAssignmentsTab` | `admin/students/components/StudentSectionAssignmentsTab.tsx` | All section enrollments for one student across all years. Table: Academic Year, Grade, Section, actions (Transfer, Remove). "Add Enrollment" button opens `AddStudentEnrollmentModal`. Reuses `TransferStudentModal` from grades components. Invalidates `ENROLLMENT_KEYS.byStudent` on add/transfer/remove. |
| `AddStudentEnrollmentModal` | `admin/students/components/AddStudentEnrollmentModal.tsx` | shadcn Dialog for adding a new section enrollment to a student. Year select (filtered to years not yet enrolled) + Section select (all grades/sections, formatted "Grade — Section"). RHF + Zod. Calls `enrollmentsApi.enroll`. 409 → Sonner error toast. |
| `CreateStudentModal` | `admin/students/components/CreateStudentModal.tsx` | shadcn Dialog + RHF + Zod for creating a student. Two-column layout: First/Last, DOB/Gender, Enrollment Date, guardian section (optional). Gender uses shadcn `Select`. Exports `createSchema` (used by `StudentDetailPage`). Empty optional string fields are stripped to `undefined` before POST. |
| `TeachersPage` | `admin/teachers/TeachersPage.tsx` | Admin page at `/admin/teachers`. Server-paginated table with Active/Inactive/All `Tabs` filter (maps to `?isActive=true/false/omit`), debounced (300 ms) search, Prev/Next pagination (`keepPreviousData`). Each row has an `Eye` icon (navigates to `/admin/teachers/:id`). Composes `CreateTeacherModal`. Inline `StatusBadge` (green=active, zinc=inactive). |
| `TeacherDetailPage` | `admin/teachers/TeacherDetailPage.tsx` | Admin detail page at `/admin/teachers/:id`. Fetches teacher via `teachersApi.getById`. Two shadcn `Tabs`: **Details** (edit form — same fields as `EditTeacherModal`) and **Assignments** (`AssignmentsTab`). Back button to `/admin/teachers`. |
| `AssignmentsTab` | `admin/teachers/components/AssignmentsTab.tsx` | Year-scoped list of a teacher's subject-section slots. Year dropdown defaults to first active year. Table shows Subject (name + code badge), Grade, Section, Remove action. "Add Assignment" button opens `AddAssignmentModal`. Invalidates `ASSIGNMENT_KEYS.byTeacherAndYear` on add/remove. |
| `AddAssignmentModal` | `admin/teachers/components/AddAssignmentModal.tsx` | shadcn Dialog for adding a teacher assignment slot. Three cascading `Select` fields: Grade → Section (filtered by grade) → Subject (all active subjects). RHF + Zod. Calls `teacherAssignmentsApi.assign`. 409 → Sonner error toast "A teacher is already assigned to this subject in this section for this year." |
| `CreateTeacherModal` | `admin/teachers/components/CreateTeacherModal.tsx` | shadcn Dialog + RHF + Zod for creating a teacher (also creates their User account). Fields: firstName, lastName, email, password (≥8 chars), joiningDate, phone (optional). Email input is `type="text"` — Zod validates email format (avoids jsdom HTML5 blocking). Exports `createTeacherSchema`. |
| `AcademicYearCard` | `admin/academic-years/components/AcademicYearCard.tsx` | Renders one `AcademicYearDto` with its semesters. Highlights current year (`border-l-4` navy + `bg-primary/5`). Shows contextual buttons: Set as Current / Archive on active non-current years; Edit + Set Current on semester rows within current year. Archive requires `window.confirm`. Archived cards show "Archived — read only" and no buttons. |
| `CreateYearModal` | `admin/academic-years/components/CreateYearModal.tsx` | shadcn Dialog + RHF + Zod for creating an academic year (`name`, `startDate`, `endDate`, cross-field `endDate > startDate`). On 409: shows "An academic year with this name already exists." toast. |
| `EditSemesterModal` | `admin/academic-years/components/EditSemesterModal.tsx` | shadcn Dialog + RHF + Zod for editing a semester. Controlled by `semester: SemesterDto \| null` (null = closed). Pre-populates from prop via `useEffect` + `reset`. |
| `SubjectsPage` | `admin/subjects/SubjectsPage.tsx` | Admin page at `/admin/subjects`. Server-paginated table with Active/Inactive/All `Tabs` filter, debounced (300 ms) search (matches name or code), Prev/Next pagination (`keepPreviousData`). State: `tab`, `search`, `debouncedSearch`, `page`, `createOpen`, `editingId`. Inline `StatusBadge` (green=active, zinc=inactive). Composes `CreateSubjectModal` + `EditSubjectModal`. |
| `CreateSubjectModal` | `admin/subjects/components/CreateSubjectModal.tsx` | shadcn Dialog + RHF + Zod for creating a subject. Fields: name, code (letters/numbers/hyphens/underscores only — immutable after create), description (optional). 409 → toast from `extractError`. |
| `EditSubjectModal` | `admin/subjects/components/EditSubjectModal.tsx` | shadcn Dialog + RHF + Zod for editing a subject. Controlled by `subjectId: string \| null`. Fetches full `SubjectDto` on open. Displays `code` as read-only subtitle — NOT in PUT payload. `isActive` uses native `<input type="checkbox">` via `Controller`. |
| `FeeTemplatesPage` | `admin/fee-templates/FeeTemplatesPage.tsx` | Admin list page at `/admin/fee-templates`. Active/Inactive/All status `Tabs`, Academic Year + Grade `Select` dropdowns (both filterable), Prev/Next pagination (`keepPreviousData`). Row click → view mode, pencil icon → edit mode (`?edit=true`). Inline `StatusBadge` and `currencyFmt` (PHP). Composes `CreateFeeTemplateModal`. |
| `FeeTemplatePage` | `admin/fee-templates/FeeTemplatePage.tsx` | Admin detail/edit page at `/admin/fee-templates/:id`. Mode controlled by `?edit=true` search param (`useSearchParams`). Breadcrumb nav back to list. Inline `TemplateHeaderSection` (view=read-only+Edit button; edit=RHF form with name+isActive checkbox). Frozen banner when `template.isFrozen`. Unsaved-changes guard via `useBlocker` (in-app nav) and `beforeunload` (browser close). Per-tab dirty dot indicator (`●`). Inline `ConfirmDiscardDialog`. Composes `LineItemsTab`, `InstallmentsTab`, `DiscountRulesTab`, `InvoicingTab`. |
| `InvoicingTab` | `admin/fee-templates/components/InvoicingTab.tsx` | Fourth tab on `FeeTemplatePage`. Two action cards: "Grade Assignment" (broadcasts template to all enrolled students via `feeAssignmentsApi.broadcast`) and "Generate Draft Invoices" (opens `GenerateInvoicesDialog`). |
| `GenerateInvoicesDialog` | `admin/fee-templates/components/GenerateInvoicesDialog.tsx` | shadcn Dialog for generating draft invoices. RHF + Zod array form with one date field per template installment. On success: navigates to `/admin/fee-invoices?academicYearId=…&gradeId=…`. |
| `FeeInvoicesPage` | `admin/fee-invoices/FeeInvoicesPage.tsx` | Admin list page at `/admin/fee-invoices`. All/Draft/Issued/Cancelled status `Tabs`, Academic Year + Grade `Select` filters, row-level issue/cancel actions, checkbox multi-select for bulk issue. Prev/Next pagination (`keepPreviousData`). Reads initial `academicYearId`/`gradeId` from URL search params (set by `GenerateInvoicesDialog` post-generate). |
| `FeeInvoicePage` | `admin/fee-invoices/FeeInvoicePage.tsx` | Admin detail page at `/admin/fee-invoices/:id`. Breadcrumb nav, `StatusBadge`, Issue/Cancel action buttons. Two sections: Line Items table (with `TableFooter` totals row) and Installment Schedule table (with `InstallmentStatusBadge`). Invalidates both detail and list query keys on status change. |
| `StudentDetailPage` | `admin/students/StudentDetailPage.tsx` | Three shadcn `Tabs`: **Details** (edit form), **Section Assignments** (`StudentSectionAssignmentsTab`), **Fee Assignment** (`FeeAssignmentTab`). |
| `FeeAssignmentTab` | `admin/students/components/FeeAssignmentTab.tsx` | Year-scoped fee template assignment for one student. Year dropdown defaults to first Active year. Shows assigned template card (with Override + Remove) or empty state with Assign button. Below the card: discount rules table with Add Discount button. Composes `SetFeeAssignmentModal` and `AddDiscountModal`. |
| `SetFeeAssignmentModal` | `admin/students/components/SetFeeAssignmentModal.tsx` | shadcn Dialog for assigning or overriding a student's fee template for a year. Fetches active templates filtered by `academicYearId`. RHF + Zod (`feeTemplateId: uuid`). Calls `feeAssignmentsApi.setStudentAssignment` (upsert). |
| `AddDiscountModal` | `admin/students/components/AddDiscountModal.tsx` | shadcn Dialog for applying a discount rule to a student. Fetches the assigned template's `discountRules` and filters out already-assigned rule IDs. RHF + Zod (`discountRuleId: uuid`). Calls `feeAssignmentsApi.addStudentDiscount`. Shows "all rules already assigned" state when nothing remains. |
| `CreateFeeTemplateModal` | `admin/fee-templates/components/CreateFeeTemplateModal.tsx` | shadcn Dialog + RHF + Zod for creating a fee template. Fields: name, academicYearId (Select), gradeId (Select). On success: invalidates `['fee-templates']`, navigates to `/admin/fee-templates/${id}?edit=true`. No `onCreated` callback — navigation replaces it. |
| `LineItemsTab` | `admin/fee-templates/components/LineItemsTab.tsx` | Tab for managing fee line items. Local `localItems`/`savedItems` state initialized from `template.lineItems`. View mode: read-only table with PHP currency. Edit mode: editable table rows (name, amount, displayOrder) + add/delete. Save calls `replaceLineItems`; sets detail query cache + invalidates list. Dirty tracked by JSON.stringify comparison. |
| `InstallmentsTab` | `admin/fee-templates/components/InstallmentsTab.tsx` | Tab for managing installment schedule. Same local state pattern. Always-visible percentage sum indicator (red when sum≠100%, green otherwise). Save disabled when `localItems.length > 0 && sum≠100%`. Calls `replaceInstallments`. |
| `DiscountRulesTab` | `admin/fee-templates/components/DiscountRulesTab.tsx` | Tab for managing discount rules. Same local state pattern. Target line item dropdown reads from `template.lineItems` (last-saved, not LineItemsTab local state). Rule type Select: `Percentage`/`FlatAmount`. `feeLineItemId=undefined` = "Invoice total". Calls `replaceDiscountRules`. |
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
| `Sheet` / `SheetContent` / `SheetHeader` / `SheetTitle` / `SheetDescription` / `SheetFooter` / `SheetClose` | `ui/sheet.tsx` | shadcn Radix Dialog variant that slides in from a side (`side="right"` default). Install via `npx shadcn@latest add sheet` — not yet installed as of spec 11. Use for slide-over panels like the Section Roster. |

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
| `teachers/__tests__/TeachersPage.test.tsx` | 8 tests: renders rows, empty state, Inactive tab → `isActive: false`, All tab → `isActive: null`, search debounce, pagination, view button navigates, add button opens modal. `teachersApi` mocked. Wrapped in `MemoryRouter`. |
| `teachers/__tests__/CreateTeacherModal.test.tsx` | 6 tests: correct payload, phone omitted when blank, short password blocked, invalid email blocked, submit disabled while pending, closes + calls onCreated on success. |
