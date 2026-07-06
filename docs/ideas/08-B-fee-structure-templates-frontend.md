# Fee Structure Templates — Frontend

## Problem Statement
How might we let school admins create, browse, and fully edit fee structure templates
(line items, installment schedules, discount rules) through a consistent, admin-friendly
UI that follows the existing React/shadcn patterns without feeling cramped for the
complexity of three child collections?

## Recommended Direction

A two-view feature: a list page and a dedicated detail page with explicit view/edit
modes — the first feature in this app complex enough to warrant its own route rather
than a modal.

### List page (`/admin/fee-templates`)

- **Status tabs:** Active / Inactive / All (same tab pattern as Teachers, Subjects)
- **Inline filter row:** Academic Year dropdown + Grade dropdown above the table,
  resetting page to 1 on change
- **Table columns:** Name · Grade · Academic Year · Total Amount · Line Item Count ·
  Status badge · Edit icon button (navigates to detail page in edit mode)
- **Row click** navigates to detail page in view mode
- **"New Template" button** opens a lightweight create modal (Name + Academic Year +
  Grade); on 201 success, navigates directly to the new template's detail page in edit mode

### Detail page (`/admin/fee-templates/:id`) — view/edit modes

The detail page has two explicit modes.

**View mode** (default when arriving via row click):
- Read-only display of template header (Name, Grade, Academic Year, IsActive status badge)
- Read-only tabs showing the three child collections as plain tables
- "Edit" button in the header area switches to edit mode

**Edit mode** (default when arriving via the list's edit icon button, or after clicking Edit in view mode):
- Inline-editable header: Name field + IsActive toggle with a "Save Header" button
  (commits via `PUT /api/fee-templates/{id}`)
- Inactive template shows a warning banner: "This template is inactive and will not
  appear in the default list"
- Three editable tabs with local client-side state and batch save per tab (see below)
- Browser "you have unsaved changes" guard fires when navigating away with any
  unsaved tab changes

**Three-tab section (Line Items | Installments | Discount Rules) — edit mode only:**
- Each tab maintains local client-side state independent of the others
- A "Save Changes" / "Discard" button pair commits the entire collection via the
  replace endpoint (`PUT …/line-items`, `PUT …/installments`, `PUT …/discount-rules`)
- Unsaved changes indicator on the tab label ("Line Items ●") when local state diverges
  from last-fetched state

**Line Items tab:** Name · Amount · DisplayOrder columns; Add row button; delete icon
per row

**Installments tab:** Name · Percentage · DisplayOrder columns; running sum shown
persistently (e.g. "Total: 75% / 100%") with a warning badge when sum ≠ 100 — Save
button disabled until sum equals 100% (or list is empty)

**Discount Rules tab:** Name · Rule Type (Percentage / FlatAmount) · Value · Target
Line Item dropdown — dropdown is populated from the *last-saved* line items for this
template; admin must save line items first before targeting them in a discount rule

## Key Assumptions

- ✅ Browser "you have unsaved changes" guard is used when navigating away with unsaved tab edits
- ✅ `PUT /api/fee-templates/{id}` accepts only Name + IsActive; Grade and Academic Year
  are fixed at creation and not editable on the detail page
- ✅ Discount Rules "Target Line Item" dropdown reflects saved line items only — admin
  must save the Line Items tab before targeting a line item in a discount rule; this is
  acceptable UX

## MVP Scope

**In:**
- `FeeTemplatesPage` — list with status tabs, Academic Year + Grade filter dropdowns,
  table (row click → view mode; edit icon → edit mode), pagination
- `CreateFeeTemplateModal` — Name + Academic Year + Grade; navigates to detail in edit mode on success
- `FeeTemplatePage` at `/admin/fee-templates/:id` with view/edit mode toggle
  - View mode: read-only header + read-only child collection tabs
  - Edit mode: inline header edit + three editable tabs with batch save
  - `LineItemsTab` — inline editable table, batch save
  - `InstallmentsTab` — inline editable table, live sum indicator, save disabled when sum ≠ 100
  - `DiscountRulesTab` — inline editable table, dropdown from saved line items
  - Browser unsaved-changes guard
- API client functions for all seven endpoints defined in spec 08
- Route added to the admin router

**Out of MVP:** drag-to-reorder rows (use DisplayOrder number input instead), per-row
individual save (batch matches the API), template duplication/copy

## Not Doing (and Why)

- **Template duplication** — not in the backend spec; add when there's a real need
- **Drag-and-drop reorder** — DisplayOrder number input achieves the same goal with
  less complexity; revisit if admins request it
- **"Target Line Item" cross-tab sync** — Discount Rules dropdown reflects saved line
  items only; syncing with unsaved edits across tabs adds state complexity for a rare
  edge case
- **Editing Grade / Academic Year after creation** — backend `PUT` endpoint does not
  accept these fields; they are fixed at creation
