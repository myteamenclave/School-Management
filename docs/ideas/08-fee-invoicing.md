# Fee Invoicing

## Problem Statement

How might we turn a fee structure template into a concrete, per-student financial
record — so admins can generate invoices, track what's owed per installment, and
lay the groundwork for parent visibility and online payment — without mixing
payment collection into the generation flow?

## Recommended Direction

Invoicing is the moment a fee template becomes money. The flow has three phases:

**1. Assignment** — Admin sets a default fee template for a grade/academic year.
This auto-assigns all enrolled students in that grade to that template. Admin can
then override individual students (different template, additional or different
discount rules pre-assigned from that template). This resolves the open question
from the fee template doc: "which template does a given student get?"

**2. Generation** — Admin selects a grade + academic year + template and generates
a batch of **Draft** invoices. Each invoice is a snapshot: line item amounts (after
applying any pre-assigned discount rules), installment entries with Admin-entered
due dates, and a derived total. Draft invoices can be regenerated; they replace
the existing Draft without touching any Issued invoice.

**3. Issuance** — Admin reviews and Issues the batch. The first Issued invoice
from a template **freezes** that template (no edits to line items, installments,
or discount rules going forward). An Issued invoice can be Cancelled but not
edited. Cancelling does not unfreeze the template.

**Payment-readiness without payment collection:** `FeeInvoiceInstallment` carries
`DueDate`, `Amount`, `Status (Pending/Paid/Overdue)`, `AmountPaid?`, and `PaidAt?`
columns from day one. The payment gateway phase populates those fields; this phase
only writes `DueDate` and `Amount`, leaving payment columns null.

## Key Assumptions to Validate

- [x] One active invoice per student per academic year — re-generating replaces
      the Draft; an Issued invoice can only be Cancelled, not overwritten.
- [x] Admin sets installment due dates at generation time — the template stores
      percentages only; Admin enters actual calendar dates when generating.
- [x] Cancelling an Issued invoice does not unfreeze the template — freeze is
      permanent once the first invoice from a template is Issued.
- [x] Grade broadcast assigns students based on their current active enrollment
      in that grade for the given academic year — confirmed: `StudentSectionEnrollment`
      with unique `(StudentId, AcademicYearId)` index is the source; query joins
      on `SectionId → Section.GradeId` to find all students enrolled in a grade.
- [x] A student's discount assignments are scoped per academic year (not global)
      — confirmed: `StudentDiscountAssignment` carries `AcademicYearId`; no
      automatic carry-forward between years.

## MVP Scope

**New entities (backend):**
- `StudentFeeAssignment` — StudentId + FeeTemplateId + AcademicYearId; one row
  per student per year; can be overridden from the grade default
- `StudentDiscountAssignment` — StudentId + DiscountRuleId + AcademicYearId;
  rules from the assigned template that apply to this student
- `FeeInvoice` — StudentId, FeeTemplateId, AcademicYearId, TotalAmount (after
  discounts), Status (Draft/Issued/Cancelled)
- `FeeInvoiceLineItem` — snapshot: name, original amount, discount amount, final
  amount; linked to FeeInvoice
- `FeeInvoiceInstallment` — snapshot: name, DueDate, Amount, Status (Pending),
  AmountPaid (null), PaidAt (null); linked to FeeInvoice

**Backend API flows:**
- Assign default template to grade: `POST /fee-templates/{id}/assign-grade`
  (bulk-creates StudentFeeAssignments for all enrolled students)
- Override per student: `PUT /students/{id}/fee-assignment`
- Manage student discount assignments: CRUD on
  `POST/DELETE /students/{id}/discount-assignments`
- Generate invoices (batch): `POST /fee-invoices/generate`
  (body: templateId, academicYearId, gradeId; creates/replaces Drafts)
- Issue invoice(s): `POST /fee-invoices/{id}/issue` + bulk endpoint
- Cancel invoice: `POST /fee-invoices/{id}/cancel`
- List invoices: `GET /fee-invoices` (filter by status/grade/year/student)
- Invoice detail: `GET /fee-invoices/{id}` (with line items + installments)

**Frontend flows:**
- Fee Assignment page (per grade/year): view grade default, override per student,
  manage per-student discount assignments
- Invoice Generation page: select grade/year/template → enter due dates per
  installment → preview count → generate
- Invoice List: filterable by status, grade, academic year; bulk Issue action
- Invoice Detail: line items + installment schedule + applied discounts

## Not Doing (and Why)

- **Payment recording / collection** — payment gateway phase; installment schema
  is payment-ready but the UI doesn't expose payment fields yet
- **Invoice PDF export** — useful but not core for the demo
- **Parent portal visibility** — parent portal is a separate module
- **Overdue auto-detection** — requires a background job / cron; add when
  payment recording is introduced
- **Partial payments** — gateway phase feature
- **Invoice editing after issuance** — immutability is the point; Cancel + re-issue
  is the correction flow
- **Multi-template per student in one year** — one active invoice per student per
  year keeps the model clean; edge cases don't justify the complexity now
- **Discount stacking rules / precedence** — discount rules are applied
  independently; no stacking logic in this phase

## Open Questions (Resolved)

- **Grade broadcast collision** — Skip students who already have a
  `StudentFeeAssignment` for that academic year (preserves any manual override);
  return a warning count of how many were skipped. ✓ Decided: Option A (skip + warn).
- **Draft regeneration** — If a Draft already exists for a student, **block** the
  generation and require the admin to cancel it first. Do not auto-replace.
  Issued invoices are always skipped with a warning. ✓ Decided: Block (Option B).
- **Invoice numbering** — Auto-generated code (e.g., `INV-2025-000001`), same
  `IOptions<InvoiceOptions>` + configurable retry pattern as `StudentCode`. The
  generator interface (`IInvoiceCodeGenerator`) is injected so the mechanism can
  be swapped without touching service logic. ✓ Decided: auto-generated, swappable.
