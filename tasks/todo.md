# Todo: Academic Year / Term Configuration

Plan: [tasks/plan.md](plan.md) | Spec: [specs/03-implement-academic-year-term-configuration.md](../specs/03-implement-academic-year-term-configuration.md)

## Task 1 — Domain layer + domain tests

- [ ] Create `SchoolMgmt.Domain/Common/DomainException.cs`
- [ ] Create `SchoolMgmt.Domain/Enums/AcademicYearStatus.cs`
- [ ] Create `SchoolMgmt.Domain/Entities/AcademicYear.cs` (private constructor, `Create()` factory, `EnsureNotArchived()`, `Archive()`, `SetCurrent()`)
- [ ] Create `SchoolMgmt.Domain/Entities/Semester.cs` (private constructor, `SetCurrent()`)
- [ ] Create `tests/SchoolMgmt.Domain.Tests/SchoolMgmt.Domain.Tests.csproj` and add to `SchoolMgmt.slnx`
- [ ] Write `tests/SchoolMgmt.Domain.Tests/AcademicYearTests.cs` (5 tests)
- [ ] **Checkpoint:** `dotnet test tests/SchoolMgmt.Domain.Tests` — 5/5 pass

## Task 2 — Application layer

- [ ] Install FluentValidation packages (`FluentValidation`, `FluentValidation.DependencyInjectionExtensions`) in Application project
- [ ] Create `SchoolMgmt.Application/AcademicYears/IAcademicYearRepository.cs`
- [ ] Create `SchoolMgmt.Application/AcademicYears/Dtos/AcademicYearDto.cs` (DTOs + request records)
- [ ] Create `SchoolMgmt.Application/AcademicYears/Validators/CreateAcademicYearRequestValidator.cs`
- [ ] Create `SchoolMgmt.Application/AcademicYears/Validators/UpdateSemesterRequestValidator.cs`
- [ ] Create `SchoolMgmt.Application/AcademicYears/AcademicYearService.cs` (7 methods)
- [ ] Create `SchoolMgmt.Application/Filters/ValidationFilter.cs` (global `IAsyncActionFilter`)
- [ ] Update `SchoolMgmt.Application/DependencyInjection.cs` — add `AcademicYearService` + `AddValidatorsFromAssembly`
- [ ] **Checkpoint:** `dotnet build SchoolMgmt.Application` passes

## Task 3 — Infrastructure layer

- [ ] Create `Persistence/Configurations/AcademicYearConfiguration.cs`
- [ ] Create `Persistence/Configurations/SemesterConfiguration.cs`
- [ ] Create `Persistence/Repositories/AcademicYearRepository.cs` (5 extra query methods)
- [ ] Add `DbSet<AcademicYear>` and `DbSet<Semester>` to `AppDbContext.cs`
- [ ] Register `IAcademicYearRepository` in `Infrastructure/DependencyInjection.cs`
- [ ] Generate migration: `dotnet ef migrations add AddAcademicYears ...`
- [ ] **Checkpoint:** Migration file created with `AcademicYears` + `Semesters` tables. `dotnet build SchoolMgmt.Infrastructure` passes

## Task 4 — WebApi layer

- [ ] Create `WebApi/Filters/DomainExceptionFilter.cs`
- [ ] Create `WebApi/Controllers/AcademicYearsController.cs` (7 endpoints, all `[Authorize(Roles = "Admin")]`)
- [ ] Update `Program.cs` — add `DomainExceptionFilter` + `ValidationFilter` to `AddControllers(options => ...)`
- [ ] **Checkpoint:** `dotnet build SchoolMgmt.WebApi` passes. Swagger shows 7 new endpoints.

## Task 5 — Integration tests

- [ ] Create `tests/SchoolMgmt.IntegrationTests/AcademicYears/AcademicYearsControllerTests.cs` (14 tests)
- [ ] **Checkpoint:** `dotnet test SchoolMgmt.slnx` — all tests pass (new + existing)

## Task 6 — Catalog update

- [ ] Update `.claude/catalog/backend.md` with all new types from this feature
