# Fee Structure Templates

## Problem Statement
How might we let school admins define reusable, structured fee templates per
grade and academic year — with line item breakdowns, installment schedules, and
discount rules — so that per-student invoicing can be generated consistently
without manual re-entry every cycle?

## Recommended Direction
A fee template is a named, reusable blueprint scoped to one academic year and
one grade level. A grade/year pair can have multiple named templates (e.g.,
"Standard", "Merit Scholarship Track"), giving the school flexibility without
over-constraining the data model.

Each template composes three sub-entities:

- **Fee line items** — named charges (Tuition, Lab Fee, Activity Fee) with
  individual amounts. The template total is derived from these.
- **Installment schedule** — ordered entries (name + percentage) that define
  how payments are split over the year. Percentages must sum to 100%.
- **Discount rules** — named reductions (percentage or flat amount) that
  optionally target a specific line item or the invoice total. Rules are
  defined on the template; they're applied to a specific student at
  invoice-generation time.

Templates are freely editable in this phase. Once invoicing is introduced,
a template will be frozen when the first invoice is generated from it.

## Key Assumptions to Validate
- [ ] A grade/year can have multiple templates — confirm the assignment flow
      (which template does a given student get?) is acceptable before invoicing.
- [ ] Installment % validation (must sum to 100) is enforced at save time.
- [ ] Discount rules targeting a specific line item are rare enough that a
      simple nullable FK (DiscountRule → FeeLineItem) is sufficient UX.
- [ ] IsActive soft-disable on templates is sufficient (no hard delete).

## MVP Scope

**Backend:**
- FeeTemplate CRUD (name, AcademicYearId, GradeId, IsActive)
- FeeLineItem CRUD on a template (name, amount, display order)
- InstallmentScheduleEntry CRUD on a template (name, percentage, display order;
  validate sum = 100 on save)
- DiscountRule CRUD on a template (name, type Percentage|FlatAmount, value,
  optional FeeLineItemId)
- Admin-only API, EF Core + PostgreSQL, all existing patterns

**Frontend:**
- Templates list page (filterable by academic year + grade)
- Template detail/edit view — tabs or sections for line items, installments,
  discount rules
- Admin-only; consistent with existing CRUD UI patterns

## Not Doing (and Why)
- **Per-student invoice generation** — separate feature; templates are the
  foundation, not the full financial module
- **Payment recording / collection tracking** — invoicing feature
- **Template versioning / immutability** — premature; invoicing will introduce
  the freeze point naturally
- **Discount application to students** — happens at invoice time, not here
- **Per-section fee variation** — grade-level scope covers the 90% case; add
  if a real school requires it
- **Timetable-linked fees** (lab fees auto-triggered by subject enrollment) —
  out of scope for the demo

## Open Questions
- When invoicing is added: if a student has multiple templates available for
  their grade/year, who picks which one applies to them — Admin at enrollment,
  or a separate assignment step?
- Should discount rules have a name unique-constraint per template, or allow
  duplicates (e.g., two different "Sibling Discount" entries at different %)?
