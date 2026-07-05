# Fee Structure Templates — Sample Data & Use Cases

Companion to [07-fee-structure-templates.md](07-fee-structure-templates.md).
Use this file to understand the feature concretely and as a reference when
testing the backend API and frontend UI.

---

## Sample Templates

### Template A — Grade 5, Standard (AY 2025-2026)

**Scope:** Grade 5 · AY 2025-2026 · Name: "Standard" · IsActive: true

**Line Items**

| Name             | Amount   |
|------------------|----------|
| Tuition Fee      | 35,000   |
| Activity Fee     |  4,000   |
| Lab Fee          |  3,000   |
| **Total**        | **42,000** |

**Installment Schedule** (must sum to 100%)

| Name              | Percentage | Due Label       |
|-------------------|------------|-----------------|
| 1st Installment   | 40%        | Upon enrollment |
| 2nd Installment   | 30%        | Start of Term 2 |
| 3rd Installment   | 30%        | Start of Term 3 |

> 40% of 42,000 = 16,800 · 30% = 12,600 · 30% = 12,600

**Discount Rules**

| Name              | Type       | Value | Targets        |
|-------------------|------------|-------|----------------|
| Sibling Discount  | Percentage | 10%   | Tuition Fee    |
| Early Bird        | FlatAmount | 500   | (whole invoice)|

---

### Template B — Grade 5, Merit Scholarship (AY 2025-2026)

**Scope:** Grade 5 · AY 2025-2026 · Name: "Merit Scholarship" · IsActive: true

> Same grade/year as Template A — demonstrates that multiple templates per
> grade/year are allowed.

**Line Items** (same as Standard)

| Name             | Amount   |
|------------------|----------|
| Tuition Fee      | 35,000   |
| Activity Fee     |  4,000   |
| Lab Fee          |  3,000   |
| **Total**        | **42,000** |

**Installment Schedule** (2-term split instead of 3)

| Name              | Percentage | Due Label       |
|-------------------|------------|-----------------|
| 1st Installment   | 50%        | Upon enrollment |
| 2nd Installment   | 50%        | Start of Term 2 |

**Discount Rules**

| Name                   | Type       | Value | Targets     |
|------------------------|------------|-------|-------------|
| Full Tuition Scholarship | Percentage | 100%  | Tuition Fee |

> Student on this template pays 0 for Tuition but still pays Activity + Lab fees
> (4,000 + 3,000 = 7,000 total).

---

### Template C — Kindergarten, Standard (AY 2025-2026)

**Scope:** Kindergarten · AY 2025-2026 · Name: "Standard" · IsActive: true

**Line Items** (no Lab Fee for KG)

| Name              | Amount   |
|-------------------|----------|
| Tuition Fee       | 28,000   |
| Materials Fee     |  5,000   |
| **Total**         | **33,000** |

**Installment Schedule** (2-term split)

| Name              | Percentage | Due Label       |
|-------------------|------------|-----------------|
| 1st Installment   | 50%        | Upon enrollment |
| 2nd Installment   | 50%        | Start of Term 2 |

**Discount Rules**

| Name              | Type       | Value | Targets        |
|-------------------|------------|-------|----------------|
| Sibling Discount  | Percentage | 10%   | Tuition Fee    |

---

## Use Cases for Testing

### 1. Create a template (happy path)
- Create Template A (Grade 5, Standard) with all three line items, 3-entry
  installment schedule summing to 100%, and two discount rules.
- Expected: template created, total derived as 42,000.

### 2. Multiple templates per grade/year
- With Template A already saved, create Template B for the same Grade 5 +
  AY 2025-2026 under a different name ("Merit Scholarship").
- Expected: both templates exist; no unique-constraint error.

### 3. Installment percentages must sum to 100%
- Create a template with two installment entries at 40% and 40% (total 80%).
- Expected: API returns validation error; save blocked.

### 4. Installment percentages over 100%
- Enter entries at 60% and 50% (total 110%).
- Expected: validation error.

### 5. Edit a line item amount
- On Template A, change "Activity Fee" from 4,000 to 4,500.
- Expected: template total updates to 42,500; all existing installment
  percentages remain valid (they apply to whatever the total is at invoice time).

### 6. Discount rule targeting a specific line item
- Add a "Lab Fee Waiver" rule (100%, targets Lab Fee) to Template A.
- Expected: rule saved with a reference to the "Lab Fee" line item, not the
  invoice total.

### 7. Discount rule targeting the whole invoice
- Add an "Early Bird" rule (FlatAmount 500, no line item target) to Template A.
- Expected: rule saved with FeeLineItemId = null.

### 8. Deactivate a template
- Set Template A's IsActive to false.
- Expected: template no longer appears in the active templates list; still
  retrievable when querying all/inactive.

### 9. Delete a line item that is referenced by a discount rule
- On Template A, try to delete "Lab Fee" while a "Lab Fee Waiver" discount rule
  references it.
- Expected: either cascade-delete the discount rule, or block and return an error
  (decide in spec which behavior to implement).

### 10. Filter templates by academic year + grade
- Create Template C (Kindergarten). Query templates filtered to Grade 5 only.
- Expected: Template A and B returned; Template C excluded.

### 11. Installment schedule with a single entry
- Create a template with one installment entry at 100% (full payment on
  enrollment).
- Expected: valid — one-installment schedules are allowed.

### 12. Template with no discount rules
- Create a template with line items and installment schedule but zero discount
  rules.
- Expected: valid — discount rules are optional.

---

## Edge Cases to Keep in Mind

- **Zero-amount line item** — is a ₱0 line item meaningful? (e.g., a waived
  fee placeholder). Decide whether to allow or block in validation.
- **Empty installment schedule** — should a template be saveable with no
  installment entries? Probably not, but confirm in the spec.
- **Renaming a line item referenced by a discount rule** — the FK is by ID, so
  renaming is safe. The discount rule UI just shows the current name.
- **Two discount rules with the same name on the same template** — the idea doc
  leaves this open. Recommend allowing duplicates (names are labels, not IDs).
