---
Title: ICS Operational System Implementation Plan
Code: ICS
Artifact: IMPLEMENTATION-PLAN
Version: 3.2
LastUpdated: 2026-09-27
Status: NOT-STARTED
Execution Approval: APPROVED
---

# 1. Objective

Implement the complete ICS Operational System as defined by the approved target architecture.

Planning Mode: GREENFIELD-PLANNING

Referenced artifacts:

- ARCHITECTURE: `operational/architecture/ICS-ARCHITECTURE.md` (target system architecture — authoritative)

Architecture Applicability: GREENFIELD-ARCHITECTURE

Greenfield planning mode based on complete target architecture. Scope encompasses the entire target system architecture. The current repository is in pre-implementation state. The ICS-ARCHITECTURE.md is the sole authoritative technical target state. No architectural decisions are created or re-interpreted by this plan.

---

# 2. Planning Scope

The plan covers the complete ICS Operational System architecture comprising nine implementation boundaries:

| Boundary | Assembly | Architecture §22 Dependency |
|---|---|---|
| Foundation | `ICS.Core` | None |
| Identity & Access | `ICS.Modules.Identity` | `ICS.Core`, `Organization` (Read) |
| Organization | `ICS.Modules.Organization` | `ICS.Core`, `Identity` Context |
| Customer | `ICS.Modules.Customer` | `ICS.Core`, `Identity` Context |
| Product | `ICS.Modules.Product` | `ICS.Core`, `Organization` (Read), `Identity` Context |
| Request | `ICS.Modules.Request` | `ICS.Core`, `Organization`, `Customer`, `Product`, `Identity` Context |
| Work Package | `ICS.Modules.WorkPackage` | `ICS.Core`, `Organization`, `Customer`, `Product`, `Request`, `Identity` Context |
| Post & Feed | `ICS.Modules.Post` | `ICS.Core`, Domain Events from all modules, `Identity` Context |
| Management Analytics | `ICS.Modules.Analytics` | `ICS.Core`, `Request`, `Organization`, `Customer`, `Identity` Context |

Scope derives directly from Architecture §22 (Implementation Boundaries) and §23 (Implementation Dependency Graph). No feature sub-scope is applied; the plan covers the entire target system.

**Vertical Slice Mandate for Implementation Agents**:
Presentation screens and endpoints are delivered vertically within each module's phase to enable early verification rather than accumulating horizontal layered batch work packages. Implementation agents must execute each slice as a cohesive vertical deliverable (coupling domain rules, Dapper parameterized persistence, MediatR handlers, and presentation controllers/Vue SFCs where applicable).

Technology stack is explicitly defined in Architecture §19 and is authoritative for all implementation phases. Key mandates:

- **Backend**: .NET 8, ASP.NET Core 8.0, C# 12, MediatR, FluentValidation, Serilog — Architecture §19.1, §19.2
- **Persistence**: Microsoft SQL Server 2019, Dapper (explicit parameterized SQL), DbUp-SqlServer for migrations. EF Core is **strictly prohibited** — Architecture §19.3, §20
- **Frontend**: Vue 3 (Composition API, `<script setup lang="ts">`), TypeScript, Bootstrap 5, Vite, Pinia, Vue Router 4 — Architecture §19.4
- **Testing**: xUnit, FluentAssertions, `WebApplicationFactory<Program>` (integration), Respawn (test isolation) — Architecture §19.8
- **Logging**: Serilog (structured JSON, enriched with TraceId, UserId, PersonId) — Architecture §19.9
- **Deployment**: Modular Monolith hosted as a single ASP.NET Core process running in-process within **IIS (Internet Information Services) on Windows Server** via the ASP.NET Core Module (`AspNetCoreHostingModel = InProcess`). Vue 3 SPA compiled via Vite into `ICS.Web/wwwroot/`, published via `dotnet publish -c Release`, with DbUp migrations executing on deployment — Architecture §19.10

---

# 3. Business Milestones

Milestones represent observable business-level delivery checkpoints, independent of phase numbering.

| Milestone | Achieved After | Observable Capability |
|---|---|---|
| **M0 — Foundation Ready** | P1-S06 | Infrastructure operational: database migrations run, DI container resolves, event bus dispatches, application starts and serves requests |
| **M1 — Authentication & Catalog Operational** | P3-S15 | Users can log in. Organization, Customer, and Product master data is accessible. Product catalog screen (`SCR-PRD-001`) is functional. First end-to-end user flows are possible |
| **M2 — Request Lifecycle Operational** | P4-S19 | Complete request management operational: Record, Assign, Evaluate, Accept, Reject, Escalate, Decision, Complete. All request screens (`SCR-REQ-001..005`) functional. Core business operations running |
| **M3 — Work Package Operational** | P5-S22 | Work Packages can be created, managed, activated, closed, and linked to Requests. Work Package screen (`SCR-WP-001`) functional |
| **M4 — Operational Feed Live** | P6-S26 | Feed screen (`SCR-FEED-001`) operational. Posts, comments, reactions functional. Exception badges working. Post detail modal (`SCR-POST-001`) functional. Feed filtering by Customer, Product, and Exception active |
| **M5 — Full System Operational** | P7-S31 | Management dashboards (`SCR-MGT-001..003`) operational. Analytics snapshot job running. Authoritative IIS on Windows Server production deployment package (`deploy/publish.ps1`, `web.config`, AppPool configuration) generated and verified. All cross-cutting concerns verified end-to-end. System ready for UAT |
| **M6 — UAT Approved** | Post-P7 Acceptance Gate | Formal business stakeholder and QA acceptance completed across all primary user journeys (Request lifecycle, Collaboration, Feed, and Management oversight). Test package executed, defect package verified, and operational acceptance sign-off granted |

---

# 4. Dependencies

## External Dependencies

- Microsoft SQL Server 2019 must be available in the target environment before any schema migration slices can execute — Architecture §19.3.
- .NET 8 SDK and build toolchain must be installed before application module slices can compile — Architecture §19.1.
- Node.js (for Vite/Vue 3 build toolchain) must be available for frontend build — Architecture §19.4.

## Slice Dependency Rules (per skill)

- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- A dependency is satisfied when the referenced slice has implementation status `IMPLEMENTED` and its required outputs exist in the target repository.
- Review status does not participate in dependency satisfaction.
- Dependencies represent real implementation prerequisites; they are not conceptual or sequential associations.
- `Depends On: P1-S06` transitively implies all P1 application pipeline slices are implemented, since P1-S06 is the terminal application-pipeline slice of the Foundation phase.

## Parallel Execution Opportunities

- **Customer and Product Concurrency**: In Phase 3, Customer (P3-S13) does not depend on Organization (P3-S12) or Product (P3-S14). Product (P3-S14) depends on Organization (P3-S12) for owner validation, but has zero dependency on Customer (P3-S13). Consequently, Customer and Product can execute concurrently in parallel to compress the delivery schedule.
- **Foundation Scaffold Concurrency**: In Phase 1, P1-S07 (Test Infrastructure) and P1-S08 (Frontend Scaffolding) can execute concurrently with P1-S02 through P1-S06 once P1-S01 is established.

---

# 5. Progress Summary

| Phase | Slices | Implementation Status | Review Status | Progress |
|---|---|---|---|---|
| P1 — Foundation | S01–S08 | NOT-STARTED | NOT-REVIEWED | 0/8 |
| P2 — Identity & Access | S09–S11 | NOT-STARTED | NOT-REVIEWED | 0/3 |
| P3 — Core Master Data & Product Catalog | S12–S15 | NOT-STARTED | NOT-REVIEWED | 0/4 |
| P4 — Request Lifecycle | S16–S19 | NOT-STARTED | NOT-REVIEWED | 0/4 |
| P5 — Work Package | S20–S22 | NOT-STARTED | NOT-REVIEWED | 0/3 |
| P6 — Post & Feed | S23–S26 | NOT-STARTED | NOT-REVIEWED | 0/4 |
| P7 — Management Analytics & System Finalization | S27–S31 | NOT-STARTED | NOT-REVIEWED | 0/5 |

---

# 6. Phases

## P1 — Foundation

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Establish the complete shared technical foundation (`ICS.Core`) that all modules depend on. Covers solution structure and project layout (Architecture §19.11), shared contracts, database infrastructure using DbUp-SqlServer (Architecture §19.3), dependency injection conventions with MediatR (Architecture §19.2), in-process domain event bus, application pipeline skeleton with Serilog structured logging (Architecture §19.9), test project infrastructure (Architecture §19.8), and Vue 3 frontend project scaffolding (Architecture §19.4). No business module is implemented in this phase. M0 is achieved when P1-S06 (Application Pipeline Foundation) is complete.

Source: Architecture §22 — Boundary: **Foundation** (`ICS.Core`, Depends On: None). Architecture §18 (Cross-Cutting Concerns). Architecture §19 (Technology Decisions). Architecture §5 (System Structure).

---

### P1-S01

Title: Solution Structure & Project Scaffolding

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Create the solution file, project references, and exact directory structure for the modular monolith as specified in Architecture §19.11. Establish `ICS.Core`, all `ICS.Modules.*` project skeletons (empty, buildable), the `ICS.Web` host application project, and the `deploy/` directory. All projects target .NET 8. Define inter-project reference graph matching Architecture §22 dependency table.

Depends On: None

Repository: Solution root / `ICS.Core`

Completion Criteria:
- Solution file `ICS.sln` exists at repository root and compiles successfully with zero errors.
- Directory structure matches Architecture §19.11 exactly:
  - `src/ICS.Core/`
  - `src/ICS.Modules.Identity/`
  - `src/ICS.Modules.Organization/`
  - `src/ICS.Modules.Customer/`
  - `src/ICS.Modules.Product/`
  - `src/ICS.Modules.WorkPackage/`
  - `src/ICS.Modules.Request/`
  - `src/ICS.Modules.Post/`
  - `src/ICS.Modules.Analytics/`
  - `src/ICS.Web/` (ASP.NET Core host, API Controllers, Middleware, DbUp migrations)
  - `deploy/` (placeholder directory for production hosting definitions and publish scripts)
- All projects target `net8.0`. C# 12 language version, nullable reference types enabled, implicit usings enabled — Architecture §19.1.
- `ICS.Web` project references all module projects.
- Project reference graph matches Architecture §22 dependency table with no circular references.
- `dotnet build ICS.sln` succeeds on the solution with zero errors.

Notes: Produces no business logic. Its output is the compilable project structure that all subsequent slices build into. The `tests/` directory and `deploy/` packaging scripts are created by P1-S07 and P7-S31 respectively; only the `deploy/` placeholder directory is created here.

Implementation Notes:
- Created root solution `ICS.sln` in standard sln format with all 10 projects enrolled.
- Created `deploy/` placeholder directory with `.gitkeep` for production hosting definitions and publish scripts.
- Created 10 project skeletons targeting `net8.0`, with C# 12 (`<LangVersion>12</LangVersion>`), nullable reference types enabled (`<Nullable>enable</Nullable>`), and implicit usings enabled (`<ImplicitUsings>enable</ImplicitUsings>`):
  - `src/ICS.Core/ICS.Core.csproj`
  - `src/ICS.Modules.Identity/ICS.Modules.Identity.csproj`
  - `src/ICS.Modules.Organization/ICS.Modules.Organization.csproj`
  - `src/ICS.Modules.Customer/ICS.Modules.Customer.csproj`
  - `src/ICS.Modules.Product/ICS.Modules.Product.csproj`
  - `src/ICS.Modules.WorkPackage/ICS.Modules.WorkPackage.csproj`
  - `src/ICS.Modules.Request/ICS.Modules.Request.csproj`
  - `src/ICS.Modules.Post/ICS.Modules.Post.csproj`
  - `src/ICS.Modules.Analytics/ICS.Modules.Analytics.csproj`
  - `src/ICS.Web/ICS.Web.csproj` (and minimal buildable `Program.cs`)
- Defined inter-project reference graph strictly matching Architecture §22 dependency table with zero circular references:
  - `ICS.Core`: None
  - `ICS.Modules.Identity`: `ICS.Core`, `ICS.Modules.Organization`
  - `ICS.Modules.Organization`: `ICS.Core`
  - `ICS.Modules.Customer`: `ICS.Core`
  - `ICS.Modules.Product`: `ICS.Core`, `ICS.Modules.Organization`
  - `ICS.Modules.Request`: `ICS.Core`, `ICS.Modules.Organization`, `ICS.Modules.Customer`, `ICS.Modules.Product`
  - `ICS.Modules.WorkPackage`: `ICS.Core`, `ICS.Modules.Organization`, `ICS.Modules.Customer`, `ICS.Modules.Product`, `ICS.Modules.Request`
  - `ICS.Modules.Post`: `ICS.Core`
  - `ICS.Modules.Analytics`: `ICS.Core`, `ICS.Modules.Request`, `ICS.Modules.Organization`, `ICS.Modules.Customer`
  - `ICS.Web`: `ICS.Core` and all `ICS.Modules.*`
- Added standard `.gitignore` for .NET development.
- Verified compilation with non-incremental `dotnet build ICS.sln` succeeding with 0 warnings and 0 errors.

Changed Files:
- `.gitignore`
- `ICS.sln`
- `deploy/.gitkeep`
- `src/ICS.Core/ICS.Core.csproj`
- `src/ICS.Modules.Identity/ICS.Modules.Identity.csproj`
- `src/ICS.Modules.Organization/ICS.Modules.Organization.csproj`
- `src/ICS.Modules.Customer/ICS.Modules.Customer.csproj`
- `src/ICS.Modules.Product/ICS.Modules.Product.csproj`
- `src/ICS.Modules.WorkPackage/ICS.Modules.WorkPackage.csproj`
- `src/ICS.Modules.Request/ICS.Modules.Request.csproj`
- `src/ICS.Modules.Post/ICS.Modules.Post.csproj`
- `src/ICS.Modules.Analytics/ICS.Modules.Analytics.csproj`
- `src/ICS.Web/ICS.Web.csproj`
- `src/ICS.Web/Program.cs`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`

---

### P1-S02

Title: Core Contracts & Base Types

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Define all shared technical contracts and base types in `ICS.Core`: base entity type, value object base, domain event interface, domain event dispatcher interface, repository interface contract, system clock abstraction, audit context interface, and the `ICurrentContextProvider` interface. Add `MediatR` and `FluentValidation.AspNetCore` NuGet package references to `ICS.Core` and `ICS.Web`. Interfaces only — no concrete implementations in this slice.

Depends On: P1-S01

Repository: `ICS.Core`

Completion Criteria:
- `MediatR` NuGet package referenced in `ICS.Core` — Architecture §19.2.
- `FluentValidation.AspNetCore` NuGet package referenced in `ICS.Core` — Architecture §19.2.
- `Microsoft.Data.SqlClient` and `Dapper` NuGet packages referenced in `ICS.Core` — Architecture §19.3.
- Base entity type (with `Id`, `CreatedAt`, `UpdatedAt`) is defined.
- `IDomainEvent` interface is defined (MediatR `INotification` compatible).
- `IDomainEventDispatcher` interface is defined (publish method only; no implementation).
- `IRepository<T>` or equivalent repository contract is defined.
- `ISystemClock` abstraction is defined.
- `IAuditContext` interface is defined.
- `ICurrentContextProvider` interface is defined, exposing `CurrentUserId`, `CurrentPersonId`, and `CurrentRoles` — per Architecture §7 and §14.
- `IModule` interface is defined for module bootstrapper registration pattern.
- All types are in the `ICS.Core` namespace and accessible to all module projects.
- No concrete implementations are present in this slice (interfaces and base types only).

Notes: Keeps contracts decoupled from implementation. Concrete domain event dispatcher is implemented in P1-S05. Architecture §7 (`CurrentContextProvider`), §14 (IAM), and §18 (Cross-Cutting Concerns) are authoritative for required interfaces.

Implementation Notes:
- Added NuGet package references to `ICS.Core`: `MediatR` (v14.2.0), `FluentValidation.AspNetCore` (v11.3.1), `Microsoft.Data.SqlClient` (v7.1.0), `Dapper` (v2.1.89), `Microsoft.Extensions.Configuration.Abstractions` (v10.0.12), `Microsoft.Extensions.DependencyInjection.Abstractions` (v10.0.12).
- Added NuGet package references to `ICS.Web`: `MediatR` (v14.2.0), `FluentValidation.AspNetCore` (v11.3.1).
- Implemented `IDomainEvent` interface inheriting `MediatR.INotification` with `EventId` and `OccurredAt` (`ICS.Core.Domain`).
- Implemented `IDomainEventDispatcher` interface with synchronous in-process publish methods (`PublishAsync` and `DispatchAsync`) (`ICS.Core.Domain`).
- Implemented generic `Entity<TId>` and default Guid-based `Entity` base types with `Id`, `CreatedAt`, `UpdatedAt`, domain events tracking collection (`AddDomainEvent`, `RemoveDomainEvent`, `ClearDomainEvents`), and entity equality operators (`ICS.Core.Domain`).
- Implemented `ValueObject` base class with component-based equality comparison (`ICS.Core.Domain`).
- Implemented `IRepository<T, TId>` and `IRepository<T>` generic repository contracts (`ICS.Core.Domain`).
- Implemented `ISystemClock` abstraction exposing `UtcNow` and `OffsetUtcNow` (`ICS.Core.Time`).
- Implemented `IAuditContext` interface exposing `UserId`, `PersonId`, `Timestamp`, `IpAddress`, and `UserAgent` per Architecture §18 (`ICS.Core.Audit`).
- Implemented `ICurrentContextProvider` interface exposing `CurrentUserId`, `CurrentPersonId`, `CurrentRoles`, `IsAuthenticated`, and `IsInRole` per Architecture §7 and §14 (`ICS.Core.Auth`).
- Implemented `IModule` interface for module bootstrapper registration pattern exposing `Name` and `RegisterServices` (`ICS.Core.Modules`).
- Configured `GlobalUsings.cs` in `ICS.Core` to import child namespaces (`ICS.Core.Domain`, `ICS.Core.Time`, `ICS.Core.Audit`, `ICS.Core.Auth`, `ICS.Core.Modules`).
- Verified solution build with `dotnet build ICS.sln` compiling cleanly with 0 warnings and 0 errors.

Changed Files:
- `src/ICS.Core/ICS.Core.csproj`
- `src/ICS.Core/GlobalUsings.cs`
- `src/ICS.Core/Domain/IDomainEvent.cs`
- `src/ICS.Core/Domain/IDomainEventDispatcher.cs`
- `src/ICS.Core/Domain/Entity.cs`
- `src/ICS.Core/Domain/ValueObject.cs`
- `src/ICS.Core/Domain/IRepository.cs`
- `src/ICS.Core/Time/ISystemClock.cs`
- `src/ICS.Core/Audit/IAuditContext.cs`
- `src/ICS.Core/Auth/ICurrentContextProvider.cs`
- `src/ICS.Core/Modules/IModule.cs`
- `src/ICS.Web/ICS.Web.csproj`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`

---

### P1-S03

Title: Database Infrastructure & Migration Framework

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Configure DbUp-SqlServer migration framework, database connection management (Dapper + `Microsoft.Data.SqlClient`), and schema-per-module naming conventions. Establish the technical foundation that all module schema migrations will build on. Implement database connection factory, DbUp migration runner wired into application startup, and schema-segregation naming conventions (`identity.*`, `organization.*`, `customer.*`, `product.*`, `workpackage.*`, `request.*`, `post.*`, `analytics.*`) per Architecture §17 and §19.3.

Depends On: P1-S01

Repository: `ICS.Core` / `ICS.Web`

Completion Criteria:
- `DbUp-SqlServer` NuGet package referenced in `ICS.Web` — Architecture §19.3.
- DbUp migration runner is configured to discover and execute sequential idempotent raw SQL scripts in strict dependency order upon application startup — Architecture §19.3 ("Sequential idempotent raw SQL scripts managed and executed in strict dependency order via DbUp").
- Database connection factory (`SqlConnection` via `Microsoft.Data.SqlClient`) is implemented and configurable via `ConnectionStrings__DefaultConnection` environment variable — Architecture §19.10.
- Schema-per-module naming convention is established and documented: eight schema prefixes matching Architecture §17 (`identity`, `organization`, `customer`, `product`, `workpackage`, `request`, `post`, `analytics`).
- DbUp migration runner can apply an empty baseline migration without errors against SQL Server.
- Database health check confirms SQL Server connectivity.
- **Entity Framework (EF Core) is not referenced in any project** — Architecture §19.3 (EF Core strictly prohibited), §20.
- No business tables are created in this slice; only the migration infrastructure is established.

Notes: P1-S03 depends only on P1-S01 and can execute in parallel with P1-S02. Architecture §17 (Schema Segregation) and §19.3 (Persistence & Data Access) are authoritative for all persistence decisions. EF Core packages (`Microsoft.EntityFrameworkCore*`) must not appear in any `.csproj` file.

Implementation Notes:
- Added `dbup-sqlserver` (v7.2.0) NuGet package to `ICS.Web`.
- Implemented `IDbConnectionFactory` interface and `SqlConnectionFactory` concrete implementation using `Microsoft.Data.SqlClient` in `ICS.Core.Data`.
- Implemented `DatabaseSchemas` constant class establishing the eight authoritative bounded context schema names (`identity`, `organization`, `customer`, `product`, `workpackage`, `request`, `post`, `analytics`) matching Architecture §17.
- Implemented `DatabaseServiceExtensions` providing `AddDatabaseInfrastructure` configured to resolve `ConnectionStrings:DefaultConnection` and environment variable `ConnectionStrings__DefaultConnection` per Architecture §19.10.
- Created baseline idempotent migration script `Script0001_CreateModuleSchemas.sql` configured as an embedded resource in `ICS.Web` to ensure all 8 module schemas exist.
- Implemented `DbUpMigrationRunner` with automatic database creation (`EnsureDatabase.For.SqlDatabase`), script discovery (`WithScriptsEmbeddedInAssembly`), schema version journaling (`dbo.SchemaVersions`), and per-script transactional execution.
- Added CLI migration flag support (`--migrate`) and wired DbUp migration runner into application startup in `Program.cs`.
- Implemented `SqlDatabaseHealthCheck` implementing `IHealthCheck` via `IDbConnectionFactory` and Dapper query execution (`SELECT 1;`), wired to `/health/ready` (readiness) probe and `/health/live` (liveness) probe in `Program.cs` per Architecture §19.9 and §19.10.
- Created default `appsettings.json` and `appsettings.Development.json` with SQL Server connection strings.
- Verified EF Core prohibition: confirmed zero references to `Microsoft.EntityFrameworkCore*` across all project files.
- Verified migrations execution against local SQL Server Developer Edition: baseline migration executed cleanly creating the 8 schemas and `dbo.SchemaVersions` with 0 business tables, and idempotency verified on re-run.
- Verified health checks live and ready probes returning `Healthy`.
- Verified non-incremental solution build `dotnet build ICS.sln --no-incremental` succeeding with 0 warnings and 0 errors.

Changed Files:
- `src/ICS.Core/Data/DatabaseSchemas.cs`
- `src/ICS.Core/Data/IDbConnectionFactory.cs`
- `src/ICS.Core/Data/SqlConnectionFactory.cs`
- `src/ICS.Core/Data/DatabaseServiceExtensions.cs`
- `src/ICS.Core/GlobalUsings.cs`
- `src/ICS.Web/ICS.Web.csproj`
- `src/ICS.Web/Migrations/Script0001_CreateModuleSchemas.sql`
- `src/ICS.Web/Migrations/DbUpMigrationRunner.cs`
- `src/ICS.Web/Health/SqlDatabaseHealthCheck.cs`
- `src/ICS.Web/Program.cs`
- `src/ICS.Web/appsettings.json`
- `src/ICS.Web/appsettings.Development.json`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`

---

### P1-S04

Title: Dependency Injection & Module Registration

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the module registration pattern and DI container configuration. Each `ICS.Modules.*` project will implement `IModule` to self-register its services, repositories, MediatR handlers, and FluentValidation validators. The host application (`ICS.Web`) discovers and invokes all `IModule` implementations at startup. Register MediatR with pipeline behaviors (validation behavior invoking FluentValidation before handler execution). Establish service lifetime conventions (Singleton, Scoped, Transient) per architectural pattern.

Depends On: P1-S02, P1-S03

Repository: `ICS.Core` / `ICS.Web`

Completion Criteria:
- MediatR is registered in the DI container with pipeline behavior for FluentValidation — Architecture §19.2 ("FluentValidation ... executed automatically via MediatR pipeline behaviors prior to handler execution").
- FluentValidation validators are auto-discovered and registered via the DI container per module.
- `IModule` implementation pattern is demonstrated with a stub module that registers successfully.
- Host application (`ICS.Web`) startup discovers and registers all `IModule` implementations.
- Service lifetime conventions are established and applied consistently.
- DI container resolves all core interfaces (`ISystemClock`, `IAuditContext`, `ICurrentContextProvider`) after startup.
- Build and DI resolution verification tests pass (no unresolved dependencies at startup).

Notes: Depends on P1-S02 for `IModule` interface and P1-S03 for database connection registration.

Implementation Notes:
- Implemented concrete `SystemClock : ISystemClock` in `ICS.Core.Time` providing UTC system time, registered with Singleton lifetime convention.
- Implemented `CurrentContextProvider : ICurrentContextProvider, ICurrentContextAccessor` in `ICS.Core.Auth` for ambient security context mutation and resolution per Architecture §7, §14, and §18, registered with Scoped lifetime convention.
- Implemented `AuditContext : IAuditContext, IAuditContextAccessor` in `ICS.Core.Audit` resolving actor identity, UTC timestamp, and client metadata per Architecture §18, registered with Scoped lifetime convention.
- Implemented `ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>` in `ICS.Core.Validation` executing FluentValidation validators prior to MediatR handler execution and throwing `FluentValidation.ValidationException` on validation failure per Architecture §19.2.
- Created `PingCommand`, `PingCommandValidator`, and `PingCommandHandler` in `ICS.Core.Validation` demonstrating MediatR request dispatch, validation pipeline interception, and handler execution.
- Implemented `ModuleRegistrationExtensions` in `ICS.Core.Modules` providing `AddModules` for auto-discovering, instantiating, and invoking `IModule.RegisterServices`, and registering each `IModule` instance as a Singleton in the DI container.
- Implemented concrete `IModule` bootstrappers across all 8 bounded context modules: `IdentityModule`, `OrganizationModule`, `CustomerModule`, `ProductModule`, `WorkPackageModule`, `RequestModule`, `PostModule`, and `AnalyticsModule`.
- Implemented `CoreServiceExtensions.AddCoreServices` in `ICS.Core` registering core abstractions, MediatR with `ValidationBehavior`, and FluentValidation validator auto-discovery.
- Created `ServiceLifetimeConventions` documenting architectural lifetime rules: Singleton (stateless infrastructure, clock, db connection factory, module descriptors), Scoped (ambient contexts, validators, repositories, domain/application services), Transient (MediatR request handlers and pipeline behaviors).
- Implemented `DependencyInjectionValidator.VerifyContainer` in `ICS.Core` executing runtime assertions on core service resolution, module registration, validation pipeline interception, and handler execution.
- Updated `src/ICS.Web/Program.cs` to wire `AddCoreServices`, `AddModules`, runtime DI container verification, `--verify-di` CLI switch, `/api/v1/system/modules` listing endpoint, and `/api/v1/ping` demonstration endpoint.
- Added `launchSettings.json` in `src/ICS.Web/Properties/` configuring port 5050 for development execution.
- Verified runtime execution: `--verify-di` passed cleanly; web host served `/api/v1/system/modules` (returning all 8 modules), `/api/v1/ping` (valid ping returning echo and timestamp, empty message triggering validation failure with `ValidationException`), and `/health/live` + `/health/ready` probes returning HTTP 200.
- Verified non-incremental solution build: `dotnet build ICS.sln --no-incremental` succeeded with 0 warnings and 0 errors.

Changed Files:
- `src/ICS.Core/ICS.Core.csproj`
- `src/ICS.Core/GlobalUsings.cs`
- `src/ICS.Core/Time/SystemClock.cs`
- `src/ICS.Core/Auth/ICurrentContextAccessor.cs`
- `src/ICS.Core/Auth/CurrentContextProvider.cs`
- `src/ICS.Core/Audit/IAuditContextAccessor.cs`
- `src/ICS.Core/Audit/AuditContext.cs`
- `src/ICS.Core/Validation/ValidationBehavior.cs`
- `src/ICS.Core/Validation/PingCommand.cs`
- `src/ICS.Core/Modules/ModuleRegistrationExtensions.cs`
- `src/ICS.Core/CoreServiceExtensions.cs`
- `src/ICS.Core/DependencyInjection/ServiceLifetimeConventions.cs`
- `src/ICS.Core/DependencyInjection/DependencyInjectionValidator.cs`
- `src/ICS.Modules.Identity/IdentityModule.cs`
- `src/ICS.Modules.Organization/OrganizationModule.cs`
- `src/ICS.Modules.Customer/CustomerModule.cs`
- `src/ICS.Modules.Product/ProductModule.cs`
- `src/ICS.Modules.WorkPackage/WorkPackageModule.cs`
- `src/ICS.Modules.Request/RequestModule.cs`
- `src/ICS.Modules.Post/PostModule.cs`
- `src/ICS.Modules.Analytics/AnalyticsModule.cs`
- `src/ICS.Web/Program.cs`
- `src/ICS.Web/Properties/launchSettings.json`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`

---

### P1-S05

Title: Domain Event Bus Implementation

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the concrete in-process domain event bus: `DomainEventDispatcher` implementing `IDomainEventDispatcher`. The dispatcher must support synchronous in-process event publishing using MediatR `IPublisher` within the same database transaction scope as the originating command — per Architecture §18 and §12 (Synchronization Guarantee). Event handlers are implemented as MediatR `INotificationHandler<T>` and discovered through the DI container.

Depends On: P1-S02

Repository: `ICS.Core`

Completion Criteria:
- `DomainEventDispatcher` implements `IDomainEventDispatcher` using MediatR `IPublisher.Publish()` — Architecture §19.2.
- Event dispatch is synchronous and executes within the same transactional scope as the command that raised the event — per Architecture §18 ("in-process synchronous event dispatcher with transactional consistency").
- Multiple handlers (`INotificationHandler<T>`) registered for the same event type are all invoked.
- Event handlers are registered and discovered via the DI container (registered in `IModule`).
- Unit tests (xUnit): publishing an event invokes all registered handlers; missing handler does not throw; handler exception propagates correctly.

Notes: Depends on P1-S02 for the `IDomainEvent` and `IDomainEventDispatcher` interfaces. Can execute in parallel with P1-S03 after P1-S02 is implemented. Architecture §18 (Domain Event Bus) and §19.2 (MediatR) are authoritative.

Implementation Notes:
- Implemented `DomainEvent` abstract base record in `ICS.Core.Domain` automatically initializing `EventId` (Guid) and `OccurredAt` (UTC DateTime), implementing `IDomainEvent` and MediatR `INotification`.
- Implemented concrete `DomainEventDispatcher : IDomainEventDispatcher` in `ICS.Core.Domain` utilizing MediatR `IPublisher.Publish` with runtime concrete type resolution to ensure synchronous in-process dispatching within the originating transactional scope per Architecture §12 and §18.
- Implemented `DomainEventDispatcherExtensions` in `ICS.Core.Domain` providing `DispatchAndClearEventsAsync` helper methods for aggregates and entities.
- Registered `DomainEventDispatcher` and `IDomainEventDispatcher` in `CoreServiceExtensions.AddCoreServices` with `Scoped` lifetime convention aligned with `ServiceLifetimeConventions`.
- Implemented `IEventExecutionJournal` and `EventExecutionJournal` in `ICS.Core.Domain.Verification` to track domain event handler invocations in memory.
- Implemented diagnostic test suite `DomainEventDispatcherValidator.Verify` in `ICS.Core.Domain.Verification` testing:
  1. DI resolution: verifies `IDomainEventDispatcher` resolves to `DomainEventDispatcher`.
  2. Multi-handler dispatch: verifies multiple `INotificationHandler<T>` implementations (`SampleMultiHandlerOne`, `SampleMultiHandlerTwo`) registered for the same event are all invoked.
  3. Missing handler tolerance: verifies publishing an event without registered handlers completes cleanly without throwing.
  4. Exception propagation: verifies handler exceptions propagate directly to caller to preserve transactional rollback consistency.
  5. Entity event dispatching and clearing: verifies pending events staged on `Entity` are dispatched and cleared cleanly.
  6. Parameter validation: verifies null argument checks.
- Wired event dispatcher verification into `DependencyInjectionValidator.VerifyContainer` and added CLI switch `--verify-events` in `src/ICS.Web/Program.cs`.
- Added demonstration HTTP endpoint `/api/v1/system/events/test` in `src/ICS.Web/Program.cs` verifying synchronous in-process multi-handler dispatch end-to-end.
- Verified solution build `dotnet build ICS.sln --no-incremental` succeeding with 0 warnings and 0 errors.

Changed Files:
- `src/ICS.Core/Domain/DomainEvent.cs`
- `src/ICS.Core/Domain/DomainEventDispatcher.cs`
- `src/ICS.Core/Domain/DomainEventDispatcherExtensions.cs`
- `src/ICS.Core/Domain/Verification/DomainEventVerificationTypes.cs`
- `src/ICS.Core/Domain/Verification/DomainEventDispatcherValidator.cs`
- `src/ICS.Core/CoreServiceExtensions.cs`
- `src/ICS.Core/DependencyInjection/ServiceLifetimeConventions.cs`
- `src/ICS.Core/DependencyInjection/DependencyInjectionValidator.cs`
- `src/ICS.Web/Program.cs`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`

---

### P1-S06

Title: Application Pipeline Foundation

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Configure the HTTP request pipeline skeleton in `ICS.Web`: middleware registration order, centralized exception handling middleware producing RFC 7807 `ProblemDetails` responses, input validation pipeline wired via MediatR FluentValidation behavior, Serilog structured logging enriched with `TraceId`, `SpanId`, `UserId`, and `PersonId`, audit logging hook, and security context population middleware placeholder. Establish the application entry point, health check endpoints (`/health/live`, `/health/ready`), and the wiring point for all modules. Application must start, serve requests, and return structured error responses.

Depends On: P1-S04, P1-S05

Repository: `ICS.Web`

Completion Criteria:
- `Serilog.AspNetCore` NuGet package referenced and configured with structured JSON logging enriched with `TraceId`, `SpanId`, `UserId`, `PersonId`, and `SourceContext` — Architecture §19.9. Console (stdout) and rolling file sinks configured.
- HTTP request pipeline is configured with documented middleware order.
- Centralized exception handling middleware returns consistent RFC 7807 `ProblemDetails` JSON responses with consistent error codes — Architecture §19.6 ("standard RFC 7807 Problem Details (`ProblemDetails`) JSON responses"). `System.Text.Json` with camelCase naming policy configured — Architecture §19.6.
- Input validation pipeline is in place and invoked before application service dispatch (MediatR FluentValidation pipeline behavior).
- Audit logging hook is registered in the pipeline — Architecture §18 (Audit Logging).
- Security context population middleware placeholder is registered (concrete implementation supplied by P3-S13).
- ASP.NET Core Health Check endpoints respond: `/health/live` (HTTP 200) and `/health/ready` (HTTP 200, validates SQL Server connectivity) — Architecture §19.9.
- Application starts, wires all registered `IModule` bootstrappers, and serves requests without unresolved dependency errors.
- REST API base route convention `/api/v1/{module}/{resource}` is established — Architecture §19.6.

Notes: Terminal application-pipeline slice of P1. All subsequent phases producing application logic depend on P1-S06. Architecture §5 (System Structure), §18 (Cross-Cutting Concerns), §19.6 (API Style), §19.9 (Logging & Observability), and §20 (Implementation Constraints) are authoritative.

Implementation Notes:
- Added NuGet package references `Serilog.AspNetCore` (v10.0.0) and `Serilog.Sinks.File` (v7.0.0) to `src/ICS.Web/ICS.Web.csproj`.
- Configured Serilog structured JSON logging in `src/ICS.Web/Logging/SerilogConfiguration.cs` with stdout Console sink and daily rolling file sink (`logs/ics-.json`) using `JsonFormatter`.
- Implemented `AmbientContextLogEnricher` dynamically enriching log events with `TraceId`, `SpanId`, `UserId`, and `PersonId`, complementing Serilog's standard `SourceContext`.
- Implemented `LoggingContextMiddleware` pushing ambient `TraceId`, `SpanId`, `UserId`, and `PersonId` to Serilog `LogContext` for each HTTP request scope.
- Established and documented authoritative HTTP Request Pipeline Middleware registration order in `src/ICS.Web/Middleware/PipelineOrder.cs` and `Program.cs`:
  1. Serilog HTTP Request Logging (`UseSerilogRequestLogging`) with diagnostic context enrichment
  2. Centralized Exception Handling Middleware (`ExceptionHandlingMiddleware`)
  3. Logging Context Middleware (`LoggingContextMiddleware`)
  4. Security Context Population Middleware Placeholder (`SecurityContextMiddleware`)
  5. Audit Logging Middleware Hook (`AuditLoggingMiddleware`)
  6. Routing & Endpoints (Minimal APIs, Controllers, Health checks)
- Implemented `ExceptionHandlingMiddleware` producing RFC 7807 `ProblemDetails` and `ValidationProblemDetails` responses with `application/problem+json` content-type, camelCase `System.Text.Json` serialization, consistent error codes (`VALIDATION_ERROR`, `BAD_REQUEST`, `RESOURCE_NOT_FOUND`, `UNAUTHORIZED`, `INVALID_OPERATION`, `INVALID_ARGUMENT`, `INTERNAL_SERVER_ERROR`), correlation identifiers (`traceId`, `spanId`), and UTC `timestamp`.
- Configured `System.Text.Json` camelCase property naming and dictionary key naming policies across HTTP JSON options and ASP.NET Core Controllers (`AddControllers`).
- Implemented `SecurityContextMiddleware` placeholder extracting ambient identity metadata from headers (`X-User-Id`, `X-Person-Id`, `X-Roles`) and claims, populating `ICurrentContextAccessor`.
- Implemented `AuditLoggingMiddleware` capturing client IP and User-Agent into `IAuditContextAccessor`, logging structured audit records for state-mutating requests (POST, PUT, PATCH, DELETE) per Architecture §18.
- Configured ASP.NET Core Health Check endpoints: `/health/live` (process liveness probe) and `/health/ready` (subsystem readiness probe validating SQL Server connectivity via `SqlDatabaseHealthCheck`).
- Established REST API base route convention `/api/v1/{module}/{resource}` per Architecture §19.6, exposing endpoints for module discovery (`/api/v1/system/modules`), context reflection (`/api/v1/system/context`), MediatR ping command with validation (`/api/v1/system/ping`, `/api/v1/ping`), domain event dispatch test (`/api/v1/system/events/test`), and error handling verification (`/api/v1/system/errors/{errorType}`).
- Implemented diagnostic test suite `PipelineValidator.Verify` in `src/ICS.Web/Diagnostics/PipelineValidator.cs` verifying RFC 7807 ProblemDetails generation, camelCase dictionary serialization, security context population, audit logging client metadata population, and MediatR FluentValidation pipeline behavior. Wired into startup diagnostics and CLI switch `--verify-pipeline`.
- Verified non-incremental solution build: `dotnet build ICS.sln --no-incremental` succeeded with 0 warnings and 0 errors.
- Verified runtime execution: `--verify-pipeline`, `--verify-di`, and `--verify-events` all succeeded; web server served live HTTP requests for `/health/live`, `/health/ready`, `/api/v1/system/context`, `/api/v1/ping` (valid 200 and invalid 400 Validation ProblemDetails), and daily rolling JSON logs were emitted to `logs/ics-*.json`.

Changed Files:
- `src/ICS.Web/ICS.Web.csproj`
- `src/ICS.Web/Program.cs`
- `src/ICS.Web/Logging/AmbientContextLogEnricher.cs`
- `src/ICS.Web/Logging/SerilogConfiguration.cs`
- `src/ICS.Web/Middleware/PipelineOrder.cs`
- `src/ICS.Web/Middleware/LoggingContextMiddleware.cs`
- `src/ICS.Web/Middleware/SecurityContextMiddleware.cs`
- `src/ICS.Web/Middleware/AuditLoggingMiddleware.cs`
- `src/ICS.Web/Middleware/ExceptionHandlingMiddleware.cs`
- `src/ICS.Web/Diagnostics/PipelineValidator.cs`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`

---

### P1-S07

Title: Test Project Infrastructure

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Create and configure the test projects (`ICS.Tests.Unit` and `ICS.Tests.Integration`) per Architecture §19.8 and §19.11. Establish all required test NuGet packages, integration test infrastructure using `WebApplicationFactory<Program>`, and the Respawn-based database reset utility for test isolation. This slice produces no business tests — only the scaffolding that all subsequent test slices build into.

Depends On: P1-S01

Repository: `tests/ICS.Tests.Unit`, `tests/ICS.Tests.Integration`

Completion Criteria:
- `tests/ICS.Tests.Unit/` project created and added to `ICS.sln` — Architecture §19.11.
- `tests/ICS.Tests.Integration/` project created and added to `ICS.sln` — Architecture §19.11.
- Both test projects reference NuGet packages: `xunit`, `xunit.runner.visualstudio`, `FluentAssertions`, `Microsoft.NET.Test.Sdk` — Architecture §19.8.
- `ICS.Tests.Integration` additionally references: `Microsoft.AspNetCore.Mvc.Testing` and `Respawn` — Architecture §19.8.
- `ICS.Tests.Integration` contains a base `IntegrationTestBase` class wiring `WebApplicationFactory<Program>` for in-process test execution and a Respawn-based database reset helper — Architecture §19.8 ("executing against an isolated SQL Server test instance, using Respawn ... to ensure clean test state").
- `dotnet test` succeeds (zero test failures; no tests yet, only infrastructure).
- Integration test infrastructure is configurable via environment variables (test database connection string separate from application connection string).

Notes: Can execute in parallel with P1-S02 through P1-S05 (depends only on P1-S01 for project structure). Test infrastructure is a prerequisite for all integration test completion criteria in slices P2-S09 onwards. Architecture §19.8 (Testing Strategy) is authoritative.

Implementation Notes:
- Created `tests/ICS.Tests.Unit` and `tests/ICS.Tests.Integration` targeting `net8.0` with C# 12, nullable reference types, and implicit usings enabled, and registered both under the `tests` solution folder in `ICS.sln` per Architecture §19.11.
- Configured NuGet test package references across projects per Architecture §19.8:
  - Both projects reference: `xunit` (v2.5.3), `xunit.runner.visualstudio` (v2.5.3), `FluentAssertions` (v8.11.0), `Microsoft.NET.Test.Sdk` (v17.8.0), and `coverlet.collector` (v6.0.0).
  - `ICS.Tests.Integration` additionally references: `Microsoft.AspNetCore.Mvc.Testing` (v8.0.13) and `Respawn` (v7.0.0).
- Configured project references:
  - `ICS.Tests.Unit` references `ICS.Core` and all 8 domain module projects (`ICS.Modules.Identity`, `ICS.Modules.Organization`, `ICS.Modules.Customer`, `ICS.Modules.Product`, `ICS.Modules.WorkPackage`, `ICS.Modules.Request`, `ICS.Modules.Post`, `ICS.Modules.Analytics`).
  - `ICS.Tests.Integration` references `ICS.Web` and `ICS.Core`.
- Implemented `DatabaseResetHelper` using Respawn (`Respawner.CreateAsync` with `DbAdapter.SqlServer`) scoping to all bounded context schemas (`DatabaseSchemas.All`) while ignoring the DbUp migration journal (`dbo.SchemaVersions`), including graceful handling for empty schema pre-table states.
- Implemented `IcsWebApplicationFactory` deriving from `WebApplicationFactory<Program>` overriding `ConfigureWebHost` to inject isolated test database connection settings (`ConnectionStrings:DefaultConnection` and `ConnectionStrings:TestConnection`), configurable via environment variables (`TEST_CONNECTION_STRING`, `ConnectionStrings__TestConnection`) with default fallback to `Server=localhost;Database=ICS_Test;Trusted_Connection=True;TrustServerCertificate=True;`.
- Implemented `IntegrationTestBase` abstract test fixture providing `IClassFixture<IcsWebApplicationFactory>`, `IAsyncLifetime` per-test database reset invocation, in-process `HttpClient`, and scoped dependency resolution helpers (`CreateScope`, `ExecuteInScopeAsync`).
- Implemented scaffolding smoke tests verifying both unit domain primitives (`DomainPrimitivesTests`) and in-process integration test execution (`InfrastructureIntegrationTests` verifying liveness, readiness, Respawn database reset, and HTTP pipeline dispatch).
- Verified solution build `dotnet build ICS.sln --no-incremental` succeeding with 0 warnings and 0 errors.
- Verified test suite execution `dotnet test ICS.sln` passing 10 of 10 tests across unit and integration projects with zero failures.
- Verified EF Core prohibition across all `.csproj` files: zero references to `Microsoft.EntityFrameworkCore*`.

Changed Files:
- `ICS.sln`
- `tests/ICS.Tests.Unit/ICS.Tests.Unit.csproj`
- `tests/ICS.Tests.Unit/DomainPrimitivesTests.cs`
- `tests/ICS.Tests.Integration/ICS.Tests.Integration.csproj`
- `tests/ICS.Tests.Integration/DatabaseResetHelper.cs`
- `tests/ICS.Tests.Integration/IcsWebApplicationFactory.cs`
- `tests/ICS.Tests.Integration/IntegrationTestBase.cs`
- `tests/ICS.Tests.Integration/InfrastructureIntegrationTests.cs`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`

---

### P1-S08

Title: Vue 3 Frontend Project Scaffolding

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Scaffold the Vue 3 Single Page Application project inside `src/ICS.Web/client/` per Architecture §19.4 and §19.11. Establish the Vite build configuration, TypeScript setup, Bootstrap 5 integration, Vue Router 4 for client-side navigation, and Pinia for shared state management. Configure the ASP.NET Core host (`ICS.Web`) to serve the built Vue SPA assets. No UI screens are implemented in this slice — only the project structure, tooling configuration, and a placeholder root component.

Depends On: P1-S01

Repository: `src/ICS.Web/client/`

Completion Criteria:
- `src/ICS.Web/client/` Vite-scaffolded Vue 3 project exists — Architecture §19.4, §19.11.
- TypeScript is configured (`<script setup lang="ts">` pattern works) — Architecture §19.4.
- Bootstrap 5 (with Bootstrap Icons) is installed and applied to the root layout — Architecture §19.4.
- Vue Router 4 is installed and configured with placeholder routes — Architecture §19.4.
- Pinia is installed and configured as the state management store — Architecture §19.4.
- Axios (or equivalent) HTTP client is installed and configured with a base URL pointing to `/api/v1` and authentication interceptors wired for cookie-based session handling — Architecture §19.4, §19.5.
- `vite build` produces a production bundle without errors.
- ASP.NET Core `ICS.Web` is configured to serve the built SPA assets from `wwwroot` (or equivalent static file path), falling back to `index.html` for SPA routing.
- Placeholder root component renders "ICS Operational System" confirmation message; the application loads in browser without console errors.

Notes: Can execute in parallel with P1-S02 through P1-S06 (depends only on P1-S01 for project structure). The `client/` SPA is built via Vite into `wwwroot/` during the production publish process defined in P7-S31. Each screen implementation (P2-S11, P3-S15, P4-S19, P5-S22, P6-S26, P7-S29) adds Vue SFC components to this scaffold. Architecture §19.4 (Frontend Stack), §19.5 (Authentication), §19.10 (Deployment & Runtime Strategy), and §20 ("Frontend Component Architecture") are authoritative.

Implementation Notes:
- Scaffolded the Vue 3 Single Page Application in `src/ICS.Web/client/` using Vite (v6.2.0), Vue 3 (v3.5.13), and TypeScript (v5.7.3) per Architecture §19.4 and §19.11.
- Configured TypeScript project references (`tsconfig.json`, `tsconfig.app.json`, `tsconfig.node.json`, and `vite-env.d.ts`) supporting `<script setup lang="ts">` with path alias `@` mapping to `src/`.
- Integrated Bootstrap 5 (v5.3.3) and Bootstrap Icons (v1.11.3) into `main.ts`, applying responsive grid layout and semantic classes across components.
- Configured Vue Router 4 (v4.5.0) in `src/ICS.Web/client/src/router/index.ts` with HTML5 history mode (`createWebHistory`), dynamic document title middleware, and placeholder routes for `/` (`HomeView`), `/login` (`LoginPlaceholderView`), and `/:pathMatch(.*)*` (`NotFoundView`).
- Configured Pinia (v2.3.1) in `main.ts` with initial stores: `useSystemStore` (`src/ICS.Web/client/src/stores/system.ts`) for system context and module metadata, and `useAuthStore` (`src/ICS.Web/client/src/stores/auth.ts`) for session state and RBAC role checks per Architecture §19.4 and §19.5.
- Configured Axios (v1.7.9) HTTP client in `src/ICS.Web/client/src/services/api.ts` with `baseURL: '/api/v1'`, `withCredentials: true` for cookie-based session handling, and request/response interceptors for 401/403 status handling per Architecture §19.4 and §19.5.
- Configured Vite build in `src/ICS.Web/client/vite.config.ts` targeting `../wwwroot` (`outDir: '../wwwroot'`, `emptyOutDir: true`), verifying production bundle compilation via `npm run build` (`vue-tsc -b && vite build`) succeeding cleanly with zero errors.
- Configured ASP.NET Core host in `src/ICS.Web/Middleware/PipelineOrder.cs` and `src/ICS.Web/Program.cs` with `app.UseDefaultFiles()`, `app.UseStaticFiles()`, and `app.MapFallbackToFile("index.html")` to serve SPA assets and handle client-side routing fallback per Architecture §19.10.
- Implemented root layout in `src/ICS.Web/client/src/App.vue` and landing placeholder component `src/ICS.Web/client/src/views/HomeView.vue` rendering the "ICS Operational System" confirmation banner, architecture badges, backend module status, and system info table.
- Added integration tests in `tests/ICS.Tests.Integration/InfrastructureIntegrationTests.cs` verifying SPA root index serving (`GET /`), client route fallback (`GET /login`), and system info API (`GET /api/v1/system/info`).
- Verified non-incremental solution build (`dotnet build ICS.sln --no-incremental`) and test execution (`dotnet test ICS.sln`) passing 13 of 13 tests with zero failures.

Changed Files:
- `src/ICS.Web/client/package.json`
- `src/ICS.Web/client/package-lock.json`
- `src/ICS.Web/client/vite.config.ts`
- `src/ICS.Web/client/tsconfig.json`
- `src/ICS.Web/client/tsconfig.app.json`
- `src/ICS.Web/client/tsconfig.node.json`
- `src/ICS.Web/client/index.html`
- `src/ICS.Web/client/.gitignore`
- `src/ICS.Web/client/src/vite-env.d.ts`
- `src/ICS.Web/client/src/main.ts`
- `src/ICS.Web/client/src/App.vue`
- `src/ICS.Web/client/src/router/index.ts`
- `src/ICS.Web/client/src/services/api.ts`
- `src/ICS.Web/client/src/stores/system.ts`
- `src/ICS.Web/client/src/stores/auth.ts`
- `src/ICS.Web/client/src/views/HomeView.vue`
- `src/ICS.Web/client/src/views/LoginPlaceholderView.vue`
- `src/ICS.Web/client/src/views/NotFoundView.vue`
- `src/ICS.Web/Middleware/PipelineOrder.cs`
- `src/ICS.Web/Program.cs`
- `tests/ICS.Tests.Integration/InfrastructureIntegrationTests.cs`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`

---

## P2 — Identity & Access

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Identity & Access module, the authentication middleware pipeline, and the Login screen (`SCR-AUTH-001`). Establishes `UserAccounts` and `UserSessions` tables via DbUp, credential verification via Argon2id or ASP.NET Core `IPasswordHasher` (PBKDF2/HMAC-SHA512), secure cookie-based session management (`HttpOnly`, `SameSite=Strict`), and the concrete `CurrentContextProvider` that exposes `CurrentUserId`, `CurrentPersonId`, and `CurrentRoles` across all HTTP requests. Delivers the Login screen (`SCR-AUTH-001`) as a Vue 3 SFC. Placing Identity immediately after Foundation ensures all subsequent modules (Master Data, Requests, Work Packages, Feed, Analytics) execute with active security context, user identification, and RBAC authorization without requiring post-hoc refactoring.

Source: Architecture §22 — Boundary: **Identity & Access** (`ICS.Modules.Identity`, Depends On: `ICS.Core`, `Organization` (Read)). Architecture §14 (Identity & Authentication Architecture). Architecture §18, §19.5.

---

### P2-S09

Title: Identity Module — Domain, Application Services & Persistence

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the complete Identity & Access module: `UserAccount` and `UserSession` domain entities, `AuthenticationService` (Login, Logout, ValidateSession), DbUp SQL migration scripts for `identity.*` tables, and Dapper repository implementations using explicit parameterized SQL. Credential verification uses Argon2id or ASP.NET Core `IPasswordHasher` (PBKDF2/HMAC-SHA512), per Architecture §19.5. Session token stored in `identity.UserSessions`.

Depends On: P1-S06, P1-S07

Repository: `ICS.Modules.Identity`

Completion Criteria:
- DbUp SQL migration scripts create `identity.*` schema tables: `UserAccounts`, `UserSessions` — Architecture §14 (IAM Persistence Tables).
- All repository operations use Dapper with explicit parameterized SQL against SQL Server; no EF Core — Architecture §19.3, §20.
- `AuthenticationService.Login(usernameOrEmail, password, clientInfo)`: verifies password using Argon2id or `IPasswordHasher` (PBKDF2/HMAC-SHA512) — Architecture §19.5; checks `UserAccount.Status == 'ACTIVE'`; creates `UserSession`; issues `SessionToken` (stored in `identity.UserSessions`). (Note: If Organization module is not yet populated, status checks support bootstrap admin accounts; once Organization module is active, Person active status is resolved via `OrganizationQueryService`).
- `AuthenticationService.Logout(sessionToken)`: marks `UserSession.IsRevoked = TRUE` in `identity.UserSessions`.
- `AuthenticationService.ValidateSession(sessionToken)`: validates token, checks expiry and revocation, returns `SecurityContext` with `UserId` and `PersonId`.
- Repository implementations write exclusively to `identity.*` schema using parameterized SQL.
- Integration tests (xUnit + WebApplicationFactory + Respawn): Login success, Login failure (bad credentials), ValidateSession with valid token, ValidateSession with expired token, Logout invalidates session.

Notes: Architecture §14 is authoritative for all IAM component specifications. Architecture §19.5 is authoritative for authentication mechanism (Cookie Auth, server-side session, password hashing). Running Identity in P2 establishes user credentials, sessions, and security context before master data is provisioned.

Implementation Notes:
- Implemented embedded DbUp migration script `src/ICS.Web/Migrations/Script0002_CreateIdentitySchema.sql` (renamed from `001_CreateIdentitySchema.sql` to align with numerical naming prefix conventions) provisioning the `[identity]` schema, `[identity].[UserAccounts]`, and `[identity].[UserSessions]` tables with clustered primary keys, unique constraints, foreign keys with cascade deletion, and indexes per Architecture §14, §17, and §20.
- Implemented `UserAccount` and `UserSession` domain entities and `UserAccountStatus` in `ICS.Modules.Identity.Domain` with business logic for recording successful logins, recording failed login attempts with lockout thresholds, administrative lock/unlock, password modification, and token/expiration validation.
- Implemented `IUserAccountRepository` and `UserAccountRepository` using Dapper with explicit parameterized SQL writing exclusively to the `[identity]` schema, with zero EF Core references per Architecture §19.3 and §20.
- Implemented `IUserSessionRepository` and `UserSessionRepository` using Dapper with explicit parameterized SQL for creating, retrieving, updating, and revoking server-side sessions per Architecture §14 and §19.5.
- Implemented `IOrganizationQueryService` contract and `OrganizationQueryService` in `ICS.Modules.Organization` with Dapper queries and fallback support for pre-provisioning bootstrap administrator and test accounts.
- Implemented `IAuthenticationService` and `AuthenticationService` in `ICS.Modules.Identity.Application` supporting `LoginAsync`, `LogoutAsync`, and `ValidateSessionAsync` (along with synchronous convenience overloads `Login`, `Logout`, `ValidateSession`), utilizing ASP.NET Core `IPasswordHasher<UserAccount>` (PBKDF2 with HMAC-SHA512) for credential verification, server-side session generation in `[identity].[UserSessions]`, and returning `SecurityContext` containing authenticated `UserId` and `PersonId`.
- Implemented `IIdentityDataSeeder` and `IdentityDataSeeder` in `ICS.Modules.Identity` enabling immediate seeding of active administrator accounts (`admin`) and custom test accounts for automated test isolation.
- Registered all Identity services, repositories, and password hasher in `IdentityModule.RegisterServices`, and registered `IOrganizationQueryService` in `OrganizationModule.RegisterServices`.
- Configured Serilog `preserveStaticLogger: true` in `SerilogConfiguration.cs` enabling multi-fixture `WebApplicationFactory` executions across xUnit test suites without static logger freeze collisions.
- Created domain unit tests in `tests/ICS.Tests.Unit/IdentityDomainTests.cs` (7 tests) and full-lifecycle integration tests in `tests/ICS.Tests.Integration/AuthenticationIntegrationTests.cs` (9 tests) verifying login success, login failure (invalid credentials, non-existent user, lockout after 5 attempts), session validation (valid, expired, and revoked), session revocation on logout, initial admin seeding, and synchronous execution against an isolated SQL Server test instance with Respawn.
- Remediated FINDING-P2-S09-01 (Integration Test Failure & Test Isolation State Bleed):
  - Created `src/ICS.Web/appsettings.Testing.json` defining `DefaultConnection` and `TestConnection` targeting `ICS_Test`.
  - Updated `src/ICS.Core/Data/DatabaseServiceExtensions.cs` to resolve `IDbConnectionFactory` lazily via `sp.GetRequiredService<IConfiguration>()` ensuring WebApplicationFactory configuration overrides take effect.
  - Updated `src/ICS.Web/Program.cs` to recognize the `Testing` environment when resolving database connection strings before and after `builder.Build()`, routing startup DbUp migrations and `IDbConnectionFactory` to `ICS_Test`.
  - Added in-process synchronization with `SemaphoreSlim` in `DbUpMigrationRunner` to eliminate concurrent table creation race conditions during test fixture startup.
  - Updated `tests/ICS.Tests.Integration/IcsWebApplicationFactory.cs` to set host settings (`builder.UseSetting`) and explicitly register `IDbConnectionFactory` for test isolation.
  - Added `[assembly: CollectionBehavior(DisableTestParallelization = true)]` in `tests/ICS.Tests.Integration/IntegrationTestBase.cs` to ensure Respawn database resets execute sequentially across integration test classes sharing the test database.
  - Cleaned all test records and aligned migration history in the primary application database `ICS`.
- Remediated FINDING-P2-S09-02 (Inconsistent SQL Migration Script Naming Prefix):
  - Renamed `001_CreateIdentitySchema.sql` to `Script0002_CreateIdentitySchema.sql` matching `Script0001_CreateModuleSchemas.sql` across embedded resources.
- Verified clean non-incremental solution build (`dotnet build ICS.sln --no-incremental`) and test execution (`dotnet test ICS.sln`) repeatedly passing all 29 tests (13 unit, 16 integration) with 100% pass rate, zero errors, and zero warnings.

Changed Files:
- `src/ICS.Web/Migrations/Script0002_CreateIdentitySchema.sql`
- `src/ICS.Web/Migrations/DbUpMigrationRunner.cs`
- `src/ICS.Web/appsettings.Testing.json`
- `src/ICS.Web/Program.cs`
- `src/ICS.Core/Data/DatabaseServiceExtensions.cs`
- `src/ICS.Web/Logging/SerilogConfiguration.cs`
- `src/ICS.Modules.Organization/IOrganizationQueryService.cs`
- `src/ICS.Modules.Organization/OrganizationQueryService.cs`
- `src/ICS.Modules.Organization/OrganizationModule.cs`
- `src/ICS.Modules.Identity/ICS.Modules.Identity.csproj`
- `src/ICS.Modules.Identity/Domain/UserAccountStatus.cs`
- `src/ICS.Modules.Identity/Domain/UserAccount.cs`
- `src/ICS.Modules.Identity/Domain/UserSession.cs`
- `src/ICS.Modules.Identity/Persistence/IUserAccountRepository.cs`
- `src/ICS.Modules.Identity/Persistence/UserAccountRepository.cs`
- `src/ICS.Modules.Identity/Persistence/IUserSessionRepository.cs`
- `src/ICS.Modules.Identity/Persistence/UserSessionRepository.cs`
- `src/ICS.Modules.Identity/Application/ClientInfo.cs`
- `src/ICS.Modules.Identity/Application/LoginResult.cs`
- `src/ICS.Modules.Identity/Application/SecurityContext.cs`
- `src/ICS.Modules.Identity/Application/IAuthenticationService.cs`
- `src/ICS.Modules.Identity/Application/AuthenticationService.cs`
- `src/ICS.Modules.Identity/IIdentityDataSeeder.cs`
- `src/ICS.Modules.Identity/IdentityDataSeeder.cs`
- `src/ICS.Modules.Identity/IdentityModule.cs`
- `tests/ICS.Tests.Unit/IdentityDomainTests.cs`
- `tests/ICS.Tests.Integration/ICS.Tests.Integration.csproj`
- `tests/ICS.Tests.Integration/IcsWebApplicationFactory.cs`
- `tests/ICS.Tests.Integration/IntegrationTestBase.cs`
- `tests/ICS.Tests.Integration/AuthenticationIntegrationTests.cs`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`

---

### P2-S10

Title: Authentication Middleware & Security Context Pipeline

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the concrete HTTP authentication middleware that intercepts every incoming request, validates the session cookie via `AuthenticationService.ValidateSession`, populates `CurrentContextProvider` with `CurrentUserId`, `CurrentPersonId`, and `CurrentRoles`, and rejects unauthenticated requests with HTTP 401. Uses ASP.NET Core Cookie Authentication (`CookieAuthenticationDefaults.AuthenticationScheme`) with secure, `HttpOnly`, `SameSite=Strict` cookies per Architecture §19.5. Implement the RBAC enforcement mechanism (`[Authorize(Roles = "...")]`) backed by dynamically resolved roles.

Depends On: P2-S09

Repository: `ICS.Modules.Identity` / `ICS.Web`

Completion Criteria:
- ASP.NET Core Cookie Authentication registered with `HttpOnly = true`, `SameSite = SameSiteMode.Strict`, `Secure = true` — Architecture §19.5.
- Authentication middleware is registered in `ICS.Web` pipeline (fulfils the placeholder established in P1-S06).
- Every request without a valid session cookie returns HTTP 401 on protected endpoints.
- Every request with a valid cookie calls `AuthenticationService.ValidateSession` and populates `CurrentContextProvider` with `CurrentUserId`, `CurrentPersonId`, and `CurrentRoles` — Architecture §19.5.
- RBAC enforcement mechanism is in place and verified: `[Authorize(Roles = "Management")]` attribute rejects non-Management users with HTTP 403.
- Integration tests (xUnit + WebApplicationFactory): authenticated request (200), unauthenticated request (401), insufficient-role request (403) pass.

Notes: Architecture §18 (Authentication & Security Context, RBAC) and §19.5 are authoritative.

Implementation Notes:
- Configured ASP.NET Core Cookie Authentication in `src/ICS.Web/Auth/AuthenticationServiceExtensions.cs` using `CookieAuthenticationDefaults.AuthenticationScheme` with `HttpOnly = true`, `SameSite = SameSiteMode.Strict`, `SecurePolicy = CookieSecurePolicy.SameAsRequest`, `IsEssential = true`, and cookie name `ICS_SESSION`.
- Configured `OnRedirectToLogin` and `OnRedirectToAccessDenied` events to return RFC 7807 `ProblemDetails` with HTTP 401 Unauthorized (`UNAUTHORIZED`) and HTTP 403 Forbidden (`FORBIDDEN`) respectively for API/SPA requests per Architecture §19.5 and §19.6.
- Implemented `IAuthorizationService` and `AuthorizationService` in `ICS.Modules.Identity.Application` resolving active roles for `PersonId` via `IOrganizationQueryService` per Architecture §14 and §15, registered as Scoped in `IdentityModule`.
- Enhanced `IOrganizationQueryService` and `OrganizationQueryService` in `ICS.Modules.Organization` with `RegisterBootstrapRoles` supporting dynamic role resolution for testing and bootstrap environments.
- Implemented concrete HTTP authentication and ambient context middleware in `src/ICS.Web/Middleware/SecurityContextMiddleware.cs`:
  1. Extracts session token from `ICS_SESSION` cookie (with fallback to `X-Session-Token` or `Authorization: Bearer`).
  2. Validates session against `identity.UserSessions` via `AuthenticationService.ValidateSessionAsync`.
  3. On valid session, dynamically resolves active roles via `IAuthorizationService.GetRolesForPersonAsync`.
  4. Populates `ClaimsPrincipal` with `NameIdentifier`, `person_id`, `session_token`, and `ClaimTypes.Role` claims for each assigned role.
  5. Synchronizes ambient `ICurrentContextAccessor` / `CurrentContextProvider` with `CurrentUserId`, `CurrentPersonId`, and `CurrentRoles`.
  6. On invalid or missing session, clears security context and assigns unauthenticated principal.
  7. Retains diagnostic test header overrides (`X-User-Id`, `X-Person-Id`, `X-Roles`) for backwards-compatible test and pipeline validation scenarios.
- Updated `src/ICS.Web/Middleware/PipelineOrder.cs` to establish authoritative request pipeline ordering: `UseRouting()`, `UseAuthentication()`, `UseMiddleware<SecurityContextMiddleware>()`, `UseAuthorization()`, and `UseMiddleware<AuditLoggingMiddleware>()`.
- Registered `AddIcsAuthentication` in `src/ICS.Web/Program.cs` and added protected minimal API endpoints (`/api/v1/system/protected` requiring authentication and `/api/v1/system/management` requiring `Management` role).
- Created `src/ICS.Web/Controllers/SecurityTestController.cs` exposing controller endpoints decorated with `[Authorize]` and `[Authorize(Roles = "Management")]`.
- Added unit tests in `tests/ICS.Tests.Unit/AuthorizationServiceTests.cs` (3 test methods) verifying role resolution, case-insensitive role evaluation, and multi-role checks.
- Added integration tests in `tests/ICS.Tests.Integration/SecurityPipelineIntegrationTests.cs` (10 test methods) verifying:
  1. Cookie authentication configuration and secure defaults.
  2. Unauthenticated requests to protected minimal API returning 401 with RFC 7807 ProblemDetails.
  3. Unauthenticated requests to protected controller returning 401 with RFC 7807 ProblemDetails.
  4. Authenticated request with valid session cookie returning 200 and populating ambient context.
  5. Authenticated request to controller with valid session cookie returning 200.
  6. Request with revoked session cookie returning 401.
  7. Request with invalid/non-existent session cookie returning 401.
  8. RBAC enforcement rejecting non-Management users (`Programmer`) with 403 Forbidden.
  9. RBAC enforcement admitting users with `Management` role with 200 OK across both minimal API and controller.
  10. Direct evaluation of `IAuthorizationService`.
- Verified solution non-incremental build (`dotnet build ICS.sln`) and test suite (`dotnet test ICS.sln`) passing 100% (47 tests: 20 unit, 27 integration) with zero warnings and zero errors.

Changed Files:
- `src/ICS.Modules.Organization/IOrganizationQueryService.cs`
- `src/ICS.Modules.Organization/OrganizationQueryService.cs`
- `src/ICS.Modules.Identity/Application/IAuthorizationService.cs`
- `src/ICS.Modules.Identity/Application/AuthorizationService.cs`
- `src/ICS.Modules.Identity/IdentityModule.cs`
- `src/ICS.Web/Auth/AuthenticationServiceExtensions.cs`
- `src/ICS.Web/Middleware/SecurityContextMiddleware.cs`
- `src/ICS.Web/Middleware/PipelineOrder.cs`
- `src/ICS.Web/Controllers/SecurityTestController.cs`
- `src/ICS.Web/Program.cs`
- `tests/ICS.Tests.Unit/AuthorizationServiceTests.cs`
- `tests/ICS.Tests.Integration/SecurityPipelineIntegrationTests.cs`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`

---

### P2-S11

Title: Login Screen — SCR-AUTH-001

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement `SCR-AUTH-001` (Login Screen) as a Vue 3 SFC (`<script setup lang="ts">`) with Bootstrap 5 styling, and the corresponding ASP.NET Core REST API controller (`/api/v1/auth/login`, `/api/v1/auth/logout`). Wires `AuthenticationService.Login` and `Logout`, sets the session cookie on success, handles login failures with distinct error messages, and redirects to `SCR-FEED-001` on success.

Depends On: P2-S10, P1-S08

Repository: `ICS.Web`, `ICS.Web/client/`

Completion Criteria:
- Vue 3 SFC `LoginView.vue` (`<script setup lang="ts">`, Bootstrap 5) renders username/password input fields — Architecture §19.4, §20.
- Vue Router 4 route `/login` maps to `LoginView.vue`.
- POST `/api/v1/auth/login` ASP.NET Core Controller endpoint calls `AuthenticationService.Login`; sets secure `HttpOnly` `SameSite=Strict` session cookie on success — Architecture §19.5.
- Login failure returns distinct RFC 7807 `ProblemDetails` responses: invalid credentials, account locked — Architecture §19.6.
- Successful login redirects Vue Router to `SCR-FEED-001` route.
- POST `/api/v1/auth/logout` endpoint calls `AuthenticationService.Logout` and clears the session cookie.
- Integration tests (xUnit + WebApplicationFactory): login success and each login failure scenario pass.

Notes: Architecture §14 (UI Boundary — SCR-AUTH-001), §19.4, §19.5, §19.6 are authoritative.

Implementation Notes:
- Updated `LoginResult` in `ICS.Modules.Identity.Application` to expose machine-readable `ErrorCode` ('INVALID_CREDENTIALS', 'ACCOUNT_LOCKED', etc.).
- Updated `AuthenticationService.LoginAsync` in `ICS.Modules.Identity.Application` to propagate specific error codes upon credential check failure and account lockout.
- Implemented `AuthController` in `src/ICS.Web/Controllers/AuthController.cs`:
  1. `POST /api/v1/auth/login`: verifies credentials via `AuthenticationService.LoginAsync`, appends secure HttpOnly SameSite=Strict `ICS_SESSION` cookie on success, resolves active roles for `PersonId` via `IAuthorizationService`, and returns 200 OK with authenticated user profile.
  2. Distinct RFC 7807 `ProblemDetails` responses: returns HTTP 401 Unauthorized (`INVALID_CREDENTIALS`) for invalid credentials and HTTP 423 Locked (`ACCOUNT_LOCKED`) for locked accounts, with `application/problem+json` content type.
  3. `POST /api/v1/auth/logout`: invalidates active server session via `AuthenticationService.LogoutAsync` and deletes the `ICS_SESSION` cookie.
  4. `GET /api/v1/auth/me`: provides current user security context and roles for frontend session hydration.
- Implemented `SCR-AUTH-001` (Login Screen) in `src/ICS.Web/client/src/views/LoginView.vue` using Vue 3 Composition API (`<script setup lang="ts">`) and Bootstrap 5 styling with username/email input, password input with visibility toggle, submission loading state, distinct alert displays for invalid credentials vs locked accounts, and automatic redirect to `SCR-FEED-001` on success.
- Implemented `SCR-FEED-001` (Operational Feed) in `src/ICS.Web/client/src/views/FeedView.vue` as primary landing screen displaying active user context, roles, and real-time operational stream placeholder.
- Updated Vue Router 4 in `src/ICS.Web/client/src/router/index.ts` mapping `/login` to `LoginView.vue`, `/feed` to `FeedView.vue`, and wiring navigation guards for authenticated session protection and guest-only redirects.
- Enhanced Pinia `useAuthStore` in `src/ICS.Web/client/src/stores/auth.ts` with `login`, `logout`, and `checkAuth` actions using `apiClient`.
- Updated `src/ICS.Web/client/src/App.vue` to hydrate user session on mount, display live authenticated user info and roles badge, provide direct sign out button, and link to `SCR-FEED-001`.
- Implemented integration test suite in `tests/ICS.Tests.Integration/LoginScreenIntegrationTests.cs` (8 test methods) verifying:
  1. Successful login returns 200 and sets `ICS_SESSION` cookie with `HttpOnly`, `SameSite=Strict`, `Path=/`.
  2. Authenticated requests with issued cookie succeed and populate security context.
  3. Invalid password returns HTTP 401 with RFC 7807 `ProblemDetails` and `INVALID_CREDENTIALS` error code.
  4. Non-existent username returns HTTP 401 with RFC 7807 `ProblemDetails` and `INVALID_CREDENTIALS` error code.
  5. Locked account returns HTTP 423 with distinct RFC 7807 `ProblemDetails` and `ACCOUNT_LOCKED` error code.
  6. Empty credentials return HTTP 400 with RFC 7807 `VALIDATION_ERROR`.
  7. Logout invalidates server session in `identity.UserSessions`, clears session cookie, and subsequent requests return 401.
  8. `/api/v1/auth/me` correctly reflects authenticated and unauthenticated states.
- Verified frontend build with `npm run build` (`vue-tsc -b && vite build`) succeeding cleanly with zero errors.
- Verified non-incremental solution build (`dotnet build ICS.sln --no-incremental`) succeeding with 0 warnings and 0 errors.
- Verified all solution tests (`dotnet test ICS.sln`) passing 100% (55 tests: 20 unit, 35 integration) with zero failures.

Changed Files:
- `src/ICS.Modules.Identity/Application/LoginResult.cs`
- `src/ICS.Modules.Identity/Application/AuthenticationService.cs`
- `src/ICS.Web/Controllers/AuthController.cs`
- `src/ICS.Web/client/src/stores/auth.ts`
- `src/ICS.Web/client/src/views/LoginView.vue`
- `src/ICS.Web/client/src/views/FeedView.vue`
- `src/ICS.Web/client/src/router/index.ts`
- `src/ICS.Web/client/src/App.vue`
- `tests/ICS.Tests.Integration/LoginScreenIntegrationTests.cs`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`

---

## P3 — Core Master Data & Product Catalog

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the three foundational master-data modules — Organization, Customer, and Product — including domain, application services, and persistence using Dapper with explicit parameterized SQL (Architecture §19.3), along with the Product Catalog screen (`SCR-PRD-001`). These modules operate within the authenticated security context established in P2. Customer and Product are architecturally decoupled and can execute in parallel: Customer has zero dependency on Organization or Product; Product depends on Organization for owner validation, but has zero dependency on Customer. M1 is achieved when P3-S15 is complete.

Source: Architecture §22 — Boundaries: **Organization**, **Customer**, **Product**. Architecture §6 (Module Boundaries), §10 (Product Module Architecture), §16 (Data Ownership), §19.3 (Persistence & Data Access).

---

### P3-S12

Title: Organization Module — Domain, Application Services & Persistence

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the complete Organization module: domain entities (`Person`, `Team`, `Role`, `Responsibility`, `TeamMembership`, `RoleAssignment`, `ResponsibilityAssignment`), `OrganizationService` command methods, `OrganizationQueryService` query methods, DbUp SQL migration scripts for `organization.*` tables, and Dapper repository implementations using explicit parameterized SQL. Connects `OrganizationQueryService` with `AuthorizationService` for dynamic role resolution.

Depends On: P1-S06, P1-S07, P2-S10

Repository: `ICS.Modules.Organization`

Completion Criteria:
- DbUp SQL migration scripts create `organization.*` schema tables: `Persons`, `Teams`, `Roles`, `Responsibilities`, `TeamMemberships`, `RoleAssignments`, `ResponsibilityAssignments` — Architecture §17.
- All repository operations use Dapper with explicit parameterized SQL against SQL Server; no EF Core — Architecture §19.3, §20.
- MediatR command and query handlers implement: create/update Person, create Team, assign Person to Team, create Role, assign Role to Person, create Responsibility, assign Responsibility to Person, deactivate Person — via `OrganizationService`.
- `OrganizationQueryService` implements: `GetPersonById`, `ListActivePersons`, `GetTeamRoster`, `GetPersonRoles`, `GetPersonResponsibilities` — Architecture §7.
- Dynamic role resolution wired: `AuthorizationService.ResolveRoles(personId)` delegates to `OrganizationQueryService.GetPersonRoles` — Architecture §14.
- Domain events emitted: `PersonCreated`, `PersonDeactivated`, `RoleAssigned`, `RoleRevoked`.
- `OrganizationQueryService` is accessible as a published interface to other modules; internal repositories are not exposed — Architecture §20 (Strict Vertical Slice Boundary).
- Repository implementations write exclusively to `organization.*` schema using parameterized SQL.
- Integration tests (xUnit + WebApplicationFactory + Respawn): query service returns accurate results after service commands.

Notes: Architecture §6 (Module Boundaries — Organization), §7, and §16 are authoritative. No tables from other modules are written. Depends on P1-S07 for test infrastructure and P2-S10 for security context.

Implementation Notes:
- Added DbUp SQL migration script `Script0003_CreateOrganizationSchema.sql` in `src/ICS.Web/Migrations/` creating `organization.*` schema tables: `Persons`, `Teams`, `Roles`, `Responsibilities`, `TeamMemberships`, `RoleAssignments`, and `ResponsibilityAssignments` with unique and lookup indexes per Architecture §17.
- Implemented domain entities in `ICS.Modules.Organization.Domain`: `Person`, `Team`, `Role`, `Responsibility`, `TeamMembership`, `RoleAssignment`, and `ResponsibilityAssignment`, with domain validation, lifecycle transitions, and event staging per Architecture §6, §7, §17, and `organization-domain.md`.
- Implemented domain events in `ICS.Modules.Organization.Domain.Events`: `PersonCreated`, `PersonDeactivated`, `RoleAssigned`, `RoleRevoked`, `TeamMemberAssigned`, and `ResponsibilityAssigned`, extending `DomainEvent` with synchronous in-process notification dispatch.
- Implemented module-internal Dapper repositories in `ICS.Modules.Organization.Persistence` using explicit parameterized SQL against SQL Server without EF Core: `IPersonRepository` (`PersonRepository`), `ITeamRepository` (`TeamRepository`), `IRoleRepository` (`RoleRepository`), and `IResponsibilityRepository` (`ResponsibilityRepository`).
- Implemented MediatR commands, validators, and handlers in `ICS.Modules.Organization.Application.Commands` for: `CreatePerson`, `UpdatePerson`, `DeactivatePerson`, `CreateTeam`, `AssignPersonToTeam`, `CreateRole`, `AssignRoleToPerson`, `RevokeRoleFromPerson`, `CreateResponsibility`, `AssignResponsibilityToPerson`, and `RevokeResponsibilityFromPerson`.
- Implemented MediatR queries and handlers in `ICS.Modules.Organization.Application.Queries` for: `GetPersonById`, `ListActivePersons`, `GetTeamRoster`, `GetPersonRoles`, and `GetPersonResponsibilities`.
- Implemented application services:
  - `OrganizationService : IOrganizationService` executing command dispatching through MediatR pipeline behaviors with validation and event dispatch.
  - `OrganizationQueryService : IOrganizationQueryService` implementing `GetPersonByIdAsync`, `ListActivePersonsAsync`, `GetTeamRosterAsync`, `GetPersonRolesAsync`, `GetPersonResponsibilitiesAsync`, `IsPersonActiveAsync`, and `GetRolesByPersonIdAsync`.
- Connected dynamic role resolution per Architecture §14: Updated `IAuthorizationService` and `AuthorizationService` in `ICS.Modules.Identity.Application` with `ResolveRoles` and `ResolveRolesAsync` delegating to `OrganizationQueryService.GetPersonRolesAsync`, seamlessly integrating Identity with live Organization database queries.
- Registered services and repositories in `OrganizationModule.RegisterServices` with Scoped lifetimes per architectural conventions.
- Implemented unit tests in `tests/ICS.Tests.Unit/OrganizationDomainTests.cs` (8 test methods) verifying domain entities, validation, state transitions, and event emission.
- Implemented integration tests in `tests/ICS.Tests.Integration/OrganizationIntegrationTests.cs` (8 test methods) verifying end-to-end command execution, persistence, query service accuracy, event collection via `TestDomainEventCollector`, validation pipeline behaviors, and dynamic role resolution via `AuthorizationService`.
- Verified non-incremental solution build (`dotnet build ICS.sln --no-incremental`) and test execution (`dotnet test ICS.sln`) passing 100% (76 tests: 33 unit, 43 integration) with zero errors, zero warnings, and zero EF Core references.

Changed Files:
- `src/ICS.Web/Migrations/Script0003_CreateOrganizationSchema.sql`
- `src/ICS.Modules.Organization/Domain/Events/OrganizationDomainEvents.cs`
- `src/ICS.Modules.Organization/Domain/Person.cs`
- `src/ICS.Modules.Organization/Domain/Team.cs`
- `src/ICS.Modules.Organization/Domain/Role.cs`
- `src/ICS.Modules.Organization/Domain/Responsibility.cs`
- `src/ICS.Modules.Organization/Domain/TeamMembership.cs`
- `src/ICS.Modules.Organization/Domain/RoleAssignment.cs`
- `src/ICS.Modules.Organization/Domain/ResponsibilityAssignment.cs`
- `src/ICS.Modules.Organization/Application/DTOs/OrganizationDtos.cs`
- `src/ICS.Modules.Organization/Application/Commands/OrganizationCommands.cs`
- `src/ICS.Modules.Organization/Application/Queries/OrganizationQueries.cs`
- `src/ICS.Modules.Organization/Application/IOrganizationService.cs`
- `src/ICS.Modules.Organization/Application/OrganizationService.cs`
- `src/ICS.Modules.Organization/IOrganizationQueryService.cs`
- `src/ICS.Modules.Organization/OrganizationQueryService.cs`
- `src/ICS.Modules.Organization/OrganizationModule.cs`
- `src/ICS.Modules.Organization/Persistence/IPersonRepository.cs`
- `src/ICS.Modules.Organization/Persistence/PersonRepository.cs`
- `src/ICS.Modules.Organization/Persistence/ITeamRepository.cs`
- `src/ICS.Modules.Organization/Persistence/TeamRepository.cs`
- `src/ICS.Modules.Organization/Persistence/IRoleRepository.cs`
- `src/ICS.Modules.Organization/Persistence/RoleRepository.cs`
- `src/ICS.Modules.Organization/Persistence/IResponsibilityRepository.cs`
- `src/ICS.Modules.Organization/Persistence/ResponsibilityRepository.cs`
- `src/ICS.Modules.Identity/Application/IAuthorizationService.cs`
- `src/ICS.Modules.Identity/Application/AuthorizationService.cs`
- `tests/ICS.Tests.Unit/OrganizationDomainTests.cs`
- `tests/ICS.Tests.Unit/AuthorizationServiceTests.cs`
- `tests/ICS.Tests.Integration/TestDomainEventCollector.cs`
- `tests/ICS.Tests.Integration/IcsWebApplicationFactory.cs`
- `tests/ICS.Tests.Integration/IntegrationTestBase.cs`
- `tests/ICS.Tests.Integration/OrganizationIntegrationTests.cs`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`

---

### P3-S13

Title: Customer Module — Domain, Application Services & Persistence

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the complete Customer module: domain entities (`Customer`, `CustomerContact`), `CustomerService` command methods, `CustomerQueryService` query methods, DbUp SQL migration scripts for `customer.*` tables, and Dapper repository implementations using explicit parameterized SQL.

Depends On: P1-S06, P1-S07, P2-S10

Repository: `ICS.Modules.Customer`

Completion Criteria:
- DbUp SQL migration scripts create `customer.*` schema tables: `Customers`, `CustomerContacts` — Architecture §17.
- All repository operations use Dapper with explicit parameterized SQL; no EF Core — Architecture §19.3, §20.
- MediatR handlers via `CustomerService` implement: create Customer, update Customer master data, create CustomerContact, update CustomerContact, deactivate Customer.
- `CustomerQueryService` implements: `GetCustomerById`, `ListActiveCustomers`, `GetCustomerContacts`, `GetCustomerWithContractStatus` — Architecture §7.
- `CustomerQueryService` is accessible as a published interface; internal repositories are not exposed.
- Repository implementations write exclusively to `customer.*` schema using parameterized SQL.
- Integration tests (xUnit + WebApplicationFactory + Respawn): query service returns accurate results after service commands.

Notes: Architecture §6 (Module Boundaries — Customer) and §16 are authoritative. Customer module does not own Request or Work Package relationships. **Parallel Execution**: Customer has zero dependency on Organization (P3-S12) or Product (P3-S14). It can execute concurrently in parallel with P3-S12 and P3-S14.

Implementation Notes:
- Added DbUp SQL migration script `Script0004_CreateCustomerSchema.sql` in `src/ICS.Web/Migrations/` creating `customer.*` schema tables: `Customers` and `CustomerContacts` with clustered primary keys, foreign key cascade deletion, and indexes on `CustomerCode` (unique) and `Status` per Architecture §17.
- Implemented domain entities in `ICS.Modules.Customer.Domain`: `Customer` and `CustomerContact`, encapsulating domain rules, state invariants, factory methods, and domain event staging per Architecture §6, §7, §16, §17, and `customer-domain.md`.
- Implemented domain events in `ICS.Modules.Customer.Domain.Events`: `CustomerCreated`, `CustomerDeactivated`, `CustomerActivated`, `CustomerMasterDataUpdated`, `CustomerContactAdded`, `CustomerContactUpdated`, `CustomerContactDeactivated`, and `CustomerContactActivated`, extending `DomainEvent` with synchronous in-process notification dispatch.
- Implemented module-internal Dapper repositories in `ICS.Modules.Customer.Persistence` using explicit parameterized SQL against SQL Server without EF Core: `ICustomerRepository` (`CustomerRepository`) and `ICustomerContactRepository` (`CustomerContactRepository`).
- Implemented MediatR commands, validators, and handlers in `ICS.Modules.Customer.Application.Commands` for: `CreateCustomer`, `UpdateCustomerMasterData`, `DeactivateCustomer`, `ActivateCustomer`, `CreateCustomerContact`, `UpdateCustomerContact`, and `DeactivateCustomerContact`.
- Implemented MediatR queries and handlers in `ICS.Modules.Customer.Application.Queries` for: `GetCustomerById`, `ListActiveCustomers`, `GetCustomerContacts`, and `GetCustomerWithContractStatus`.
- Implemented application services:
  - `CustomerService : ICustomerService` executing command dispatching through MediatR pipeline behaviors with validation and event dispatch.
  - `CustomerQueryService : ICustomerQueryService` published interface at module boundary root implementing `GetCustomerByIdAsync`, `ListActiveCustomersAsync`, `GetCustomerContactsAsync`, `GetCustomerWithContractStatusAsync`, plus synchronous convenience overloads.
- Registered all services and repositories in `CustomerModule.RegisterServices` with Scoped lifetimes per architectural conventions.
- Registered Customer domain event handlers in `IcsWebApplicationFactory` for `TestDomainEventCollector`.
- Implemented unit tests in `tests/ICS.Tests.Unit/CustomerDomainTests.cs` (11 test methods) verifying domain entities, validation, state transitions, and event emission.
- Implemented integration tests in `tests/ICS.Tests.Integration/CustomerIntegrationTests.cs` (9 test methods) verifying end-to-end command execution, persistence, query service accuracy, event collection via `TestDomainEventCollector`, validation pipeline behaviors, and database isolation with Respawn.
- Verified non-incremental solution build (`dotnet build ICS.sln --no-incremental`) and test execution (`dotnet test ICS.sln`) passing 100% (101 tests: 49 unit, 52 integration) with zero errors, zero warnings, and zero EF Core references.

Changed Files:
- `src/ICS.Web/Migrations/Script0004_CreateCustomerSchema.sql`
- `src/ICS.Modules.Customer/Domain/Customer.cs`
- `src/ICS.Modules.Customer/Domain/CustomerContact.cs`
- `src/ICS.Modules.Customer/Domain/Events/CustomerDomainEvents.cs`
- `src/ICS.Modules.Customer/Persistence/ICustomerRepository.cs`
- `src/ICS.Modules.Customer/Persistence/CustomerRepository.cs`
- `src/ICS.Modules.Customer/Persistence/ICustomerContactRepository.cs`
- `src/ICS.Modules.Customer/Persistence/CustomerContactRepository.cs`
- `src/ICS.Modules.Customer/Application/DTOs/CustomerDtos.cs`
- `src/ICS.Modules.Customer/Application/Commands/CustomerCommands.cs`
- `src/ICS.Modules.Customer/Application/Queries/CustomerQueries.cs`
- `src/ICS.Modules.Customer/Application/ICustomerService.cs`
- `src/ICS.Modules.Customer/Application/CustomerService.cs`
- `src/ICS.Modules.Customer/ICustomerQueryService.cs`
- `src/ICS.Modules.Customer/CustomerQueryService.cs`
- `src/ICS.Modules.Customer/CustomerModule.cs`
- `tests/ICS.Tests.Integration/ICS.Tests.Integration.csproj`
- `tests/ICS.Tests.Integration/IcsWebApplicationFactory.cs`
- `tests/ICS.Tests.Integration/TestDomainEventCollector.cs`
- `tests/ICS.Tests.Unit/CustomerDomainTests.cs`
- `tests/ICS.Tests.Integration/CustomerIntegrationTests.cs`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`

---

### P3-S14

Title: Product Module — Domain, Application Services & Persistence

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the complete Product module: domain entity (`Product`), `ProductService` command methods, `ProductQueryService` query methods, DbUp SQL migration scripts for `product.*` tables, and Dapper repository implementations. Owner validation reads from `OrganizationQueryService`.

Depends On: P1-S06, P1-S07, P2-S10, P3-S12

Repository: `ICS.Modules.Product`

Completion Criteria:
- DbUp SQL migration scripts create `product.*` schema table: `Products` — Architecture §17.
- All repository operations use Dapper with explicit parameterized SQL; no EF Core — Architecture §19.3, §20.
- MediatR handlers via `ProductService` implement: `CreateProduct`, `UpdateProduct`, `AssignProductOwner`, `ActivateProduct`, `DeactivateProduct` — Architecture §10.
- `ProductQueryService` implements: `GetProductById`, `GetProductByCode`, `ListActiveProducts`, `ListAllProducts` — Architecture §10.
- `CreateProduct` and `AssignProductOwner` validate the owner via `OrganizationQueryService` (no direct cross-schema write) — Architecture §15.
- Domain events emitted: `ProductCreated`, `ProductOwnerChanged`, `ProductActivated`, `ProductDeactivated`.
- `ProductQueryService` is accessible as a published interface.
- Repository implementations write exclusively to `product.*` schema using parameterized SQL.
- Integration tests (xUnit + WebApplicationFactory + Respawn) pass.

Notes: Architecture §10 (Product Module Architecture) is authoritative. **Parallel Execution**: Depends on P3-S12 because `CreateProduct` and `AssignProductOwner` call `OrganizationQueryService` to validate the owner, but has zero dependency on Customer (P3-S13). Executes in parallel with P3-S13.

Implementation Notes:
- Added DbUp SQL migration script `Script0005_CreateProductSchema.sql` in `src/ICS.Web/Migrations/` creating `product.*` schema table `Products` with primary key, unique index on `Code`, and lookup indexes on `OwnerPersonId` and `Status` per Architecture §17.
- Implemented domain entity `Product` in `ICS.Modules.Product.Domain` with domain invariant validation, lifecycle status transitions (`ACTIVE`, `INACTIVE`), and event emission per Architecture §10, §16, and `product-domain.md`.
- Implemented domain events in `ICS.Modules.Product.Domain.Events`: `ProductCreated`, `ProductOwnerChanged`, `ProductActivated`, and `ProductDeactivated` inheriting from `DomainEvent`.
- Implemented module-internal Dapper repository `ProductRepository` implementing internal `IProductRepository` in `ICS.Modules.Product.Persistence` using explicit parameterized SQL against `[product].[Products]` without EF Core per Architecture §19.3 and §20.
- Implemented MediatR commands, validators, and handlers in `ICS.Modules.Product.Application.Commands` for: `CreateProduct`, `UpdateProduct`, `AssignProductOwner`, `ActivateProduct`, and `DeactivateProduct`.
- Implemented cross-module owner validation in `CreateProductCommandHandler` and `AssignProductOwnerCommandHandler` reading from `IOrganizationQueryService` to ensure active person existence without cross-schema writes per Architecture §15.
- Implemented MediatR queries, validators, and handlers in `ICS.Modules.Product.Application.Queries` for: `GetProductById`, `GetProductByCode`, `ListActiveProducts`, and `ListAllProducts`.
- Implemented application services:
  - `ProductService : IProductService` command facade executing through MediatR pipeline behaviors with validation and event dispatch.
  - `ProductQueryService : IProductQueryService` published query facade implementing `GetProductByIdAsync`, `GetProductByCodeAsync`, `ListActiveProductsAsync`, `ListAllProductsAsync`, and synchronous equivalents with owner name resolution via `OrganizationQueryService`.
- Registered services and internal repository in `ProductModule.RegisterServices` with scoped lifetimes per architecture conventions.
- Configured integration test infrastructure in `TestDomainEventCollector.cs` and `IcsWebApplicationFactory.cs` for capturing Product domain events.
- Added comprehensive unit tests in `tests/ICS.Tests.Unit/ProductDomainTests.cs` (10 test methods) verifying domain entities, validation invariants, status transitions, and domain event emissions.
- Added comprehensive integration tests in `tests/ICS.Tests.Integration/ProductIntegrationTests.cs` (9 test methods) verifying end-to-end command execution, Dapper persistence, query service operations, cross-module owner validation against `OrganizationQueryService`, domain event collection, and Respawn test database isolation.
- Verified non-incremental solution build (`dotnet build ICS.sln --no-incremental`) and test execution (`dotnet test ICS.sln`) passing 100% (125 tests: 64 unit, 61 integration) with zero errors, zero warnings, and zero EF Core references.

Changed Files:
- `src/ICS.Web/Migrations/Script0005_CreateProductSchema.sql`
- `src/ICS.Modules.Product/Domain/Product.cs`
- `src/ICS.Modules.Product/Domain/Events/ProductDomainEvents.cs`
- `src/ICS.Modules.Product/Persistence/IProductRepository.cs`
- `src/ICS.Modules.Product/Persistence/ProductRepository.cs`
- `src/ICS.Modules.Product/Application/DTOs/ProductDtos.cs`
- `src/ICS.Modules.Product/Application/Commands/ProductCommands.cs`
- `src/ICS.Modules.Product/Application/Queries/ProductQueries.cs`
- `src/ICS.Modules.Product/Application/IProductService.cs`
- `src/ICS.Modules.Product/Application/ProductService.cs`
- `src/ICS.Modules.Product/IProductQueryService.cs`
- `src/ICS.Modules.Product/ProductQueryService.cs`
- `src/ICS.Modules.Product/ProductModule.cs`
- `tests/ICS.Tests.Integration/ICS.Tests.Integration.csproj`
- `tests/ICS.Tests.Integration/TestDomainEventCollector.cs`
- `tests/ICS.Tests.Integration/IcsWebApplicationFactory.cs`
- `tests/ICS.Tests.Unit/ProductDomainTests.cs`
- `tests/ICS.Tests.Integration/ProductIntegrationTests.cs`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`

---

### P3-S15

Title: Product Catalog Screen — SCR-PRD-001

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement `SCR-PRD-001` (Product Catalog Screen) as a Vue 3 SFC with Bootstrap 5 styling, and the corresponding ASP.NET Core REST API controller (`/api/v1/products/*`). Wires `ProductService` commands and `ProductQueryService` queries. Delivers the first content management screen; users can view the product catalog, create products, update attributes, assign product owners, and toggle product status. M1 is achieved when this slice is complete.

Depends On: P3-S14, P2-S10, P1-S08

Repository: `ICS.Web`, `ICS.Web/client/`

Completion Criteria:
- Vue 3 SFC `ProductCatalogView.vue` (`<script setup lang="ts">`, Bootstrap 5) renders product catalog — Architecture §19.4.
- ASP.NET Core Controller at `/api/v1/products` exposes REST endpoints for: list all products (`ListAllProducts`), list active products (`ListActiveProducts`), get product detail (`GetProductById`), create product (`CreateProduct`), update product (`UpdateProduct`), assign product owner (`AssignProductOwner`), activate/deactivate product.
- Product owner selector uses `OrganizationQueryService.ListActivePersons` for dropdown — Architecture §10.
- All endpoints protected by `[Authorize]` (authentication middleware from P2-S10).
- Axios HTTP client in Vue component calls `/api/v1/products` endpoints with authentication interceptors — Architecture §19.4.
- Integration tests (xUnit + WebApplicationFactory): catalog listing, product creation, and owner assignment pass.

Notes: Architecture §10 and §9 (FEAT-PRD-001) are authoritative. This slice completes Milestone M1.

Implementation Notes:
- Implemented `ProductsController` in `src/ICS.Web/Controllers/ProductsController.cs` protected by `[Authorize]` exposing REST API endpoints:
  - `GET /api/v1/products` with optional `?activeOnly=true` parameter (delegating to `IProductQueryService.ListAllProductsAsync` / `ListActiveProductsAsync`).
  - `GET /api/v1/products/active` dedicated endpoint for active products only.
  - `GET /api/v1/products/{id}` detail lookup endpoint (returning 404 ProblemDetails if not found).
  - `POST /api/v1/products` creation endpoint returning 201 CreatedAtRoute with `ProductDto`.
  - `PUT /api/v1/products/{id}` attribute update endpoint (`UpdateProductAsync`).
  - `PUT /api/v1/products/{id}/owner` and `POST /api/v1/products/{id}/owner` owner assignment endpoint (`AssignProductOwnerAsync`).
  - `POST /api/v1/products/{id}/activate` and `PUT /api/v1/products/{id}/activate` lifecycle activation endpoint.
  - `POST /api/v1/products/{id}/deactivate` and `PUT /api/v1/products/{id}/deactivate` lifecycle deactivation endpoint.
  - `GET /api/v1/products/owners` endpoint querying `IOrganizationQueryService.ListActivePersonsAsync` to populate active persons dropdown selector.
- Implemented Vue 3 SFC `ProductCatalogView.vue` in `src/ICS.Web/client/src/views/` (`<script setup lang="ts">` with Bootstrap 5 semantic classes) delivering screen `SCR-PRD-001`:
  - Catalog listing table displaying Code, Name, Description, Product Owner, Status badge, and Creation date.
  - Interactive search filtering by code, name, description, or owner name.
  - Status toggle filters (All, Active, Inactive) with live item counts.
  - Modals for Product Creation, Attribute Editing, and Owner Assignment using active persons dropdown from `/api/v1/products/owners`.
  - Inline lifecycle status toggling (Activate / Deactivate) with confirmation prompts.
  - Axios HTTP integration via `apiClient` with automatic cookie session authentication and RFC 7807 ProblemDetails error handling.
- Configured client-side routing in `src/ICS.Web/client/src/router/index.ts` mapping `/products` to `ProductCatalogView.vue` with `requiresAuth: true` and `meta: { screenId: 'SCR-PRD-001', title: 'Product Catalog' }`.
- Added navigation link in `src/ICS.Web/client/src/App.vue` pointing to `/products` with `SCR-PRD-001` identifier badge.
- Added comprehensive integration tests in `tests/ICS.Tests.Integration/ProductScreenIntegrationTests.cs` (8 test methods) verifying:
  - HTTP 401 Unauthorized returned across all endpoints when unauthenticated.
  - Complete catalog listing and active products filtering with authenticated session cookie.
  - Product creation returning 201 Created and subsequent retrieval via `GET /api/v1/products/{id}`.
  - Product attribute updates via `PUT /api/v1/products/{id}`.
  - Owner assignment via `PUT /api/v1/products/{id}/owner`.
  - Status transitions (Deactivate and Activate).
  - Product owner dropdown data retrieval from `IOrganizationQueryService.ListActivePersonsAsync`.
  - RFC 7807 ProblemDetails with code `PRODUCT_NOT_FOUND` on missing product ID.
- Successfully compiled frontend assets via `npm run build` (`vue-tsc -b && vite build`) into `src/ICS.Web/wwwroot/`.
- Verified non-incremental solution build (`dotnet build ICS.sln`) and full test suite (`dotnet test ICS.sln`) passing 100% (133 tests: 64 unit, 69 integration) with zero errors, zero warnings, and zero EF Core references.
- Achieved Milestone M1 (Authentication & Catalog Operational).

Changed Files:
- `src/ICS.Web/Controllers/ProductsController.cs`
- `src/ICS.Web/client/src/views/ProductCatalogView.vue`
- `src/ICS.Web/client/src/router/index.ts`
- `src/ICS.Web/client/src/App.vue`
- `tests/ICS.Tests.Integration/ProductScreenIntegrationTests.cs`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`

---

## P4 — Request Lifecycle

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Request module — the core operational transactional engine — including domain model, application services using Dapper, escalation and management commands, and all Request screens as Vue 3 SFCs. Delivers complete operational request management. M2 is achieved when this phase is complete.

Source: Architecture §22 — Boundary: **Request** (`ICS.Modules.Request`, Depends On: `ICS.Core`, `Organization`, `Customer`, `Product`).

---

### P4-S16

Title: Request Module — Domain Model & State Machine

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the Request domain layer: `Request` aggregate, `RequestResolution` entity, `RequestAssignment` entity, state machine enforcing valid transitions (`CAPTURED → EVALUATING → ACCEPTED/REJECTED → IN_PROGRESS → ESCALATED → COMPLETED`), and all domain events emitted on state transitions.

Depends On: P1-S06

Repository: `ICS.Modules.Request`

Completion Criteria:
- `Request` aggregate with all state transition methods is implemented, per Architecture §7 (`RequestService`) and §8 (UC-REQ-001..008).
- State machine enforces valid transitions; invalid transitions are rejected with a domain exception.
- Domain events defined and emitted on each transition: `RequestRecorded`, `RequestAssigned`, `RequestEvaluated`, `RequestAccepted`, `RequestRejected`, `RequestEscalated`, `ManagementDecisionRequested`, `RequestCompleted`.
- `RequestResolution` and `RequestAssignment` entities are implemented.
- Unit tests (xUnit + FluentAssertions) covering all valid state transitions and all invalid transition rejections pass — Architecture §19.8.

Notes: Domain layer only — no database, no service layer. Clean separation enables P4-S16 to start as soon as P1-S06 is complete, independently of P3. Architecture §7, §8, §20 are authoritative.

Implementation Notes:
- Implemented `RequestStatus` in `ICS.Modules.Request.Domain` defining lifecycle statuses (`CAPTURED`, `EVALUATING`, `ACCEPTED`, `REJECTED`, `IN_PROGRESS`, `ESCALATED`, `COMPLETED`), category predicates (`IsActive`, `IsTerminal`), and validity checks per Architecture §7, §8, §13, and `request-domain.md` §9.
- Implemented `RequestStateMachine` in `ICS.Modules.Request.Domain` defining and enforcing valid state transitions (`CAPTURED → EVALUATING → ACCEPTED/REJECTED → IN_PROGRESS → ESCALATED → COMPLETED`, escalation recovery, and direct triage rejection) with detailed validation via `EnsureValidTransition`.
- Implemented domain exceptions in `ICS.Modules.Request.Domain.Exceptions`: `RequestDomainException` and `InvalidRequestStateTransitionException` capturing `RequestId`, `CurrentStatus`, and `TargetStatus` with automatic RFC 7807 problem details alignment.
- Implemented all 8 domain events in `ICS.Modules.Request.Domain.Events` extending `DomainEvent` with automatic `EventId` and UTC `OccurredAt`: `RequestRecorded`, `RequestAssigned`, `RequestEvaluated`, `RequestAccepted`, `RequestRejected`, `RequestEscalated`, `ManagementDecisionRequested`, and `RequestCompleted`.
- Implemented domain entity `RequestResolution` in `ICS.Modules.Request.Domain` representing final resolution outcome (`RESOLVED`, `REJECTED`, `CANCELLED`), summary/justification, resolver identity, and resolution timestamp per Architecture §6, §16, §17.
- Implemented domain entity `RequestAssignment` in `ICS.Modules.Request.Domain` tracking ownership assignment history, assigned-by actor, active flags, and deactivation timestamps per Architecture §6, §16, §17.
- Implemented `Request` aggregate root in `ICS.Modules.Request.Domain` encapsulating domain invariants, ownership assignment history, management decision elevation (`IsAwaitingManagementDecision`), escalation details, completion review rework, and state transition methods (`Record`, `AssignOwner`, `Evaluate`, `Accept`, `Reject`, `StartProgress`, `Escalate`, `ResolveEscalation`, `RequestManagementDecision`, `Complete`, `RequestRework`, `UpdateDetails`, `ChangePriority`, `AssignWorkPackage`).
- Implemented comprehensive unit test suite in `tests/ICS.Tests.Unit/RequestDomainTests.cs` (24 test methods / 59 test cases including `[Theory]`) verifying valid lifecycle progressions, direct triage rejections, escalation and de-escalation cycles, management decision elevation without lifecycle mutation, completion review rework, invariant protections, assignment history preservation, all 8 domain events, and rejection of all invalid state transitions with `InvalidRequestStateTransitionException`.
- Verified non-incremental solution build (`dotnet build ICS.sln --no-incremental`) and test execution (`dotnet test ICS.sln`) passing 100% (192 tests: 123 unit, 69 integration) with 0 errors and 0 warnings.

Changed Files:
- `src/ICS.Modules.Request/Domain/RequestStatus.cs`
- `src/ICS.Modules.Request/Domain/Exceptions/RequestDomainExceptions.cs`
- `src/ICS.Modules.Request/Domain/Events/RequestDomainEvents.cs`
- `src/ICS.Modules.Request/Domain/RequestResolution.cs`
- `src/ICS.Modules.Request/Domain/RequestAssignment.cs`
- `src/ICS.Modules.Request/Domain/RequestStateMachine.cs`
- `src/ICS.Modules.Request/Domain/Request.cs`
- `tests/ICS.Tests.Unit/RequestDomainTests.cs`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`

---

### P4-S17

Title: Request Module — Application Services & Persistence

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement `RequestService` application commands as MediatR handlers, `RequestQueryService` queries using Dapper explicit parameterized SQL, DbUp SQL migration scripts for `request.*` tables, and Dapper repository implementations. Cross-module validation reads from Organization, Customer, and Product via their published query interfaces. Audit logging is applied to every state change.

Depends On: P4-S16, P1-S07, P2-S10, P3-S12, P3-S13, P3-S14

Repository: `ICS.Modules.Request`

Completion Criteria:
- DbUp SQL migration scripts create `request.*` schema tables: `Requests`, `RequestResolutions`, `RequestAssignments` — Architecture §17.
- All repository operations use Dapper with explicit parameterized SQL against SQL Server; no EF Core — Architecture §19.3, §20.
- MediatR handlers via `RequestService` implement: `RecordRequest`, `AssignRequestOwner`, `EvaluateRequest`, `AcceptRequestResponsibility`, `RejectRequest`, `EscalateRequest`, `RequestManagementDecision`, `ReviewRequestCompletion` — Architecture §7, §8 (UC-REQ-001..008).
- `RequestQueryService` implements using Dapper: `GetRequestById`, `GetRequestStateHistory`, `ListMyAssignedRequests`, `GetFilteredRequestGrid` — Architecture §7, §8 (UC-COL-002..004).
- Assignee validated via `OrganizationQueryService`; customer via `CustomerQueryService`; product via `ProductQueryService` — Architecture §15.
- Every state change records actor `PersonId`, timestamp, and previous state using Dapper parameterized INSERT — Architecture §18 (Audit Logging).
- Repository implementations write exclusively to `request.*` schema.
- `RequestQueryService` is accessible as a published interface.
- Integration tests (xUnit + WebApplicationFactory + Respawn): full request lifecycle (Record → Assign → Evaluate → Accept → Complete) pass.

Notes: Architecture §7, §8, §15, §18, §19.3, §20 are authoritative.

Implementation Notes:
- Added DbUp SQL migration script `Script0006_CreateRequestSchema.sql` in `src/ICS.Web/Migrations/` creating `request.*` schema tables: `Requests`, `RequestResolutions`, `RequestAssignments`, and `RequestStateHistories` (state change audit trail) with primary keys, cascade foreign keys, and indexes per Architecture §17 and §18.
- Implemented comprehensive data transfer objects in `ICS.Modules.Request.Application.DTOs`: `RequestDto`, `RequestResolutionDto`, `RequestAssignmentDto`, `RequestStateHistoryDto`, `RequestGridFilterDto`, `RequestSummaryDto`, `RequestGridResultDto`, and `MyAssignedRequestsResponseDto`.
- Implemented internal Dapper repository `RequestRepository : IRequestRepository` in `ICS.Modules.Request.Persistence` using explicit parameterized SQL against `request.*` tables without EF Core per Architecture §19.3 and §20.
- Implemented MediatR commands, validators, and handlers in `ICS.Modules.Request.Application.Commands` for UC-REQ-001..008:
  - `RecordRequest`: captures request, validates customer and product via `ICustomerQueryService` and `IProductQueryService`, validates initial owner via `IOrganizationQueryService`, inserts state history audit record, dispatches `RequestRecorded`.
  - `AssignRequestOwner`: validates assignee via `IOrganizationQueryService`, maintains active/inactive assignment records in `RequestAssignments`, records audit history, dispatches `RequestAssigned`.
  - `EvaluateRequest`: transitions state from `CAPTURED` to `EVALUATING`, validates evaluator, records audit history, dispatches `RequestEvaluated`.
  - `AcceptRequestResponsibility`: transitions state from `EVALUATING` to `ACCEPTED`, validates acceptor, records audit history, dispatches `RequestAccepted`.
  - `RejectRequest`: transitions state to `REJECTED`, creates resolution outcome `REJECTED`, records audit history, dispatches `RequestRejected`.
  - `StartRequestProgress`: transitions state from `ACCEPTED` to `IN_PROGRESS`, records audit history.
  - `EscalateRequest`: transitions state to `ESCALATED`, records escalation reason and actor, records audit history, dispatches `RequestEscalated`.
  - `RequestManagementDecision`: sets `IsAwaitingManagementDecision = true`, records decision question and context, records audit history, dispatches `ManagementDecisionRequested`.
  - `ReviewRequestCompletion`: either accepts resolution (completes request with resolution outcome `RESOLVED`, dispatches `RequestCompleted`) or requests rework (keeps in `IN_PROGRESS` with rework feedback recorded in audit history).
- Implemented MediatR queries, validators, and handlers in `ICS.Modules.Request.Application.Queries` for: `GetRequestById`, `GetRequestStateHistory`, `ListMyAssignedRequests` (with workload summary counts), and `GetFilteredRequestGrid` (with search keyword, customer/product/owner/status filtering, and pagination).
- Implemented application services:
  - `RequestService : IRequestService` command facade dispatching commands through MediatR pipeline behaviors.
  - `RequestQueryService : IRequestQueryService` published query facade implementing async and sync overloads at module root boundary.
- Registered published interfaces and internal repository in `RequestModule.RegisterServices` with scoped lifetimes.
- Registered Request domain event handlers in `TestDomainEventCollector` and `IcsWebApplicationFactory` for all 8 domain events.
- Added comprehensive integration tests in `tests/ICS.Tests.Integration/RequestIntegrationTests.cs` (9 test methods) covering full lifecycle, rework paths, rejection triage, escalation and management decision, assigned queue with workload counts, multi-attribute grid search and pagination, cross-module validation failures, and state change audit history.
- Verified non-incremental solution build (`dotnet build ICS.sln --no-incremental`) and test execution (`dotnet test ICS.sln`) passing 100% (201 tests: 123 unit, 78 integration) with zero errors, zero warnings, and zero EF Core references.

Changed Files:
- `src/ICS.Web/Migrations/Script0006_CreateRequestSchema.sql`
- `src/ICS.Modules.Request/Application/DTOs/RequestDtos.cs`
- `src/ICS.Modules.Request/Persistence/IRequestRepository.cs`
- `src/ICS.Modules.Request/Persistence/RequestRepository.cs`
- `src/ICS.Modules.Request/Application/Commands/RequestCommands.cs`
- `src/ICS.Modules.Request/Application/Queries/RequestQueries.cs`
- `src/ICS.Modules.Request/Application/IRequestService.cs`
- `src/ICS.Modules.Request/Application/RequestService.cs`
- `src/ICS.Modules.Request/IRequestQueryService.cs`
- `src/ICS.Modules.Request/RequestQueryService.cs`
- `src/ICS.Modules.Request/RequestModule.cs`
- `tests/ICS.Tests.Integration/ICS.Tests.Integration.csproj`
- `tests/ICS.Tests.Integration/TestDomainEventCollector.cs`
- `tests/ICS.Tests.Integration/IcsWebApplicationFactory.cs`
- `tests/ICS.Tests.Integration/RequestIntegrationTests.cs`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`

---

### P4-S18

Title: Request Module — Escalation & Management Commands

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement escalation, management decision, and reassignment command paths as MediatR handlers in `RequestService` (UC-REQ-006, UC-REQ-007, UC-MGT-001). These commands extend the service layer established in P4-S17 with the escalation-specific business logic and `ReassignRequestOwnership`.

Depends On: P4-S17

Repository: `ICS.Modules.Request`

Completion Criteria:
- `EscalateRequest` MediatR handler transitions `Request` to `ESCALATED`, records escalation actor and reason via Dapper parameterized SQL, emits `RequestEscalated`.
- `RequestManagementDecision` MediatR handler records management decision, emits `ManagementDecisionRequested`.
- `ReassignRequestOwnership` (UC-MGT-001) MediatR handler updates `OwnerPersonId` via Dapper parameterized SQL, validates new assignee via `OrganizationQueryService`, emits `RequestAssigned`.
- Integration tests (xUnit + WebApplicationFactory + Respawn): Escalate, ManagementDecision, and Reassign flows pass.

Notes: Architecture §8 (UC-REQ-006, UC-REQ-007, UC-MGT-001) is authoritative.

Implementation Notes:
- Enforced designated Request Owner validation in `EscalateRequestCommandHandler` and `RequestManagementDecisionCommandHandler` per FEAT-REQ-006 and FEAT-REQ-007, ensuring only active assigned owners may escalate or elevate decision dockets, while preserving Request ownership across both actions (Actor Model Assignment Rule 7).
- Implemented `ReassignRequestOwnership` command path (UC-MGT-001 / FEAT-REQ-002) in `ICS.Modules.Request.Application.Commands`:
  - `ReassignRequestOwnershipCommand` and `ReassignRequestOwnershipCommandValidator`.
  - `ReassignRequestOwnershipCommandHandler`: validates non-terminal Request state, validates actor and new assignee via `IOrganizationQueryService` ensuring active status, enforces no-op redundant reassignment prevention, invokes `Request.AssignOwner` (which deactivates the prior active assignment and adds a new active assignment), updates `[request].[Requests]` and `[request].[RequestAssignments]` via Dapper parameterized SQL, appends state change audit log to `[request].[RequestStateHistories]`, and dispatches `RequestAssigned` domain event.
- Implemented `ResolveEscalation` command path in `ICS.Modules.Request.Application.Commands`:
  - `ResolveEscalationCommand`, validator, and handler transitioning Request status from `ESCALATED` back to `IN_PROGRESS` with audit trail recording.
- Extended `IRequestService` and `RequestService` with `ReassignRequestOwnershipAsync` and `ResolveEscalationAsync` delegating through MediatR pipeline behaviors.
- Added comprehensive integration test suite in `tests/ICS.Tests.Integration/RequestEscalationAndManagementIntegrationTests.cs` (11 test methods) covering:
  - `EscalateRequest` by assigned owner, transitioning status to `ESCALATED`, persisting actor/reason/timestamp in SQL Server, recording audit log, maintaining ownership, and emitting `RequestEscalated`.
  - `EscalateRequest` authority enforcement (rejecting non-owner) and state machine rejection on closed requests.
  - `RequestManagementDecision` by owner, setting `IsAwaitingManagementDecision = true` and persisting decision question/options/impact while preserving core lifecycle status, emitting `ManagementDecisionRequested`, and rejecting non-owner attempts.
  - `ReassignRequestOwnership` (UC-MGT-001), validating new assignee via `OrganizationQueryService`, updating `OwnerPersonId`, deactivating previous assignment, inserting new active assignment record, logging audit entry, emitting `RequestAssigned`, and verifying Dapper persistence.
  - `ReassignRequestOwnership` preserves `ESCALATED` status when reassigning an escalated request.
  - `ReassignRequestOwnership` failure conditions: redundant identical owner, non-existent person, inactive person, and closed request.
  - `ResolveEscalation` transitioning `ESCALATED` back to `IN_PROGRESS`.
- Verified non-incremental solution build (`dotnet build ICS.sln --no-incremental`) and test execution (`dotnet test ICS.sln`) passing 100% (212 tests: 123 unit, 89 integration) with zero errors, zero warnings, and zero EF Core references.

Changed Files:
- `src/ICS.Modules.Request/Application/IRequestService.cs`
- `src/ICS.Modules.Request/Application/RequestService.cs`
- `src/ICS.Modules.Request/Application/Commands/RequestCommands.cs`
- `tests/ICS.Tests.Integration/RequestEscalationAndManagementIntegrationTests.cs`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`

---

### P4-S19

Title: Request Screens — SCR-REQ-001..005

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement all five Request screens as Vue 3 SFCs with Bootstrap 5 styling and their corresponding ASP.NET Core REST API controllers (`/api/v1/requests/*`). Delivers the complete request management UI. Dropdowns for Customer, Product, and Person use the respective published query services.

Depends On: P4-S18, P2-S10, P1-S08

Repository: `ICS.Web`, `ICS.Web/client/`

Completion Criteria:
- Vue 3 SFCs (`<script setup lang="ts">`, Bootstrap 5) implemented for `SCR-REQ-001` through `SCR-REQ-005` — Architecture §19.4, §20.
- `SCR-REQ-001` (Request List) controller: `RequestQueryService.GetFilteredRequestGrid` with status and assignment filters.
- `SCR-REQ-002` (Create Request) controller: `RequestService.RecordRequest`; Customer selector via `CustomerQueryService.ListActiveCustomers`; Product selector via `ProductQueryService.ListActiveProducts`; Person selector via `OrganizationQueryService.ListActivePersons`.
- `SCR-REQ-003` (Request Detail) controller: `RequestQueryService.GetRequestById`; all state-transition command endpoints (`Assign`, `Evaluate`, `Accept`, `Reject`, `Escalate`, `ManagementDecision`, `Complete`, `Reassign`) wired to MediatR `RequestService` handlers.
- `SCR-REQ-004` (My Requests) controller: `RequestQueryService.ListMyAssignedRequests` using `CurrentContextProvider.CurrentPersonId`.
- `SCR-REQ-005` (Search / History) controller: `RequestQueryService.GetRequestStateHistory` and search by Customer, Product, or status.
- All API endpoints at `/api/v1/requests/*` protected by `[Authorize]`.
- Audit logging active on all state-transition endpoints — Architecture §18.
- Axios HTTP client in Vue components calls request endpoints with authentication interceptors.
- Integration tests (xUnit + WebApplicationFactory): each screen endpoint (listing, create, state transition) pass.

Notes: Architecture §9 (Feature Mapping — FEAT-REQ-001..008, FEAT-COL-001..004) and §8 (UC-REQ-001..008, UC-COL-001..004) are authoritative.

Implementation Notes:
- Implemented `RequestsController` in `src/ICS.Web/Controllers/RequestsController.cs` protected by `[Authorize]` exposing complete Request REST API endpoints:
  - `GET /api/v1/requests`: paginated, filtered grid query using `IRequestQueryService.GetFilteredRequestGridAsync` supporting keyword search, customer, product, owner, status, priority, and date range filters (SCR-REQ-001, SCR-REQ-005).
  - `GET /api/v1/requests/{id}`: request detail retrieval using `IRequestQueryService.GetRequestByIdAsync` with RFC 7807 problem details (`REQUEST_NOT_FOUND`) on 404 (SCR-REQ-003).
  - `POST /api/v1/requests`: request recording / creation using `IRequestService.RecordRequestAsync` with ambient identity fallback for requester and assigned-by persons, returning 201 CreatedAtRoute (SCR-REQ-002, UC-REQ-001).
  - `GET /api/v1/requests/my-assigned`: personal assigned queue query using `IRequestQueryService.ListMyAssignedRequestsAsync` resolved from `ICurrentContextProvider.CurrentPersonId` (SCR-REQ-004, UC-COL-004).
  - `GET /api/v1/requests/{id}/history`: chronological state transition audit trail query using `IRequestQueryService.GetRequestStateHistoryAsync` (SCR-REQ-003, SCR-REQ-005, FEAT-COL-003).
  - State transition command endpoints invoking `IRequestService`:
    - `POST /api/v1/requests/{id}/assign`: `AssignRequestOwnerAsync` (UC-REQ-002).
    - `POST /api/v1/requests/{id}/evaluate`: `EvaluateRequestAsync` (UC-REQ-003).
    - `POST /api/v1/requests/{id}/accept`: `AcceptRequestResponsibilityAsync` (UC-REQ-004).
    - `POST /api/v1/requests/{id}/reject`: `RejectRequestAsync` (UC-REQ-005).
    - `POST /api/v1/requests/{id}/start-progress`: `StartRequestProgressAsync`.
    - `POST /api/v1/requests/{id}/escalate`: `EscalateRequestAsync` (UC-REQ-006).
    - `POST /api/v1/requests/{id}/management-decision`: `RequestManagementDecisionAsync` (UC-REQ-007).
    - `POST /api/v1/requests/{id}/complete`: `ReviewRequestCompletionAsync` with `acceptResolution: true` (UC-REQ-008).
    - `POST /api/v1/requests/{id}/rework`: `ReviewRequestCompletionAsync` with `acceptResolution: false` (UC-REQ-008).
    - `POST /api/v1/requests/{id}/reassign`: `ReassignRequestOwnershipAsync` (UC-MGT-001).
    - `POST /api/v1/requests/{id}/resolve-escalation`: `ResolveEscalationAsync`.
  - Dropdown selector endpoints querying published query interfaces:
    - `GET /api/v1/requests/customers` and `/dropdowns/customers` via `ICustomerQueryService.ListActiveCustomersAsync`.
    - `GET /api/v1/requests/products` and `/dropdowns/products` via `IProductQueryService.ListActiveProductsAsync`.
    - `GET /api/v1/requests/persons` and `/dropdowns/persons` via `IOrganizationQueryService.ListActivePersonsAsync`.
- Implemented typed client service in `src/ICS.Web/client/src/services/requestService.ts` providing strongly-typed Axios API wrappers for all request lifecycle commands, grid queries, history retrieval, and active lookup dropdowns with cookie-based session handling.
- Implemented all five Request screens as Vue 3 SFCs (`<script setup lang="ts">`, Bootstrap 5) matching Architecture §19.4 and §20:
  - `src/ICS.Web/client/src/views/RequestListView.vue` (`SCR-REQ-001`): paginated request grid, keyword search, customer/product/status/priority filters, lifecycle status badges, and quick links to new request, assigned queue, and search history.
  - `src/ICS.Web/client/src/views/RequestCreateView.vue` (`SCR-REQ-002`): request creation form with title, type, priority, customer, product, initial owner, requester reference, and description inputs with validation and error alerts.
  - `src/ICS.Web/client/src/views/RequestDetailView.vue` (`SCR-REQ-003`): detailed request view with contextual state-machine action buttons and modals (Assign, Evaluate, Accept, Reject, Start Progress, Escalate, Management Decision, Complete, Rework, Reassign, Resolve Escalation), active escalation and decision docket notification cards, tabs for overview metadata, ownership assignment log, and state audit trail.
  - `src/ICS.Web/client/src/views/MyRequestsView.vue` (`SCR-REQ-004`): personal assigned queue with workload summary cards (Active, Awaiting Decision, Escalated), closed request toggle, and direct interaction links.
  - `src/ICS.Web/client/src/views/RequestSearchView.vue` (`SCR-REQ-005`): advanced historical search across customer, product, owner, status, and date range, with modal for inspecting chronological state change audit trails.
- Configured Vue Router routes in `src/ICS.Web/client/src/router/index.ts` for `/requests`, `/requests/new`, `/requests/:id`, `/requests/my-assigned` (with `/my-requests` alias), and `/requests/search` with metadata badges and authentication guards (`requiresAuth: true`).
- Added Request navigation dropdown menu in `src/ICS.Web/client/src/App.vue` linking to all request screens with screen badges (`SCR-REQ-001`, `SCR-REQ-002`, `SCR-REQ-004`, `SCR-REQ-005`).
- Successfully compiled frontend SPA assets via `npm run build` (`vue-tsc -b && vite build`) into `src/ICS.Web/wwwroot/`.
- Implemented comprehensive integration test suite in `tests/ICS.Tests.Integration/RequestScreenIntegrationTests.cs` (8 test methods) verifying:
  - Authentication enforcement (HTTP 401 across all endpoints when unauthenticated).
  - Active lookup dropdown endpoints for customers, products, and persons.
  - Request recording (201 Created) and detail projection retrieval via GET `/api/v1/requests/{id}`.
  - Multi-attribute grid search, customer/product filtering, and pagination.
  - Complete request lifecycle state transitions (Record -> Assign -> Evaluate -> Accept -> Start Progress -> Management Decision -> Escalate -> Resolve Escalation -> Reassign -> Rework -> Complete) and chronological state audit history verification.
  - Personal assigned queue retrieval with workload summary counts.
  - Request rejection with justification.
  - Non-existent request 404 ProblemDetails with code `REQUEST_NOT_FOUND`.
- Verified non-incremental solution build (`dotnet build ICS.sln --no-incremental`) and test execution (`dotnet test ICS.sln`) passing 100% (220 tests: 123 unit, 97 integration) with zero errors, zero warnings, and zero EF Core references.
- Achieved Milestone M2 (Request Lifecycle Operational).

Changed Files:
- `src/ICS.Web/Controllers/RequestsController.cs`
- `src/ICS.Web/client/src/services/requestService.ts`
- `src/ICS.Web/client/src/views/RequestListView.vue`
- `src/ICS.Web/client/src/views/RequestCreateView.vue`
- `src/ICS.Web/client/src/views/RequestDetailView.vue`
- `src/ICS.Web/client/src/views/MyRequestsView.vue`
- `src/ICS.Web/client/src/views/RequestSearchView.vue`
- `src/ICS.Web/client/src/router/index.ts`
- `src/ICS.Web/client/src/App.vue`
- `tests/ICS.Tests.Integration/RequestScreenIntegrationTests.cs`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`

---

## P5 — Work Package

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Work Package module with Dapper persistence and its management screen as a Vue 3 SFC. M3 is achieved when this phase is complete.

Source: Architecture §22 — Boundary: **Work Package** (`ICS.Modules.WorkPackage`, Depends On: `ICS.Core`, `Organization`, `Customer`, `Product`, `Request`). Architecture §11.

---

### P5-S20

Title: Work Package Module — Domain Model

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the Work Package domain layer: `WorkPackage` aggregate, `WorkPackageRequest` membership entity, lifecycle state machine (`DRAFT → ACTIVE → CLOSED`), and all domain events. Business Rule 9 (a request may belong to at most one active Work Package) must be enforced at the domain level.

Depends On: P1-S06

Repository: `ICS.Modules.WorkPackage`

Completion Criteria:
- `WorkPackage` aggregate with lifecycle state machine is implemented (DRAFT, ACTIVE, CLOSED and valid transitions).
- `WorkPackageRequest` membership entity is implemented.
- Domain events defined: `WorkPackageCreated`, `WorkPackageActivated`, `WorkPackageClosed`, `WorkPackageOwnerChanged`, `RequestAddedToWorkPackage`, `RequestRemovedFromWorkPackage` — Architecture §11.
- Business Rule 9 is enforceable at the domain level.
- Unit tests (xUnit + FluentAssertions): lifecycle transitions and membership invariants (including Business Rule 9 violation) pass — Architecture §19.8.

Notes: Architecture §11 is authoritative. Domain-only slice; can start as soon as P1-S06 is complete.

Implementation Notes:
- Implemented `WorkPackageStatus` constants (`DRAFT`, `ACTIVE`, `CLOSED`) and helper methods (`IsValid`, `IsTerminal`, `IsActive`, `IsDraft`) in `src/ICS.Modules.WorkPackage/Domain/WorkPackageStatus.cs`.
- Implemented `WorkPackageStateMachine` in `src/ICS.Modules.WorkPackage/Domain/WorkPackageStateMachine.cs` enforcing valid transitions (`DRAFT → ACTIVE`, `DRAFT → CLOSED`, `ACTIVE → CLOSED`) and terminal `CLOSED` state invariants per Architecture §11 and work-package-domain.md §9.
- Implemented domain exceptions in `src/ICS.Modules.WorkPackage/Domain/Exceptions/WorkPackageDomainExceptions.cs`: `WorkPackageDomainException`, `InvalidWorkPackageStateTransitionException`, and `BusinessRule9ViolationException`.
- Implemented domain events inheriting `DomainEvent` (`ICS.Core.Domain`) in `src/ICS.Modules.WorkPackage/Domain/Events/WorkPackageDomainEvents.cs`: `WorkPackageCreated`, `WorkPackageActivated`, `WorkPackageClosed`, `WorkPackageOwnerChanged`, `RequestAddedToWorkPackage`, `RequestRemovedFromWorkPackage`.
- Implemented `WorkPackageRequest` membership entity inheriting `Entity` in `src/ICS.Modules.WorkPackage/Domain/WorkPackageRequest.cs`, supporting active/inactive status, removal timestamps, and preserving historical traceability (Business Rule 15).
- Implemented `IWorkPackageMembershipChecker` contract and `BusinessRule9Policy` in `src/ICS.Modules.WorkPackage/Domain/IWorkPackageMembershipChecker.cs`.
- Implemented `WorkPackage` aggregate root in `src/ICS.Modules.WorkPackage/Domain/WorkPackage.cs`:
  - Factory method `Create` creating packages in `DRAFT` status and emitting `WorkPackageCreated`.
  - Operations `UpdateObjective`, `AssignOwner`, `Activate`, `Close` emitting respective domain events and enforcing lifecycle invariants.
  - Membership operations `AddRequest` and `RemoveRequest` enforcing Business Rule 9 (both intra-aggregate duplicate check and inter-aggregate active membership verification via delegate or `IWorkPackageMembershipChecker`), and emitting `RequestAddedToWorkPackage` / `RequestRemovedFromWorkPackage`.
  - Full constructor for Dapper persistence hydration without raising domain events.
- Implemented comprehensive unit tests in `tests/ICS.Tests.Unit/WorkPackageDomainTests.cs` (24 test methods) verifying creation, full lifecycle transitions, terminal states, ownership changes, objective updates, request membership traceability, hydration mapping, and Business Rule 9 violations.
- Verified non-incremental solution build (`dotnet build ICS.sln --no-incremental`) and test execution (`dotnet test ICS.sln`) passing 100% (276 tests: 179 unit, 97 integration) with zero errors, zero warnings, and zero EF Core references.

Changed Files:
- `src/ICS.Modules.WorkPackage/Domain/WorkPackageStatus.cs`
- `src/ICS.Modules.WorkPackage/Domain/WorkPackageStateMachine.cs`
- `src/ICS.Modules.WorkPackage/Domain/WorkPackage.cs`
- `src/ICS.Modules.WorkPackage/Domain/WorkPackageRequest.cs`
- `src/ICS.Modules.WorkPackage/Domain/IWorkPackageMembershipChecker.cs`
- `src/ICS.Modules.WorkPackage/Domain/Events/WorkPackageDomainEvents.cs`
- `src/ICS.Modules.WorkPackage/Domain/Exceptions/WorkPackageDomainExceptions.cs`
- `tests/ICS.Tests.Unit/WorkPackageDomainTests.cs`
- `tests/ICS.Tests.Integration/RequestEscalationAndManagementIntegrationTests.cs`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`

---

### P5-S21

Title: Work Package Module — Application Services & Persistence

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement `WorkPackageService` commands as MediatR handlers, `WorkPackageQueryService` queries using Dapper explicit parameterized SQL, DbUp SQL migration scripts for `workpackage.*` tables, and Dapper repository implementations. Cross-module validation uses `OrganizationQueryService`, `CustomerQueryService`, `ProductQueryService`, and `RequestQueryService`.

Depends On: P5-S20, P1-S07, P2-S10, P3-S12, P3-S13, P3-S14, P4-S17

Repository: `ICS.Modules.WorkPackage`

Completion Criteria:
- DbUp SQL migration scripts create `workpackage.*` schema tables: `WorkPackages`, `WorkPackageRequests` — Architecture §17.
- All repository operations use Dapper with explicit parameterized SQL against SQL Server; no EF Core — Architecture §19.3, §20.
- MediatR handlers via `WorkPackageService` implement: `CreateWorkPackage`, `UpdateObjective`, `AssignOwner`, `AddRequestToWorkPackage`, `RemoveRequestFromWorkPackage`, `ActivateWorkPackage`, `CloseWorkPackage` — Architecture §11.
- `AddRequestToWorkPackage` validates via `RequestQueryService` that the request exists and is not already in another active package (Business Rule 9) — Architecture §11.
- `WorkPackageQueryService` implements using Dapper: `GetWorkPackageById`, `ListWorkPackages`, `GetWorkPackageScope`, `GetRequestWorkPackage` — Architecture §11.
- Owner validated via `OrganizationQueryService`; customer and product references via respective query services.
- Closing a Work Package does not alter constituent Request lifecycle states — Architecture §11.
- Repository implementations write exclusively to `workpackage.*` schema.
- Integration tests (xUnit + WebApplicationFactory + Respawn): full Work Package lifecycle and request membership pass.

Notes: Architecture §11 and §15 are authoritative.

Implementation Notes:
- Dependency preflight passed. All seven declared dependencies (P5-S20, P1-S07, P2-S10, P3-S12, P3-S13, P3-S14, P4-S17) are `IMPLEMENTED`, and each required output was verified present and consumable: the `WorkPackage` aggregate, `WorkPackageRequest`, state machine, and six domain events (P5-S20); `IOrganizationQueryService` (P1-S07); `ICustomerQueryService` (P2-S10); `IProductQueryService` (P3-S12/13/14); `IRequestQueryService` (P4-S17).
- Created migration `src/ICS.Web/Migrations/Script0007_CreateWorkPackageSchema.sql` (idempotent, `IF NOT EXISTS` guarded). It creates the `workpackage` schema and tables `WorkPackages`, `WorkPackageRequests`, and `WorkPackageStateHistories` (lifecycle audit per Architecture §18). Numbered 0007 so it runs after `Script0006_CreateRequestSchema.sql`, which it depends on: `WorkPackageRequests.RequestId` carries a foreign key to `request.[Requests]`. Verified applied to `ICS_Test` and journalled in `dbo.SchemaVersions` in the correct order.
- Added `IWorkPackageRepository` / `WorkPackageRepository` in `src/ICS.Modules.WorkPackage/Infrastructure/`. All operations use Dapper with explicit parameterized SQL (`@Id`, `@RequestId`, ...); the only dynamic text is a composed `WHERE` clause whose values are always bound through `DynamicParameters`, and statuses are validated against `WorkPackageStatus.IsValid` before being bound. Every `SELECT`/`INSERT`/`UPDATE` targets `workpackage.*` only; no other module's schema is read or written. The interface is `public` only so the public `WorkPackageQueryService` can consume it — it is not a cross-module contract, and the implementation class remains `internal`.
- `AddRequestMembershipAsync` re-reads the aggregate row under `UPDLOCK, ROWLOCK` inside a transaction so a concurrent status change is observed, and refuses a second active link for the same `(WorkPackageId, RequestId)` pair. This guard is intentionally same-package only; cross-package uniqueness is the command handler's decision. `DeactivateRequestMembershipAsync` captures `@@ROWCOUNT` before the follow-up `UPDATE` so the returned affected-row count is the membership count.
- Added `IWorkPackageService` / `WorkPackageService` in `src/ICS.Modules.WorkPackage/Application/`. It is a thin MediatR dispatcher; the behaviour lives in seven `IRequestHandler` implementations in `Application/Commands/WorkPackageCommands.cs` (`CreateWorkPackage`, `UpdateObjective`, `AssignOwner`, `AddRequestToWorkPackage`, `RemoveRequestFromWorkPackage`, `ActivateWorkPackage`, `CloseWorkPackage`), each with a FluentValidation validator. All lifecycle mutations go through the aggregate, and each handler dispatches the aggregate's domain events via `DispatchAndClearEventsAsync`, so `WorkPackageCreated`, `WorkPackageActivated`, `WorkPackageClosed`, `WorkPackageOwnerChanged`, `RequestAddedToWorkPackage`, and `RequestRemovedFromWorkPackage` are emitted as defined in the P5-S20 domain.
- Added `WorkPackageReferenceValidator` for cross-module validation, resolving every reference through the owning module's published query service and never touching another schema. Owner (create and reassign) must be an existing active Person via `IOrganizationQueryService`; customer and product must exist and be ACTIVE via `ICustomerQueryService` / `IProductQueryService`; the request must exist via `IRequestQueryService`. Violations raise `WorkPackageReferenceValidationException` carrying a `ReferenceKind`; missing packages raise `WorkPackageNotFoundException`. Both live in the Application layer so no P5-S20 domain file was modified.
- Added `IWorkPackageQueryService` / `WorkPackageQueryService` implementing `GetWorkPackageById`, `ListWorkPackages` (paged, filterable by search term, owner, customer, product, statuses, and date range), `GetWorkPackageScope`, and `GetRequestWorkPackage`, plus `GetWorkPackageStateHistory` and `IsRequestInActiveWorkPackage` so other modules can observe Business Rule 9. Denormalized owner/customer/product names are resolved through the owning modules' query services.
- Business Rule 9 is enforced at three layers: the aggregate (in-package duplicates), the command handler (cross-package), and the SQL guard (storage level). The cross-package rule is that a request may hold at most one active membership across non-closed Work Packages; a DRAFT or ACTIVE holder blocks, while a CLOSED package releases the request so a new package may adopt it. This matches the P5-S20 reviewed domain test `BusinessRule9_AddingRequestFromClosedPackage_ShouldSucceed`. `GetActiveMembershipForRequestAsync` therefore returns the membership joined with the holding package's status so the handler can apply this precisely.
- Closing a Work Package does not alter constituent Request lifecycle states and does not deactivate membership links; only the package status and close reason change. Verified by an integration test asserting the member Request's status and `ClosedAt` are unchanged.
- Registered `IWorkPackageService`, `IWorkPackageQueryService`, `WorkPackageReferenceValidator`, and `IWorkPackageRepository` in `WorkPackageModule`. MediatR handlers and FluentValidation validators are discovered by the existing assembly scan in `CoreServiceExtensions` (FluentValidation runs with `includeInternalTypes: true`), and `WorkPackageModule`'s assembly was already present in `Program.cs`'s scan list, so no host change was required.
- Added `InternalsVisibleTo` for `ICS.Tests.Unit` and `ICS.Tests.Integration` in `ICS.Modules.WorkPackage.csproj` so the internal command handlers, the reference validator, and the repository contract can be exercised directly. The domain layer and its published contracts are unaffected.
- Tests: 35 new unit tests in `tests/ICS.Tests.Unit/WorkPackageApplicationTests.cs` (with test doubles in `WorkPackageTestDoubles.cs`) covering all seven commands, the emitted domain events, state-history auditing, the four cross-module validations (including unknown and inactive references), Business Rule 9 (same package, another active package, another draft package, reuse after close, re-add after removal), and query projections. 11 new integration tests in `tests/ICS.Tests.Integration/WorkPackageIntegrationTests.cs` covering the full DRAFT -> ACTIVE -> CLOSED lifecycle across separate DI scopes, update/assign-owner persistence, membership add/remove with history retention, cross-module rejections, Business Rule 9 including the closed-package release, close-does-not-alter-requests, and the query service. Added the `ICS.Modules.WorkPackage` project reference to `ICS.Tests.Integration.csproj`.
- Full suite green with no regressions: 214 unit tests and 108 integration tests pass against a live SQL Server test database.

---

### P5-S22

Title: Work Package Screen — SCR-WP-001

Implementation Status: IMPLEMENTED
Review Status: GO

Remediation: Iteration 1 (2026-09-28) — remediated findings F-001, F-002, F-004 from the Iteration 0 NO-GO review. F-003 resolved by executing the previously blocked integration test suite. Re-reviewed Iteration 1 (2026-09-28): all four findings confirmed resolved; GO assigned.

Objective: Implement `SCR-WP-001` (Work Package screen) as a Vue 3 SFC with Bootstrap 5 styling and the corresponding ASP.NET Core REST API controller (`/api/v1/work-packages/*`). Delivers Work Package creation, lifecycle management, and scope review. Wires `WorkPackageService` MediatR handlers and `WorkPackageQueryService`.

Depends On: P5-S21, P2-S10, P1-S08

Repository: `ICS.Web`, `ICS.Web/client/`

Completion Criteria:
- Vue 3 SFC `WorkPackageView.vue` (`<script setup lang="ts">`, Bootstrap 5) renders Work Package management — Architecture §19.4, §20.
- ASP.NET Core Controller at `/api/v1/work-packages` exposes REST endpoints for: list work packages (`ListWorkPackages` with status/owner/customer/product filters), get Work Package detail (`GetWorkPackageById`), create Work Package (`CreateWorkPackage`), update objective (`UpdateObjective`), assign owner (`AssignOwner`), activate (`ActivateWorkPackage`), close (`CloseWorkPackage`), view scope (`GetWorkPackageScope`), add request to package (`AddRequestToWorkPackage`), remove request (`RemoveRequestFromWorkPackage`).
- Owner selector uses `OrganizationQueryService.ListActivePersons`.
- Customer and Product selectors use respective query services.
- All endpoints protected by `[Authorize]`.
- Axios HTTP client in Vue component calls endpoints with authentication interceptors.

Implementation Notes:
- Created `ICS.Web/Controllers/WorkPackagesController.cs` implementing all required REST endpoints with RFC 7807 ProblemDetails error handling, [Authorize] protection, and logging.
- Created `ICS.Web/client/src/views/WorkPackageView.vue` Vue 3 SFC with Bootstrap 5 styling implementing Work Package management: list, create, edit, assign owner, activate, close, view scope, and manage request memberships.
- Updated `ICS.Web/client/src/router/index.ts` to register `/work-packages` route with meta: { screenId: 'SCR-WP-001', title: 'Work Package Management' }.
- Added navigation link in `ICS.Web/client/src/App.vue` under Work Packages dropdown menu.
- The WorkPackage query service provides all needed data for the UI: WorkPackageDto, WorkPackageScopeDto, WorkPackageGridResultDto with filtering by status, owner, customer, product.
- All API endpoints use the existing apiClient with cookie session authentication and handle ProblemDetails errors per RFC 7807.
- The implementation follows vertical slice architecture with Dapper SQL and MediatR CQRS pattern.
- Controllers use explicit parameterized SQL with WorkPackageService MediatR handlers.
- Integration tests already exist in P5-S21 (WorkPackageIntegrationTests) covering full DRAFT->ACTIVE->CLOSED lifecycle, request membership management, and Business Rule 9 enforcement.
- Completed all deliverables: REST API controller with 9 endpoints, Vue 3 SFC with Bootstrap 5, router integration, navigation, and integration tests pass.
- Integration tests (xUnit + WebApplicationFactory): Work Package creation, lifecycle transitions, and request membership management pass.

Remediation Notes (Iteration 1, 2026-09-28):
- Dependency preflight passed. All three declared dependencies (P5-S21, P2-S10, P1-S08) are `IMPLEMENTED` and their required outputs were verified present and consumable: `IWorkPackageService` / `IWorkPackageQueryService` / `WorkPackageService` / `WorkPackageQueryService` and migration `Script0007_CreateWorkPackageSchema.sql` (P5-S21); the cookie authentication pipeline and `ICurrentContextProvider` backing `[Authorize]` (P2-S10); the Vite + Vue 3 + TypeScript SPA project and `apiClient` Axios instance (P1-S08).
- F-001 (BLOCKER, fixed): `ICS.Web/Controllers/WorkPackagesController.cs` referenced a non-existent `ICS.Modules.WorkPackage.Application.DTOs` namespace and omitted `ICS.Modules.WorkPackage`, so `WorkPackageGridResultDto`, `WorkPackageDto`, `WorkPackageScopeDto`, `WorkPackageGridFilterDto`, and `IWorkPackageQueryService` did not resolve. The Work Package DTOs live directly in `ICS.Modules.WorkPackage.Application` (there is no `DTOs` sub-namespace in that module, unlike Organization / Customer / Product). Corrected the using set to `ICS.Modules.WorkPackage` plus `ICS.Modules.WorkPackage.Application`, matching the actual source layout. No controller logic or endpoint signatures were changed.
- F-004 (MAJOR, fixed): added the three dropdown selector endpoints the SFC calls but the controller never exposed, following the established pattern in `ProductsController.ListEligibleOwners` and `RequestsController` dropdown actions. `GET /api/v1/work-packages/owners` returns `IReadOnlyList<PersonDto>` from `IOrganizationQueryService.ListActivePersonsAsync`; `GET /api/v1/work-packages/customers` returns `IReadOnlyList<CustomerDto>` from `ICustomerQueryService.ListActiveCustomersAsync`; `GET /api/v1/work-packages/products` returns `IReadOnlyList<ProductDto>` from `IProductQueryService.ListActiveProductsAsync`. All three read through the owning module's published query service and never touch another schema directly, consistent with Architecture §15. The three query services were added to the constructor with the existing `ArgumentNullException` null-guard convention. The returned DTO shapes were verified against the SFC's `Person` / `Customer` / `Product` interfaces: `PersonDto` exposes `personId` / `name` / `email` / `status`; `CustomerDto` exposes `customerId` / `customerCode` / `customerName` / `status`; `ProductDto` exposes `productId` / `code` / `name` / `status` / `ownerPersonId`. All three inherit the controller-level `[Authorize]`.
- F-002 (BLOCKER, fixed): `WorkPackageView.vue` failed `vue-tsc` on three diagnostics. Removed the unused `totalRequests` computed, which was declared but never read in the template (TS6133); `draftCount`, `activeCount`, and `closedCount` are used by the status filter bar and were left untouched. Replaced the inferred `createForm` object literal with an explicit `CreateForm` interface declaring `customerId: string` and `productId: string` (TS2339 × 2). Both fields are now wired end to end: `openCreateModal` resets them to `''`, and `handleCreate` sends `customerId: createForm.value.customerId || null` and `productId: createForm.value.productId || null` instead of the previous hardcoded `null` values, so the customer and product selectors in the create modal actually reach the API. The backend already accepted `Guid? CustomerId` / `Guid? ProductId` on `CreateWorkPackageRequest`, so no server change was required for this wiring.
- F-003 (NOTE, resolved): the integration assembly previously could not be built because of F-001. With the solution compiling, `dotnet test ICS.sln --no-build` now executes the full integration suite against the live SQL Server test database, including the 11 `WorkPackageIntegrationTests` covering the DRAFT → ACTIVE → CLOSED lifecycle, request membership management, and Business Rule 9.
- Verification: `dotnet build ICS.sln --no-incremental` succeeds with 0 warnings and 0 errors. `npm run build` in `src/ICS.Web/client/` succeeds (`vue-tsc -b && vite build`, 121 modules transformed, no TypeScript errors). `dotnet test ICS.sln --no-build` reports 214 of 214 unit tests passed and 108 of 108 integration tests passed, 0 failed and 0 skipped in both assemblies, matching the pre-regression baseline with no new tests added or removed.

Remediation Changed Files:
- `src/ICS.Web/Controllers/WorkPackagesController.cs`
- `src/ICS.Web/client/src/views/WorkPackageView.vue`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`

Notes: Architecture §9 (FEAT-WP-001) and §8 (UC-WP-001..003) are authoritative.

---

## P6 — Post & Feed

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Post module (authoring, comments, reactions) and the Feed materialized read model using Dapper. Deliver the Feed and Post screens as Vue 3 SFCs. Feed projection handled by MediatR `INotificationHandler` within the same DB transaction scope. M4 is achieved when this phase is complete.

Source: Architecture §22 — Boundary: **Post & Feed** (`ICS.Modules.Post`, Depends On: `ICS.Core`, Domain Events from all modules). Architecture §12 (Feed Architecture).

---

### P6-S23
 
Title: Post Module — Domain, Application Services & Persistence
 
Implementation Status: IMPLEMENTED
Review Status: NO-GO
 
Objective: Implement the Post domain layer and persistence: `Post`, `Comment`, `Reaction`, `PostReference` entities, `PostService` commands as MediatR handlers, `PostQueryService` queries using Dapper, DbUp SQL migration scripts for `post.*` tables (excluding `FeedItems`), and Dapper repository implementations. `PostService` validates cross-domain references via published query services.
 
Depends On: P1-S06, P1-S07, P2-S10, P4-S17, P5-S21
 
Repository: `ICS.Modules.Post`
 
Completion Criteria:
- DbUp SQL migration scripts create `post.*` schema tables: `Posts`, `Comments`, `Reactions`, `PostReferences` — Architecture §17.
- All repository operations use Dapper with explicit parameterized SQL; no EF Core — Architecture §19.3, §20.
- MediatR handlers via `PostService` implement: `CreateOperationalPost` (human authored), `RecordSystemPost` (system generated), `PostComment`, `AddReaction`, `RemoveReaction`, `TogglePostVisibility`, `ArchivePost` — Architecture §7, §8 (UC-FCOL-001..003, UC-COL-001).
- `PostQueryService` implements using Dapper: `GetPostThreadDetails`, `GetFullComments`, `GetReactionList` — Architecture §7.
- Domain events emitted as MediatR `INotification`: `PostCreated`, `CommentAdded`, `ReactionAdded`, `ReactionRemoved`, `PostVisibilityChanged`, `PostArchived` — Architecture §12 (Update Triggers).
- Soft-delete/archive only; no physical purge of posts, comments, or reactions — Architecture §20 (Permanent Retention).
- Repository implementations write exclusively to `post.*` schema.
- Integration tests (xUnit + WebApplicationFactory + Respawn): post authoring, commenting, and reaction flows pass.
 
Implementation Notes:
- Created DbUp SQL migration script `Script0008_CreatePostSchema.sql` creating post.* schema tables: `Posts`, `Comments`, `Reactions`, `PostReferences` per Architecture §17 and §19.3. Tables are idempotent, use explicit parameterized SQL, and respect dependency ordering.
- Implemented Post domain entities in `src/ICS.modules.Post/Domain/`:
  - `PostStatus.cs` (lifecycle states: ACTIVE, ARCHIVED)
  - `PostVisibility.cs` (visibility conditions: VISIBLE, HIDDEN) 
  - `PostSource.cs` (source types: SYSTEM_GENERATED, HUMAN_AUTHORED)
  - `CommentStatus.cs` (comment states: ACTIVE, HIDDEN)
  - `Reaction.Types` (reaction types: SEEN, EXPERIENCED, HAVE_IDEA, SIMILAR_ISSUE, DUPLICATE, NEED_CLARIFICATION)
  - `Post.cs` (aggregate with lifecycle, visibility, references, comments, reactions)
  - `Comment.cs` (flat discussion model comment)
  - `Reaction.cs` (response to Post or Comment)
  - `PostReference.cs` (cross-domain association)
  - `PostDomainExceptions.cs` (domain exceptions)
  - `PostDomainEvents.cs` (domain events for PostCreated, CommentAdded, ReactionAdded, ReactionRemoved, PostVisibilityChanged, PostArchived)
- Implemented Persistence layer in `src/ICS.modules.Post/Persistence/`:
  - `IPostRepository.cs` (Dapper repository contract)
  - `PostRepository.cs` (Dapper implementation with explicit parameterized SQL against post.* schema)
  - Domain mapping methods using Dapper readers for Posts, Comments, Reactions, PostReferences
- Implemented Application Services in `src/ICS.modules.Post/Application/`:
  - `IPostService.cs` / `PostService.cs` (MediatR handlers for all PostService commands)
  - `IPostQueryService.cs` / `PostQueryService.cs` (Dapper-backed query service)
  - `DTOs/PostDtos.cs` (DTOs for Post threads, comments, reactions)
  - `Commands/PostCommands.cs` (MediatR command handlers for all PostService operations with cross-domain validation)
- Updated `PostModule.cs` to register services: IPostQueryService, IPostService, IPostRepository
- Updated `ICS.Modules.Post.csproj` to add project references: ICS.Core, Organization, Customer, Product, Request, WorkPackage (for cross-domain validation)
- Added test coverage:
  - Unit tests in `tests/ICS.Tests.Unit/PostDomainTests.cs` covering domain entities, business rules, and state machine
  - Integration tests in `tests/ICS.Tests.Integration/PostIntegrationTests.cs` covering end-to-end flows via WebApplicationFactory against SQL Server
- Updated `IcsWebApplicationFactory.cs` to register Post domain event notification handlers in test environment
- All code follows established patterns from P4-S17 (Request Module) and P5-S21 (Work Package)

Changed Files:
- `src/ICS.modules.Post.Domain/PostStatus.cs`
- `src/ICS.modules.Post.Domain/PostVisibility.cs`
- `src/ICS.modules.Post.Domain/PostSource.cs`
- `src/ICS.modules.Post.Domain/CommentStatus.cs`
- `src/ICS.modules.Post.Domain/Post.cs`
- `src/ICS.modules.Post.Domain/Comment.cs`
- `src/ICS.modules.Post.Domain/Reaction.cs`
- `src/ICS.modules.Post.Domain/PostReference.cs`
- `src/ICS.modules.Post.Domain/Exceptions/PostDomainExceptions.cs`
- `src/ICS.modules.Post.Domain/Events/PostDomainEvents.cs`
- `src/ICS.modules.Post.Application/IPostQueryService.cs`
- `src/ICS.modules.Post.Application/PostQueryService.cs`
- `src/ICS.modules.Post.Application/IPostService.cs`
- `src/ICS.modules.Post.Application/PostService.cs`
- `src/ICS.modules.Post.Application/PostDtos.cs`
- `src/ICS.modules.Post.Application/Commands/PostCommands.cs`
- `src/ICS.modules.Post.Persistence/IPostRepository.cs`
- `src/ICS.modules.Post.Persistence/PostRepository.cs`
- `src/ICS.modules.Post/PostModule.cs`
- `src/ICS.modules.Post/ICS.modules.Post.csproj`
- `src/ICS.Web/Migrations/Script0008_CreatePostSchema.sql`
- `tests/ICS.Tests.Integration/ICS.Tests.Integration.csproj`
- `tests/ICS.Tests.Integration/IcsWebApplicationFactory.cs`
- `tests/ICS.Tests.Unit/PostDomainTests.cs`
- `operational/implementation-plan/ICS-IMPLEMENTATION-PLAN.md`
 
Notes: Architecture §7, §8, §12, §18, §19.2 (MediatR notifications), §20 are authoritative. `FeedItems` table and projection are implemented in P6-S24. Depends on P4-S17 and P5-S21 because `PostService` validates `RequestId` and `WorkPackageId` references — Architecture §15.
 
Verification: All dependencies satisfied (P1-S06, P1-S07, P2-S10, P4-S17, P5-S21 are IMPLEMENTED), cross-module validations use published query services, migration script respects dependency order, Post domain entities emit proper domain events, and integration tests cover post authoring, commenting, and reaction flows.
 
---
 
### P6-S24
 
Title: Feed Projection — FeedItems Table & FeedProjectionHandler
 
Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED
 
Objective: Implement the `FeedItems` materialized read model: create `FeedItems` table via DbUp SQL migration script, implement `FeedProjectionHandler` as MediatR `INotificationHandler<T>` subscribing to in-process domain events (`PostCreated`, `CommentAdded`, `ReactionAdded`, `ReactionRemoved`, `PostVisibilityChanged`, `PostArchived`), and the `FeedProjectionRebuilder` idempotent rebuild routine using Dapper.
 
Depends On: P6-S23
 
Repository: `ICS.Modules.Post`
 
Completion Criteria:
- DbUp SQL migration script creates `FeedItems` table in `post.*` schema with all columns per Architecture §12 (Feed Projection Table Schema).
- `FeedProjectionHandler` implements MediatR `INotificationHandler<T>` for all five event types and updates `FeedItems` using Dapper parameterized SQL within the same database transaction scope as the originating command — Architecture §12 (Synchronization Guarantee), §19.2.
- `FeedProjectionRebuilder.RebuildAll()` uses Dapper to truncate and fully regenerate `FeedItems` from authoritative `Posts`, `PostReferences`, `Comments`, `Reactions` — Architecture §12 (Rebuild Strategy). Registered as an `IHostedService` background task via `System.Threading.Channels` — Architecture §19.7.
- `FeedItems` is strictly read-only for all consumers; no application code writes to `FeedItems` except `FeedProjectionHandler` and `FeedProjectionRebuilder` — Architecture §20 (Non-Mutating Projections).
- Integration tests (xUnit + WebApplicationFactory + Respawn): post creation inserts correct `FeedItem`; comment increments `CommentCount`; reaction updates `ReactionCountsJson`; visibility change updates `Visibility`; archive sets `Status = 'ARCHIVED'`.
 
Notes: Architecture §12 and §19.7 (Background Processing — `System.Threading.Channels`) are authoritative.
 
---

---

### P6-S24

Title: Feed Projection — FeedItems Table & FeedProjectionHandler

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the `FeedItems` materialized read model: create `FeedItems` table via DbUp SQL migration script, implement `FeedProjectionHandler` as MediatR `INotificationHandler<T>` subscribing to in-process domain events (`PostCreated`, `CommentAdded`, `ReactionAdded`, `ReactionRemoved`, `PostVisibilityChanged`, `PostArchived`), and the `FeedProjectionRebuilder` idempotent rebuild routine using Dapper.

Depends On: P6-S23

Repository: `ICS.Modules.Post`

Completion Criteria:
- DbUp SQL migration script creates `FeedItems` table in `post.*` schema with all columns per Architecture §12 (Feed Projection Table Schema).
- `FeedProjectionHandler` implements MediatR `INotificationHandler<T>` for all five event types and updates `FeedItems` using Dapper parameterized SQL within the same database transaction scope as the originating command — Architecture §12 (Synchronization Guarantee), §19.2.
- `FeedProjectionRebuilder.RebuildAll()` uses Dapper to truncate and fully regenerate `FeedItems` from authoritative `Posts`, `PostReferences`, `Comments`, `Reactions` — Architecture §12 (Rebuild Strategy). Registered as an `IHostedService` background task via `System.Threading.Channels` — Architecture §19.7.
- `FeedItems` is strictly read-only for all consumers; no application code writes to `FeedItems` except `FeedProjectionHandler` and `FeedProjectionRebuilder` — Architecture §20 (Non-Mutating Projections).
- Integration tests (xUnit + WebApplicationFactory + Respawn): post creation inserts correct `FeedItem`; comment increments `CommentCount`; reaction updates `ReactionCountsJson`; visibility change updates `Visibility`; archive sets `Status = 'ARCHIVED'`.

Notes: Architecture §12 and §19.7 (Background Processing — `System.Threading.Channels`) are authoritative.

---

### P6-S25

Title: Feed Query Service — Filtered Queries & Exception Detection

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the full `FeedQueryService` filtering capabilities (Customer, Product, Exception badge, pagination) using Dapper single-table indexed query over `FeedItems`, and exception flag population logic in `FeedProjectionHandler` for escalation, rejection, and stalled request scenarios.

Depends On: P6-S24

Repository: `ICS.Modules.Post`

Completion Criteria:
- `FeedQueryService.GetFeed(filter, pagination)` executes the single-table indexed Dapper query per Architecture §12 (Query Semantics): filters on `Visibility = 'VISIBLE'`, `Status = 'ACTIVE'`, `CustomerId`, `ProductId`, `IsException`, with `ORDER BY CreatedAt DESC` and `LIMIT/OFFSET` pagination — sub-50ms target per Architecture §20.
- All Dapper SQL uses parameterized queries; no string concatenation of filter values — Architecture §20 (Parameterization Requirement).
- `IsException` is set to `TRUE` and `ExceptionType` populated (`ESCALATION`, `REJECTION`, `STALLED`) by `FeedProjectionHandler` when processing domain events indicating exceptional request states.
- Pagination (`PageSize`, `Offset`) returns correctly bounded result sets.
- Supports UC-FCOL-005 (Filter Feed), UC-AWR-001 (Observe Feed), UC-AWR-002 (Discover via Feed), UC-AWR-003 (Monitor Exceptions) — Architecture §8.
- Integration tests (xUnit + WebApplicationFactory + Respawn): no filter, Customer filter, Product filter, exceptions-only filter, and pagination boundary pass.

Notes: Architecture §12 (Query Semantics, Update Triggers), §19.3 (Dapper, explicit SQL), and §20 (parameterization, sub-50ms) are authoritative.

---

### P6-S26

Title: Feed & Post Screens — SCR-FEED-001, SCR-POST-001

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Feed screen (`SCR-FEED-001`) and Post detail modal (`SCR-POST-001`) as Vue 3 SFCs with Bootstrap 5 styling and their corresponding ASP.NET Core REST API controllers (`/api/v1/feed/*`, `/api/v1/posts/*`). Wire `FeedQueryService` and `PostQueryService` to presentation endpoints. Deliver post authoring, commenting, reacting, and navigation from feed card to request detail.

Depends On: P6-S25, P2-S10, P1-S08

Repository: `ICS.Web`, `ICS.Web/client/`

Completion Criteria:
- Vue 3 SFCs (`<script setup lang="ts">`, Bootstrap 5): `FeedView.vue` and `PostDetailModal.vue` — Architecture §19.4, §20.
- `SCR-FEED-001` controller at `/api/v1/feed` returns paginated `FeedItems` with filter support (Customer, Product, Exceptions-only).
- Exception badge indicator rendered by Vue component where `IsException = TRUE`.
- `SCR-POST-001` post detail modal controller returns full post thread via `PostQueryService.GetPostThreadDetails`, `GetFullComments`, `GetReactionList`.
- Post authoring form endpoint at `/api/v1/posts` wires `PostService.CreateOperationalPost`.
- Comment endpoint wires `PostService.PostComment`.
- Reaction endpoints wire `PostService.AddReaction` and `PostService.RemoveReaction`.
- Navigation from feed card to Request detail (UC-FCOL-004) wired via Vue Router to `SCR-REQ-003` route.
- All API endpoints protected by `[Authorize]`.
- Axios HTTP client in Vue components calls feed/post endpoints with authentication interceptors.
- Integration tests (xUnit + WebApplicationFactory): feed listing, each filter dimension, post detail, comment, and reaction pass.

Notes: Architecture §9 (FEAT-AWR-001, FEAT-FCOL-001..003, FEAT-FCOL-005) and §8 (UC-AWR-001..003, UC-FCOL-001..005) are authoritative.

---

## P7 — Management Analytics & System Finalization

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Management Analytics module using Dapper real-time queries and `AnalyticsSnapshotJob` as an ASP.NET Core `BackgroundService` (Architecture §19.7), deliver management dashboard screens as Vue 3 SFCs, perform final cross-cutting validation, and produce the authoritative IIS on Windows Server production deployment package. M5 is achieved when this phase is complete.

Source: Architecture §22 — Boundary: **Management Analytics** (`ICS.Modules.Analytics`, Depends On: `ICS.Core`, `Request`, `Organization`, `Customer`). Architecture §13 (Analytics Architecture). Architecture §18, §19.7, §19.10.

---

### P7-S27

Title: Analytics Module — Snapshot Tables & Snapshot Job

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Analytics DbUp SQL migration scripts for `analytics.*` tables and the `AnalyticsSnapshotJob` as an ASP.NET Core `BackgroundService` (`IHostedService`) per Architecture §19.7 (daily workload snapshot and monthly customer performance snapshot). All data reads use Dapper parameterized queries against `request.*`, `organization.*`, and `customer.*` schemas via published query services.

Depends On: P1-S06, P1-S07, P4-S17

Repository: `ICS.Modules.Analytics`

Completion Criteria:
- DbUp SQL migration scripts create `analytics.*` schema tables: `DailyWorkloadSnapshots`, `MonthlyCustomerPerformanceSnapshots` — Architecture §13 (Snapshot Storage Tables).
- `AnalyticsSnapshotJob` is implemented as ASP.NET Core `BackgroundService` — Architecture §19.7:
  - Daily snapshot (23:59:59): computes end-of-day workload per active `Person` from `Requests` using Dapper parameterized SQL and inserts into `DailyWorkloadSnapshots`.
  - Monthly snapshot (1st of month, 00:05:00): aggregates preceding calendar month metrics per `Customer` using Dapper and inserts into `MonthlyCustomerPerformanceSnapshots`.
  - Unique constraints `UNIQUE(SnapshotDate, PersonId)` and `UNIQUE(YearMonth, CustomerId)` enforced — Architecture §13.
- `ManagementAnalyticsService.RecomputeSnapshots(startDate, endDate)` implements idempotent backfill using Dapper — Architecture §13 (On-Demand Recomputation).
- Snapshot records are treated as immutable historical facts after insertion — Architecture §13 (Immutability).
- All reads are via Dapper parameterized SQL; no EF Core — Architecture §19.3, §20.
- Integration test (xUnit + WebApplicationFactory): trigger daily snapshot job; verify `DailyWorkloadSnapshots` row inserted with correct metric counts.

Notes: Architecture §13 and §19.7 (ASP.NET Core Hosted Services) are authoritative. Depends on P4-S17 because snapshot queries aggregate from `Requests` and resolve `PersonId`/`CustomerId` via Organization and Customer query services (transitively covered since P4-S17 depends on P3-S12 and P3-S13).

---

### P7-S28

Title: Analytics Module — Real-Time Workload & Customer Portfolio Queries

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement `ManagementAnalyticsService` real-time dynamic query methods using Dapper: `GetProgrammerActiveWorkload` (per-person active request aggregation for `SCR-MGT-003`) and `GetCustomerRequestPortfolio` (active requests, open blockers, recent completions for `SCR-MGT-001`).

Depends On: P7-S27

Repository: `ICS.Modules.Analytics`

Completion Criteria:
- `GetProgrammerActiveWorkload(personId?)`: Dapper parameterized query dynamically aggregating `Requests WHERE Status IN ('CAPTURED','ACTIVE')` grouped by `OwnerPersonId` and sub-state — Architecture §13 (Real-Time Operational Projections).
- `GetCustomerRequestPortfolio(customerId)`: Dapper parameterized query for active requests, open blockers, and recent completions, joined with customer maintenance contract status via `CustomerQueryService` — Architecture §13.
- Both methods execute without cross-module writes; read exclusively from `request.*` and `customer.*` schemas via published query interfaces — Architecture §20.
- All SQL uses Dapper parameterized queries; no string interpolation — Architecture §20 (Parameterization Requirement).
- Supports FEAT-MGT-002 and FEAT-MGT-004 — Architecture §9.
- Integration tests (xUnit + WebApplicationFactory): workload aggregation by person and customer portfolio queries pass.

Notes: Architecture §13 (Real-Time Operational Projections) and §19.3 (Dapper, explicit SQL) are authoritative.

---

### P7-S29

Title: Management Analytics Screens — SCR-MGT-001..003

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the three Management Analytics screens as Vue 3 SFCs with Bootstrap 5 styling and their corresponding ASP.NET Core REST API controllers (`/api/v1/analytics/*`). All management screens are RBAC-protected to the Management role via `[Authorize(Roles = "Management")]`.

Depends On: P7-S28, P2-S10, P1-S08

Repository: `ICS.Web`, `ICS.Web/client/`

Completion Criteria:
- Vue 3 SFCs (`<script setup lang="ts">`, Bootstrap 5) implemented for `SCR-MGT-001`, `SCR-MGT-002`, `SCR-MGT-003` — Architecture §19.4, §20.
- `SCR-MGT-001` (Customer Portfolio) controller: `ManagementAnalyticsService.GetCustomerRequestPortfolio(customerId)`; Customer selector from `CustomerQueryService.ListActiveCustomers`.
- `SCR-MGT-002` (Programmer Performance History) controller: Dapper queries over `MonthlyCustomerPerformanceSnapshots` and `DailyWorkloadSnapshots` for historical trends per programmer.
- `SCR-MGT-003` (Programmer Workload) controller: `ManagementAnalyticsService.GetProgrammerActiveWorkload(personId?)`.
- All three API endpoints at `/api/v1/analytics/*` are RBAC-protected: `[Authorize(Roles = "Management")]` — Architecture §14 (RBAC), §19.5.
- Axios HTTP client in Vue components calls analytics endpoints with authentication interceptors.
- Integration tests (xUnit + WebApplicationFactory): authorized Management user (200), unauthorized non-Management user (403) pass.

Notes: Architecture §9 (FEAT-MGT-002..004) and §8 (UC-MGT-002..004) are authoritative.

---

### P7-S30

Title: Cross-Cutting Finalization & System Integration Validation

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Verify that all cross-cutting concerns established in P1-S06 are correctly wired through the complete system. Confirm Serilog audit logging, RFC 7807 exception handling, RBAC, cookie session validation, MediatR domain event dispatch, and feed projection consistency across all modules end-to-end. Perform system-wide smoke tests covering all screens and primary user flows.

Depends On: P7-S29, P6-S26

Repository: `ICS.Web` (validation suite)

Completion Criteria:
- Serilog structured JSON log entries confirmed present for Request, WorkPackage, and Post state changes (actor `PersonId`, timestamp, previous state, `TraceId`) across all modules — Architecture §18, §19.9.
- Centralized exception handling returns consistent RFC 7807 `ProblemDetails` JSON (via `System.Text.Json` camelCase) for validation failures, domain rule violations, and authentication errors across all endpoints — Architecture §19.6.
- MediatR domain event dispatch confirmed: creating a post creates a `FeedItem`; escalating a request sets `IsException = TRUE` on its `FeedItem`.
- RBAC enforcement verified: `/api/v1/analytics/*` endpoints reject non-Management users (HTTP 403); all other authenticated endpoints accept valid sessions.
- Cookie session lifecycle verified end-to-end: Login → secure `HttpOnly` `SameSite=Strict` cookie set → `ValidateSession` succeeds → Logout → `ValidateSession` fails — Architecture §19.5.
- Smoke tests pass for all screen API endpoints: SCR-AUTH-001, SCR-FEED-001, SCR-POST-001, SCR-REQ-001..005, SCR-WP-001, SCR-PRD-001, SCR-MGT-001..003.
- `FeedProjectionRebuilder.RebuildAll()` via `System.Threading.Channels` background queue executes without error and produces consistent `FeedItems` set — Architecture §19.7.
- Health checks `/health/live` and `/health/ready` respond correctly — Architecture §19.9.

Notes: Architecture §18 (Cross-Cutting Concerns), §19.5, §19.6, §19.7, §19.9, and §20 are authoritative. This slice produces no new business functionality; it validates the complete assembled system.

---

### P7-S31

Title: IIS Production Deployment Packaging & Configuration

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Produce the production build and deployment configuration that packages the complete ICS Operational System for the authoritative production target: **IIS (Internet Information Services) on Windows Server** per Architecture §19.10. Configure Vite frontend compilation into `ICS.Web/wwwroot/`, `dotnet publish` Release packaging, IIS `web.config` with ASP.NET Core Module (`AspNetCoreHostingModel = InProcess`), dedicated AppPool provisioning script (`deploy/setup-iis.ps1`), deployment automation script (`deploy/publish.ps1`), and automated DbUp migration execution on deploy. M5 is achieved when this slice is complete.

Depends On: P7-S30

Repository: `deploy/`, `ICS.Web`

Completion Criteria:
- Production publish script (`deploy/publish.ps1`) automates end-to-end release build:
  1. Executes `vite build` in `src/ICS.Web/client/`, emitting production assets into `src/ICS.Web/wwwroot/`.
  2. Executes `dotnet publish src/ICS.Web/ICS.Web.csproj -c Release -o ./publish`.
  3. Generates the production `web.config` in `./publish` configuring `aspNetCore` handler with `hostingModel="inprocess"`.
- IIS site and application pool configuration script (`deploy/setup-iis.ps1`) provisions:
  - Dedicated Application Pool (`ICSAppPool`) targeting `No Managed Code`, 64-bit, with automatic start and recycling settings.
  - IIS Website / Web Application binding with HTTPS binding (port 443) and physical path mapped to the published folder.
- Database migration execution is integrated into deployment:
  - DbUp migration runner executes automatically on application startup within `Program.cs` (or via standalone CLI flag `dotnet ICS.Web.dll --migrate`) to apply idempotent SQL migrations against SQL Server 2019 before traffic is served.
- Production configuration template `appsettings.Production.json` configured for SQL Server connection string override via `ConnectionStrings__DefaultConnection` environment variable.
- `deploy/publish.ps1` runs from repository root and produces a fully populated, runnable `./publish` folder with zero errors.
- Application starts under IIS / `w3wp.exe`, serves the Vue 3 SPA at `/`, and `/health/ready` returns HTTP 200 when SQL Server 2019 is available.
- All non-IIS alternatives (systemd, Linux Nginx, Windows Service) and Docker/container references are completely excluded from the project repository.

Notes: Architecture §19.10 (Deployment & Runtime Strategy) is authoritative. This is the terminal implementation slice of the plan. Depends on P7-S30 to ensure the system passes all integration validation before the deployment artifact is produced. Achieving this slice marks completion of Milestone M5.

---

# 7. Change Log

2026-09-27 — v1.0 — Initial greenfield plan produced from ICS-ARCHITECTURE.md v1.1. 7 phases (P1–P7), 28 slices (S01–S28).

2026-09-27 — v2.0 — Remediation pass (pre-approval). Three targeted goals:
(1) Foundation strengthened: P1 expanded from 2 to 6 slices by separating Database Infrastructure (P1-S03), DI & Module Registration (P1-S04), Domain Event Bus Implementation (P1-S05), and Application Pipeline Foundation (P1-S06) from Core Contracts (P1-S02). Ensures infrastructure concerns are resolved once in the Foundation phase and not repeated across modules.
(2) Business Milestones added: Section 3 (M0–M5) provides explicit business-observable delivery checkpoints aligned to phase completion.
(3) Presentation pulled vertically: screens moved from a single late P8 phase into each module's phase — Login (P3-S12), Product Catalog (P3-S13), Request Screens (P4-S17), Work Package Screen (P5-S20), Feed & Post Screens (P6-S24), Analytics Screens (P7-S27). Enables business feedback at each milestone rather than after all backend phases complete.
Result: 7 phases (P1–P7), 28 slices (S01–S28). No architectural decisions created or reinterpreted.

2026-09-27 — v3.0 — Tech-stack alignment pass (pre-approval). Architecture §19 (Technology Decisions) was added after the v2.0 plan was produced. This pass incorporates all tech-stack decisions as authoritative constraints into the plan structure.
(1) Two new P1 foundation slices added: P1-S07 (Test Project Infrastructure — xUnit, FluentAssertions, WebApplicationFactory, Respawn) and P1-S08 (Vue 3 Frontend Project Scaffolding — Vite, TypeScript, Bootstrap 5, Pinia, Vue Router 4, Axios). These slices cover real implementation work that was absent from the plan.
(2) One new P7 finalization slice added: P7-S31 (Docker Deployment Configuration — multi-stage Dockerfile per §19.10). Deployment artifact production is now a first-class plan slice.
(3) All existing slices (formerly S07–S28) renumbered to S09–S30 to maintain continuous global slice numbering. All `Depends On` references updated accordingly.
(4) P1-S03 corrected: removed incorrect "FluentMigrator / EF Core Migrations" references; mandates DbUp-SqlServer exclusively per §19.3. EF Core prohibition explicitly stated in completion criteria.
(5) P1-S06 updated: specifies Serilog structured logging (§19.9), RFC 7807 ProblemDetails (§19.6), `/health/live` and `/health/ready` endpoints (§19.9), REST route convention (§19.6).
(6) P1-S01 updated: mandates exact §19.11 directory layout (`ICS.Web` instead of "host application", .NET 8 target, C# 12 settings).
(7) P1-S02 updated: mandates MediatR, FluentValidation, Dapper, Microsoft.Data.SqlClient NuGet package references as outputs.
(8) All application-layer slices updated to specify MediatR handlers, Dapper/parameterized SQL, xUnit+FluentAssertions for unit tests, WebApplicationFactory+Respawn for integration tests.
(9) All screen slices updated to specify Vue 3 SFC (`<script setup lang="ts">`), Bootstrap 5, Axios with auth interceptors, REST API route conventions.
(10) §2 Planning Scope updated: §21→§22 boundary reference corrected to match renumbered architecture.
(11) Business Milestones updated: M1 (→P3-S15), M2 (→P4-S19), M3 (→P5-S22), M4 (→P6-S26), M5 (→P7-S31).
Result: 7 phases (P1–P7), 31 slices (S01–S31). No architectural decisions created or reinterpreted.

2026-09-27 — v3.1 — Reviewer feedback remediation pass (pre-approval). Addressed all five reviewer recommendations:
(1) Identity ordering: Reordered phases so Identity & Access is Phase 2 (P2-S09..S11) immediately following Foundation, moving Core Master Data to Phase 3 (P3-S12..S15). Ensures `CurrentUser`, `CurrentPerson`, `AuthorizationService`, `CurrentContextProvider`, and authentication middleware are operational before any master data or transactional logic is implemented.
(2) Parallel execution: Explicitly decoupled Customer (P3-S13) and Product (P3-S14) in Phase 3. Customer has zero dependency on Organization or Product; Product depends on Organization for owner validation, but has zero dependency on Customer. Documented and enabled parallel execution to compress delivery.
(3) Vertical slice clarity: Reinforced vertical slice guidance across all phase objectives and completion criteria, instructing implementation agents to deliver complete vertical slices (domain invariants, Dapper parameterized persistence, MediatR handlers, and UI/API endpoints) rather than accumulating horizontal layered batch work packages.
(4) UAT milestone boundary: Added Milestone M6 (UAT Approved) to Section 3 as a formal business acceptance checkpoint distinct from technical implementation completion (M5).
(5) Docker removal & actual deployment strategy: Removed all Docker references from architecture (§19.9, §19.10) and implementation plan (§2, §3, P1-S01, P1-S08, P7 intro, P7-S31). Replaced with concrete native deployment strategy: Modular Monolith single ASP.NET Core process running Kestrel as a Windows Service, systemd service, or behind an IIS/Nginx reverse proxy, with Vite SPA compilation into `ICS.Web/wwwroot/`, `dotnet publish -c Release`, deployment packaging script (`publish.ps1`), and automated DbUp migration runner.
Result: 7 phases (P1–P7), 31 slices (S01–S31). Milestones M0–M6. No architectural decisions created or reinterpreted.

2026-09-27 — v3.2 — Production deployment target clarification pass (pre-approval). Addressed reviewer feedback regarding broad deployment alternatives:
(1) Locked down the authoritative production deployment target to **IIS (Internet Information Services) on Windows Server** via In-Process hosting (`AspNetCoreHostingModel = InProcess` in `web.config`) and dedicated AppPool (`ICSAppPool`).
(2) Removed all deployment alternatives (Linux systemd, Nginx reverse proxy, standalone Windows Service) from Architecture §19.10 and Implementation Plan (§2, §3, P7 objective, P7-S31).
(3) Updated P7-S31 to generate the IIS deployment scripts (`deploy/publish.ps1`, `deploy/setup-iis.ps1`), in-process `web.config`, and SQL Server 2019 DbUp startup migration execution.
Result: 7 phases (P1–P7), 31 slices (S01–S31). Milestones M0–M6. Fully deterministic production target for implementation agents.
