# Spec: Implement Fee Structure Templates

## Related docs & specs

- [docs/ideas/07-fee-structure-templates.md](../docs/ideas/07-fee-structure-templates.md) — idea doc: problem statement, template composition model, scope decisions, not-doing list
- [docs/ideas/07-fee-structure-templates-examples.md](../docs/ideas/07-fee-structure-templates-examples.md) — sample data and test use cases
- [specs/01-implement-multi-tenant-data-model.md](01-implement-multi-tenant-data-model.md) — `BaseEntity`, `ITenantScoped`, `IRepository<TEntity>`, `IUnitOfWork`, `AppDbContext`; used as-is
- [specs/03-implement-academic-year-term-configuration.md](03-implement-academic-year-term-configuration.md) — `AcademicYear` entity; `FeeTemplate.AcademicYearId` references it
- [specs/04-implement-class-section-structure.md](04-implement-class-section-structure.md) — `Grade` entity; `FeeTemplate.GradeId` references it
- [specs/07-implement-subject-management.md](07-implement-subject-management.md) — primary structural reference: paged list pattern, `PagedResult<T>`, `ConflictException`, `NotFoundException`, DI conventions
- [.claude/rules/backend.md](../.claude/rules/backend.md) — thin-controller / Application-service pattern, GET-must-be-read-only, Repository / UnitOfWork rules

## Objective

Implement Admin-only CRUD for fee structure templates — the financial foundation the invoicing feature will build on. A `FeeTemplate` is a named, reusable blueprint scoped to one academic year and one grade level. Each template owns three child collections:

- **Fee line items** — named charges (Tuition, Lab Fee, etc.) with individual amounts
- **Installment schedule** — ordered entries defining how the total is split over the year (percentages that must sum to 100%)
- **Discount rules** — named reductions (percentage or flat amount) that optionally target a specific line item or the invoice total

Multiple templates are allowed per grade/year pair (distinguished by name), enabling scenarios like "Standard" and "Merit Scholarship" coexisting for Grade 5.

**Out of scope for this spec:** per-student invoice generation, payment recording, template versioning / immutability, discount application to individual students, frontend UI.

## Tech Stack

- .NET 8.0, C# — same solution as all prior specs
- EF Core (Npgsql) — four new tables (`FeeTemplates`, `FeeLineItems`, `FeeInstallments`, `DiscountRules`)
- FluentValidation — request validators via the existing global `IAsyncActionFilter`
- xUnit + `WebApplicationFactory` + Testcontainers (Postgres) — same integration test setup

## Design

### Domain — `SchoolMgmt.Domain`

#### New enum: `DiscountRuleType`

```csharp
// SchoolMgmt.Domain/Enums/DiscountRuleType.cs
namespace SchoolMgmt.Domain.Enums;

public enum DiscountRuleType
{
    Percentage,
    FlatAmount
}
```

#### New entity: `FeeTemplate`

```csharp
// SchoolMgmt.Domain/Entities/FeeTemplate.cs
namespace SchoolMgmt.Domain.Entities;

public class FeeTemplate : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
    public Guid GradeId { get; set; }
    public Grade Grade { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    private readonly List<FeeLineItem> _lineItems = [];
    public IReadOnlyList<FeeLineItem> LineItems => _lineItems.AsReadOnly();

    private readonly List<FeeInstallment> _installments = [];
    public IReadOnlyList<FeeInstallment> Installments => _installments.AsReadOnly();

    private readonly List<DiscountRule> _discountRules = [];
    public IReadOnlyList<DiscountRule> DiscountRules => _discountRules.AsReadOnly();
}
```

#### New entity: `FeeLineItem`

```csharp
// SchoolMgmt.Domain/Entities/FeeLineItem.cs
namespace SchoolMgmt.Domain.Entities;

public class FeeLineItem : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid FeeTemplateId { get; set; }
    public FeeTemplate FeeTemplate { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int DisplayOrder { get; set; }
}
```

#### New entity: `FeeInstallment`

```csharp
// SchoolMgmt.Domain/Entities/FeeInstallment.cs
namespace SchoolMgmt.Domain.Entities;

public class FeeInstallment : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid FeeTemplateId { get; set; }
    public FeeTemplate FeeTemplate { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public decimal Percentage { get; set; }  // e.g. 40.00 means 40 %
    public int DisplayOrder { get; set; }
}
```

#### New entity: `DiscountRule`

```csharp
// SchoolMgmt.Domain/Entities/DiscountRule.cs
namespace SchoolMgmt.Domain.Entities;

public class DiscountRule : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid FeeTemplateId { get; set; }
    public FeeTemplate FeeTemplate { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public DiscountRuleType RuleType { get; set; }
    public decimal Value { get; set; }
    public Guid? FeeLineItemId { get; set; }  // null → applies to invoice total
    public FeeLineItem? FeeLineItem { get; set; }
}
```

`FeeLineItemId` is nullable. When the referenced `FeeLineItem` is deleted, EF Core is configured to set this field to `null` (rule degrades to a whole-invoice discount rather than being deleted silently — see Infrastructure configuration below).

None of the four child entities contain domain behavior beyond data storage; no domain guards are needed in this spec.

---

### Application layer — `SchoolMgmt.Application`

#### `IFeeTemplateRepository`

```csharp
// SchoolMgmt.Application/FeeTemplates/IFeeTemplateRepository.cs
namespace SchoolMgmt.Application.FeeTemplates;

public interface IFeeTemplateRepository : IRepository<FeeTemplate>
{
    Task<(List<FeeTemplate> Items, int TotalCount)> GetPagedAsync(
        Guid? academicYearId, Guid? gradeId, bool? isActive,
        int page, int pageSize, CancellationToken ct = default);

    Task<FeeTemplate?> GetByIdWithChildrenAsync(Guid id, CancellationToken ct = default);
}
```

- `GetPagedAsync`: when `isActive` is null, defaults to active only; filters by `academicYearId`/`gradeId` when provided. Does NOT Include children (list view only needs the header).
- `GetByIdWithChildrenAsync`: loads `FeeTemplate` together with `LineItems`, `Installments`, and `DiscountRules` (all three collections via `.Include`). Returns `null` if not found.

#### DTOs

```csharp
// SchoolMgmt.Application/FeeTemplates/Dtos/

// --- list view ---
public record FeeTemplateSummaryDto(
    Guid Id,
    string Name,
    Guid AcademicYearId,
    string AcademicYearName,
    Guid GradeId,
    string GradeName,
    decimal TotalAmount,        // sum of all FeeLineItem.Amount
    int LineItemCount,
    bool IsActive,
    DateTimeOffset CreatedAt
);

// --- detail view (includes all children) ---
public record FeeTemplateDto(
    Guid Id,
    string Name,
    Guid AcademicYearId,
    string AcademicYearName,
    Guid GradeId,
    string GradeName,
    decimal TotalAmount,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<FeeLineItemDto> LineItems,
    IReadOnlyList<FeeInstallmentDto> Installments,
    IReadOnlyList<DiscountRuleDto> DiscountRules
);

public record FeeLineItemDto(
    Guid Id,
    string Name,
    decimal Amount,
    int DisplayOrder
);

public record FeeInstallmentDto(
    Guid Id,
    string Name,
    decimal Percentage,
    int DisplayOrder
);

public record DiscountRuleDto(
    Guid Id,
    string Name,
    DiscountRuleType RuleType,
    decimal Value,
    Guid? FeeLineItemId,
    string? FeeLineItemName    // null when FeeLineItemId is null
);

// --- request bodies ---
public record CreateFeeTemplateRequest(
    string Name,
    Guid AcademicYearId,
    Guid GradeId
);

public record UpdateFeeTemplateRequest(
    string Name,
    bool IsActive
);

// ReplaceLineItems — each item carries an optional Id:
//   Id provided + matches an existing line item on this template → update
//   Id not provided (null) → create new
//   Existing line items whose Id does not appear in the request → delete
//     (DiscountRules referencing deleted items have FeeLineItemId set to null via DB ON DELETE SET NULL)
public record ReplaceLineItemsRequest(
    IReadOnlyList<LineItemInput> Items
);

public record LineItemInput(
    Guid? Id,
    string Name,
    decimal Amount,
    int DisplayOrder
);

// ReplaceInstallments — full delete-all + insert; no Id tracking needed.
public record ReplaceInstallmentsRequest(
    IReadOnlyList<InstallmentInput> Items
);

public record InstallmentInput(
    string Name,
    decimal Percentage,
    int DisplayOrder
);

// ReplaceDiscountRules — full delete-all + insert.
// FeeLineItemId must reference a line item belonging to this template (validated in service).
public record ReplaceDiscountRulesRequest(
    IReadOnlyList<DiscountRuleInput> Items
);

public record DiscountRuleInput(
    string Name,
    DiscountRuleType RuleType,
    decimal Value,
    Guid? FeeLineItemId
);
```

#### Validators (FluentValidation)

```csharp
// CreateFeeTemplateRequestValidator
RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
RuleFor(x => x.AcademicYearId).NotEmpty();
RuleFor(x => x.GradeId).NotEmpty();

// UpdateFeeTemplateRequestValidator
RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

// ReplaceLineItemsRequestValidator
RuleForEach(x => x.Items).ChildRules(item =>
{
    item.RuleFor(i => i.Name).NotEmpty().MaximumLength(200);
    item.RuleFor(i => i.Amount).GreaterThan(0);
});

// ReplaceInstallmentsRequestValidator
// Empty list is allowed (no installment schedule configured yet).
RuleForEach(x => x.Items).ChildRules(item =>
{
    item.RuleFor(i => i.Name).NotEmpty().MaximumLength(200);
    item.RuleFor(i => i.Percentage).GreaterThan(0).LessThanOrEqualTo(100);
});
// Sum = 100% validated in the service (not in the validator — requires cross-item logic).

// ReplaceDiscountRulesRequestValidator
RuleForEach(x => x.Items).ChildRules(item =>
{
    item.RuleFor(i => i.Name).NotEmpty().MaximumLength(200);
    item.RuleFor(i => i.Value).GreaterThan(0);
    item.When(i => i.RuleType == DiscountRuleType.Percentage,
        () => item.RuleFor(i => i.Value).LessThanOrEqualTo(100));
});
// FeeLineItemId cross-entity validation done in the service.
```

#### `FeeTemplateService`

One service injecting `IFeeTemplateRepository`, `IAcademicYearRepository`, `IGradeRepository`, and `IUnitOfWork`.

**Method signatures:**

```
CreateAsync(CreateFeeTemplateRequest, CancellationToken) → FeeTemplateDto
GetTemplatesAsync(Guid? academicYearId, Guid? gradeId, bool? isActive, int page, int pageSize, CancellationToken) → PagedResult<FeeTemplateSummaryDto>
GetByIdAsync(Guid id, CancellationToken) → FeeTemplateDto
UpdateHeaderAsync(Guid id, UpdateFeeTemplateRequest, CancellationToken) → FeeTemplateDto
ReplaceLineItemsAsync(Guid id, ReplaceLineItemsRequest, CancellationToken) → FeeTemplateDto
ReplaceInstallmentsAsync(Guid id, ReplaceInstallmentsRequest, CancellationToken) → FeeTemplateDto
ReplaceDiscountRulesAsync(Guid id, ReplaceDiscountRulesRequest, CancellationToken) → FeeTemplateDto
```

**Algorithms:**

```
CreateAsync(CreateFeeTemplateRequest, CancellationToken) → FeeTemplateDto
  await academicYearRepository.GetByIdAsync(request.AcademicYearId, ct)
      ?? throw NotFoundException("Academic year not found.")
  await gradeRepository.GetByIdAsync(request.GradeId, ct)
      ?? throw NotFoundException("Grade not found.")
  template = new FeeTemplate
      { AcademicYearId, GradeId, Name = request.Name, IsActive = true }
  await repository.AddAsync(template, ct)
  await unitOfWork.SaveChangesAsync(ct)
      // unique index on (SchoolId, AcademicYearId, GradeId, Name) → ConflictException on duplicate
  return ToDto(template)   // children collections are empty on creation

GetTemplatesAsync(...) → PagedResult<FeeTemplateSummaryDto>
  (items, total) = await repository.GetPagedAsync(academicYearId, gradeId, isActive, page, pageSize, ct)
  return PagedResult<FeeTemplateSummaryDto>(items.Select(ToSummaryDto), total, page, pageSize)

GetByIdAsync(Guid id, CancellationToken) → FeeTemplateDto
  template = await repository.GetByIdWithChildrenAsync(id, ct)
      ?? throw NotFoundException("Fee template not found.")
  return ToDetailDto(template)

UpdateHeaderAsync(Guid id, UpdateFeeTemplateRequest, CancellationToken) → FeeTemplateDto
  template = await repository.GetByIdWithChildrenAsync(id, ct)
      ?? throw NotFoundException("Fee template not found.")
  template.Name = request.Name
  template.IsActive = request.IsActive
  repository.Update(template)
  await unitOfWork.SaveChangesAsync(ct)
      // ConflictException if the new name clashes with another template on same school/year/grade
  return ToDetailDto(template)

ReplaceLineItemsAsync(Guid id, ReplaceLineItemsRequest, CancellationToken) → FeeTemplateDto
  template = await repository.GetByIdWithChildrenAsync(id, ct)
      ?? throw NotFoundException("Fee template not found.")

  existingById = template.LineItems.ToDictionary(li => li.Id)
  requestedIds = request.Items.Where(i => i.Id.HasValue).Select(i => i.Id!.Value).ToHashSet()

  // Delete items no longer present in the request
  // (DB ON DELETE SET NULL handles DiscountRules.FeeLineItemId nullification automatically)
  foreach item in existingById where item.Key not in requestedIds:
      dbContext.FeeLineItems.Remove(item.Value)

  // Update existing
  foreach input in request.Items where input.Id.HasValue and existingById.ContainsKey(input.Id.Value):
      existing = existingById[input.Id.Value]
      existing.Name = input.Name
      existing.Amount = input.Amount
      existing.DisplayOrder = input.DisplayOrder

  // Create new
  foreach input in request.Items where input.Id is null or not found in existingById:
      newItem = new FeeLineItem { FeeTemplateId = id, Name, Amount, DisplayOrder }
      dbContext.FeeLineItems.Add(newItem)

  await unitOfWork.SaveChangesAsync(ct)
  template = await repository.GetByIdWithChildrenAsync(id, ct)!   // reload to pick up new IDs
  return ToDetailDto(template)

ReplaceInstallmentsAsync(Guid id, ReplaceInstallmentsRequest, CancellationToken) → FeeTemplateDto
  template = await repository.GetByIdWithChildrenAsync(id, ct)
      ?? throw NotFoundException("Fee template not found.")

  if request.Items.Count > 0:
      sum = request.Items.Sum(i => i.Percentage)
      if Math.Abs(sum - 100m) > 0.01m:
          throw ValidationException("Installment percentages must sum to 100%.")

  // Delete all existing installments, insert the new set
  dbContext.FeeInstallments.RemoveRange(template.Installments)
  foreach input in request.Items:
      dbContext.FeeInstallments.Add(
          new FeeInstallment { FeeTemplateId = id, Name, Percentage, DisplayOrder })

  await unitOfWork.SaveChangesAsync(ct)
  template = await repository.GetByIdWithChildrenAsync(id, ct)!
  return ToDetailDto(template)

ReplaceDiscountRulesAsync(Guid id, ReplaceDiscountRulesRequest, CancellationToken) → FeeTemplateDto
  template = await repository.GetByIdWithChildrenAsync(id, ct)
      ?? throw NotFoundException("Fee template not found.")

  // Validate FeeLineItemId references — each non-null Id must belong to this template
  validLineItemIds = template.LineItems.Select(li => li.Id).ToHashSet()
  foreach input in request.Items where input.FeeLineItemId.HasValue:
      if not validLineItemIds.Contains(input.FeeLineItemId.Value):
          throw ValidationException(
              $"FeeLineItemId {input.FeeLineItemId} does not belong to this template.")

  // Delete all existing, insert new
  dbContext.DiscountRules.RemoveRange(template.DiscountRules)
  foreach input in request.Items:
      dbContext.DiscountRules.Add(
          new DiscountRule { FeeTemplateId = id, Name, RuleType, Value, FeeLineItemId })

  await unitOfWork.SaveChangesAsync(ct)
  template = await repository.GetByIdWithChildrenAsync(id, ct)!
  return ToDetailDto(template)
```

> **Note on `ReplaceLineItemsAsync` direct DbContext access:** The service accesses `dbContext.FeeLineItems` directly for the remove/add operations on child entities. This is the pragmatic approach for bulk child-collection management — wrapping FeeLineItem/FeeInstallment/DiscountRule in their own repositories would add significant ceremony for no meaningful abstraction gain. The `IUnitOfWork.SaveChangesAsync` call still owns persistence; the pattern deviation is limited and intentional.

#### `DependencyInjection.cs` (Application)

Append to `AddApplication`:

```csharp
services.AddScoped<FeeTemplateService>();
```

---

### Infrastructure — `SchoolMgmt.Infrastructure`

#### `FeeTemplateConfiguration`

```csharp
// Table: FeeTemplates
builder.ToTable("FeeTemplates");
builder.HasKey(x => x.Id);
builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
builder.HasIndex(x => new { x.SchoolId, x.AcademicYearId, x.GradeId, x.Name }).IsUnique();
builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

builder.HasMany(x => x.LineItems)
    .WithOne(x => x.FeeTemplate)
    .HasForeignKey(x => x.FeeTemplateId)
    .OnDelete(DeleteBehavior.Cascade);

builder.HasMany(x => x.Installments)
    .WithOne(x => x.FeeTemplate)
    .HasForeignKey(x => x.FeeTemplateId)
    .OnDelete(DeleteBehavior.Cascade);

builder.HasMany(x => x.DiscountRules)
    .WithOne(x => x.FeeTemplate)
    .HasForeignKey(x => x.FeeTemplateId)
    .OnDelete(DeleteBehavior.Cascade);
```

#### `FeeLineItemConfiguration`

```csharp
// Table: FeeLineItems
builder.ToTable("FeeLineItems");
builder.HasKey(x => x.Id);
builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
builder.Property(x => x.Amount).IsRequired().HasColumnType("numeric(18,2)");
builder.Property(x => x.DisplayOrder).IsRequired();
```

#### `FeeInstallmentConfiguration`

```csharp
// Table: FeeInstallments
builder.ToTable("FeeInstallments");
builder.HasKey(x => x.Id);
builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
builder.Property(x => x.Percentage).IsRequired().HasColumnType("numeric(5,2)");
builder.Property(x => x.DisplayOrder).IsRequired();
```

#### `DiscountRuleConfiguration`

```csharp
// Table: DiscountRules
builder.ToTable("DiscountRules");
builder.HasKey(x => x.Id);
builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
builder.Property(x => x.RuleType).IsRequired().HasConversion<string>();
builder.Property(x => x.Value).IsRequired().HasColumnType("numeric(18,2)");

builder.HasOne(x => x.FeeLineItem)
    .WithMany()
    .HasForeignKey(x => x.FeeLineItemId)
    .IsRequired(false)
    .OnDelete(DeleteBehavior.SetNull);   // deleting a line item nullifies the rule, not removes it
```

#### `FeeTemplateRepository`

```csharp
internal sealed class FeeTemplateRepository(AppDbContext context)
    : Repository<FeeTemplate>(context), IFeeTemplateRepository
{
    public async Task<(List<FeeTemplate> Items, int TotalCount)> GetPagedAsync(
        Guid? academicYearId, Guid? gradeId, bool? isActive,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet
            .Include(t => t.AcademicYear)
            .Include(t => t.Grade)
            .Include(t => t.LineItems)
            .AsQueryable();

        query = isActive.HasValue
            ? query.Where(t => t.IsActive == isActive.Value)
            : query.Where(t => t.IsActive);   // default: active only

        if (academicYearId.HasValue)
            query = query.Where(t => t.AcademicYearId == academicYearId.Value);
        if (gradeId.HasValue)
            query = query.Where(t => t.GradeId == gradeId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(t => t.Grade.DisplayOrder)
            .ThenBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<FeeTemplate?> GetByIdWithChildrenAsync(Guid id, CancellationToken ct = default)
    {
        return await DbSet
            .Include(t => t.AcademicYear)
            .Include(t => t.Grade)
            .Include(t => t.LineItems.OrderBy(li => li.DisplayOrder))
            .Include(t => t.Installments.OrderBy(i => i.DisplayOrder))
            .Include(t => t.DiscountRules)
                .ThenInclude(dr => dr.FeeLineItem)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }
}
```

The EF Core global query filter on `SchoolId` (applied automatically via `ITenantScoped`) scopes all four entities to the current tenant.

#### Modified: `AppDbContext`

Add four `DbSet` properties:

```csharp
public DbSet<FeeTemplate>   FeeTemplates   => Set<FeeTemplate>();
public DbSet<FeeLineItem>   FeeLineItems   => Set<FeeLineItem>();
public DbSet<FeeInstallment> FeeInstallments => Set<FeeInstallment>();
public DbSet<DiscountRule>  DiscountRules  => Set<DiscountRule>();
```

#### `DependencyInjection.cs` (Infrastructure)

Add to `AddInfrastructure`:

```csharp
services.AddScoped<IFeeTemplateRepository, FeeTemplateRepository>();
```

---

### WebApi — `SchoolMgmt.WebApi`

#### `FeeTemplatesController`

All endpoints `[Authorize(Roles = "Admin")]`. All state-mutating operations use POST/PUT — no GET side effects.

| Method | Route | Service call | Success | Notes |
|---|---|---|---|---|
| `POST` | `/api/fee-templates` | `CreateAsync` | 201 + `Location: /api/fee-templates/{id}` | 404 if AcademicYear/Grade not found; 409 if name already exists for same school/year/grade |
| `GET` | `/api/fee-templates` | `GetTemplatesAsync` | 200 — `PagedResult<FeeTemplateSummaryDto>` | `academicYearId` (optional); `gradeId` (optional); `isActive` (default: active only); `page` (default 1); `pageSize` (default 20, max 100) |
| `GET` | `/api/fee-templates/{id}` | `GetByIdAsync` | 200 — `FeeTemplateDto` | 404 if not found |
| `PUT` | `/api/fee-templates/{id}` | `UpdateHeaderAsync` | 200 — `FeeTemplateDto` | 404 if not found; 409 if name clashes |
| `PUT` | `/api/fee-templates/{id}/line-items` | `ReplaceLineItemsAsync` | 200 — `FeeTemplateDto` | 404 if template not found; 400 on validation failure |
| `PUT` | `/api/fee-templates/{id}/installments` | `ReplaceInstallmentsAsync` | 200 — `FeeTemplateDto` | 404 if template not found; 400 if percentages don't sum to 100% |
| `PUT` | `/api/fee-templates/{id}/discount-rules` | `ReplaceDiscountRulesAsync` | 200 — `FeeTemplateDto` | 404 if template not found; 400 if FeeLineItemId doesn't belong to template |

`pageSize` cap:

```csharp
pageSize = Math.Min(pageSize, 100);
```

No DELETE endpoint — set `IsActive = false` to retire a template.

---

## Project Structure

New and modified files introduced by this spec:

```
backend/
  SchoolMgmt.Domain/
    Enums/
      DiscountRuleType.cs                              # new
    Entities/
      FeeTemplate.cs                                   # new
      FeeLineItem.cs                                   # new
      FeeInstallment.cs                                # new
      DiscountRule.cs                                  # new

  SchoolMgmt.Application/
    FeeTemplates/
      IFeeTemplateRepository.cs                        # new
      FeeTemplateService.cs                            # new
      Dtos/
        FeeTemplateSummaryDto.cs                       # new
        FeeTemplateDto.cs                              # new
        FeeLineItemDto.cs                              # new
        FeeInstallmentDto.cs                           # new
        DiscountRuleDto.cs                             # new
        CreateFeeTemplateRequest.cs                    # new
        UpdateFeeTemplateRequest.cs                    # new
        ReplaceLineItemsRequest.cs                     # new
        ReplaceInstallmentsRequest.cs                  # new
        ReplaceDiscountRulesRequest.cs                 # new
      Validators/
        CreateFeeTemplateRequestValidator.cs           # new
        UpdateFeeTemplateRequestValidator.cs           # new
        ReplaceLineItemsRequestValidator.cs            # new
        ReplaceInstallmentsRequestValidator.cs         # new
        ReplaceDiscountRulesRequestValidator.cs        # new
    DependencyInjection.cs                             # add FeeTemplateService

  SchoolMgmt.Infrastructure/
    Persistence/
      AppDbContext.cs                                  # add 4 DbSets
      Configurations/
        FeeTemplateConfiguration.cs                    # new
        FeeLineItemConfiguration.cs                    # new
        FeeInstallmentConfiguration.cs                 # new
        DiscountRuleConfiguration.cs                   # new
      Repositories/
        FeeTemplateRepository.cs                       # new
      Migrations/
        <AddFeeStructureTemplates>                     # new — EF Core generated
    DependencyInjection.cs                             # add IFeeTemplateRepository registration

  SchoolMgmt.WebApi/
    Controllers/
      FeeTemplatesController.cs                        # new

tests/
  SchoolMgmt.IntegrationTests/
    FeeTemplatesControllerTests.cs                     # new — real Postgres via Testcontainers
```

---

## Commands

```
Build:          dotnet build SchoolMgmt.slnx
Test:           dotnet test SchoolMgmt.slnx
Add migration:  dotnet ef migrations add AddFeeStructureTemplates --project backend/SchoolMgmt.Infrastructure --startup-project backend/SchoolMgmt.Infrastructure
Apply locally:  dotnet ef database update --project backend/SchoolMgmt.Infrastructure --startup-project backend/SchoolMgmt.Infrastructure
```

---

## Database Schema

### New: `FeeTemplates`

| Column | PG Type | Max Length | Default | Nullable | Key | Constraints | Description |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | — | — | NOT NULL | PK | — | Surrogate primary key |
| `SchoolId` | `uuid` | — | — | NOT NULL | — | — | Tenant scope (auto-stamped) |
| `AcademicYearId` | `uuid` | — | — | NOT NULL | FK → `AcademicYears.Id` | ON DELETE RESTRICT | — |
| `GradeId` | `uuid` | — | — | NOT NULL | FK → `Grades.Id` | ON DELETE RESTRICT | — |
| `Name` | `varchar` | 200 | — | NOT NULL | — | UNIQUE with `SchoolId`, `AcademicYearId`, `GradeId` | Template name; unique per school/year/grade |
| `IsActive` | `boolean` | — | `true` | NOT NULL | — | — | Soft-disable retired templates |
| `CreatedAt` | `timestamptz` | — | — | NOT NULL | — | — | Auto-stamped |
| `UpdatedAt` | `timestamptz` | — | — | NULL | — | — | Auto-stamped on modification |

### New: `FeeLineItems`

| Column | PG Type | Max Length | Default | Nullable | Key | Constraints | Description |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | — | — | NOT NULL | PK | — | — |
| `SchoolId` | `uuid` | — | — | NOT NULL | — | — | Tenant scope (auto-stamped) |
| `FeeTemplateId` | `uuid` | — | — | NOT NULL | FK → `FeeTemplates.Id` | ON DELETE CASCADE | — |
| `Name` | `varchar` | 200 | — | NOT NULL | — | — | e.g. "Tuition Fee", "Lab Fee" |
| `Amount` | `numeric(18,2)` | — | — | NOT NULL | — | — | Positive, enforced in validator |
| `DisplayOrder` | `int` | — | — | NOT NULL | — | — | Sort order within template |
| `CreatedAt` | `timestamptz` | — | — | NOT NULL | — | — | — |
| `UpdatedAt` | `timestamptz` | — | — | NULL | — | — | — |

### New: `FeeInstallments`

| Column | PG Type | Max Length | Default | Nullable | Key | Constraints | Description |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | — | — | NOT NULL | PK | — | — |
| `SchoolId` | `uuid` | — | — | NOT NULL | — | — | Tenant scope (auto-stamped) |
| `FeeTemplateId` | `uuid` | — | — | NOT NULL | FK → `FeeTemplates.Id` | ON DELETE CASCADE | — |
| `Name` | `varchar` | 200 | — | NOT NULL | — | — | e.g. "1st Installment", "Enrollment" |
| `Percentage` | `numeric(5,2)` | — | — | NOT NULL | — | — | Per-entry %; service validates sum = 100 across the template |
| `DisplayOrder` | `int` | — | — | NOT NULL | — | — | — |
| `CreatedAt` | `timestamptz` | — | — | NOT NULL | — | — | — |
| `UpdatedAt` | `timestamptz` | — | — | NULL | — | — | — |

### New: `DiscountRules`

| Column | PG Type | Max Length | Default | Nullable | Key | Constraints | Description |
|---|---|---|---|---|---|---|---|
| `Id` | `uuid` | — | — | NOT NULL | PK | — | — |
| `SchoolId` | `uuid` | — | — | NOT NULL | — | — | Tenant scope (auto-stamped) |
| `FeeTemplateId` | `uuid` | — | — | NOT NULL | FK → `FeeTemplates.Id` | ON DELETE CASCADE | — |
| `Name` | `varchar` | 200 | — | NOT NULL | — | — | e.g. "Sibling Discount", "Merit Scholarship" |
| `RuleType` | `varchar` | — | — | NOT NULL | — | — | `Percentage` or `FlatAmount` (stored as string) |
| `Value` | `numeric(18,2)` | — | — | NOT NULL | — | — | % value (0–100) or flat amount; must be > 0 |
| `FeeLineItemId` | `uuid` | — | — | NULL | FK → `FeeLineItems.Id` | ON DELETE SET NULL | null = applies to invoice total |
| `CreatedAt` | `timestamptz` | — | — | NOT NULL | — | — | — |
| `UpdatedAt` | `timestamptz` | — | — | NULL | — | — | — |

---

## Testing Strategy

No domain unit tests — none of the four entities have domain behavior (no guards, no invariants, no factory methods).

### Integration tests (`SchoolMgmt.IntegrationTests/FeeTemplatesControllerTests.cs`)

Against real Postgres (Testcontainers), authenticated as the demo Admin. Seed data: one `AcademicYear` and two `Grade` rows for FK targets.

Use cases from [docs/ideas/07-fee-structure-templates-examples.md](../docs/ideas/07-fee-structure-templates-examples.md) are the basis for the test cases below.

**Create template:**
- `POST /api/fee-templates` with valid (Name, AcademicYearId, GradeId) → 201; response has empty children collections
- `POST /api/fee-templates` with missing name → 400
- `POST /api/fee-templates` with non-existent AcademicYearId → 404
- `POST /api/fee-templates` with non-existent GradeId → 404
- `POST /api/fee-templates` with duplicate (AcademicYearId, GradeId, Name) for same school → 409
- `POST /api/fee-templates` with same name but different GradeId → 201 (name uniqueness is per grade/year)

**Multiple templates per grade/year:**
- Create two templates for the same Grade + AcademicYear with different names → both succeed (201)

**Read:**
- `GET /api/fee-templates` (no params) → returns only active templates; `totalAmount` is 0 on empty template
- `GET /api/fee-templates?isActive=false` → returns only inactive templates
- `GET /api/fee-templates?gradeId=<id>` → returns only templates for that grade
- `GET /api/fee-templates?academicYearId=<id>` → filters correctly
- `GET /api/fee-templates?page=1&pageSize=5` → returns up to 5; pagination fields present
- `GET /api/fee-templates/{id}` → 200 with all child collections (initially empty)
- `GET /api/fee-templates/{unknownId}` → 404

**Replace line items:**
- `PUT /api/fee-templates/{id}/line-items` with 3 items → 200; `FeeTemplateDto.LineItems` has 3 entries ordered by `DisplayOrder`; `TotalAmount` = sum
- `PUT /api/fee-templates/{id}/line-items` — re-send same items preserving their Ids → same 3 Ids in response (update, not recreate)
- `PUT /api/fee-templates/{id}/line-items` — omit one existing item (no Id in request) → that item is removed; 200 with 2 items
- `PUT /api/fee-templates/{id}/line-items` with a negative amount → 400
- `PUT /api/fee-templates/{id}/line-items` with an empty name → 400
- `PUT /api/fee-templates/{id}/line-items` on unknown template → 404
- `PUT /api/fee-templates/{id}/line-items` with empty list → 200; `LineItems` is empty (clearing is allowed)

**Replace installments:**
- `PUT /api/fee-templates/{id}/installments` with 3 entries summing to 100% → 200; installments present in response
- `PUT /api/fee-templates/{id}/installments` with entries summing to 80% → 400 ("must sum to 100%")
- `PUT /api/fee-templates/{id}/installments` with entries summing to 110% → 400
- `PUT /api/fee-templates/{id}/installments` with a single 100% entry → 200 (one installment allowed)
- `PUT /api/fee-templates/{id}/installments` with empty list → 200; `Installments` is empty (no schedule yet)
- `PUT /api/fee-templates/{id}/installments` on unknown template → 404

**Replace discount rules:**
- `PUT /api/fee-templates/{id}/discount-rules` with a Percentage rule (FeeLineItemId = null) → 200; `FeeLineItemName` is null in response
- `PUT /api/fee-templates/{id}/discount-rules` with a rule targeting a valid FeeLineItemId from this template → 200; `FeeLineItemName` populated
- `PUT /api/fee-templates/{id}/discount-rules` with a FeeLineItemId from a different template → 400
- `PUT /api/fee-templates/{id}/discount-rules` with Percentage rule, Value > 100 → 400
- `PUT /api/fee-templates/{id}/discount-rules` with FlatAmount rule, Value = 0 → 400
- `PUT /api/fee-templates/{id}/discount-rules` with empty list → 200; `DiscountRules` is empty
- `PUT /api/fee-templates/{id}/discount-rules` on unknown template → 404

**ON DELETE SET NULL behavior:**
- Set up a template with a line item and a discount rule targeting that line item
- `PUT /api/fee-templates/{id}/line-items` removing that line item
- `GET /api/fee-templates/{id}` → the discount rule still exists but `FeeLineItemId` is now null, `FeeLineItemName` is null

**Update header:**
- `PUT /api/fee-templates/{id}` with new name and `isActive = false` → 200; name updated; template no longer appears in default list
- `PUT /api/fee-templates/{id}` with a name that clashes → 409
- `PUT /api/fee-templates/{unknownId}` → 404

**Auth:**
- Unauthenticated request to any endpoint → 401
- Teacher-role request → 403

---

## Boundaries

- **Always:** call `dotnet test SchoolMgmt.slnx` before considering any task done; follow all rules in `.claude/rules/backend.md`; keep GET endpoints side-effect-free; validate installment sum = 100% in the service, not just the validator.
- **Ask first:** adding a hard-delete endpoint; adding academic-year-archived guard on template creation (the idea doc left this open); changing the `ON DELETE SET NULL` behavior on `DiscountRules.FeeLineItemId` to cascade-delete instead.
- **Never:** hard-delete a `FeeTemplate`; call `SaveChangesAsync` or transaction methods from inside `FeeTemplateRepository`; access child `DbSet`s from the controller directly.

---

## Success Criteria

- `POST /api/fee-templates` returns 201 with empty child collections — integration test passes.
- `POST /api/fee-templates` with duplicate (AcademicYearId, GradeId, Name) for same school returns 409 — integration test confirms.
- `PUT /api/fee-templates/{id}/installments` with percentages summing to 80% returns 400 — integration test confirms.
- `PUT /api/fee-templates/{id}/installments` with percentages summing to 100% returns 200 with all entries present — integration test confirms.
- `PUT /api/fee-templates/{id}/line-items` — items sent with their existing IDs are updated in place (same IDs in response); items sent without IDs are created (new IDs in response); items omitted are deleted — integration test confirms each case.
- `PUT /api/fee-templates/{id}/discount-rules` with a FeeLineItemId from another template returns 400 — integration test confirms.
- Removing a line item that a discount rule targets: the discount rule remains but `FeeLineItemId` becomes null — integration test confirms (ON DELETE SET NULL).
- All four entity tables carry a `SchoolId` column; all four are visible only to the authenticated tenant — confirmed by the existing global query filter mechanism.
- All endpoints return 401 for unauthenticated requests and 403 for Teacher-role requests — integration tests confirm.
- `dotnet test SchoolMgmt.slnx` passes with all new and existing tests green.

---

## Open Questions

- **Archived academic year guard:** should `CreateAsync` (or `UpdateHeaderAsync`) throw if the referenced `AcademicYear` is archived? The idea doc leaves this open. Consistent with how other features treat archived years (spec 03 uses `EnsureNotArchived`), the guard is the right call — but it's deferred to implementation review.
- **Decimal precision for `Amount`:** `numeric(18,2)` is assumed (pesos/cents). Confirm this is appropriate for the school's currency before the migration is applied.
- **Discount rule name uniqueness:** the idea doc explicitly leaves this open. Current spec: no unique constraint on rule names (duplicates allowed per template). Override in the spec if a unique constraint is desired before implementation starts.
