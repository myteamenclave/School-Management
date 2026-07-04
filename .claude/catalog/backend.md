<!-- Last verified: 2026-07-01. Update this file whenever a new public type/function is added or removed from the backend. Check here before adding new code — don't duplicate something that already exists. -->

# Backend Catalog

## Domain (`SchoolMgmt.Domain`)

| Type | Location | Purpose |
|---|---|---|
| `BaseEntity` | `Common/BaseEntity.cs` | Base class for all entities — `Id`, `CreatedAt`, `UpdatedAt` (audit fields stamped automatically by `AppDbContext.SaveChangesAsync`, not set manually) |
| `ITenantScoped` | `Common/ITenantScoped.cs` | Marker interface (`SchoolId`) — implement on any entity that belongs to a school. Separate from `BaseEntity` since not every entity is tenant-scoped |
| `DomainException` | `Common/DomainException.cs` | Shared base exception for all domain rule violations — mapped to HTTP 400 by `DomainExceptionFilter` |
| `NotFoundException` | `Common/NotFoundException.cs` | Thrown when a required entity is not found — mapped to HTTP 404 by `DomainExceptionFilter` |
| `ConflictException` | `Common/ConflictException.cs` | Thrown on uniqueness violations (e.g. duplicate academic year name) — mapped to HTTP 409 by `DomainExceptionFilter` |
| `School` | `Entities/School.cs` | The tenant entity itself — `BaseEntity` but NOT `ITenantScoped` (a school doesn't belong to itself) |
| `User` | `Entities/User.cs` | `BaseEntity` + `ITenantScoped`. Email (unique per school), password hash, display name, `Role` |
| `UserRole` (enum) | `Entities/UserRole.cs` | `Admin`, `Teacher`, `Parent` — Principal/Owner merged into Admin |
| `RefreshToken` | `Entities/RefreshToken.cs` | `BaseEntity` + `ITenantScoped`. Hashed token, `SessionId` (groups tokens from one login for family revocation), `ExpiresAt`/`RevokedAt`/`ReplacedByTokenId`, `User` navigation (eager-loaded by `GetByTokenHashAsync`) |
| `AcademicYear` | `Entities/AcademicYear.cs` | `BaseEntity` + `ITenantScoped`. Two-level calendar root. `Create(name, startDate, endDate)` factory always produces 2 auto-scaffolded semesters. `Archive()` enforces the "cannot archive the current year" rule. `EnsureNotArchived()` is the domain guard downstream services call before any write. `SetCurrent(bool)` is the only way to set `IsCurrent`. |
| `Semester` | `Entities/Semester.cs` | `BaseEntity` + `ITenantScoped`. Owned by `AcademicYear`. `SetCurrent(bool)` is the only way to set `IsCurrent`. Always accessed through `AcademicYear` — no standalone repository. |
| `AcademicYearStatus` (enum) | `Enums/AcademicYearStatus.cs` | `Active`, `Archived` — stored as string in the DB (not int) to prevent silent corruption on reorder |
| `Student` | `Entities/Student.cs` | `BaseEntity` + `ITenantScoped`. Central operational record. `StudentCode` (`YYYY-NNNNNN`) is immutable after creation. `EnrollmentStatus` drives lifecycle — no hard delete. Guardian fields inline on the row. |
| `Gender` (enum) | `Enums/Gender.cs` | `Male`, `Female`, `Other` — stored as string in DB |
| `EnrollmentStatus` (enum) | `Enums/EnrollmentStatus.cs` | `Active`, `Transferred`, `Graduated`, `Dropped` — stored as string in DB |
| `Grade` | `Entities/Grade.cs` | `BaseEntity` + `ITenantScoped`. Top-level structural catalog entry (e.g. "Grade 5"). `EnsureNoSections()` enforces the delete guard — throws `DomainException` if any sections exist. Backing field `_sections` exposed as `IReadOnlyList<Section> Sections`. Direct instantiation (no factory — no invariant requiring controlled construction). |
| `Section` | `Entities/Section.cs` | `BaseEntity` + `ITenantScoped`. Belongs to a `Grade`. Name unique per grade (not globally). Always accessed through `IGradeRepository` — no standalone repository. |

## Application (`SchoolMgmt.Application`)

| Type | Location | Purpose |
|---|---|---|
| `ITenantProvider` | `Interfaces/ITenantProvider.cs` | `CurrentSchoolId` — resolves the current tenant. Implemented by `HttpContextTenantProvider` (real, claims-based) at runtime; `StaticTenantProvider` only at EF Core design-time (migrations have no `HttpContext`) |
| `IDateTimeProvider` | `Interfaces/IDateTimeProvider.cs` | `UtcNow` — testable abstraction over `DateTimeOffset.UtcNow` |
| `IRepository<TEntity>` | `Interfaces/IRepository.cs` | Generic repository base (`GetByIdAsync`, `AddAsync`, `Update`, `Remove`). Per-entity repositories extend this — `IUserRepository`, `IRefreshTokenRepository` exist; more land with future feature specs |
| `IUnitOfWork` | `Interfaces/IUnitOfWork.cs` | Owns persistence/transactions (`SaveChangesAsync`, `BeginTransactionAsync`, `CommitAsync`, `RollbackAsync`, `Detach<T>`). Repositories never call these directly. `Detach<T>` detaches a tracked entity from the EF change tracker — used by `StudentService` retry loop to drop a failed `Added` entity before retrying with a new `StudentCode` |
| `IUserRepository` | `Interfaces/IUserRepository.cs` | Extends `IRepository<User>`. `GetByEmailAsync` — bypasses the tenant filter (login is pre-authentication) |
| `IRefreshTokenRepository` | `Interfaces/IRefreshTokenRepository.cs` | Extends `IRepository<RefreshToken>`. `GetByTokenHashAsync` (eager-loads `User`, bypasses tenant filter), `GetActiveBySessionIdAsync` (for theft-detection family revocation) |
| `IPasswordHasher` | `Interfaces/IPasswordHasher.cs` | `HashPassword`/`VerifyPassword`. Implemented by `PasswordHasherAdapter` wrapping ASP.NET Core Identity's standalone `PasswordHasher<TUser>` (not full Identity) |
| `IJwtTokenGenerator` | `Interfaces/IJwtTokenGenerator.cs` | `GenerateAccessToken(User)` (JWT), `GenerateRefreshToken()` (raw random string, not a JWT) |
| `JwtOptions` | `Auth/JwtOptions.cs` | Config POCO (`Jwt` section) — `Secret`/`Issuer`/`Audience`/`AccessTokenMinutes`/`RefreshTokenDays`. Shared by `AuthService` (Application) and `JwtTokenGenerator`/JWT-bearer validation (Infrastructure/WebApi) |
| `AuthService` | `Auth/AuthService.cs` | `LoginAsync`, `RefreshAsync` (rotation + theft-family-revocation), `LogoutAsync`. One service per the established Application-service pattern |
| `LoginRequest` / `AuthResult` / `AuthenticatedUser` | `Auth/*.cs` | Plain DTOs — `AuthResult` has zero `HttpContext`/cookie knowledge; the controller sets cookies |
| `IAcademicYearRepository` | `AcademicYears/IAcademicYearRepository.cs` | Extends `IRepository<AcademicYear>`. `GetAllWithSemestersAsync`, `GetWithSemestersAsync`, `GetCurrentAsync`, `GetCurrentSemesterAsync`, `GetSemesterByIdAsync`, `NameExistsAsync` |
| `AcademicYearService` | `AcademicYears/AcademicYearService.cs` | `CreateAcademicYearAsync` (409 on duplicate name), `GetAllAcademicYearsAsync`, `GetAcademicYearByIdAsync`, `UpdateSemesterAsync`, `SetCurrentYearAsync` (atomic — unsets prev year/semester, auto-sets Semester 1 of new year), `SetCurrentSemesterAsync`, `ArchiveAcademicYearAsync` |
| `AcademicYearDto` / `SemesterDto` | `AcademicYears/Dtos/*.cs` | Read DTOs for academic years and semesters |
| `CreateAcademicYearRequest` / `UpdateSemesterRequest` | `AcademicYears/Dtos/*.cs` | Write DTOs validated by FluentValidation |
| `CreateAcademicYearRequestValidator` / `UpdateSemesterRequestValidator` | `AcademicYears/Validators/*.cs` | FluentValidation validators — auto-discovered via `AddValidatorsFromAssembly` |
| `IGradeRepository` | `Grades/IGradeRepository.cs` | Extends `IRepository<Grade>`. `GetAllWithSectionsAsync`, `GetWithSectionsAsync`, `GradeNameExistsAsync`, `GetSectionAsync`, `SectionNameExistsInGradeAsync`, `AddSectionAsync`, `RemoveSection`. Section ops go through the grade repository — no standalone `ISectionRepository`. |
| `GradeService` | `Grades/GradeService.cs` | `CreateGradeAsync` (409 on duplicate name), `GetAllGradesAsync` (ordered by `DisplayOrder`), `GetGradeByIdAsync`, `UpdateGradeAsync`, `DeleteGradeAsync` (calls `EnsureNoSections`), `AddSectionAsync`, `UpdateSectionAsync`, `DeleteSectionAsync`. Private static `ToDto(Grade)` / `ToDto(Section)` mappers — no AutoMapper. |
| `GradeDto` / `SectionDto` | `Grades/Dtos/GradeDto.cs` | Read DTOs for grades and sections |
| `CreateGradeRequest` / `UpdateGradeRequest` / `CreateSectionRequest` / `UpdateSectionRequest` | `Grades/Dtos/CreateGradeRequest.cs` | Write DTOs validated by FluentValidation |
| `CreateGradeRequestValidator` / `UpdateGradeRequestValidator` / `CreateSectionRequestValidator` / `UpdateSectionRequestValidator` | `Grades/Validators/GradeRequestValidators.cs` | FluentValidation validators — auto-discovered via `AddValidatorsFromAssembly` |
| `StudentOptions` | `Students/StudentOptions.cs` | Config POCO (`Students` section) — `StudentCodeMaxRetries` (default 3). Registered via `services.Configure<StudentOptions>` in Infrastructure DI; injected into `StudentService` via `IOptions<StudentOptions>`. |
| `IStudentRepository` | `Students/IStudentRepository.cs` | Extends `IRepository<Student>`. `GetPagedAsync(status?, search?, page, pageSize)` (filters by status, defaults to Active; optional ILIKE search on FirstName+LastName and StudentCode; ordered by LastName/FirstName), `GetNextStudentCodeAsync(enrollmentYear)` (queries MAX(StudentCode) for the year prefix, returns next `YYYY-NNNNNN`) |
| `StudentService` | `Students/StudentService.cs` | `CreateStudentAsync` (configurable retry loop for StudentCode collision via `ConflictException`/`Detach` — retry count from `IOptions<StudentOptions>`), `GetStudentsAsync` (paged, status + search? filter), `GetStudentByIdAsync`, `UpdateStudentAsync` (`StudentCode` immutable). Private `ToDto`/`ToSummaryDto` — no AutoMapper. |
| `StudentDto` | `Students/Dtos/StudentDto.cs` | Full student read DTO including guardian fields, `CreatedAt`, `UpdatedAt` |
| `StudentSummaryDto` | `Students/Dtos/StudentSummaryDto.cs` | List-view student DTO — no guardian fields |
| `PagedResult<T>` | `Students/Dtos/PagedResult.cs` | Generic paged response wrapper (`Items`, `TotalCount`, `Page`, `PageSize`) — reusable by future slices |
| `CreateStudentRequest` | `Students/Dtos/CreateStudentRequest.cs` | Write DTO for student creation — `Gender` as string (`"Male"\|"Female"\|"Other"`) |
| `UpdateStudentRequest` | `Students/Dtos/UpdateStudentRequest.cs` | Write DTO for student update — includes `EnrollmentStatus` string; `StudentCode` is not updatable |
| `CreateStudentRequestValidator` | `Students/Validators/CreateStudentRequestValidator.cs` | FluentValidation — `IsEnumName` for Gender, DOB must be in the past |
| `UpdateStudentRequestValidator` | `Students/Validators/UpdateStudentRequestValidator.cs` | FluentValidation — same as Create plus `IsEnumName` for EnrollmentStatus |
| `DependencyInjection.AddApplication()` | `DependencyInjection.cs` | Registers `AuthService`, `AcademicYearService`, `GradeService`, `StudentService`, and all FluentValidation validators from the Application assembly |

## Infrastructure (`SchoolMgmt.Infrastructure`)

| Type | Location | Purpose |
|---|---|---|
| `AppDbContext` | `Persistence/AppDbContext.cs` | EF Core `DbContext`. Applies a global query filter to every `ITenantScoped` entity automatically (reflection over the model, not hand-written per entity). `SaveChangesAsync` is overridden to stamp `CreatedAt`/`UpdatedAt`/`SchoolId` automatically. `DbSet`s: `Schools`, `Users`, `RefreshTokens`, `AcademicYears`, `Semesters`, `Grades`, `Sections`, `Students` |
| `AppDbContextDesignTimeFactory` | `Persistence/AppDbContextDesignTimeFactory.cs` | `IDesignTimeDbContextFactory<AppDbContext>` — lets `dotnet ef` construct the context without the WebApi host. Reads `SCHOOLMGMT_CONNECTION_STRING` env var, falls back to a local default. Uses `StaticTenantProvider` (no `HttpContext` at design time) |
| `SchoolConfiguration` | `Persistence/Configurations/SchoolConfiguration.cs` | `IEntityTypeConfiguration<School>` — incl. the `HasData` seed for the one demo school (well-known id `00000000-0000-0000-0000-000000000001`) |
| `UserConfiguration` | `Persistence/Configurations/UserConfiguration.cs` | `IEntityTypeConfiguration<User>` — unique index `(SchoolId, Email)`, `Role` stored as string. Deliberately NO `HasData` seed — see `DemoDataSeeder` |
| `RefreshTokenConfiguration` | `Persistence/Configurations/RefreshTokenConfiguration.cs` | `IEntityTypeConfiguration<RefreshToken>` — unique index on `TokenHash`, FK to `User` (cascade delete) |
| `Repository<TEntity>` (internal) | `Persistence/Repositories/Repository.cs` | Generic `IRepository<TEntity>` implementation — touches `DbSet` only, never calls `SaveChanges`/transaction methods |
| `UserRepository` (internal) | `Persistence/Repositories/UserRepository.cs` | `IUserRepository` implementation — `GetByEmailAsync` uses `IgnoreQueryFilters()` |
| `RefreshTokenRepository` (internal) | `Persistence/Repositories/RefreshTokenRepository.cs` | `IRefreshTokenRepository` implementation — `IgnoreQueryFilters()` + `.Include(rt => rt.User)` |
| `UnitOfWork` (internal) | `Persistence/UnitOfWork.cs` | `IUnitOfWork` implementation — wraps `AppDbContext.SaveChangesAsync` and `Database.BeginTransactionAsync`/commit/rollback. `SaveChangesAsync` catches `DbUpdateException` with Postgres `SqlState 23505` (unique_violation) and re-throws as `ConflictException` → every service gets race-condition-safe 409s without extra code. `Detach<T>` sets entity state to `Detached` via `context.Entry(entity).State` |
| `StaticTenantProvider` (internal) | `MultiTenancy/StaticTenantProvider.cs` | `ITenantProvider` for EF Core design-time tooling only (migrations have no `HttpContext`) — always returns the seeded school's id |
| `HttpContextTenantProvider` (internal) | `MultiTenancy/HttpContextTenantProvider.cs` | Real runtime `ITenantProvider` — reads `school_id` from the authenticated user's claims via `IHttpContextAccessor`. Throws if accessed with no authenticated request (deliberate — surfaces bugs loudly) |
| `SeedDataOptions` | `MultiTenancy/SeedDataOptions.cs` | Options-bound (`SeedData` config section); default values double as the well-known school id/name (seed migration) and admin-user id/email/displayName/hash (`DemoDataSeeder`, runtime, not migration) |
| `DemoDataSeeder` | `Persistence/DemoDataSeeder.cs` | `IServiceProvider.SeedDemoDataAsync(IHostEnvironment)` extension — seeds the demo Admin user at app startup, gated by `IsDevelopment()`. Idempotent (checked by email). NOT wired via migration `HasData` — see Key Decision in architecture.md |
| `SystemDateTimeProvider` (internal) | `Common/SystemDateTimeProvider.cs` | Real `IDateTimeProvider` — wraps `DateTimeOffset.UtcNow` |
| `PasswordHasherAdapter` (internal) | `Auth/PasswordHasherAdapter.cs` | `IPasswordHasher` wrapping `Microsoft.AspNetCore.Identity.PasswordHasher<User>` |
| `JwtTokenGenerator` (internal) | `Auth/JwtTokenGenerator.cs` | `IJwtTokenGenerator` implementation — HMAC-SHA256 signed JWTs via `System.IdentityModel.Tokens.Jwt` |
| `AcademicYearConfiguration` | `Persistence/Configurations/AcademicYearConfiguration.cs` | `IEntityTypeConfiguration<AcademicYear>` — unique index `(SchoolId, Name)`, `Status` stored as string, backing-field wiring for `_semesters` navigation |
| `SemesterConfiguration` | `Persistence/Configurations/SemesterConfiguration.cs` | `IEntityTypeConfiguration<Semester>` — FK to `AcademicYears.Id` with `ON DELETE RESTRICT` (years are never hard-deleted, only archived) |
| `AcademicYearRepository` (internal) | `Persistence/Repositories/AcademicYearRepository.cs` | `IAcademicYearRepository` implementation. Uses `context.Set<Semester>()` directly for semester queries — no standalone `ISemesterRepository` |
| `GradeConfiguration` | `Persistence/Configurations/GradeConfiguration.cs` | `IEntityTypeConfiguration<Grade>` — unique index `(SchoolId, Name)`, backing-field wiring for `_sections` navigation via `builder.Navigation(x => x.Sections).HasField("_sections")` |
| `SectionConfiguration` | `Persistence/Configurations/SectionConfiguration.cs` | `IEntityTypeConfiguration<Section>` — unique index `(GradeId, Name)`, FK to `Grades.Id` with `ON DELETE RESTRICT`. Uses `.WithMany(g => g.Sections)` (public property, not string) to avoid conflict with `HasField` in `GradeConfiguration`. |
| `GradeRepository` (internal) | `Persistence/Repositories/GradeRepository.cs` | `IGradeRepository` implementation. Uses `_context.Set<Section>()` directly for section queries — same pattern as `AcademicYearRepository` with `Semester`. |
| `StudentConfiguration` | `Persistence/Configurations/StudentConfiguration.cs` | `IEntityTypeConfiguration<Student>` — unique index `(SchoolId, StudentCode)`, `Gender`/`EnrollmentStatus` stored as string (`HasConversion<string>()`), `DateOfBirth`/`EnrollmentDate` as PostgreSQL `date` |
| `StudentRepository` (internal) | `Persistence/Repositories/StudentRepository.cs` | `IStudentRepository` implementation. `GetPagedAsync` filters by status (defaults Active), optional ILIKE search on `FirstName + " " + LastName` and `StudentCode` (via `EF.Functions.ILike`), orders by LastName/FirstName. `GetNextStudentCodeAsync` uses `MAX(StudentCode)` with year prefix — EF global query filter ensures tenant scoping automatically |
| `DependencyInjection.AddInfrastructure()` | `DependencyInjection.cs` | Composition-root extension method — registers `AppDbContext`, `ITenantProvider` (→ `HttpContextTenantProvider`), `IDateTimeProvider`, `IUnitOfWork`, `IRepository<>`, `IUserRepository`, `IRefreshTokenRepository`, `IAcademicYearRepository`, `IGradeRepository`, `IStudentRepository`, `IPasswordHasher`, `IJwtTokenGenerator` |

## WebApi (`SchoolMgmt.WebApi`)

| Type | Location | Purpose |
|---|---|---|
| `AuthController` | `Controllers/AuthController.cs` | `POST /api/auth/login`, `/refresh`, `/logout` (anonymous, all side-effecting → POST), `GET /api/auth/me` (`[Authorize]`, read-only). Sets/clears `access_token`/`refresh_token` httpOnly cookies — the only place HTTP/cookie concerns touch auth |
| `Program.cs` JWT wiring | `Program.cs` | `AddAuthentication().AddJwtBearer()` + `AddOptions<JwtBearerOptions>().Configure<IOptions<JwtOptions>>(...)` (lazy DI-resolved binding — see Key Decision in architecture.md about why eager config reads broke under `WebApplicationFactory`). `MapInboundClaims = false`. Reads the access token from the `access_token` cookie via `OnMessageReceived`, not the `Authorization` header |
| `Program.cs` health check | `Program.cs` | `GET /health` (`AddHealthChecks().AddDbContextCheck<AppDbContext>()`) — anonymous, verifies real DB connectivity. Not wired into docker-compose (no compose-level healthcheck on `api`), available for external monitoring |
| `Program.cs` demo seeding | `Program.cs` | `await app.Services.SeedDemoDataAsync(app.Environment)` right after `app.Build()` — see `DemoDataSeeder` |
| `DomainExceptionFilter` | `Filters/DomainExceptionFilter.cs` | Global `IExceptionFilter` — maps `DomainException` → 400, `NotFoundException` → 404, `ConflictException` → 409. Registered in `AddControllers(options => ...)` in `Program.cs` |
| `ValidationFilter` | `Filters/ValidationFilter.cs` | Global `IAsyncActionFilter` — resolves `IValidator<TRequest>` from DI for each action argument, short-circuits with 400 + field-grouped errors on validation failure |
| `AcademicYearsController` | `Controllers/AcademicYearsController.cs` | `[Authorize(Roles = "Admin")]`. `GET /api/academic-years`, `GET /api/academic-years/{id}`, `POST /api/academic-years` (201 + Location), `PUT /api/academic-years/{yearId}/semesters/{semesterId}`, `POST /api/academic-years/{id}/set-current`, `POST /api/academic-years/{yearId}/semesters/{semesterId}/set-current`, `POST /api/academic-years/{id}/archive` |
| `GradesController` | `Controllers/GradesController.cs` | `[Authorize(Roles = "Admin")]`. `GET /api/grades`, `GET /api/grades/{id}`, `POST /api/grades` (201), `PUT /api/grades/{id}`, `DELETE /api/grades/{id}` (400 if has sections), `POST /api/grades/{gradeId}/sections` (201), `PUT /api/grades/{gradeId}/sections/{sectionId}`, `DELETE /api/grades/{gradeId}/sections/{sectionId}` |
| `StudentsController` | `Controllers/StudentsController.cs` | `[Authorize(Roles = "Admin")]`. `POST /api/students` (201 + Location), `GET /api/students` (paged, `?status=&search=&page=&pageSize=`, defaults Active/1/20, capped at 100; `search` does ILIKE on FirstName+LastName and StudentCode), `GET /api/students/{id}`, `PUT /api/students/{id}`. No DELETE endpoint — students are never hard-deleted. |

## Migrations

| Migration | Purpose |
|---|---|
| `InitialCreate` (`Persistence/Migrations/`) | Creates `Schools` table, seeds the one demo school |
| `AddUsersAndRefreshTokens` (`Persistence/Migrations/`) | Creates `Users`/`RefreshTokens` tables. No seed data — the demo Admin user is seeded at runtime instead, see `DemoDataSeeder` |
| `AddAcademicYears` (`Persistence/Migrations/`) | Creates `AcademicYears` and `Semesters` tables. `Status` stored as `varchar(20)`. Unique index `(SchoolId, Name)` on `AcademicYears`. FK `AcademicYearId → AcademicYears.Id` with `ON DELETE RESTRICT`. |
| `AddGradesAndSections` (`Persistence/Migrations/`) | Creates `Grades` and `Sections` tables. Unique index `(SchoolId, Name)` on `Grades`. Unique index `(GradeId, Name)` on `Sections`. FK `GradeId → Grades.Id` with `ON DELETE RESTRICT`. |
| `AddStudents` (`Persistence/Migrations/`) | Creates `Students` table. Unique index `(SchoolId, StudentCode)`. `Gender`/`EnrollmentStatus` stored as `varchar(20)`. `DateOfBirth`/`EnrollmentDate` as PostgreSQL `date`. |

## Tests

| Project | Covers |
|---|---|
| `tests/SchoolMgmt.Infrastructure.Tests` | `AppDbContext.SaveChangesAsync` audit/tenant stamping; `AuthService` (login/refresh-rotation/theft-detection/expiry) via hand-written fakes; `JwtTokenGenerator` claim correctness. EF Core InMemory provider where a `DbContext` is needed, no mocking library anywhere |
| `tests/SchoolMgmt.Domain.Tests` | Pure domain logic unit tests — no DB, no fakes. `AcademicYearTests`: `Create` produces exactly 2 semesters named correctly; `EnsureNotArchived` throws/doesn't throw; `Archive` sets status / throws when current |
| `tests/SchoolMgmt.IntegrationTests` | Tenant query-filter isolation, `Repository`/`UnitOfWork` staging + transaction commit/rollback, seed-migration correctness, full HTTP auth flows (`LoginTests`, `RefreshRotationTests` incl. theft-family-revocation, `LogoutTests`, `TenantResolutionTests` — proves `HttpContextTenantProvider` resolves `SchoolId` on a real authenticated request), and a composition-root smoke test — all against real Postgres via Testcontainers. `PostgresContainerFixture.CreateFactory()` is the shared `WebApplicationFactory<Program>` builder (overrides connection string + JWT config, explicitly sets `UseEnvironment("Development")` so `DemoDataSeeder` actually runs — `WebApplicationFactory` defaults to `Production` otherwise) reused across all HTTP-level tests. `AcademicYearsControllerTests`: full CRUD + set-current + archive + auth gates (401/403). `GradesControllerTests`: full CRUD for grades and sections, per-grade name uniqueness (same name allowed across grades), delete-with-sections returns 400, auth gates (401/403). `StudentsControllerTests`: create (valid + validation failures + sequential code increment + year prefix), read (paged, status filter, pageSize clamp, 404), update (name change, status transfer removes from default list, invalid status → 400, immutable StudentCode, 404), no-DELETE returns 405, auth gates (401/403). |
