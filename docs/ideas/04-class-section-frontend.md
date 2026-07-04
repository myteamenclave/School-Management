# Class / Section Structure — Frontend

## Problem Statement
How might we give school admins a clear, efficient UI to manage their Grade/Section
catalog — the foundational hierarchy that every downstream module (enrollment,
attendance, gradebook) will reference?

## Recommended Direction
An accordion-based admin page where each Grade is a collapsible card. Sections are
managed inline within the expanded card, not in separate pages or modals. The
design mirrors the Academic Year page style (same patterns: React Query, React Hook
Form + Zod, shadcn/ui components, toast feedback) while keeping section creation and
editing as lightweight in-place interactions.

**Key UX decisions:**
- **Accordion per grade**, sorted by `DisplayOrder`. Section count shown in the
  collapsed header so admins know at a glance what's inside.
- **Create Grade** via a modal (Name + DisplayOrder). After creation, the new grade's
  accordion auto-opens to prompt adding sections immediately.
- **Add Section** inline: a small input + save button appears at the end of the
  section chip row. No modal needed — sections are simple one-field records.
- **Edit Section** inline: clicking a section chip transforms it into an editable
  input with save/cancel + delete icon. The chip reverts to its default state on
  cancel or success.
- **Edit Grade** via a modal (same fields as create).
- **Delete Grade**: button is disabled with a tooltip when the grade has sections
  ("Delete all sections first"). A confirmation dialog is shown when the grade is
  empty. No relying on a 400 response to communicate this — the UI guards it.
- **DisplayOrder**: numeric field in the create/edit modal. Drag-and-drop deferred
  as premature complexity.

## Key Assumptions to Validate
- [ ] A school will typically have 6–12 grades — accordion layout stays manageable at
      that scale without needing pagination or search.
- [ ] Sections per grade will typically be 2–6 — inline chip layout fits without
      wrapping issues at normal viewport widths.
- [ ] Inline section edit (no modal) is sufficient — single-field records don't
      justify the overhead of a full dialog.

## MVP Scope
**In:**
- `GradesPage` — accordion list of all grades, sorted by DisplayOrder
- `CreateGradeModal` — name + display order, auto-opens accordion on success
- `EditGradeModal` — same fields as create
- `GradeAccordionItem` — expandable card with inline section chip row
- Inline "Add Section" form within the accordion (input + save button)
- Inline section chip edit: click → editable input + save/cancel + delete icon
- Disabled delete button on grade with sections (tooltip explanation)
- Confirmation dialog before deleting an empty grade
- Sidebar link added next to Academic Year and Teachers
- Route: `/admin/grades` (Admin role only)
- Full React Query integration: `useQuery` for list, `useMutation` for all CUD ops
- Toast feedback for all mutations (success + error)
- Zod + React Hook Form for grade create/edit modals

**Out:**
- Drag-and-drop reordering of grades (deferred — numeric DisplayOrder field suffices)
- Bulk section creation (one-at-a-time inline is sufficient for a rarely-changing catalog)
- Search / filter (out of scope for a catalog of ~10 grades max)
- Section ordering within a grade (not a backend concept; backend has no `DisplayOrder`
  on sections)

## Not Doing (and Why)
- **Section creation inside the Create Grade modal** — grade must exist before sections
  can be added (gradeId is required). Doing both in one modal means N+1 API calls
  with partial-failure risk. Two-phase flow (create grade → add sections inline) is
  simpler and more resilient.
- **Separate section management page** — overkill for a one-field entity. Inline is
  faster and sufficient.
- **Drag-and-drop reordering** — adds library complexity (dnd-kit or similar) with
  low payoff for a catalog admins set up once and rarely touch.
- **Archive instead of delete** — grades/sections are structural catalog entries.
  Hard delete with referential integrity is the right model (backend spec decision).

## Open Questions
None — all design decisions resolved during ideation session.
