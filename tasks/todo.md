# Todo: Academic Year / Term Configuration UI

Plan: [tasks/plan.md](plan.md) | Spec: [specs/03-B-academic-year-term-config-UI.md](../specs/03-B-academic-year-term-config-UI.md)
Design: [Claude Design](https://claude.ai/design/p/2ee7d4a1-a721-42d8-b77e-737609059f37?file=Academic+Years.dc.html) — follow for all visual details

---

## Phase 1 — Foundation

### Task 1 — Install dependencies
- [ ] `npm install sonner` (from `frontend/`)
- [ ] `npx shadcn@latest add dialog` (from `frontend/`)
- [ ] **Checkpoint:** `npm run build` passes

### Task 2 — API client
- [ ] Create `frontend/src/api/academicYears.ts` with DTOs, `ACADEMIC_YEAR_KEYS`, and `academicYearsApi` (7 methods: `list`, `create`, `updateSemester`, `setCurrentYear`, `setCurrentSemester`, `archive`)
- [ ] **Checkpoint:** `npm run build` passes

### Task 3 — Wire Toaster
- [ ] Add `<Toaster richColors position="top-right" />` from sonner to `frontend/src/App.tsx`
- [ ] **Checkpoint:** `npm run build` passes

---

## Phase 2 — Components

### Task 4 — `AcademicYearCard`
- [ ] Create `frontend/src/pages/admin/academic-years/components/AcademicYearCard.tsx`
- [ ] Current year card: navy `border-l-4` + `#EEF2F7` header background
- [ ] Archived card: `opacity-75`, "Archived — read only" label, no action buttons
- [ ] 3px vertical accent bar per semester row (navy if current, grey otherwise)
- [ ] Semester status sub-label: "Upcoming" / "Completed" on non-current semesters
- [ ] Current year header: "Protected — cannot archive" chip (no Archive button)
- [ ] Previous year actions: `Set as Current` + `Archive` (with `window.confirm`)
- [ ] `Set Current` semester button: only on non-current semesters within the current year
- [ ] `fadeIn` CSS animation on card mount
- [ ] **Checkpoint:** `npm run build` passes

### Task 5 — `CreateYearModal`
- [ ] Create `frontend/src/pages/admin/academic-years/components/CreateYearModal.tsx`
- [ ] shadcn Dialog + RHF + Zod (`name`, `startDate`, `endDate`; `endDate > startDate` refinement)
- [ ] Info note: "Two semesters will be created automatically…"
- [ ] On 409: `toast.error('An academic year with this name already exists.')`
- [ ] `modalIn` animation on Dialog content
- [ ] **Checkpoint:** `npm run build` passes

### Task 6 — `EditSemesterModal`
- [ ] Create `frontend/src/pages/admin/academic-years/components/EditSemesterModal.tsx`
- [ ] shadcn Dialog + RHF + Zod, controlled by `semester: SemesterDto | null`
- [ ] Pre-populate form from `semester` prop
- [ ] On success: `toast.success('Semester updated')`
- [ ] **Checkpoint:** `npm run build` passes

---

## Phase 3 — Page + Wiring

### Task 7 — `AcademicYearsPage`
- [ ] Create `frontend/src/pages/admin/academic-years/AcademicYearsPage.tsx`
- [ ] `useQuery` fetching `academicYearsApi.list`, partition into `currentYear` / `previousYears` / `archivedYears`
- [ ] Loading state (spinner/skeleton), error state, empty state
- [ ] "Current Year" section (only if `currentYear` exists)
- [ ] "Previous Years" section (only if `previousYears.length > 0`)
- [ ] "Show archived (N)" toggle (hidden by default, only if `archivedYears.length > 0`)
- [ ] Page subtitle: "Manage academic years, semesters, and set the current active period."
- [ ] All mutations invalidate `ACADEMIC_YEAR_KEYS.all`; domain errors → `toast.error(error.response.data.error)`
- [ ] **Checkpoint:** `npm run build` passes

### Task 8 — Router + nav wiring
- [ ] `AppShell.tsx`: add `{ label: 'Academic Years', to: '/admin/academic-years', icon: <CalendarDays size={18} />, roles: ['Admin'] }` to `NAV_ITEMS`
- [ ] `router/index.tsx`: add `{ path: 'academic-years', element: <AcademicYearsPage /> }` under `/admin/*`
- [ ] `pages/admin/index.tsx`: replace stub `<Outlet />` with `<Routes><Route path="academic-years" element={<AcademicYearsPage />} /></Routes>`
- [ ] **Checkpoint:** Log in as Admin → "Academic Years" visible in sidebar → `/admin/academic-years` loads with live data

---

## Phase 4 — Tests + Catalog

### Task 9 — Component tests
- [ ] Create `frontend/src/pages/admin/academic-years/__tests__/AcademicYearsPage.test.tsx`
- [ ] Test 1: empty state renders correctly
- [ ] Test 2: current year card has correct treatment; Set-Current and Archive absent
- [ ] Test 3: "Set as Current" calls `setCurrentYear` with correct id
- [ ] Test 4: "Archive" triggers confirm; no call if cancelled
- [ ] Test 5: archived toggle hides/shows archived section
- [ ] Test 6: create modal opens, submits, closes on success
- [ ] Test 7: edit modal pre-populates with semester values
- [ ] Test 8: "Set Current" semester button only appears on correct rows
- [ ] **Checkpoint:** `npm run test` — all tests green, no regressions

### Task 10 — Catalog update
- [ ] Update `.claude/catalog/frontend.md` with `academicYearsApi`, DTOs, `ACADEMIC_YEAR_KEYS`, `AcademicYearsPage`, `AcademicYearCard`, `CreateYearModal`, `EditSemesterModal`
