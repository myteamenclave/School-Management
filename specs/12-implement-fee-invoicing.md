# Spec 12 — Implement Fee Invoicing (Backend)

## Related Docs & Prior Specs

- **Idea doc**: [docs/ideas/08-fee-invoicing.md](../docs/ideas/08-fee-invoicing.md)
- **Fee template entities**: [specs/08-implement-fee-structure-templates.md](08-implement-fee-structure-templates.md)
- **Student section enrollment** (for grade broadcast query): [specs/10-implement-class-section-assignment.md](10-implement-class-section-assignment.md)
- **Multi-tenant base**: [specs/01-implement-multi-tenant-data-model.md](01-implement-multi-tenant-data-model.md)

## Overview

Turn a fee template into per-student financial records. Three flows: **Assignment** (which template a student gets), **Generation** (Draft invoices with snapshotted amounts + due dates), **Issuance** (immutable Issued → Cancelled lifecycle).

## New Domain Entities

### `StudentFeeAssignment`

```csharp
// Domain/Entities/StudentFeeAssignment.cs
public class StudentFeeAssignment : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Guid FeeTemplateId { get; set; }
    public FeeTemplate FeeTemplate { get; set; } = null!;
    public Guid AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
}
```

Unique index: `(SchoolId, StudentId, AcademicYearId)` — one assignment per student per year.

### `StudentDiscountAssignment`

```csharp
// Domain/Entities/StudentDiscountAssignment.cs
public class StudentDiscountAssignment : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Guid DiscountRuleId { get; set; }
    public DiscountRule DiscountRule { get; set; } = null!;
    public Guid AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
}
```

Unique index: `(SchoolId, StudentId, DiscountRuleId, AcademicYearId)` — each rule applied at most once per student per year.

### `FeeInvoice`

```csharp
// Domain/Entities/FeeInvoice.cs
public class FeeInvoice : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Guid FeeTemplateId { get; set; }
    public FeeTemplate FeeTemplate { get; set; } = null!;
    public Guid AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateTime? IssuedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    private readonly List<FeeInvoiceLineItem> _lineItems = [];
    public IReadOnlyList<FeeInvoiceLineItem> LineItems => _lineItems.AsReadOnly();

    private readonly List<FeeInvoiceInstallment> _installments = [];
    public IReadOnlyList<FeeInvoiceInstallment> Installments => _installments.AsReadOnly();
}
```

Uniqueness: enforce in application layer — only one non-Cancelled invoice per `(SchoolId, StudentId, AcademicYearId)` at a time. Use a partial unique index in EF config:

```csharp
// Infrastructure: FeeInvoiceConfiguration
entity.HasIndex(e => new { e.SchoolId, e.StudentId, e.AcademicYearId })
    .HasFilter("\"Status\" != 2")   // 2 = Cancelled enum value
    .IsUnique();
```

### `FeeInvoiceLineItem`

```csharp
// Domain/Entities/FeeInvoiceLineItem.cs
public class FeeInvoiceLineItem : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid FeeInvoiceId { get; set; }
    public FeeInvoice FeeInvoice { get; set; } = null!;
    public Guid? SourceLineItemId { get; set; }     // nullable — source may be deleted later
    public string Name { get; set; } = string.Empty; // snapshot
    public decimal OriginalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public int DisplayOrder { get; set; }
}
```

### `FeeInvoiceInstallment`

```csharp
// Domain/Entities/FeeInvoiceInstallment.cs
public class FeeInvoiceInstallment : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid FeeInvoiceId { get; set; }
    public FeeInvoice FeeInvoice { get; set; } = null!;
    public Guid? SourceInstallmentId { get; set; }  // nullable — source may be deleted later
    public string Name { get; set; } = string.Empty; // snapshot
    public decimal Percentage { get; set; }          // snapshot
    public DateOnly? DueDate { get; set; }
    public decimal Amount { get; set; }              // TotalAmount * Percentage / 100
    public InstallmentStatus Status { get; set; } = InstallmentStatus.Pending;
    public decimal? AmountPaid { get; set; }         // null until payment gateway phase
    public DateTime? PaidAt { get; set; }
    public int DisplayOrder { get; set; }
}
```

## New Enums

```csharp
// Domain/Enums/InvoiceStatus.cs
public enum InvoiceStatus { Draft = 0, Issued = 1, Cancelled = 2 }

// Domain/Enums/InstallmentStatus.cs
public enum InstallmentStatus { Pending = 0, Paid = 1, Overdue = 2 }
```

## Modify `FeeTemplate` — Add `IsFrozen`

```csharp
// Add to FeeTemplate.cs
public bool IsFrozen { get; set; } = false;
```

`IsFrozen` is set to `true` when the first invoice from this template is Issued. Frozen templates cannot have their `LineItems`, `Installments`, or `DiscountRules` modified — `FeeTemplateService` checks this guard before any replace operation.

## Invoice Code Generation

Mirror the `StudentCode` pattern exactly.

### Options

```csharp
// Application/FeeInvoices/InvoiceOptions.cs
public class InvoiceOptions
{
    public const string SectionName = "Invoices";
    public int InvoiceCodeMaxRetries { get; set; } = 3;
}
```

Register in `appsettings.json` under `"Invoices": { "InvoiceCodeMaxRetries": 3 }` and wire in `Application/DependencyInjection.cs`:

```csharp
services.Configure<InvoiceOptions>(configuration.GetSection(InvoiceOptions.SectionName));
```

### Repository Method

Add to `IFeeInvoiceRepository`:

```csharp
Task<string> GetNextInvoiceCodeAsync(int year, CancellationToken ct = default);
```

Implementation (in `FeeInvoiceRepository`): query `MAX(InvoiceCode)` where code matches `INV-{year}-%`, parse the counter, increment, format as `INV-{year}-{counter:D6}`. Returns `INV-{year}-000001` if none exist for that year.

### Retry in Service

```csharp
for (var attempt = 0; attempt < _maxRetries; attempt++)
{
    invoice.InvoiceCode = await invoiceRepository.GetNextInvoiceCodeAsync(academicYear.StartDate.Year, ct);
    await invoiceRepository.AddAsync(invoice, ct);
    try
    {
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(invoice);
    }
    catch (ConflictException) when (attempt < _maxRetries - 1)
    {
        unitOfWork.Detach(invoice);
    }
}
throw new DomainException("Unable to assign an invoice code. Please try again.");
```

## Repository Interfaces

All in `Application` layer. All extend `IRepository<T>` from `Application/Interfaces`.

### `IStudentFeeAssignmentRepository`

```csharp
// Application/FeeInvoices/IStudentFeeAssignmentRepository.cs
public interface IStudentFeeAssignmentRepository : IRepository<StudentFeeAssignment>
{
    Task<StudentFeeAssignment?> GetByStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default);

    Task<List<StudentFeeAssignment>> GetByTemplateAsync(
        Guid templateId, CancellationToken ct = default);

    Task<List<StudentFeeAssignment>> GetByGradeAndYearAsync(
        Guid gradeId, Guid academicYearId, CancellationToken ct = default);
}
```

`GetByGradeAndYearAsync` joins through `FeeTemplate` to filter by `Template.GradeId`.

### `IStudentDiscountAssignmentRepository`

```csharp
// Application/FeeInvoices/IStudentDiscountAssignmentRepository.cs
public interface IStudentDiscountAssignmentRepository : IRepository<StudentDiscountAssignment>
{
    Task<List<StudentDiscountAssignment>> GetByStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default);

    Task<StudentDiscountAssignment?> GetByStudentRuleAndYearAsync(
        Guid studentId, Guid discountRuleId, Guid academicYearId, CancellationToken ct = default);
}
```

### `IFeeInvoiceRepository`

```csharp
// Application/FeeInvoices/IFeeInvoiceRepository.cs
public interface IFeeInvoiceRepository : IRepository<FeeInvoice>
{
    Task<string> GetNextInvoiceCodeAsync(int year, CancellationToken ct = default);

    Task<FeeInvoice?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

    Task<FeeInvoice?> GetActiveForStudentAndYearAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default);

    Task<(List<FeeInvoice> Items, int TotalCount)> GetPagedAsync(
        InvoiceStatus? status, Guid? gradeId, Guid? academicYearId,
        Guid? studentId, int page, int pageSize, CancellationToken ct = default);
}
```

`GetActiveForStudentAndYearAsync` returns the single non-Cancelled invoice for a student/year, or null.

`GetPagedAsync` with `gradeId` filter joins through `FeeTemplate` to filter by `Template.GradeId`.

## DTOs

All records in `Application/FeeInvoices/Dtos/`.

```csharp
// Assignment DTOs
public record StudentFeeAssignmentDto(
    Guid Id, Guid StudentId, string StudentName, string StudentCode,
    Guid FeeTemplateId, string TemplateName,
    Guid AcademicYearId, string AcademicYearName);

public record StudentDiscountAssignmentDto(
    Guid Id, Guid StudentId, Guid DiscountRuleId, string DiscountRuleName,
    string RuleType, decimal Value, Guid AcademicYearId);

// Broadcast
public record BroadcastAssignmentResult(int Assigned, int Skipped);

// Invoice DTOs
public record FeeInvoiceSummaryDto(
    Guid Id, string InvoiceCode,
    Guid StudentId, string StudentName, string StudentCode,
    Guid AcademicYearId, string AcademicYearName,
    Guid FeeTemplateId, string TemplateName,
    decimal TotalAmount, string Status,
    DateTime? IssuedAt, DateTime CreatedAt);

public record FeeInvoiceDto(
    Guid Id, string InvoiceCode,
    Guid StudentId, string StudentName, string StudentCode,
    Guid AcademicYearId, string AcademicYearName,
    Guid FeeTemplateId, string TemplateName,
    decimal TotalAmount, string Status,
    DateTime? IssuedAt, DateTime? CancelledAt,
    DateTime CreatedAt, DateTime? UpdatedAt,
    List<FeeInvoiceLineItemDto> LineItems,
    List<FeeInvoiceInstallmentDto> Installments);

public record FeeInvoiceLineItemDto(
    Guid Id, string Name,
    decimal OriginalAmount, decimal DiscountAmount, decimal FinalAmount,
    int DisplayOrder);

public record FeeInvoiceInstallmentDto(
    Guid Id, string Name, decimal Percentage,
    DateOnly? DueDate, decimal Amount, string Status, int DisplayOrder);

// Generate
public record GenerateInvoicesRequest(
    Guid GradeId,
    Guid AcademicYearId,
    List<InstallmentDueDateInput> InstallmentDueDates);

public record InstallmentDueDateInput(Guid TemplateInstallmentId, DateOnly DueDate);

public record GenerateInvoicesResult(int Generated, int Skipped);

// Issue / Cancel
public record BulkIssueRequest(List<Guid> Ids);
public record BulkIssueResult(int Issued, int Skipped);
```

## Request Objects (for FluentValidation)

```csharp
// SetStudentAssignmentRequest
public record SetStudentAssignmentRequest(Guid FeeTemplateId, Guid AcademicYearId);

// AddStudentDiscountRequest
public record AddStudentDiscountRequest(Guid DiscountRuleId, Guid AcademicYearId);
```

## Application Service — `FeeInvoiceService`

File: `Application/FeeInvoices/FeeInvoiceService.cs`

Constructor injection:
- `IStudentFeeAssignmentRepository assignmentRepo`
- `IStudentDiscountAssignmentRepository discountAssignmentRepo`
- `IFeeInvoiceRepository invoiceRepo`
- `IRepository<FeeInvoiceLineItem> lineItemRepo`
- `IRepository<FeeInvoiceInstallment> installmentRepo`
- `IFeeTemplateRepository templateRepo`
- `IAcademicYearRepository yearRepo`
- `IGradeRepository gradeRepo`
- `IStudentRepository studentRepo`
- `IUnitOfWork unitOfWork`
- `IOptions<InvoiceOptions> options`

### Key Methods

**`BroadcastAssignmentAsync(Guid templateId, CancellationToken ct)`**

1. Load template (with Grade + AcademicYear), throw `NotFoundException` if missing.
2. Load all existing `StudentFeeAssignment`s for `(Template.AcademicYearId)` via `assignmentRepo.GetByGradeAndYearAsync(template.GradeId, template.AcademicYearId)`.
3. Build `HashSet<Guid>` of studentIds that already have an assignment.
4. Load all students enrolled in this grade/year via `IStudentSectionEnrollmentRepository.GetByGradeAndYearAsync` (see below — add this method).
5. For each enrolled student not already assigned: create `StudentFeeAssignment`.
6. `SaveChangesAsync`.
7. Return `BroadcastAssignmentResult(assigned, skipped)`.

**`GenerateInvoicesAsync(GenerateInvoicesRequest request, CancellationToken ct)`**

1. Validate grade and academic year exist.
2. Load all `StudentFeeAssignment`s for this grade/year via `assignmentRepo.GetByGradeAndYearAsync`.
3. Load all `StudentDiscountAssignment`s for this year (batch query by academicYearId for all relevant students — or load per-student; implementation detail).
4. For each assignment:
   a. `invoiceRepo.GetActiveForStudentAndYearAsync` — if a non-Cancelled invoice exists, `skipped++`, continue.
   b. Load the student's template (with line items, installments, discount rules via `templateRepo.GetByIdWithChildrenAsync`).
   c. Compute per-line-item amounts:
      - Find student's applicable discount rules: `StudentDiscountAssignment`s where rule belongs to this template and `(rule.FeeLineItemId == lineItem.Id || rule.FeeLineItemId == null)`.
      - For each matched rule: `Percentage` → `lineItem.Amount * rule.Value / 100`; `FlatAmount` → `Math.Min(rule.Value, lineItem.Amount)`.
      - `DiscountAmount = Min(sum of applicable discounts, lineItem.Amount)`.
      - `FinalAmount = lineItem.Amount - DiscountAmount`.
   d. `TotalAmount = FinalAmount` sum across all line items.
   e. Build `FeeInvoiceInstallment`s — for each template installment, find matching due date from `request.InstallmentDueDates`; `DueDate = null` if not provided.
   f. Create invoice with retry loop (invoice code conflicts).
5. Return `GenerateInvoicesResult(generated, skipped)`.

**`IssueInvoiceAsync(Guid id, CancellationToken ct)`**

1. Load invoice, throw `NotFoundException` if missing.
2. If `Status != Draft`, throw `DomainException("Only Draft invoices can be issued.")`.
3. Set `Status = Issued`, `IssuedAt = now`.
4. Load the template; if `!IsFrozen`, set `IsFrozen = true`, call `templateRepo.Update(template)`.
5. `SaveChangesAsync`.
6. Return updated DTO.

**`CancelInvoiceAsync(Guid id, CancellationToken ct)`**

1. Load invoice. If `Status == Cancelled`, throw `DomainException("Invoice is already cancelled.")`.
2. Set `Status = Cancelled`, `CancelledAt = now`.
3. `SaveChangesAsync`.
4. Return updated DTO.

**`BulkIssueAsync(List<Guid> ids, CancellationToken ct)`**

Issue in a loop; skip and count invoices that cannot be issued (not Draft). Use a transaction via `unitOfWork.BeginTransactionAsync` + `CommitAsync`.

## `IStudentSectionEnrollmentRepository` Addition

Add to the existing interface:

```csharp
Task<List<StudentSectionEnrollment>> GetByGradeAndYearAsync(
    Guid gradeId, Guid academicYearId, CancellationToken ct = default);
```

Implementation: join `Section` on `Section.GradeId == gradeId`, filter on `AcademicYearId`. Include `Student`.

## `FeeTemplateService` Frozen Guard

Add to the top of `ReplaceLineItemsAsync`, `ReplaceInstallmentsAsync`, and `ReplaceDiscountRulesAsync`:

```csharp
if (template.IsFrozen)
    throw new DomainException(
        "This template is frozen because one or more invoices have been issued from it. " +
        "Line items, installments, and discount rules cannot be modified.");
```

## API Controllers

### `FeeAssignmentsController`

Route: `api/fee-assignments`

| Method | Route | Body/Query | Returns |
|---|---|---|---|
| POST | `/broadcast` | `{ templateId }` | `BroadcastAssignmentResult` |
| GET | `/` | `?studentId=&academicYearId=` | `StudentFeeAssignmentDto?` |
| PUT | `/` | `SetStudentAssignmentRequest` + `?studentId=` | `StudentFeeAssignmentDto` |
| DELETE | `/` | `?studentId=&academicYearId=` | 204 |
| GET | `/discounts` | `?studentId=&academicYearId=` | `List<StudentDiscountAssignmentDto>` |
| POST | `/discounts` | `AddStudentDiscountRequest` + `?studentId=` | `StudentDiscountAssignmentDto` |
| DELETE | `/discounts/{id}` | — | 204 |

The `DELETE /` and `GET /` are read/delete with no side effects per query param shape — `DELETE` uses the `[HttpDelete]` verb (compliant with CSRF rules).

### `FeeInvoicesController`

Route: `api/fee-invoices`

| Method | Route | Body/Query | Returns |
|---|---|---|---|
| POST | `/generate` | `GenerateInvoicesRequest` | `GenerateInvoicesResult` |
| GET | `/` | `?status=&gradeId=&academicYearId=&studentId=&page=&pageSize=` | `PagedResult<FeeInvoiceSummaryDto>` |
| GET | `/{id}` | — | `FeeInvoiceDto` |
| POST | `/{id}/issue` | — | `FeeInvoiceDto` |
| POST | `/bulk-issue` | `BulkIssueRequest` | `BulkIssueResult` |
| POST | `/{id}/cancel` | — | `FeeInvoiceDto` |

All state-mutating endpoints use POST/PUT/DELETE (no GET side effects — CSRF rule compliant).

## `AppDbContext` Additions

```csharp
public DbSet<StudentFeeAssignment> StudentFeeAssignments => Set<StudentFeeAssignment>();
public DbSet<StudentDiscountAssignment> StudentDiscountAssignments => Set<StudentDiscountAssignment>();
public DbSet<FeeInvoice> FeeInvoices => Set<FeeInvoice>();
public DbSet<FeeInvoiceLineItem> FeeInvoiceLineItems => Set<FeeInvoiceLineItem>();
public DbSet<FeeInvoiceInstallment> FeeInvoiceInstallments => Set<FeeInvoiceInstallment>();
```

## EF Core Entity Configurations

Create `Infrastructure/Persistence/Configurations/` files for each new entity:
- `StudentFeeAssignmentConfiguration` — unique index `(SchoolId, StudentId, AcademicYearId)`
- `StudentDiscountAssignmentConfiguration` — unique index `(SchoolId, StudentId, DiscountRuleId, AcademicYearId)`
- `FeeInvoiceConfiguration` — partial unique index on non-Cancelled, `InvoiceCode` max length 20, decimal precision for `TotalAmount (18,2)`
- `FeeInvoiceLineItemConfiguration` — decimal precision `(18,2)` on all amount columns
- `FeeInvoiceInstallmentConfiguration` — decimal precision `(18,2)` on `Amount`/`AmountPaid`/`Percentage`

## Migration

Add one migration: `AddFeeInvoicing`. It creates the 5 new tables and adds `IsFrozen` (bool, default false) to `FeeTemplates`.

```
dotnet ef migrations add AddFeeInvoicing --project SchoolMgmt.Infrastructure --startup-project SchoolMgmt.WebApi
```

## DI Registration

In `Infrastructure/DependencyInjection.cs`:

```csharp
services.AddScoped<IStudentFeeAssignmentRepository, StudentFeeAssignmentRepository>();
services.AddScoped<IStudentDiscountAssignmentRepository, StudentDiscountAssignmentRepository>();
services.AddScoped<IFeeInvoiceRepository, FeeInvoiceRepository>();
```

In `Application/DependencyInjection.cs`:

```csharp
services.AddScoped<FeeInvoiceService>();
services.Configure<InvoiceOptions>(configuration.GetSection(InvoiceOptions.SectionName));
```

## Acceptance Criteria

1. `POST /fee-assignments/broadcast` creates `StudentFeeAssignment` rows for all students enrolled in the template's grade/year; returns skip count for students already assigned.
2. `PUT /fee-assignments` upserts (create or update) a student's fee assignment for a year.
3. `POST /fee-invoices/generate` creates Draft invoices with snapshotted line item amounts (including applied discounts) and installments with due dates; skips students who already have a non-Cancelled invoice.
4. `POST /fee-invoices/{id}/issue` transitions Draft → Issued, sets `IssuedAt`, freezes the template if not already frozen.
5. `POST /fee-invoices/{id}/cancel` transitions any non-Cancelled invoice → Cancelled.
6. `POST /fee-invoices/bulk-issue` issues all Draft invoices in the provided ID list; returns count of issued and skipped.
7. `GET /fee-invoices` returns paged list filterable by status, grade, academic year, student.
8. `GET /fee-invoices/{id}` returns full invoice with line items and installments.
9. Attempting to modify line items/installments/discount rules on a frozen template returns 400 with a clear message.
10. Attempting to re-generate invoices for students who already have non-Cancelled invoices skips them (does not overwrite), returning the skip count.
11. `InvoiceCode` follows `INV-{year}-{counter:D6}` format and is unique per school.
12. All new endpoints are covered by the existing global validation and logging filters.
