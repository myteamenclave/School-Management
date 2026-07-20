# Spec 17-B — Implement Parent–Guardian Link Frontend

## Related docs & specs

- **Idea doc**: [docs/ideas/14-parent-guardian-link.md](../docs/ideas/14-parent-guardian-link.md) — problem statement, account-creation flow, "Not doing" list (frontend section §MVP Scope)
- **Backend spec**: [specs/17-implement-parent-guardian-link.md](17-implement-parent-guardian-link.md) (**implemented**) — this is the exact API contract this frontend consumes: `POST /api/students/{id}/parent-login`, `GET .../parents`, `DELETE .../parents/{parentUserId}`, the `ParentLoginResultDto` `accountCreated`/`linkCreated` flags, and the 400/409 error semantics
- **Sibling frontend spec (pattern source)**: [specs/13-implement-fee-invoicing-frontend.md](13-implement-fee-invoicing-frontend.md) — the "Student Detail → new tab + API client + modal" shape this mirrors
- **Existing files reused as templates**: `StudentDetailPage.tsx` (tab host), `FeeAssignmentTab.tsx` (tab layout / year-selector / list / confirm-delete), `SetFeeAssignmentModal.tsx` (RHF+Zod modal), `students.ts` (API-client shape)

---

## Objective

One surface: a **"Parent Accounts" tab** on the Student Detail page (`/admin/students/:id`, Admin-only) that lets an Admin:

1. See the student's linked parent accounts (email, display name, account-created date).
2. Create a parent login from the student's `GuardianEmail` — Admin sets a temporary password; on success the modal shows the credentials for manual handoff (no email/SMTP in v1).
3. Remove a parent link (removes the link only — never deletes the parent user).

**Target user:** Admin (the page is already behind `RoleRoute role="Admin"`).

**Out of scope** (deferred, matching the backend spec and idea doc): any parent-facing UI / parent portal, self-registration, password reset, editing an existing parent's password or display name, deactivating a parent account, searching/paginating parents (a student has few).

---

## Tech Stack

Same as the rest of the frontend — no new dependencies:

- React 19 + TypeScript + Vite
- TanStack Query v5 (data + invalidation), Zustand (auth, already wired)
- React Hook Form + Zod (`@hookform/resolvers/zod`) for the modal form
- shadcn/ui (`Dialog`, `Table`, `Button`, `Input`, `Label`, `Tabs`) — all already installed
- Sonner (`toast`) for notifications
- Axios shared instance `src/api/axios.ts` (`baseURL: '/api'`, `withCredentials`, 401-refresh interceptor)
- `lucide-react` icons

---

## Part A — API client

### A1 — `frontend/src/api/parentAccounts.ts` (new file)

Mirrors the backend DTOs from spec 17 exactly. Note the endpoints are **student sub-resources** (`/students/{studentId}/…`), so the client methods take `studentId` as the first argument.

```ts
import api from './axios'

export interface ParentAccountDto {
  parentUserId: string
  email: string
  displayName: string
  accountCreatedAt: string    // ISO timestamp
}

export interface ParentLoginResultDto {
  parentUserId: string
  email: string
  displayName: string
  accountCreated: boolean      // false = an existing Parent account was reused (temp password NOT applied)
  linkCreated: boolean         // false = the link already existed (no-op)
}

export interface CreateParentLoginRequest {
  temporaryPassword: string
}

export const PARENT_ACCOUNT_KEYS = {
  forStudent: (studentId: string) => ['parent-accounts', studentId] as const,
}

export const parentAccountsApi = {
  list: (studentId: string) =>
    api.get<ParentAccountDto[]>(`/students/${studentId}/parents`).then((r) => r.data),

  createLogin: (studentId: string, body: CreateParentLoginRequest) =>
    api
      .post<ParentLoginResultDto>(`/students/${studentId}/parent-login`, body)
      .then((r) => r.data),

  removeLink: (studentId: string, parentUserId: string) =>
    api.delete(`/students/${studentId}/parents/${parentUserId}`),
}
```

---

## Part B — Student Detail "Parent Accounts" tab

### B1 — Modify `frontend/src/pages/admin/students/StudentDetailPage.tsx`

Add a fourth tab after "Fee Assignment":

```tsx
import { ParentAccountsTab } from './components/ParentAccountsTab'
// ...
<TabsTrigger value="parents">Parent Accounts</TabsTrigger>
// ...
<TabsContent value="parents">
  <ParentAccountsTab student={student} />
</TabsContent>
```

**Pass the whole `student`** (not just `id`) — the tab needs `student.guardianEmail` and `student.guardianName` to drive the create flow. `student` is already loaded in the page's `useQuery`.

### B2 — `ParentAccountsTab.tsx` (new file)

**Location:** `frontend/src/pages/admin/students/components/ParentAccountsTab.tsx`

**Props:** `{ student: StudentDto }`

**Data:** `useQuery({ queryKey: PARENT_ACCOUNT_KEYS.forStudent(student.id), queryFn: () => parentAccountsApi.list(student.id) })`.

**Layout (top to bottom):**

1. **Header row** — heading "Parent Accounts" + a **"Create parent login"** `Button` (`UserPlus` icon).
   - The button is **disabled when `student.guardianEmail` is empty/null**, with helper text beside/below it: *"Add a guardian email on the Details tab to enable parent login."* (This mirrors the backend's 400 guard — surface it as a disabled state instead of letting the request fail.)
   - Clicking opens `CreateParentLoginModal`.

2. **Guardian email line** (context) — small muted text: `Guardian email: {student.guardianEmail}` (or "No guardian email set." when empty).

3. **Linked-parents table** (`Table`):
   - Columns: `Name` | `Email` (mono) | `Added` (format `accountCreatedAt` as a date) | actions (remove).
   - **Loading:** "Loading…" row.
   - **Empty:** single centered row — "No parent accounts linked yet."
   - **Remove action:** ghost `Trash2` button → `window.confirm('Remove this parent's access to {student.firstName}? Their account will not be deleted.')` → `parentAccountsApi.removeLink(student.id, parentUserId)` → on success invalidate `PARENT_ACCOUNT_KEYS.forStudent(student.id)` + toast "Parent link removed." On error, `toast.error(extractError(err))`.

**Error extraction helper:** reuse the `extractError` pattern (`isAxiosError(err) && err.response?.data?.error`) already used across the students pages.

**Date formatting:** `new Date(accountCreatedAt).toLocaleDateString()` — no new dependency.

### B3 — `CreateParentLoginModal.tsx` (new file)

**Location:** `frontend/src/pages/admin/students/components/CreateParentLoginModal.tsx`

**Props:**
```ts
interface CreateParentLoginModalProps {
  open: boolean
  studentId: string
  guardianEmail: string        // pre-filled, read-only (source of truth is the student record)
  onClose: () => void
  onCreated: () => void        // parent invalidates the linked-parents query
}
```

**Two visual states inside the same `Dialog`:**

**State 1 — form (before success):**
- Title: "Create Parent Login".
- Read-only display of the guardian email (shown as a disabled `Input` or a labelled static row) — makes clear the login *is* the guardian email; not editable here.
- **Temporary Password** — **auto-generated**, not typed. A strong random password is generated when the modal opens (via `generatePassword()`, using `crypto.getRandomValues` over an unambiguous alphabet — no `0/O/1/I/l` — length 14, comfortably above the backend's 8-char minimum). Shown read-only in an `Input` with a **Copy** button and a **Regenerate** button (regenerate draws a fresh password). Helper text: "The parent uses this to log in. Share it with them directly".
- Footer: "Cancel" + "Create Login" (submitting → "Creating…").
- **No RHF/Zod** — there is no user-entered value to validate; the generated password is held in component state. (Backend validation still applies as a safety net.)
- Submit → `parentAccountsApi.createLogin(studentId, { temporaryPassword })` with the generated password.
- On error:
  - 409 → the returned `error` message (email owned by a non-parent account) via `toast.error(extractError(err))`; dialog stays open.
  - other → generic `toast.error(extractError(err))`.

**State 2 — credentials panel (after success):**
Switch the dialog body (keep it open) to a confirmation panel driven by the `ParentLoginResultDto`:

- **If `result.accountCreated === true`** (new account — the generated password applies):
  - Heading "✓ Parent login ready" (or a `CheckCircle` icon).
  - `Email: {result.email}` with a **copy** button.
  - `Password: {the generated password just submitted}` with a **copy** button. (The password is **not** in the response — read it from the generated value held in component state.)
  - Note: "Share these with the parent — they won't be emailed."
- **If `result.accountCreated === false`** (reused existing parent account — password unchanged/unknown):
  - Heading "✓ Linked to existing parent account".
  - `Email: {result.email}` with a copy button.
  - Note: "This parent already had an account, so the generated password was **not** applied — their existing password is unchanged."
  - If additionally `result.linkCreated === false`: add a line "This parent was already linked to this student." (idempotent no-op — nothing changed).
- Footer: single "Done" button → calls `onCreated()` then `onClose()`.

**Copy button:** `navigator.clipboard.writeText(value)` + a brief `toast.success('Copied')`. Small `Copy` icon button (lucide).

**On close/reopen:** reset both the form and the local success state so reopening the modal starts fresh at State 1 (reset in an `onOpenChange`/`useEffect` on `open`, same reset discipline as `SetFeeAssignmentModal`).

**Invalidation:** the parent calls `queryClient.invalidateQueries({ queryKey: PARENT_ACCOUNT_KEYS.forStudent(studentId) })` from `onCreated` (so the new parent appears in the table). Do the invalidation in the parent, consistent with `FeeAssignmentTab`/`SetFeeAssignmentModal`.

---

## Project Structure

### New files
```
frontend/src/api/
  parentAccounts.ts

frontend/src/pages/admin/students/components/
  ParentAccountsTab.tsx
  CreateParentLoginModal.tsx
```

### Modified files
```
frontend/src/pages/admin/students/
  StudentDetailPage.tsx        (+ "Parent Accounts" tab; pass `student` to the tab)
```

No changes to `AppShell.tsx` or routing — the tab lives inside the existing `/admin/students/:id` route. No new shadcn components to install.

---

## Commands

```
Dev server:   cd frontend && npm run dev
Typecheck:    cd frontend && npm run build      # tsc -b runs as part of the build
Lint:         cd frontend && npm run lint
```

---

## Code Style

- Match `FeeAssignmentTab.tsx` / `SetFeeAssignmentModal.tsx` exactly: TanStack Query for reads, `useMutation` for writes, Sonner toasts, RHF + Zod for the form, shadcn primitives, Tailwind utility classes consistent with the surrounding files (`text-sm`, `text-muted-foreground`, `rounded-lg border border-border`, destructive ghost buttons for delete).
- Co-locate the DTO/interface types with the API client in `parentAccounts.ts` (project convention — see `students.ts`, `feeAssignments.ts`).
- Query-key factory object (`PARENT_ACCOUNT_KEYS`) in the API client, used for both fetch and invalidation.
- `extractError(err)` helper local to the tab (same 3-line pattern used elsewhere) — do not add a shared util in this spec.
- Reuse `window.confirm` for the remove confirmation (consistent with `FeeAssignmentTab` remove flows) — no custom confirm dialog.

---

## Testing Strategy

The frontend has no automated test setup in this repo (prior frontend specs — 06-B, 07-B, 13 — ship with manual verification, not unit tests). Follow that precedent: **manual verification against the running backend**, plus a green `npm run build` (typecheck) and `npm run lint`.

**Manual test script (logged in as demo Admin `admin@demoschool.test` / `Passw0rd!`):**

1. Open a student that **has** a guardian email → Parent Accounts tab → "Create parent login" is enabled; guardian email is shown.
2. Open a student with **no** guardian email → the button is disabled with the helper hint; the guardian-email line reads "No guardian email set."
3. Open "Create parent login" → a temporary password is already generated; Copy and Regenerate work. Click "Create Login" → success panel shows email + password with working copy buttons; closing it, the parent appears in the table.
4. Regenerate produces a different password each click; reopening the modal starts with a fresh password (and back at the form, not the success panel).
5. Create a login for a **second** student sharing the same guardian email → success panel shows the **reused-account** variant (no password shown, "existing password unchanged" note); the parent appears under both students.
6. Click "Create parent login" again for a student already linked to that parent → success panel shows both the reused-account note and "already linked to this student."
7. Guardian email that belongs to a **Teacher/Admin** account → 409 → error toast with the backend message; dialog stays open.
8. Remove a parent link → confirm prompt → row disappears; re-open another student the same parent is linked to → still present there (link-only delete).
9. Verify the created parent can log in: log out, log in with the guardian email + the temp password → lands in the app.

---

## Boundaries

- **Always:** drive the login email from `student.guardianEmail` (read-only in the UI — the student record is the single source of truth); auto-generate the temporary password client-side with `crypto.getRandomValues` (never `Math.random`); read the temp password for the success panel from the generated value (it is never returned by the API); regenerate a fresh password each time the modal opens; invalidate `PARENT_ACCOUNT_KEYS.forStudent(studentId)` after create/remove.
- **Ask first:** adding an "edit parent" / "reset password" affordance (backend has no endpoint for it — out of scope); surfacing parent accounts anywhere other than the Student Detail page; adding a parent-facing route or nav item.
- **Never:** send the temporary password anywhere except the create request body; put it in a query string, query key, or `localStorage`; call `DELETE .../parents/{id}` without a confirm; imply that removing a link deletes the parent user (copy must say the account is not deleted); add a GET that mutates state.

---

## Success Criteria

1. A "Parent Accounts" tab appears on `/admin/students/:id` and lists linked parents (name, email, added date).
2. "Create parent login" is disabled (with a hint) when the student has no guardian email, and enabled otherwise.
3. Opening the modal shows an auto-generated temporary password (copyable, regeneratable); "Create Login" creates the account and shows a credentials panel with copy-to-clipboard for email + password; the new parent then appears in the table.
4. The reused-account case renders the distinct panel variant (no password, "unchanged" note), and the idempotent repeat shows the "already linked" note.
5. A 409 (email owned by a non-parent) surfaces the backend error message and keeps the dialog open.
6. Removing a link prompts for confirmation, removes the row, and leaves the parent linked to any other children.
7. `npm run build` (typecheck) and `npm run lint` pass; the created parent can log in with the handed-off credentials.

## Open Questions

None. UI decisions resolved during spec clarification: management lives in a dedicated **Parent Accounts tab**, and post-create credentials are shown in an **in-modal credentials panel** with a distinct reuse variant.
