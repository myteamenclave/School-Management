# Backend Coding Rules

## HTTP methods & CSRF

Auth cookies use `SameSite=Lax`. `Lax` blocks the cookie on cross-site `POST`/`PUT`/`PATCH`/`DELETE` and on cross-site `fetch`/`XHR`/form submissions — that's the actual CSRF protection. It does **not** block the cookie on a cross-site top-level GET navigation (e.g. a link click, an `<img>`/redirect).

**Rule: GET endpoints must be read-only with no side effects.** No state mutation, no marking-as-read/paid/done, no logout, no unsubscribe, behind a GET route. If an action changes state, it must be POST/PUT/PATCH/DELETE — no exceptions for "it's just a small toggle."

Why: if a GET endpoint mutates state, the `Lax` SameSite cookie config no longer protects it from CSRF — an attacker can trigger it just by getting a logged-in user to load a link or image pointing at it.

## Application layer & thin controllers

**Rule: controllers stay thin via direct DI.** Controllers inject an Application-layer service and call one method — no business logic in the controller, no manual mapping logic beyond request/response shaping.

```csharp
[HttpPost]
public async Task<IActionResult> Enroll(EnrollStudentRequest request)
{
    var result = await _studentService.EnrollAsync(request);
    return Ok(result);
}
```

**Rule: organize the Application layer as one service per feature area** (e.g. `StudentService`, `FeeService`), with one method per use case — not as a Command/Query + Handler pair per use case. Cross-cutting concerns are implemented as ASP.NET Core primitives, not as a request pipeline:

- **Validation** — a global `IAsyncActionFilter` resolves `IValidator<TRequest>` (FluentValidation) via DI for the action's request type and short-circuits with 400 on failure.
- **Logging** — a global action filter logs entry/exit and duration per controller action.
- **Transactions** — via `IUnitOfWork` (see "Repository pattern & Unit of Work" below), called from the Application service method — never raw `IDbContextTransaction` usage in a service.

Why: thin controllers come from dependency injection, not from any particular request-dispatch pattern. One service class per feature area keeps the Clean Architecture layering (Controller → Application → Domain/Infrastructure) without adding a file pair (Command + Handler) per use case.

## Repository pattern & Unit of Work

**Rule: define `IRepository`/per-entity repository interfaces (e.g. `IStudentRepository`) and `IUnitOfWork` in `SchoolMgmt.Application`**, implemented in `SchoolMgmt.Infrastructure` — same interfaces-live-where-consumed rule as everywhere else.

**Rule: repositories only touch the `DbSet` — never call `SaveChanges`/`SaveChangesAsync`, and never start/commit/roll back a transaction.** A repository method does `_dbSet.Add(entity)`, `_dbSet.Update(entity)`, `_dbSet.Remove(entity)`, `_dbSet.FindAsync(...)`, etc., and nothing that persists or transacts.

**Rule: `IUnitOfWork` owns persistence and transaction control.** It exposes `SaveChangesAsync(CancellationToken)` plus `BeginTransactionAsync`/`CommitAsync`/`RollbackAsync` (wrapping `IDbContextTransaction`). Only the Application-layer service method calls `IUnitOfWork.SaveChangesAsync()` — typically once, at the end of a use case, after one or more repository calls.

```csharp
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
```

```csharp
public class StudentService(IStudentRepository students, IUnitOfWork unitOfWork)
{
    public async Task<StudentDto> EnrollAsync(EnrollStudentRequest request, CancellationToken ct)
    {
        var student = Student.Create(request.Name, request.ClassId);
        await students.AddAsync(student, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return StudentDto.From(student);
    }
}
```

Why: keeping persistence/transaction control out of repositories means a service method can call several repositories and commit them together as one atomic save — repositories stay pure data-access wrappers, and the Application layer is the only place that decides when a unit of work actually completes.

## Dependency injection conventions

**Rule: each layer registers its own services via a `DependencyInjection.cs` extension method at the project root**, not by registering services ad hoc in `Program.cs`.

- `SchoolMgmt.Application/DependencyInjection.cs` — `AddApplication(this IServiceCollection services)`
- `SchoolMgmt.Infrastructure/DependencyInjection.cs` — `AddInfrastructure(this IServiceCollection services, IConfiguration configuration)` (needs `IConfiguration` for the connection string, external API keys, etc.)
- `SchoolMgmt.Domain` registers nothing — it has no framework dependencies at all.
- `WebApi/Program.cs` composes them: `builder.Services.AddApplication().AddInfrastructure(builder.Configuration);` — `Program.cs` never calls `services.AddScoped<...>()` directly for anything that belongs to another layer.

**Rule: interfaces live where they're consumed, implementations live where they're fulfilled.** A repository interface (e.g. `IStudentRepository`) is defined in `Application` (or `Domain`, if Domain logic needs it directly); the EF Core implementation lives in `Infrastructure`. The registration mapping interface → implementation (`services.AddScoped<IStudentRepository, StudentRepository>()`) lives in `Infrastructure/DependencyInjection.cs`, since only Infrastructure references the concrete type. This is the Dependency Inversion in "Clean Architecture" — Application never references Infrastructure's concrete types, only its own interfaces.

**Rule: default to `Scoped` lifetime.** `AddDbContext` registers `DbContext` as `Scoped` by default — match that for anything that depends on it directly or transitively (repositories, Application services, most Infrastructure services). Only use `Singleton` for services that are genuinely stateless and hold no `DbContext`/per-request state (e.g. a clock/time provider, a password hasher with no per-request data). Never let a `Singleton` depend on a `Scoped` service — that's a captured-dependency bug that surfaces as stale/shared `DbContext` state across requests. Avoid `Transient` unless there's a specific reason to create a new instance every time it's resolved.

**Rule: constructor-inject everything. No service locator.** Never resolve a dependency via `IServiceProvider.GetService<T>()` (or `HttpContext.RequestServices`) inside business logic — always take it as a constructor parameter. The only place `IServiceProvider` should appear directly is composition-root code (`Program.cs`, `DependencyInjection.cs` files) and framework-required exceptions (e.g. scoped-instance creation inside a background service).
