# Parent Portal — View Fee Balance / Invoices

## Problem Statement
**How Might We** let a logged-in parent see, for a chosen child and year, *what they owe and what they've paid* — reusing the existing parent shell + `ResolveLinkedChildOrThrow` guard — so the questions *"how much do I owe?"* and *"when is it due?"* are answered at a glance, not buried in a raw installment table?

## Context
This is the **fourth parent-facing read surface** and the **third to copy the spec-18/19 pattern verbatim**:
- **Parent shell + auth** (specs 18/19): `api/parent` controller, `ParentPortalService`, the single `ResolveLinkedChildOrThrow(parentUserId, childId)` guard (unlinked/unknown child → **404**, never trust a client id), child switcher + year selector already extracted into `ParentChildYearBar`. This feature slots a fourth child-scoped read into that frame.
- **Fee invoicing** (spec 12 / doc 08): `FeeInvoiceService` already exposes `GetInvoicesAsync(studentId, …)` and `GetInvoiceByIdAsync` returning `FeeInvoiceDto` (line items + installments). `FeeInvoiceInstallment` already carries `Status`, `AmountPaid`, `PaidAt` — **payment-ready but always `Pending`/`null` today** (the pay-online module is a later slice). Doc 08 explicitly deferred "parent portal visibility" to this module.

**What makes this NOT a pure copy of attendance:** attendance needed a server-computed *rate*; fees need a server-computed **balance summary** (billed / paid / outstanding / next-due / overdue). Same architectural move — the rollup is a *business rule* with one authoritative home — applied to money instead of a percentage.

## Recommended Direction
Add one child-scoped endpoint, `GET /api/parent/children/{childId}/fees?academicYearId=`, to the existing `ParentPortalController`. It routes through the **same** `ResolveLinkedChildOrThrow` guard (no new auth surface), then returns a **combined payload**: a **balance summary block** + the child's **Issued invoice** for that year (line items + installment schedule), or an empty state when nothing is issued.

Four resolved product decisions (confirmed with the client):
- **Visibility — Issued only.** Drafts are internal admin working state; Cancelled are voided. A parent only ever sees a finalized bill. Because the model allows at most one active (Draft *or* Issued) invoice per student per year, "Issued only" resolves to **at most one invoice per child per year** — no list, no pagination.
- **Overdue — computed on-the-fly at read time.** An installment is overdue when `Status == Pending && DueDate < today` (server clock via the already-injected `IDateTimeProvider`). Pure display-layer derivation — no persisted state, no background job (doc 08 deferred persisted overdue detection to the payment module). This gives parents a real "past due" signal now.
- **Detail depth — summary + installment schedule + line items.** Full transparency: the balance hero, the installment schedule (due date · amount · status), *and* the line-item breakdown (tuition/fees + discount applied). Reuses `FeeInvoiceDto` whole.
- **Balance framing — Billed / Paid / Outstanding + Next Due.** All four, forward-compatible with pay-online. `Paid` reads `0.00` today (honest, no rework when payments land).

The **summary is computed server-side** in `FeeInvoiceService` (a new `GetStudentFeeSummaryAsync`), keeping the balance/overdue rules owned by the fee feature and reusable by the future pay-online module and admin dashboard. `ParentPortalService.GetChildFeesAsync` just orchestrates guard → summary + invoice into one `ParentFeeDto`. Navigation adds a **fourth parent nav item, "Fees"** (`/parent/fees`); `ChildFeesPage` consumes the shared `ParentChildYearBar`, mirroring the grades and attendance pages.

## Key Assumptions to Validate
- [ ] **At most one Issued invoice exists per child per year** (from the one-active-invoice-per-year rule), so the parent view renders a single invoice, never a list. — Confirm against `GetActiveForStudentAndYearAsync` semantics + seed data.
- [ ] **`FeeInvoiceDto` (line items + installments) carries everything the parent view needs** — only the *summary* and a per-installment *overdue flag* are new. — Mock the page against the DTO before touching backend.
- [ ] **Overdue is safe to derive at read time** with no persisted state or job, using the server clock. — Confirm `IDateTimeProvider` is the one clock source.
- [ ] **A parent sees fees regardless of the child's `EnrollmentStatus`** — the `StudentParent` link is the authorization truth (same rule specs 18/19 resolved).
- [ ] **No new authorization sub-permissions** — the fee read reduces entirely to "is this child linked to me?"; reuses `ResolveLinkedChildOrThrow` untouched.
- [ ] **Money is `decimal` end-to-end and rendered with a single currency convention** — reuse whatever the admin invoice pages already do; don't invent a second formatter.

## MVP Scope
**In:**
- **Backend:** `FeeInvoiceService.GetStudentFeeSummaryAsync(studentId, academicYearId)` → `StudentFeeSummaryDto` (TotalBilled, TotalPaid, Outstanding, NextDueDate, NextDueAmount, OverdueAmount, OverdueCount; nulls when no issued invoice); `ParentPortalService.GetChildFeesAsync(parentUserId, childId, academicYearId?)` (guard → summary + Issued invoice) → `ParentFeeDto { summary, invoice }` (invoice nullable); `GET /api/parent/children/{childId}/fees?academicYearId=` on the existing controller (Parent-only, GET/read-only, defaults to current year). One repo addition: fetch the **Issued** invoice for a student+year *with details* (line items + installments).
- **Frontend:** fourth parent nav item **"Fees"**; `ChildFeesPage` — balance summary hero (Billed / Paid / Outstanding / **Next Due**, plus an overdue callout when `OverdueCount > 0`) over the installment schedule (date · amount · status badge, overdue rows flagged red) and the line-item breakdown (name · original · discount · final); reuses `ParentChildYearBar` and the existing invoice status-color map.
- **Empty/edge states:** no children linked; child+year with no Issued invoice ("No fees have been issued for {child} in {year} yet."); loading/error — mirroring specs 18/19.

**Out:**
- **Any payment / "Pay now" action** — the pay-online sandbox (Stripe test mode) is the *next* module; this slice is read-only. `Paid` shows `0.00`.
- **Draft or Cancelled invoices** in the parent view (Issued only).
- **Multi-year / multi-invoice history rollup** — one invoice per child per year; the year selector is the only time dimension.
- **PDF / printable statement** (doc 08 not-doing; revisit with payments).
- **Persisted overdue state / reminder notifications** — overdue is display-only; SMS/email is out of project scope.
- **A parent home / combined dashboard** — deferred (same call as specs 18/19); four nav items for now.

## Not Doing (and Why)
- **Computing outstanding/overdue client-side** — these are business rules; a React `.filter()` is one refactor from the parent view, the pay-online module, and the admin dashboard disagreeing on what a parent owes. One server-side definition in `FeeInvoiceService`.
- **Widening `api/fee-invoices` for parents** — same IDOR risk specs 18/19 avoided; the dedicated `api/parent` controller with its link guard stays the only parent path. No parent ever passes an invoice id.
- **Exposing Draft/Cancelled invoices** — Drafts are unfinalized admin scratch; showing them would surface numbers that may still change. Issued is the contract.
- **A new invoice DTO for the parent** — `FeeInvoiceDto` already carries line items + installments; only the *summary* (and one overdue flag) is new, matching how attendance added only its summary.
- **Persisting overdue or running a cron** — doc 08 tied persisted overdue to the payment module; deriving it at read time is correct and cheaper until then.

## Open Questions
- **Where does the per-installment `IsOverdue` flag live?** Recommended: add an additive `bool IsOverdue` to the shared `FeeInvoiceInstallmentDto`, computed in a shared helper so the admin invoice detail and the parent view agree (admin currently ignores it → renders `false`, harmless). Alternative: a parent-scoped installment projection to avoid touching shared code. Lean shared-helper to keep one overdue definition.
- **Currency display** — is there an existing formatter/symbol convention on the admin invoice pages to reuse, or does this slice pick one? (Assume reuse; confirm.)
- **Should the summary show a "fully paid" state now?** Today `Outstanding` always equals `TotalBilled` (no payments). The hero should still read cleanly at `Outstanding = 0` for when payments land — worth designing the zero/paid state now even though it's unreachable this slice.

## Resolved Decisions
- **Visibility:** **Issued only** — Drafts and Cancelled are never parent-visible. Resolves to at most one invoice per child per year.
- **Overdue:** **computed on-the-fly at read time** (`Status == Pending && DueDate < today`, server clock) — display-only, no persisted state, no job.
- **Detail depth:** **summary + installment schedule + line items** — full transparency, reuse `FeeInvoiceDto` whole.
- **Balance framing:** **Billed / Paid / Outstanding + Next Due** — all four, forward-compatible; `Paid = 0.00` today.

## Resolved (inherited from specs 18/19, restated for this slice)
- **Withdrawn/transferred children:** listed and viewable regardless of `EnrollmentStatus` (badged, not filtered) — the link is the authorization truth; revocation has an explicit path (spec 17 link `DELETE`).
- **Unlinked/unknown child:** `ResolveLinkedChildOrThrow` → **404**, never leak existence, never 403.
- **Year selector:** lists all school years; a year with no Issued invoice renders the empty state.
- **All endpoints GET/read-only** (SameSite=Lax CSRF rule); parent identity always from the JWT `sub` claim, never the request.
