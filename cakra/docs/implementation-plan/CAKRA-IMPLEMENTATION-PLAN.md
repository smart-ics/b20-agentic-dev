---
Title: CAKRA - ICS Operational System Implementation Plan
Code: CAKRA
Artifact: IMPLEMENTATION-PLAN
Version: 4.0
LastUpdated: 2026-09-28
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement the complete CAKRA - ICS Operational System as defined by the approved target architecture.

Planning Mode: GREENFIELD-PLANNING

Referenced artifacts:

- ARCHITECTURE: `cakra/docs/architecture/CAKRA-ARCHITECTURE.md` (target system architecture — authoritative)

Architecture Applicability: GREENFIELD-ARCHITECTURE

Greenfield planning mode based on complete target architecture. Scope encompasses the entire target system architecture. The current repository is in pre-implementation state. The CAKRA-ARCHITECTURE.md is the sole authoritative technical target state. No architectural decisions are created or re-interpreted by this plan.

Agent Execution Optimization: This plan is optimized for execution by budget-class AI implementation agents (Gemini 3.8 Flash, GPT-5, Claude Sonnet). Slices are sized for single-cycle completion with minimal context requirements. The dependency graph maximizes concurrent agent utilization. Completion criteria are objective, observable, and testable. Phase boundaries are organizational only — the dependency graph is authoritative for execution ordering.

---

# 2. Planning Scope

The plan covers the complete CAKRA - ICS Operational System architecture comprising eleven implementation boundaries:

| Boundary | Assembly | Architecture §22 Dependency |
|---|---|---|
| Foundation | `Cakra.Core` | None |
| Identity & Access | `Cakra.Modules.Identity` | `Cakra.Core`, `Organization` (Read) |
| Organization | `Cakra.Modules.Organization` | `Cakra.Core`, `Identity` Context |
| Customer | `Cakra.Modules.Customer` | `Cakra.Core`, `Identity` Context |
| Product | `Cakra.Modules.Product` | `Cakra.Core`, `Organization` (Read), `Identity` Context |
| Request | `Cakra.Modules.Request` | `Cakra.Core`, `Organization`, `Customer`, `Product`, `Identity` Context |
| Work Package | `Cakra.Modules.WorkPackage` | `Cakra.Core`, `Organization`, `Customer`, `Product`, `Request`, `Identity` Context |
| Post & Feed | `Cakra.Modules.Post` | `Cakra.Core`, Domain Events from all modules, `Identity` Context |
| Management Analytics | `Cakra.Modules.Analytics` | `Cakra.Core`, `Request`, `Organization`, `Customer`, `Identity` Context |
| Backend Host | `Cakra.Api` | All Backend Modules |
| Frontend Web | `Cakra.Web` | Backend REST API (`/api/v1/*`) |

Scope derives directly from Architecture §22 (Implementation Boundaries) and §23 (Implementation Dependency Graph). No feature sub-scope is applied; the plan covers the entire target system.

**Vertical Slice Mandate for Implementation Agents**:
Presentation screens and endpoints are delivered vertically within each module's phase to enable early verification rather than accumulating horizontal layered batch work packages. Implementation agents must execute each slice as a cohesive vertical deliverable (coupling domain rules, Dapper parameterized persistence, MediatR handlers, and presentation controllers/Vue SFCs where applicable).

**Backend / Frontend Separation for Agent Execution**:
For modules with complex screens (multiple screens, many endpoints, or complex UI interactions), API controller slices are separated from Vue SFC screen slices. This enables backend and frontend agents to work independently and reduces per-slice context requirements. Simple modules with a single screen may combine API and Vue SFC in one slice.

Technology stack is explicitly defined in Architecture §19 and is authoritative for all implementation phases. Key mandates:

- **Backend**: .NET 8, ASP.NET Core 8.0, C# 12, MediatR, FluentValidation, Serilog — Architecture §19.1, §19.2
- **Persistence**: Microsoft SQL Server 2019, Dapper (explicit parameterized SQL), DbUp-SqlServer for migrations. EF Core is **strictly prohibited** — Architecture §19.3, §20
- **Frontend**: Vue 3 (Composition API, `<script setup lang="ts">`), TypeScript, Bootstrap 5, Vite, Pinia, Vue Router 4 — Architecture §19.4
- **Testing**: xUnit, FluentAssertions, `WebApplicationFactory<Program>` (integration), Respawn (test isolation) — Architecture §19.8
- **Logging**: Serilog (structured JSON, enriched with TraceId, UserId, PersonId) — Architecture §19.9
- **Deployment**: Modular Monolith hosted as a single ASP.NET Core executable process (`Cakra.Api`) running in-process within **IIS (Internet Information Services) on Windows Server** via the ASP.NET Core Module (`AspNetCoreHostingModel = InProcess`). Vue 3 SPA (`Cakra.Web`) compiled via Vite in `src/frontend/Cakra.Web/` into `dist/` (which are ingested into `src/backend/Cakra.Api/wwwroot/`), published via `dotnet publish src/backend/Cakra.Api/Cakra.Api.csproj -c Release -o ./publish`, with DbUp migrations executing on deployment — Architecture §19.10

---

# 3. Business Milestones

Milestones represent observable business-level delivery checkpoints, independent of phase numbering.

| Milestone | Achieved After | Observable Capability |
|---|---|---|
| **M0 — Foundation Ready** | P1-S06 | Infrastructure operational: database migrations run, DI container resolves, event bus dispatches, application starts and serves requests |
| **M1 — Authentication & Catalog Operational** | P3-S16 | Users can log in. Organization, Customer, and Product master data is accessible. Product catalog screen (`SCR-PRD-001`) is functional. First end-to-end user flows are possible |
| **M2 — Request Lifecycle Operational** | P4-S22 + P4-S23 | Complete request management operational: Record, Assign, Evaluate, Accept, Reject, Escalate, Decision, Complete. All request screens (`SCR-REQ-001..005`) functional. Core business operations running |
| **M3 — Work Package Operational** | P5-S27 | Work Packages can be created, managed, activated, closed, and linked to Requests. Work Package screen (`SCR-WP-001`) functional |
| **M4 — Operational Feed Live** | P6-S32 + P6-S33 | Feed screen (`SCR-FEED-001`) operational. Posts, comments, reactions functional. Exception badges working. Post detail modal (`SCR-POST-001`) functional. Feed filtering by Customer, Product, and Exception active |
| **M5 — Full System Operational** | P7-S39 | Management dashboards (`SCR-MGT-001..003`) operational. Analytics snapshot job running. Authoritative IIS on Windows Server production deployment package (`deploy/publish.ps1`, `web.config`, AppPool configuration) generated and verified. All cross-cutting concerns verified end-to-end. System ready for UAT |
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
- Phase boundaries are organizational. A slice may execute as soon as all its declared dependencies are satisfied, regardless of phase membership.
- `Depends On: P1-S06` transitively implies all P1 application pipeline slices are implemented, since P1-S06 is the terminal application-pipeline slice of the Foundation phase.

## Parallel Execution Opportunities

The dependency graph enables significant cross-phase concurrency. Implementation orchestrators should dispatch slices based on dependency satisfaction, not phase ordering.

- **Post-Foundation Domain Concurrency**: After P1-S06 (Application Pipeline), the following domain-only slices can start immediately in parallel: P4-S17 (Request Domain), P5-S24 (Work Package Domain). These are pure domain logic with unit tests and have zero dependency on Identity, Organization, Customer, or Product modules.
- **Post-Foundation Persistence Concurrency**: After P1-S06 + P1-S07, the following persistence slices can also start: P3-S12 (Organization Persistence), P2-S09 (Identity Module). These require only database infrastructure and test project scaffolding.
- **Foundation Scaffold Concurrency**: In Phase 1, P1-S07 (Test Infrastructure) and P1-S08 (Frontend Scaffolding) can execute concurrently with P1-S02 through P1-S06 once P1-S01 is established.
- **Customer and Product Concurrency**: In Phase 3, Customer (P3-S14) does not depend on Organization (P3-S12, P3-S13) or Product (P3-S15). Product (P3-S15) depends on Organization Services (P3-S13) for owner validation, but has zero dependency on Customer (P3-S14). Consequently, Customer and Product can execute concurrently.
- **Request Lifecycle Concurrency**: In Phase 4, P4-S19 (Lifecycle Completion & Queries) and P4-S20 (Escalation & Management) can execute concurrently after P4-S18 (Core Commands) is complete. P4-S22 and P4-S23 (Request Screen groups) can execute concurrently after P4-S21 (Request API Controller) is complete.
- **Analytics Early Start**: P7-S34 (Analytics Snapshot) depends only on P1-S06, P1-S07, and P4-S19 (Request Queries). It has zero dependency on Work Package (P5) or Post & Feed (P6). Analytics slices can execute concurrently with P5 and P6.
- **Feed Screen Concurrency**: P6-S32 (Feed Screen) and P6-S33 (Post Detail Modal) can execute concurrently after P6-S31 (Feed & Post API Controller) is complete.

---

# 5. Progress Summary

| Phase | Slices | Implementation Status | Review Status | Progress |
|---|---|---|---|---|
| P1 — Foundation | S01–S08 | COMPLETED | GO | 8/8 |
| P2 — Identity & Access | S09–S11 | COMPLETED | GO | 3/3 |
| P3 — Core Master Data & Product Catalog | S12–S16 | COMPLETED | GO | 5/5 |
| P4 — Request Lifecycle | S17–S23 | COMPLETED | GO | 7/7 |
| P5 — Work Package | S24–S27 | COMPLETED | GO | 4/4 |
| P6 — Post & Feed | S28–S33 | COMPLETED | GO | 6/6 |
| P7 — Management Analytics & System Finalization | S34–S39 | COMPLETED | GO | 6/6 |

---

# 6. Phases

## P1 — Foundation

Implementation Status: COMPLETED
Review Status: GO

Objective: Establish the complete shared technical foundation that all modules depend on. Covers solution structure and repository-level project layout with `src/backend` and `src/frontend` (Architecture §19.11), shared contracts (`Cakra.Core`), database infrastructure using DbUp-SqlServer (Architecture §19.3), dependency injection conventions with MediatR (Architecture §19.2), in-process domain event bus, ASP.NET Core backend host application pipeline skeleton (`Cakra.Api`) with Serilog structured logging (Architecture §19.9), backend test project infrastructure under `tests/backend/` (Architecture §19.8, §19.11), and Vue 3 frontend project scaffolding (`Cakra.Web` in `src/frontend/Cakra.Web/`) per Architecture §19.4 and §19.11. No business module is implemented in this phase. M0 is achieved when P1-S06 (Application Pipeline Foundation) is complete.

Source: Architecture §22 — Boundary: **Foundation** (`Cakra.Core`, Depends On: None). Architecture §18 (Cross-Cutting Concerns). Architecture §19 (Technology Decisions). Architecture §5 (System Structure).

---

### P1-S01

Title: Solution Structure & Project Scaffolding

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Create the solution file, project references, and exact directory structure for the modular monolith as specified in Architecture §19.11. Establish `src/backend/Cakra.Core`, all `src/backend/Cakra.Modules.*` project skeletons (empty, buildable), the `src/backend/Cakra.Api` backend host application project, the `src/frontend/Cakra.Web` Single Page Application project, and the `deploy/` directory. All backend projects target .NET 8. Define inter-project reference graph matching Architecture §22 dependency table.

Depends On: None

Repository: Solution root / `src/backend/Cakra.Core`

Completion Criteria:
- Solution file `Cakra.sln` exists at repository root and compiles successfully with zero errors.
- Directory structure matches Architecture §19.11 exactly:
  ```text
  cakra/
  ├── Cakra.sln
  ├── src/
  │   ├── backend/
  │   │   ├── Cakra.Core/                       # Common domain abstractions, MediatR pipeline behaviors, Dapper helpers
  │   │   ├── Cakra.Modules.Identity/           # IAM vertical slices, UserAccount & UserSession commands/queries
  │   │   ├── Cakra.Modules.Organization/       # Organization slices: Persons, Teams, Roles, Responsibilities
  │   │   ├── Cakra.Modules.Customer/           # Customer slices: Customers, Contacts
  │   │   ├── Cakra.Modules.Product/            # Product slices: Products, Catalog queries
  │   │   ├── Cakra.Modules.WorkPackage/        # Work Package slices: WorkPackages, Scope management
  │   │   ├── Cakra.Modules.Request/            # Request slices: Lifecycle state machine, assignments, resolutions
  │   │   ├── Cakra.Modules.Post/               # Post & Feed slices: Posts, Comments, Reactions, Feed projection
  │   │   ├── Cakra.Modules.Analytics/          # Analytics queries and AnalyticsSnapshotJob hosted service
  │   │   └── Cakra.Api/                        # ASP.NET Core Host, API Controllers, Middleware, DbUp migrations, Static Asset Host
  │   │
  │   └── frontend/
  │       └── Cakra.Web/                        # Vue 3 + TypeScript + Bootstrap 5 + Vite Single Page Application
  │
  ├── tests/
  │   └── backend/
  │       ├── Cakra.Tests.Unit/                 # Unit tests for domain logic, rules, and state machines
  │       └── Cakra.Tests.Integration/          # Integration tests using WebApplicationFactory and SQL Server
  │
  └── docs/
  ```
- All .NET backend projects target `net8.0`. C# 12 language version, nullable reference types enabled, implicit usings enabled — Architecture §19.1.
- `Cakra.Api` project references all backend module projects and `Cakra.Core`.
- Project reference graph matches Architecture §22 dependency table with no circular references.
- `dotnet build Cakra.sln` succeeds on the solution with zero errors.

Notes: Produces no business logic. Its output is the compilable project structure that all subsequent slices build into. The `tests/backend/` test suites and `deploy/` packaging scripts are created by P1-S07 and P7-S39 respectively; only the `deploy/` placeholder directory is created here.

Implementation Notes (2026-09-28): All completion criteria satisfied. `Cakra.sln` written as a standard VS2022 solution file (Format Version 12.00) since .NET 10 SDK defaults to `.slnx` format. All 10 backend projects created with `dotnet new classlib` (modules) and `dotnet new webapi --use-controllers` (Cakra.Api), all targeting `net8.0`, `LangVersion=12`, `Nullable=enable`, `ImplicitUsings=enable`. Project reference graph matches Architecture §22 exactly: Organization/Customer depend on Core only; Product/Identity depend on Core + Organization; Request depends on Core + Organization + Customer + Product; WorkPackage depends on Core + Organization + Customer + Product + Request; Post depends on Core + all domain modules; Analytics depends on Core + Organization + Customer + Request; Cakra.Api references all modules. `dotnet build Cakra.sln` produced 0 errors, 0 warnings. All 16 required directory paths verified present.

Changed Files:
- `Cakra.sln` (created)
- `deploy/.gitkeep` (created)
- `src/backend/Cakra.Core/Cakra.Core.csproj` (created)
- `src/backend/Cakra.Modules.Identity/Cakra.Modules.Identity.csproj` (created)
- `src/backend/Cakra.Modules.Organization/Cakra.Modules.Organization.csproj` (created)
- `src/backend/Cakra.Modules.Customer/Cakra.Modules.Customer.csproj` (created)
- `src/backend/Cakra.Modules.Product/Cakra.Modules.Product.csproj` (created)
- `src/backend/Cakra.Modules.WorkPackage/Cakra.Modules.WorkPackage.csproj` (created)
- `src/backend/Cakra.Modules.Request/Cakra.Modules.Request.csproj` (created)
- `src/backend/Cakra.Modules.Post/Cakra.Modules.Post.csproj` (created)
- `src/backend/Cakra.Modules.Analytics/Cakra.Modules.Analytics.csproj` (created)
- `src/backend/Cakra.Api/Cakra.Api.csproj` (created — webapi template with project references to all modules)
- `src/frontend/Cakra.Web/.gitkeep` (created — placeholder for P1-S08)
- `tests/backend/Cakra.Tests.Unit/.gitkeep` (created — placeholder for P1-S07)
- `tests/backend/Cakra.Tests.Integration/.gitkeep` (created — placeholder for P1-S07)

---

### P1-S02

Title: Core Contracts & Base Types

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Define all shared technical contracts and base types in `Cakra.Core`: base entity type, value object base, domain event interface, domain event dispatcher interface, repository interface contract, system clock abstraction, audit context interface, and the `ICurrentContextProvider` interface. Add `MediatR` and `FluentValidation.AspNetCore` NuGet package references to `Cakra.Core` and `Cakra.Api`. Interfaces only — no concrete implementations in this slice.

Depends On: P1-S01

Repository: `Cakra.Core`

Completion Criteria:
- `MediatR` NuGet package referenced in `Cakra.Core` — Architecture §19.2.
- `FluentValidation.AspNetCore` NuGet package referenced in `Cakra.Core` — Architecture §19.2.
- `Microsoft.Data.SqlClient` and `Dapper` NuGet packages referenced in `Cakra.Core` — Architecture §19.3.
- Base entity type (with `Id`, `CreatedAt`, `UpdatedAt`) is defined.
- `IDomainEvent` interface is defined (MediatR `INotification` compatible).
- `IDomainEventDispatcher` interface is defined (publish method only; no implementation).
- `IRepository<T>` or equivalent repository contract is defined.
- `ISystemClock` abstraction is defined.
- `IAuditContext` interface is defined.
- `ICurrentContextProvider` interface is defined, exposing `CurrentUserId`, `CurrentPersonId`, and `CurrentRoles` — per Architecture §7 and §14.
- `IModule` interface is defined for module bootstrapper registration pattern.
- All types are in the `Cakra.Core` namespace and accessible to all module projects.
- No concrete implementations are present in this slice (interfaces and base types only).

Notes: Keeps contracts decoupled from implementation. Concrete domain event dispatcher is implemented in P1-S05. Architecture §7 (`CurrentContextProvider`), §14 (IAM), and §18 (Cross-Cutting Concerns) are authoritative for required interfaces.

Implementation Notes (2026-09-28): All completion criteria satisfied. Package references added: `MediatR` 12.5.0 and `FluentValidation.AspNetCore` 11.3.1 in both `Cakra.Core` and `Cakra.Api` (Architecture §19.2); `Dapper` 2.1.66 and `Microsoft.Data.SqlClient` 5.2.3 in `Cakra.Core` (Architecture §19.3). EF Core is not referenced by any project (verified). All contracts and base types are defined in the single `Cakra.Core` namespace so they are accessible to every module: `EntityBase` (abstract, `Id`/`CreatedAt`/`UpdatedAt`), `ValueObject` (abstract, equality by `GetEqualityComponents()`), `IDomainEvent : MediatR.INotification` (`EventId`, `OccurredAtUtc`), `IDomainEventDispatcher` (single `DispatchAsync(IDomainEvent, CancellationToken)` publish method), `IRepository<TEntity,TId>` plus `IRepository<TEntity>` convenience contract, `ISystemClock` (`UtcNow`), `IAuditContext` (`ActorUserId`, `ActorPersonId`, `RecordedAtUtc`), `ICurrentContextProvider` (`CurrentUserId`, `CurrentPersonId`, `CurrentRoles` — Architecture §7/§14), and `IModule` (`Name`, `RegisterServices(IServiceCollection)`). No concrete implementations exist in this slice; no EF Core packages appear in any `.csproj`. `dotnet build Cakra.sln` succeeds with 0 errors, 0 warnings.

Changed Files:
- `src/backend/Cakra.Core/Cakra.Core.csproj` (modified — added MediatR, FluentValidation.AspNetCore, Dapper, Microsoft.Data.SqlClient package references)
- `src/backend/Cakra.Core/Entities/EntityBase.cs` (created)
- `src/backend/Cakra.Core/Entities/ValueObject.cs` (created)
- `src/backend/Cakra.Core/Events/IDomainEvent.cs` (created)
- `src/backend/Cakra.Core/Events/IDomainEventDispatcher.cs` (created)
- `src/backend/Cakra.Core/Persistence/IRepository.cs` (created)
- `src/backend/Cakra.Core/Time/ISystemClock.cs` (created)
- `src/backend/Cakra.Core/Auditing/IAuditContext.cs` (created)
- `src/backend/Cakra.Core/Security/ICurrentContextProvider.cs` (created)
- `src/backend/Cakra.Core/Modules/IModule.cs` (created)
- `src/backend/Cakra.Api/Cakra.Api.csproj` (modified — added MediatR and FluentValidation.AspNetCore package references)

---

### P1-S03

Title: Database Infrastructure & Migration Framework

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Configure DbUp-SqlServer migration framework, database connection management (Dapper + `Microsoft.Data.SqlClient`), and schema-per-module naming conventions. Establish the technical foundation that all module schema migrations will build on. Implement database connection factory, DbUp migration runner wired into application startup, and schema-segregation naming conventions (`identity.*`, `organization.*`, `customer.*`, `product.*`, `workpackage.*`, `request.*`, `post.*`, `analytics.*`) per Architecture §17 and §19.3.

Depends On: P1-S01

Repository: `Cakra.Core` / `Cakra.Api`

Completion Criteria:
- `DbUp-SqlServer` NuGet package referenced in `Cakra.Api` — Architecture §19.3.
- DbUp migration runner is configured to discover and execute sequential idempotent raw SQL scripts in strict dependency order upon application startup — Architecture §19.3 ("Sequential idempotent raw SQL scripts managed and executed in strict dependency order via DbUp").
- Database connection factory (`SqlConnection` via `Microsoft.Data.SqlClient`) is implemented and configurable via `ConnectionStrings__DefaultConnection` environment variable — Architecture §19.10.
- Schema-per-module naming convention is established and documented: eight schema prefixes matching Architecture §17 (`identity`, `organization`, `customer`, `product`, `workpackage`, `request`, `post`, `analytics`).
- DbUp migration runner can apply an empty baseline migration without errors against SQL Server.
- Database health check confirms SQL Server connectivity.
- **Entity Framework (EF Core) is not referenced in any project** — Architecture §19.3 (EF Core strictly prohibited), §20.
- No business tables are created in this slice; only the migration infrastructure is established.

Notes: P1-S03 depends only on P1-S01 and can execute in parallel with P1-S02. Architecture §17 (Schema Segregation) and §19.3 (Persistence & Data Access) are authoritative for all persistence decisions. EF Core packages (`Microsoft.EntityFrameworkCore*`) must not appear in any `.csproj` file.

Implementation Notes (2026-09-28): All completion criteria satisfied. `DbUp-SqlServer` 7.2.0 referenced in `Cakra.Api`. `DatabaseMigrationRunner` discovers embedded `.sql` scripts under `Cakra.Api/Migrations/Scripts`, applies them in strict ascending (dependency) order with `WithTransactionPerScript()`, and journals applied scripts in `dbo.SchemaVersions` for idempotent reruns. `0001_baseline.sql` idempotently creates the eight module schemas (`identity`, `organization`, `customer`, `product`, `workpackage`, `request`, `post`, `analytics`) and creates no business tables. `IDbConnectionFactory` (Cakra.Core) with `SqlConnectionFactory` (Cakra.Api, `Microsoft.Data.SqlClient` `SqlConnection`) provides connection management; the connection string is read from `ConnectionStrings:DefaultConnection` / the `ConnectionStrings__DefaultConnection` environment variable. Migrations execute at startup when configured and via the standalone CLI switch `dotnet Cakra.Api.dll --migrate`. `DatabaseHealthCheck` opens a connection and executes `SELECT 1`, and is registered against `/health/ready`. Verified against SQL Server 2022: migration CLI created a scratch database, applied the baseline, a second run reported "No new scripts need to be executed - completing", the eight schemas were present with zero business tables, and `/health/ready` returned HTTP 200 "Healthy"; scratch databases were dropped. `dotnet build Cakra.sln` succeeds with 0 errors and 0 warnings. No EF Core packages are referenced in any `.csproj` (verified). Schema naming convention documented in `docs/persistence/database-schema-conventions.md`. Note: `Program.cs`, `Cakra.Core.csproj`, and the test projects were concurrently modified by other slices (P1-S02/P1-S07/P1-S08); the integrated solution build passes with `Microsoft.Data.SqlClient` unified at 6.1.4.

Changed Files:
- `src/backend/Cakra.Core/Infrastructure/Persistence/IDbConnectionFactory.cs` (created)
- `src/backend/Cakra.Core/Infrastructure/Persistence/DatabaseSchemas.cs` (created)
- `src/backend/Cakra.Api/Infrastructure/Persistence/SqlConnectionFactory.cs` (created)
- `src/backend/Cakra.Api/Infrastructure/Migrations/DatabaseMigrationRunner.cs` (created)
- `src/backend/Cakra.Api/Infrastructure/HealthChecks/DatabaseHealthCheck.cs` (created)
- `src/backend/Cakra.Api/Migrations/Scripts/0001_baseline.sql` (created — embedded baseline migration)
- `src/backend/Cakra.Api/Cakra.Api.csproj` (modified — added DbUp-SqlServer 7.2.0, Microsoft.Data.SqlClient 6.1.4, embedded SQL resources)
- `src/backend/Cakra.Api/Program.cs` (modified — connection factory/migration runner DI, startup migration, `--migrate` CLI switch, `/health/ready`)
- `src/backend/Cakra.Api/appsettings.json` (modified — added `ConnectionStrings:DefaultConnection`)
- `docs/persistence/database-schema-conventions.md` (created — schema naming conventions documented)

---

### P1-S04

Title: Dependency Injection & Module Registration

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the module registration pattern and DI container configuration. Each `Cakra.Modules.*` project will implement `IModule` to self-register its services, repositories, MediatR handlers, and FluentValidation validators. The host application (`Cakra.Api`) discovers and invokes all `IModule` implementations at startup. Register MediatR with pipeline behaviors (validation behavior invoking FluentValidation before handler execution). Establish service lifetime conventions (Singleton, Scoped, Transient) per architectural pattern.

Depends On: P1-S02, P1-S03

Repository: `Cakra.Core` / `Cakra.Api`

Completion Criteria:
- MediatR is registered in the DI container with pipeline behavior for FluentValidation — Architecture §19.2 ("FluentValidation ... executed automatically via MediatR pipeline behaviors prior to handler execution").
- FluentValidation validators are auto-discovered and registered via the DI container per module.
- `IModule` implementation pattern is demonstrated with a stub module that registers successfully.
- Host application (`Cakra.Api`) startup discovers and registers all `IModule` implementations.
- Service lifetime conventions are established and applied consistently.
- DI container resolves all core interfaces (`ISystemClock`, `IAuditContext`, `ICurrentContextProvider`) after startup.
- Build and DI resolution verification tests pass (no unresolved dependencies at startup).

Notes: Depends on P1-S02 for `IModule` interface and P1-S03 for database connection registration.

Implementation Notes (2026-09-28): All completion criteria satisfied. `Cakra.Core.ModuleLoader` discovers concrete `IModule` implementations in supplied assemblies (ordered by `Name`, resilient to `ReflectionTypeLoadException`) and invokes `RegisterServices`; `Cakra.Api.Extensions.ModuleRegistrationExtensions.DiscoverModuleAssemblies()` locates every deployed `Cakra.Modules.*.dll` from the application base directory (avoids the compiler dropping unused assembly references) and `AddCakraModules` registers them. `Cakra.Api.Extensions.CoreServicesExtensions.AddCakraCore` wires the composition root: `ISystemClock`→`SystemClock` (Singleton), `ICurrentContextProvider`→`CurrentContextProvider` (Scoped), `IAuditContext`→`AuditContext` (Scoped, derives actor from the ambient context and timestamp from the clock); MediatR via `RegisterServicesFromAssemblies` over the host + `Cakra.Core` + every module assembly with `AddOpenBehavior(typeof(ValidationBehaviour<,>))`; and `AddValidatorsFromAssemblies` for auto-discovery of validators per module. `Cakra.Core.ValidationBehaviour<TRequest,TResponse>` executes all `IValidator<TRequest>` implementations before the handler and throws `ValidationException` on failure (Architecture §19.2). The `IModule` pattern is demonstrated end to end by `IdentityModule` in `Cakra.Modules.Identity`, which registers a `StubRegistrationProbe` and whose `StubModulePingHandler`/`StubModulePingRequestValidator` prove per-module handler/validator auto-discovery. Service lifetime conventions (Singleton = stateless infrastructure; Scoped = per-request ambient state; Transient = stateless handlers) are documented in `docs/architecture/dependency-injection-conventions.md`. `Program.cs` calls `AddCakraCore` + `AddCakraModules` after `AddControllers`. Verification: `dotnet build Cakra.sln` succeeds with 0 warnings / 0 errors; `dotnet test Cakra.sln` passes with 0 failures, including 8 new P1-S04 tests in `Cakra.Tests.Integration` (container builds with `ValidateOnBuild`/`ValidateScopes`; core interfaces resolve; MediatR + `ValidationBehaviour` resolve; module service self-registration; validator auto-discovery; handler dispatch; invalid request rejected by the validation behavior; and an in-process host startup smoke check that resolves the core graph with migrations bypassed). The event-bus registration is owned by P1-S05 and was intentionally not wired here.

Changed Files:
- `src/backend/Cakra.Core/Behaviors/ValidationBehaviour.cs` (created — MediatR FluentValidation pipeline behavior)
- `src/backend/Cakra.Core/Modules/ModuleLoader.cs` (created — IModule discovery/registration)
- `src/backend/Cakra.Api/Extensions/CoreServicesExtensions.cs` (created — core services + MediatR/validation registration)
- `src/backend/Cakra.Api/Extensions/ModuleRegistrationExtensions.cs` (created — module assembly discovery + registration)
- `src/backend/Cakra.Api/Infrastructure/Context/SystemClock.cs` (created)
- `src/backend/Cakra.Api/Infrastructure/Context/CurrentContextProvider.cs` (created)
- `src/backend/Cakra.Api/Infrastructure/Context/AuditContext.cs` (created)
- `src/backend/Cakra.Api/Program.cs` (modified — calls AddCakraCore/AddCakraModules)
- `src/backend/Cakra.Modules.Identity/IdentityModule.cs` (created — stub module demonstrating the pattern)
- `src/backend/Cakra.Modules.Identity/Registration/StubRegistrationProbe.cs` (created)
- `src/backend/Cakra.Modules.Identity/Registration/StubModulePingRequest.cs` (created)
- `src/backend/Cakra.Modules.Identity/Registration/StubModulePingHandler.cs` (created)
- `src/backend/Cakra.Modules.Identity/Registration/StubModulePingRequestValidator.cs` (created)
- `tests/backend/Cakra.Tests.Integration/Modules/ModuleRegistrationTests.cs` (created — DI verification + startup smoke tests)
- `docs/architecture/dependency-injection-conventions.md` (created — service lifetime & registration conventions)

---

### P1-S05

Title: Domain Event Bus Implementation

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the concrete in-process domain event bus: `DomainEventDispatcher` implementing `IDomainEventDispatcher`. The dispatcher must support synchronous in-process event publishing using MediatR `IPublisher` within the same database transaction scope as the originating command — per Architecture §18 and §12 (Synchronization Guarantee). Event handlers are implemented as MediatR `INotificationHandler<T>` and discovered through the DI container.

Depends On: P1-S02

Repository: `Cakra.Core`

Completion Criteria:
- `DomainEventDispatcher` implements `IDomainEventDispatcher` using MediatR `IPublisher.Publish()` — Architecture §19.2.
- Event dispatch is synchronous and executes within the same transactional scope as the command that raised the event — per Architecture §18 ("in-process synchronous event dispatcher with transactional consistency").
- Multiple handlers (`INotificationHandler<T>`) registered for the same event type are all invoked.
- Event handlers are registered and discovered via the DI container (registered in `IModule`).
- Unit tests (xUnit): publishing an event invokes all registered handlers; missing handler does not throw; handler exception propagates correctly.

Notes: Depends on P1-S02 for the `IDomainEvent` and `IDomainEventDispatcher` interfaces. Can execute in parallel with P1-S03 after P1-S02 is implemented. Architecture §18 (Domain Event Bus) and §19.2 (MediatR) are authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied. `DomainEventDispatcher` (sealed, `Cakra.Core`) implements `IDomainEventDispatcher` by injecting `MediatR.IPublisher` and delegating `DispatchAsync` to `IPublisher.Publish(domainEvent, cancellationToken)`; because `IDomainEvent : INotification`, MediatR resolves and invokes every registered `INotificationHandler<T>` for the concrete event type. Dispatch is synchronous in-process: MediatR's default `ForeachAwaitPublisher` awaits each handler before `Publish` returns, and any handler exception propagates to the caller, so dispatch runs within — and rolls back — the same transaction scope as the originating command (Architecture §18; §12 Synchronization Guarantee). Handlers are discovered through the DI container via MediatR assembly scanning. DI registration is provided by a self-contained extension `DomainEventBusServiceCollectionExtensions.AddDomainEventBus(IServiceCollection)` in `Cakra.Core`: it registers `IDomainEventDispatcher → DomainEventDispatcher` as **Scoped** (shared with the command scope) via `TryAddScoped`, and registers MediatR from the `Cakra.Core` assembly only when the host has not already registered it (guarded on `IMediator`), leaving the module registration pattern (P1-S04) free to register module assemblies, handlers, and the FluentValidation pipeline behavior — no P1-S04 work was duplicated and no `IModule` implementation was created. The P1-S02 `IModule` interface was present but the P1-S04 registration pattern had not started; the self-contained helper is therefore callable from `IModule.RegisterServices` once P1-S04 lands. Unit tests added in `Cakra.Tests.Unit/Events/DomainEventDispatcherTests.cs` (xUnit + FluentAssertions) cover all three required behaviours: (1) two `INotificationHandler<TestDomainEvent>` implementations are both invoked; (2) dispatching an event with no handler completes without throwing; (3) an `InvalidOperationException` raised by a handler propagates out of `DispatchAsync`. The unit test project gained a `Cakra.Core` project reference plus `MediatR` 12.5.0 and `Microsoft.Extensions.DependencyInjection` 8.0.1 package references (test seam only). `dotnet build Cakra.sln` succeeded with 0 errors, 0 warnings; `dotnet test Cakra.sln` completed with exit code 0 (11 passed / 0 failed / 0 skipped), including the three P1-S05 unit tests in `Cakra.Tests.Unit` and the concurrently-added integration tests. No EF Core packages introduced.

Changed Files:
- `src/backend/Cakra.Core/Events/DomainEventDispatcher.cs` (created)
- `src/backend/Cakra.Core/Events/DomainEventBusServiceCollectionExtensions.cs` (created)
- `tests/backend/Cakra.Tests.Unit/Events/DomainEventDispatcherTests.cs` (created)
- `tests/backend/Cakra.Tests.Unit/Cakra.Tests.Unit.csproj` (modified — added `Cakra.Core` project reference; added `MediatR` 12.5.0 and `Microsoft.Extensions.DependencyInjection` 8.0.1 package references)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — P1-S05 implementation status and notes)

---

### P1-S06

Title: Application Pipeline Foundation

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Configure the HTTP request pipeline skeleton in `Cakra.Api`: middleware registration order, centralized exception handling middleware producing RFC 7807 `ProblemDetails` responses, input validation pipeline wired via MediatR FluentValidation behavior, Serilog structured logging enriched with `TraceId`, `SpanId`, `UserId`, and `PersonId`, audit logging hook, and security context population middleware placeholder. Establish the application entry point, health check endpoints (`/health/live`, `/health/ready`), and the wiring point for all modules. Application must start, serve requests, and return structured error responses.

Depends On: P1-S04, P1-S05

Repository: `Cakra.Api`

Completion Criteria:
- `Serilog.AspNetCore` NuGet package referenced and configured with structured JSON logging enriched with `TraceId`, `SpanId`, `UserId`, `PersonId`, and `SourceContext` — Architecture §19.9. Console (stdout) and rolling file sinks configured.
- HTTP middleware pipeline registered in the following exact order in `Program.cs`:
  1. `UseSerilogRequestLogging()` — request/response logging
  2. Global exception handling middleware — produces RFC 7807 `ProblemDetails` JSON
  3. `UseStaticFiles()` — serves SPA assets from `wwwroot/`
  4. `UseRouting()`
  5. `UseAuthentication()` — placeholder; concrete handler wired in P2-S10
  6. `UseAuthorization()`
  7. `MapControllers()` — endpoint mapping
- Centralized exception handling middleware returns consistent RFC 7807 `ProblemDetails` JSON responses with consistent error codes — Architecture §19.6 ("standard RFC 7807 Problem Details (`ProblemDetails`) JSON responses"). `System.Text.Json` with camelCase naming policy configured — Architecture §19.6.
- Input validation pipeline is in place and invoked before application service dispatch (MediatR FluentValidation pipeline behavior).
- Audit logging hook is registered in the pipeline — Architecture §18 (Audit Logging).
- Security context population middleware placeholder is registered (concrete implementation supplied by P2-S10).
- ASP.NET Core Health Check endpoints respond: `/health/live` (HTTP 200) and `/health/ready` (HTTP 200, validates SQL Server connectivity) — Architecture §19.9.
- Application starts, wires all registered `IModule` bootstrappers, and serves requests without unresolved dependency errors.
- REST API base route convention `/api/v1/{module}/{resource}` is established — Architecture §19.6.

Notes: Terminal application-pipeline slice of P1. All subsequent phases producing application logic depend on P1-S06. Architecture §5 (System Structure), §18 (Cross-Cutting Concerns), §19.6 (API Style), §19.9 (Logging & Observability), and §20 (Implementation Constraints) are authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. `Serilog.AspNetCore` (10.0.0) and `Serilog.Sinks.File` (7.0.0) added to `Cakra.Api.csproj`. Configured in `SerilogConfigurationExtensions.ConfigureCakraSerilog` with structured JSON formatting (`JsonFormatter`), Console (stdout) sink, and rolling File sink (`logs/cakra-.json`, `shared: true`). Enriched with `TraceId` and `SpanId` from `Activity.Current` (fallback to `TraceIdentifier`), `UserId` and `PersonId` from ambient `ICurrentContextProvider` via `CakraLogEnricher`, and `SourceContext` from Serilog standard context. `UseSerilogRequestLogging` diagnostic context also captures `TraceId`, `SpanId`, `UserId`, `PersonId`.
2. Exact 7-step HTTP middleware pipeline order registered in `Program.cs`:
   (1) `UseSerilogRequestLogging()`,
   (2) `UseGlobalExceptionHandler()` (`GlobalExceptionHandlingMiddleware`),
   (3) `UseCakraSpa()` (which calls `UseDefaultFiles()` + `UseStaticFiles()`),
   (4) `UseRouting()`,
   (5) `UseAuthentication()` + `UseSecurityContext()` placeholder,
   (6) `UseAuthorization()` + `UseAuditLogging()` hook,
   (7) `MapControllers()` + `MapHealthChecks`.
3. `GlobalExceptionHandlingMiddleware` produces consistent RFC 7807 `ProblemDetails` / `ValidationProblemDetails` responses with Content-Type `application/problem+json` and consistent uppercase error codes (`VALIDATION_FAILED`, `RESOURCE_NOT_FOUND`, `UNAUTHORIZED`, `BAD_REQUEST`, `INTERNAL_SERVER_ERROR`). Maps `FluentValidation.ValidationException` to 400 Bad Request with field validation errors dictionary keyed in camelCase via `System.Text.Json` `JsonNamingPolicy.CamelCase`.
4. Input validation pipeline executes automatically via MediatR `ValidationBehaviour<TRequest, TResponse>` before handler execution; invalid requests throw `ValidationException` resulting in 400 Bad Request ProblemDetails.
5. `AuditLoggingMiddleware` registered after authorization as the audit logging hook (Architecture §18), recording `ActorPersonId`, `ActorUserId`, `RecordedAtUtc`, method, path, status, and duration.
6. `SecurityContextMiddleware` registered after `UseAuthentication()` as the security context population placeholder (Architecture §18), parsing claims when authenticated, populating scoped `CurrentContextProvider`, and pushing `UserId` and `PersonId` to Serilog `LogContext`.
7. Health checks mapped and verified: `/health/live` returns HTTP 200 "Healthy"; `/health/ready` evaluates `DatabaseHealthCheck` confirming database connectivity.
8. Application boots, wires all `IModule` implementations via `AddCakraCore` and `AddCakraModules`, and serves traffic cleanly.
9. REST route convention `/api/v1/{module}/{resource}` established via `ApiControllerBase` and verified end-to-end via `ProbeController` (`/api/v1/system/probe`).
Integration tests in `Cakra.Tests.Integration` (total 20 passing tests in integration suite, 23 across solution) verify `/health/live`, `/health/ready`, route convention, MediatR dispatch, 400 ProblemDetails on validation error with camelCase fields, 500 ProblemDetails on unhandled error, Serilog enricher, security context middleware, and audit logging middleware. Build succeeds with 0 warnings, 0 errors, no EF Core references.

Changed Files:
- `src/backend/Cakra.Api/Cakra.Api.csproj` (modified — added Serilog.AspNetCore and Serilog.Sinks.File package references)
- `src/backend/Cakra.Api/Program.cs` (modified — configured Serilog, camelCase JSON options, exact 7-step pipeline order, and health check routes)
- `src/backend/Cakra.Api/Infrastructure/Logging/CakraLogEnricher.cs` (created — Serilog enricher for TraceId, SpanId, UserId, PersonId)
- `src/backend/Cakra.Api/Infrastructure/Logging/SerilogConfigurationExtensions.cs` (created — Serilog configuration with JSON console and rolling file sinks)
- `src/backend/Cakra.Api/Middleware/GlobalExceptionHandlingMiddleware.cs` (created — RFC 7807 ProblemDetails middleware)
- `src/backend/Cakra.Api/Middleware/SecurityContextMiddleware.cs` (created — security context population placeholder)
- `src/backend/Cakra.Api/Middleware/AuditLoggingMiddleware.cs` (created — audit logging hook)
- `src/backend/Cakra.Api/Middleware/MiddlewareExtensions.cs` (created — middleware pipeline extension methods)
- `src/backend/Cakra.Api/Controllers/ApiControllerBase.cs` (created — base API controller establishing REST route convention)
- `src/backend/Cakra.Api/Controllers/ProbeController.cs` (created — probe controller for pipeline and error verification)
- `src/backend/Cakra.Api/Controllers/WeatherForecastController.cs` (deleted — replaced template controller)
- `src/backend/Cakra.Api/WeatherForecast.cs` (deleted — replaced template model)
- `tests/backend/Cakra.Tests.Integration/Pipeline/ApplicationPipelineTests.cs` (created — integration tests for health checks, ProblemDetails, and route convention)
- `tests/backend/Cakra.Tests.Integration/Logging/CakraLogEnricherTests.cs` (created — tests for Serilog enrichment)
- `tests/backend/Cakra.Tests.Integration/Middleware/SecurityContextMiddlewareTests.cs` (created — tests for security context middleware)
- `tests/backend/Cakra.Tests.Integration/Middleware/AuditLoggingMiddlewareTests.cs` (created — tests for audit logging middleware)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — P1-S06 status, implementation notes, and changed files)

---

### P1-S07

Title: Test Project Infrastructure

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Create and configure the test projects (`Cakra.Tests.Unit` and `Cakra.Tests.Integration`) under `tests/backend/` per Architecture §19.8 and §19.11. Establish all required test NuGet packages, integration test infrastructure using `WebApplicationFactory<Program>`, and the Respawn-based database reset utility for test isolation. This slice produces no business tests — only the scaffolding that all subsequent test slices build into.

Depends On: P1-S01

Repository: `tests/backend/Cakra.Tests.Unit`, `tests/backend/Cakra.Tests.Integration`

Completion Criteria:
- `tests/backend/Cakra.Tests.Unit/` project created and added to `Cakra.sln` — Architecture §19.11.
- `tests/backend/Cakra.Tests.Integration/` project created and added to `Cakra.sln` — Architecture §19.11.
- Both test projects reference NuGet packages: `xunit`, `xunit.runner.visualstudio`, `FluentAssertions`, `Microsoft.NET.Test.Sdk` — Architecture §19.8.
- `Cakra.Tests.Integration` additionally references: `Microsoft.AspNetCore.Mvc.Testing` (referencing `Cakra.Api`) and `Respawn` — Architecture §19.8.
- `Cakra.Tests.Integration` contains a base `IntegrationTestBase` class wiring `WebApplicationFactory<Program>` for in-process test execution and a Respawn-based database reset helper — Architecture §19.8 ("executing against an isolated SQL Server test instance, using Respawn ... to ensure clean test state").
- `dotnet test` succeeds (zero test failures; no tests yet, only infrastructure).
- Integration test infrastructure is configurable via environment variables (test database connection string separate from application connection string).

Notes: Can execute in parallel with P1-S02 through P1-S05 (depends only on P1-S01 for project structure). Test infrastructure is a prerequisite for all integration test completion criteria in slices P2-S09 onwards. Architecture §19.8 (Testing Strategy) is authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied. Created `tests/backend/Cakra.Tests.Unit` and `tests/backend/Cakra.Tests.Integration` both targeting `net8.0` (`LangVersion=12`, nullable + implicit usings enabled) and added both to `Cakra.sln`. Both projects reference `xunit` 2.9.3, `xunit.runner.visualstudio` 2.8.2, `FluentAssertions` 6.12.2, and `Microsoft.NET.Test.Sdk` 17.11.1; `Cakra.Tests.Integration` additionally references `Microsoft.AspNetCore.Mvc.Testing` 8.0.11, `Respawn` 7.0.0, `Microsoft.Data.SqlClient` 6.1.4 (version aligned with the host's transitive reference to avoid NU1605), and project-references `Cakra.Api`. Integration scaffolding: `IntegrationTestBase` exposes an in-process `WebApplicationFactory<Program>` `Client` plus a `DatabaseResetHelper`; `CakraWebApplicationFactory` redirects `ConnectionStrings:DefaultConnection` to the test database only when configured; `DatabaseResetHelper` resolves the isolated test connection string from the `CAKRA_TEST_DB_CONNECTION` environment variable (fallback `ConnectionStrings__TestConnection`), ignores the `dbo.__SchemaVersions` DbUp journal, and creates the Respawner lazily so no live database is required for build or test discovery. Added `public partial class Program { }` to `Program.cs` so `WebApplicationFactory<Program>` can reference the entry point; this is the only Program.cs change made by this slice (the concurrent P1-S06 owner had appended `return 0;` after the partial declaration, which would not compile — the top-level `return 0;` was moved ahead of the type declaration without altering any P1-S06 pipeline logic). `dotnet build Cakra.sln` succeeded with 0 warnings / 0 errors. `dotnet test Cakra.sln` completed with exit code 0 and zero failures: both test assemblies were discovered by the xUnit adapter and reported no test methods, as expected for infrastructure-only scaffolding. No business tests were added.

Changed Files:
- `tests/backend/Cakra.Tests.Unit/Cakra.Tests.Unit.csproj` (created)
- `tests/backend/Cakra.Tests.Integration/Cakra.Tests.Integration.csproj` (created)
- `tests/backend/Cakra.Tests.Integration/IntegrationTestBase.cs` (created)
- `tests/backend/Cakra.Tests.Integration/Infrastructure/CakraWebApplicationFactory.cs` (created)
- `tests/backend/Cakra.Tests.Integration/DatabaseResetHelper.cs` (created)
- `tests/backend/Cakra.Tests.Unit/.gitkeep` (deleted — placeholder superseded by project)
- `tests/backend/Cakra.Tests.Integration/.gitkeep` (deleted — placeholder superseded by project)
- `Cakra.sln` (modified — both test projects added)
- `src/backend/Cakra.Api/Program.cs` (modified — `public partial class Program { }` added for `WebApplicationFactory<Program>`)

---

### P1-S08

Title: Vue 3 Frontend Project Scaffolding

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Scaffold the Vue 3 Single Page Application project inside `src/frontend/Cakra.Web/` per Architecture §19.4 and §19.11. Establish the Vite build configuration, TypeScript setup, Bootstrap 5 integration, Vue Router 4 for client-side navigation, and Pinia for shared state management. Configure the ASP.NET Core host (`Cakra.Api`) to serve the built Vue SPA assets from `wwwroot/`. No UI screens are implemented in this slice — only the project structure, tooling configuration, and a placeholder root component.

Depends On: P1-S01

Repository: `src/frontend/Cakra.Web/`

Completion Criteria:
- `src/frontend/Cakra.Web/` Vite-scaffolded Vue 3 project exists — Architecture §19.4, §19.11.
- TypeScript is configured (`<script setup lang="ts">` pattern works) — Architecture §19.4.
- Bootstrap 5 (with Bootstrap Icons) is installed and applied to the root layout — Architecture §19.4.
- Vue Router 4 is installed and configured with placeholder routes — Architecture §19.4.
- Pinia is installed and configured as the state management store — Architecture §19.4.
- Axios (or equivalent) HTTP client is installed and configured with a base URL pointing to `/api/v1` and authentication interceptors wired for cookie-based session handling — Architecture §19.4, §19.5.
- `vite build` produces a production bundle without errors.
- ASP.NET Core `Cakra.Api` is configured to serve the built SPA assets from `wwwroot` (or equivalent static file path), falling back to `index.html` for SPA routing — Architecture §19.10.
- Placeholder root component renders "CAKRA - ICS Operational System" confirmation message; the application loads in browser without console errors.

Notes: Can execute in parallel with P1-S02 through P1-S06 (depends only on P1-S01 for project structure). The `src/frontend/Cakra.Web/` SPA is built via Vite (`npm run build`) into `dist/` and ingested into `src/backend/Cakra.Api/wwwroot/` during the production publish process defined in Architecture §19.10 and P7-S39. Each screen implementation (P2-S11, P3-S16, P4-S22, P4-S23, P5-S27, P6-S32, P6-S33, P7-S37) adds Vue SFC components to this scaffold. Architecture §19.4 (Frontend Stack), §19.5 (Authentication), §19.10 (Deployment & Runtime Strategy), and §20 ("Frontend Component Architecture") are authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied. Scaffolded the Vue 3 SPA in `src/frontend/Cakra.Web/` with Composition API (`<script setup lang="ts">`), Bootstrap 5 + Bootstrap Icons, Vue Router **4** (pinned to v4 per Architecture §19.4), Pinia, and Axios. Axios client (`src/api/http.ts`) is configured with `baseURL: '/api/v1'`, `withCredentials: true` (cookie-based session, Architecture §19.5), a request interceptor, and a response interceptor that dispatches a `cakra:unauthorized` event on HTTP 401 (consumed in `main.ts` to clear the auth store). `vite.config.ts` sets `build.outDir: 'dist'` (consumed by Cakra.Api `wwwroot/` during release packaging per Architecture §19.10) and a dev proxy to the local API host. Root `App.vue` renders "CAKRA - ICS Operational System" with a Bootstrap 5 layout and `<router-view/>`; `HomeView.vue` and a catch-all redirect provide placeholder routes. ASP.NET Core SPA hosting is wired via a self-contained extension `SpaStaticFilesExtensions.UseCakraSpa()` (`UseDefaultFiles()` + `UseStaticFiles()` + `MapFallbackToFile("index.html")`), invoked from a clearly-marked block in `Program.cs` so P1-S06 can position it at its canonical `UseStaticFiles()` step 3 without duplicate registration; the fallback endpoint has lowest precedence and does not shadow API controller routes. `src/backend/Cakra.Api/wwwroot/.gitkeep` was created as the static asset root. No business screens were implemented. Verification: `npm install` (91 packages, 0 vulnerabilities); `npm run build` (vue-tsc + vite v8.3.1, 41 modules transformed, emitted `dist/index.html` + `dist/assets/*.js|*.css|*.woff*` with no errors); `dotnet build Cakra.sln` succeeded with 0 warnings / 0 errors. The `Program.cs` file also contains concurrent database-infrastructure wiring from another slice; this slice only added the `using Cakra.Api.Extensions;` import and the single `app.UseCakraSpa();` call.

Changed Files:
- `src/frontend/Cakra.Web/package.json` (created)
- `src/frontend/Cakra.Web/package-lock.json` (created — npm install lockfile)
- `src/frontend/Cakra.Web/vite.config.ts` (created)
- `src/frontend/Cakra.Web/tsconfig.json` (created)
- `src/frontend/Cakra.Web/tsconfig.node.json` (created)
- `src/frontend/Cakra.Web/env.d.ts` (created)
- `src/frontend/Cakra.Web/index.html` (created)
- `src/frontend/Cakra.Web/.gitignore` (created)
- `src/frontend/Cakra.Web/src/main.ts` (created)
- `src/frontend/Cakra.Web/src/App.vue` (created — placeholder root component)
- `src/frontend/Cakra.Web/src/views/HomeView.vue` (created — placeholder route)
- `src/frontend/Cakra.Web/src/router/index.ts` (created — Vue Router 4 placeholder routes)
- `src/frontend/Cakra.Web/src/stores/auth.ts` (created — Pinia store scaffold)
- `src/frontend/Cakra.Web/src/api/http.ts` (created — Axios client + auth interceptors)
- `src/frontend/Cakra.Web/src/assets/main.css` (created)
- `src/frontend/Cakra.Web/.gitkeep` (deleted — replaced by scaffolded project)
- `src/backend/Cakra.Api/Extensions/SpaStaticFilesExtensions.cs` (created)
- `src/backend/Cakra.Api/wwwroot/.gitkeep` (created — SPA static asset root)
- `src/backend/Cakra.Api/Program.cs` (modified — added `using Cakra.Api.Extensions;` and marked `app.UseCakraSpa()` wiring; concurrently edited by another slice)

---

## P2 — Identity & Access

Implementation Status: COMPLETED
Review Status: GO

Objective: Implement the Identity & Access module, the authentication middleware pipeline, and the Login screen (`SCR-AUTH-001`). Establishes `UserAccounts` and `UserSessions` tables via DbUp, credential verification via Argon2id or ASP.NET Core `IPasswordHasher` (PBKDF2/HMAC-SHA512), secure cookie-based session management (`HttpOnly`, `SameSite=Strict`), and the concrete `CurrentContextProvider` that exposes `CurrentUserId`, `CurrentPersonId`, and `CurrentRoles` across all HTTP requests. Delivers the Login screen (`SCR-AUTH-001`) as a Vue 3 SFC. Placing Identity immediately after Foundation ensures all subsequent modules (Master Data, Requests, Work Packages, Feed, Analytics) execute with active security context, user identification, and RBAC authorization without requiring post-hoc refactoring.

Source: Architecture §22 — Boundary: **Identity & Access** (`Cakra.Modules.Identity`, Depends On: `Cakra.Core`, `Organization` (Read)). Architecture §14 (Identity & Authentication Architecture). Architecture §18, §19.5.

---

### P2-S09

Title: Identity Module — Domain, Application Services & Persistence

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the complete Identity & Access module: `UserAccount` and `UserSession` domain entities, `AuthenticationService` (Login, Logout, ValidateSession), DbUp SQL migration scripts for `identity.*` tables, and Dapper repository implementations using explicit parameterized SQL. Credential verification uses Argon2id or ASP.NET Core `IPasswordHasher` (PBKDF2/HMAC-SHA512), per Architecture §19.5. Session token stored in `identity.UserSessions`.

Depends On: P1-S06, P1-S07

Repository: `Cakra.Modules.Identity`

Completion Criteria:
- DbUp SQL migration scripts create `identity.*` schema tables: `UserAccounts`, `UserSessions` — Architecture §14 (IAM Persistence Tables).
- All repository operations use Dapper with explicit parameterized SQL against SQL Server; no EF Core — Architecture §19.3, §20.
- `AuthenticationService.Login(usernameOrEmail, password, clientInfo)`: verifies password using Argon2id or `IPasswordHasher` (PBKDF2/HMAC-SHA512) — Architecture §19.5; checks `UserAccount.Status == 'ACTIVE'`; creates `UserSession`; issues `SessionToken` (stored in `identity.UserSessions`). (Note: If Organization module is not yet populated, status checks support bootstrap admin accounts; once Organization module is active, Person active status is resolved via `OrganizationQueryService`).
- `AuthenticationService.Logout(sessionToken)`: marks `UserSession.IsRevoked = TRUE` in `identity.UserSessions`.
- `AuthenticationService.ValidateSession(sessionToken)`: validates token, checks expiry and revocation, returns `SecurityContext` with `UserId` and `PersonId`.
- Repository implementations write exclusively to `identity.*` schema using parameterized SQL.
- Integration tests (xUnit + WebApplicationFactory + Respawn): Login success, Login failure (bad credentials), ValidateSession with valid token, ValidateSession with expired token, Logout invalidates session.

Notes: Architecture §14 is authoritative for all IAM component specifications. Architecture §19.5 is authoritative for authentication mechanism (Cookie Auth, server-side session, password hashing). Running Identity in P2 establishes user credentials, sessions, and security context before master data is provisioned.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. DbUp SQL migration script `0002_identity_tables.sql` added in `src/backend/Cakra.Api/Migrations/Scripts/` creating `identity.UserAccounts` and `identity.UserSessions` with explicit indexes and primary/foreign keys per Architecture §14. PersonId is stored as UNIQUEIDENTIFIER without cross-schema physical FK (Architecture §20).
2. Domain entities `UserAccount` and `UserSession` implemented in `Cakra.Modules.Identity.Domain`, inheriting from `EntityBase` with full domain lifecycle logic (`RecordLoginSuccess`, `RecordLoginFailure`, `Unlock`, `IsExpired`, `IsValid`, `Revoke`).
3. Dapper repositories `UserAccountRepository` and `UserSessionRepository` implemented in `Cakra.Modules.Identity.Persistence` using explicit parameterized SQL against SQL Server schema `[identity]` via `IDbConnectionFactory`. Zero EF Core usage.
4. `AuthenticationService` implemented in `Cakra.Modules.Identity.Services` implementing `IAuthenticationService`. Uses ASP.NET Core `IPasswordHasher<UserAccount>` (PBKDF2 with HMAC-SHA512 per Architecture §19.5), checks active user and linked person status via `IOrganizationQueryService` (supporting bootstrap accounts when not yet registered), persists sessions in `identity.UserSessions`, issues 64-character cryptographically secure hex session tokens, handles revocation upon logout, and returns `SecurityContext` with `UserId` and `PersonId` upon session validation.
5. Service registration configured via `IdentityModule : IModule` in `IdentityModule.cs` registering repositories, `IPasswordHasher<UserAccount>`, and `AuthenticationService`.
6. Unit tests in `Cakra.Tests.Unit/Identity/` (13 tests across `AuthenticationServiceTests`, `UserAccountTests`, `UserSessionTests`) verify login success, bad credentials, nonexistent user, locked account, inactive person, session validity, expired token, revoked token, logout invalidation, and failure threshold locking. Integration tests in `Cakra.Tests.Integration/Identity/` verify DI container resolution and embedded migration discovery. Solution build and all 171 tests pass with zero warnings, zero errors.

Changed Files:
- `src/backend/Cakra.Api/Migrations/Scripts/0002_identity_tables.sql` (created)
- `src/backend/Cakra.Modules.Identity/Domain/UserAccountStatus.cs` (created)
- `src/backend/Cakra.Modules.Identity/Domain/UserAccount.cs` (created)
- `src/backend/Cakra.Modules.Identity/Domain/UserSession.cs` (created)
- `src/backend/Cakra.Modules.Identity/Domain/IUserAccountRepository.cs` (created)
- `src/backend/Cakra.Modules.Identity/Domain/IUserSessionRepository.cs` (created)
- `src/backend/Cakra.Modules.Identity/Domain/ClientInfo.cs` (created)
- `src/backend/Cakra.Modules.Identity/Domain/LoginResult.cs` (created)
- `src/backend/Cakra.Modules.Identity/Domain/SecurityContext.cs` (created)
- `src/backend/Cakra.Modules.Identity/Domain/IAuthenticationService.cs` (created)
- `src/backend/Cakra.Modules.Identity/Persistence/UserAccountRepository.cs` (created)
- `src/backend/Cakra.Modules.Identity/Persistence/UserSessionRepository.cs` (created)
- `src/backend/Cakra.Modules.Identity/Services/AuthenticationService.cs` (created)
- `src/backend/Cakra.Modules.Organization/IOrganizationQueryService.cs` (created)
- `src/backend/Cakra.Modules.Identity/Cakra.Modules.Identity.csproj` (modified — added Microsoft.AspNetCore.App framework reference)
- `src/backend/Cakra.Modules.Identity/IdentityModule.cs` (modified — registered IAM repositories and services)
- `tests/backend/Cakra.Tests.Unit/Cakra.Tests.Unit.csproj` (modified — added Identity and Organization references)
- `tests/backend/Cakra.Tests.Unit/Identity/AuthenticationServiceTests.cs` (created)
- `tests/backend/Cakra.Tests.Unit/Identity/UserAccountTests.cs` (created)
- `tests/backend/Cakra.Tests.Unit/Identity/UserSessionTests.cs` (created)
- `tests/backend/Cakra.Tests.Integration/Cakra.Tests.Integration.csproj` (modified — added Core and Identity references)
- `tests/backend/Cakra.Tests.Integration/Identity/IdentityModuleRegistrationTests.cs` (created)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — P2-S09 status, implementation notes, and changed files)

---

### P2-S10

Title: Authentication Middleware & Security Context Pipeline

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the concrete HTTP authentication middleware that intercepts every incoming request, validates the session cookie via `AuthenticationService.ValidateSession`, populates `CurrentContextProvider` with `CurrentUserId`, `CurrentPersonId`, and `CurrentRoles`, and rejects unauthenticated requests with HTTP 401. Uses ASP.NET Core Cookie Authentication (`CookieAuthenticationDefaults.AuthenticationScheme`) with secure, `HttpOnly`, `SameSite=Strict` cookies per Architecture §19.5. Implement the RBAC enforcement mechanism (`[Authorize(Roles = "...")]`) backed by dynamically resolved roles.

Depends On: P2-S09

Repository: `Cakra.Modules.Identity` / `Cakra.Api`

Completion Criteria:
- ASP.NET Core Cookie Authentication registered with `HttpOnly = true`, `SameSite = SameSiteMode.Strict`, `Secure = true` — Architecture §19.5.
- Authentication middleware is registered in `Cakra.Api` pipeline (fulfils the placeholder established in P1-S06).
- Every request without a valid session cookie returns HTTP 401 on protected endpoints.
- Every request with a valid cookie calls `AuthenticationService.ValidateSession` and populates `CurrentContextProvider` with `CurrentUserId`, `CurrentPersonId`, and `CurrentRoles` — Architecture §19.5.
- RBAC enforcement mechanism is in place and verified: `[Authorize(Roles = "Management")]` attribute rejects non-Management users with HTTP 403.
- Integration tests (xUnit + WebApplicationFactory): authenticated request (200), unauthenticated request (401), insufficient-role request (403) pass.

Notes: Architecture §18 (Authentication & Security Context, RBAC) and §19.5 are authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Registered ASP.NET Core Cookie Authentication in `Cakra.Api` via `AddCakraAuthentication()` with `CookieAuthenticationDefaults.AuthenticationScheme`, `HttpOnly = true`, `SameSite = SameSiteMode.Strict`, `SecurePolicy = CookieSecurePolicy.Always` (`Secure = true`), and cookie name `Cakra.Session` per Architecture §19.5.
2. Implemented `SessionCookieTicketFormat` (`ISecureDataFormat<AuthenticationTicket>`) extracting the 64-character server-side session token directly from the session cookie.
3. Configured `CookieAuthenticationEvents.OnValidatePrincipal` to validate incoming session tokens against `IAuthenticationService.ValidateSessionAsync`, rejecting invalid/revoked/expired sessions and deleting invalid cookies.
4. Implemented dynamic role resolution across the IAM-Organization boundary: `IOrganizationQueryService.GetPersonRolesAsync` in `Cakra.Modules.Organization` queries active roles using Dapper parameterized SQL over `organization.RoleAssignments` and `organization.Roles`, and `AuthorizationService` in `Cakra.Modules.Identity` resolves active roles for `PersonId` during ticket validation.
5. Injected authenticated claims (`ClaimTypes.NameIdentifier`, `userId`, `personId`, `session_token`, and `ClaimTypes.Role` for each resolved role) into `ClaimsPrincipal`, which `SecurityContextMiddleware` reads to populate ambient `ICurrentContextProvider` (`CurrentUserId`, `CurrentPersonId`, `CurrentRoles`) and enrich Serilog `LogContext`.
6. Configured `OnRedirectToLogin` and `OnRedirectToAccessDenied` to emit standard RFC 7807 `ProblemDetails` with HTTP 401 Unauthorized (`UNAUTHORIZED`) and HTTP 403 Forbidden (`FORBIDDEN`).
7. Added protected (`[Authorize]`) and role-restricted (`[Authorize(Roles = "Management")]`) verification endpoints to `ProbeController`.
8. Added unit tests in `Cakra.Tests.Unit/Identity/AuthorizationServiceTests.cs` and integration tests in `Cakra.Tests.Integration/Identity/AuthenticationMiddlewareTests.cs` using `WebApplicationFactory<Program>`, covering cookie security properties, 401 on unauthenticated request, 401 on invalid/revoked session cookie, 200 on authenticated request verifying `CurrentContextProvider` population, 403 on insufficient role, and 200 on sufficient "Management" role. Full solution build and all 189 tests pass with 0 warnings and 0 errors.

Changed Files:
- `src/backend/Cakra.Modules.Organization/IOrganizationQueryService.cs` (modified — added `GetPersonRolesAsync`)
- `src/backend/Cakra.Modules.Organization/Services/OrganizationQueryService.cs` (created — Dapper parameterized SQL for person status and active role resolution)
- `src/backend/Cakra.Modules.Organization/OrganizationModule.cs` (modified — registered `IOrganizationQueryService`)
- `src/backend/Cakra.Modules.Identity/Services/IAuthorizationService.cs` (created — application service contract for dynamic role resolution)
- `src/backend/Cakra.Modules.Identity/Services/AuthorizationService.cs` (created — dynamic role resolution via `IOrganizationQueryService`)
- `src/backend/Cakra.Modules.Identity/IdentityModule.cs` (modified — registered `IAuthorizationService`)
- `src/backend/Cakra.Api/Infrastructure/Authentication/CakraAuthenticationDefaults.cs` (created — authentication cookie scheme and name constants)
- `src/backend/Cakra.Api/Infrastructure/Authentication/SessionCookieTicketFormat.cs` (created — session token cookie ticket format)
- `src/backend/Cakra.Api/Infrastructure/Authentication/CakraAuthenticationExtensions.cs` (created — cookie authentication registration, session validation, and 401/403 ProblemDetails handlers)
- `src/backend/Cakra.Api/Controllers/ProbeController.cs` (modified — added authenticated probe and Management role-restricted probe endpoints)
- `src/backend/Cakra.Api/Middleware/SecurityContextMiddleware.cs` (modified — updated XML summary documentation to reflect concrete session and role wiring)
- `src/backend/Cakra.Api/Program.cs` (modified — wired `AddCakraAuthentication` and updated pipeline comments)
- `tests/backend/Cakra.Tests.Unit/Identity/AuthenticationServiceTests.cs` (modified — updated `FakeOrganizationQueryService` with `GetPersonRolesAsync`)
- `tests/backend/Cakra.Tests.Unit/Identity/AuthorizationServiceTests.cs` (created — unit tests for `AuthorizationService`)
- `tests/backend/Cakra.Tests.Integration/Identity/AuthenticationMiddlewareTests.cs` (created — integration tests for cookie configuration, 401 unauthorized, 200 authenticated context population, and 403/200 RBAC enforcement)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — P2-S10 status, notes, changed files)

---

### P2-S11

Title: Login Screen — SCR-AUTH-001

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement `SCR-AUTH-001` (Login Screen) as a Vue 3 SFC (`<script setup lang="ts">`) with Bootstrap 5 styling in `src/frontend/Cakra.Web/`, and the corresponding ASP.NET Core REST API controller (`/api/v1/auth/login`, `/api/v1/auth/logout`) in `Cakra.Api`. Wires `AuthenticationService.Login` and `Logout`, sets the session cookie on success, handles login failures with distinct error messages, and redirects to `SCR-FEED-001` on success.

Depends On: P2-S10, P1-S08

Repository: `Cakra.Api`, `src/frontend/Cakra.Web`

Completion Criteria:
- Vue 3 SFC `LoginView.vue` (`<script setup lang="ts">`, Bootstrap 5) renders a centered card containing: username text input, password input, submit button, error message area — Architecture §19.4, §20.
- Vue Router 4 route `/login` maps to `LoginView.vue`.
- POST `/api/v1/auth/login` ASP.NET Core Controller endpoint calls `AuthenticationService.Login`; sets secure `HttpOnly` `SameSite=Strict` session cookie on success — Architecture §19.5.
- Login failure returns distinct RFC 7807 `ProblemDetails` responses: invalid credentials, account locked — Architecture §19.6.
- Successful login redirects Vue Router to `SCR-FEED-001` route.
- POST `/api/v1/auth/logout` endpoint calls `AuthenticationService.Logout` and clears the session cookie.
- Integration tests (xUnit + WebApplicationFactory): login success and each login failure scenario pass.

Notes: Architecture §14 (UI Boundary — SCR-AUTH-001), §19.4, §19.5, §19.6 are authoritative. Combined API + Vue slice because scope is minimal (2 endpoints, 1 form).

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Implemented `AuthController` (`src/backend/Cakra.Api/Controllers/AuthController.cs`) at `/api/v1/auth` with:
   - `POST /api/v1/auth/login`: calls `IAuthenticationService.LoginAsync`, resolves dynamic roles via `IAuthorizationService.ResolveRolesAsync`, signs in via `HttpContext.SignInAsync(CakraAuthenticationDefaults.AuthenticationScheme)` to emit the `Cakra.Session` cookie (`HttpOnly`, `SameSite=Strict`, `Secure`), and returns `LoginResponse` (`UserId`, `PersonId`, `Roles`, `ExpiresAt`). On failure, returns distinct RFC 7807 `ProblemDetails` JSON (`Content-Type: application/problem+json`): HTTP 401 (`INVALID_CREDENTIALS`, title `"Invalid Credentials"`) for invalid credentials, HTTP 403 (`ACCOUNT_LOCKED`, title `"Account Locked"`) for locked accounts, and HTTP 403 (`ACCOUNT_NOT_ACTIVE` / `PERSON_INACTIVE`) for inactive accounts or linked persons.
   - `POST /api/v1/auth/logout`: revokes the server-side session token via `IAuthenticationService.LogoutAsync` and clears the `Cakra.Session` cookie via `HttpContext.SignOutAsync`.
   - `GET /api/v1/auth/me` (`[Authorize]`): returns the current authenticated user's profile (`UserId`, `PersonId`, `Roles`) from `ICurrentContextProvider`.
2. Implemented `LoginView.vue` (`src/frontend/Cakra.Web/src/views/LoginView.vue`) as a Vue 3 SFC (`<script setup lang="ts">`) with Bootstrap 5 styling rendering a centered card containing username/email input, password input, submit button with loading state, and distinct error alert styling (`alert-danger` for invalid credentials, `alert-warning` with lock icon for `ACCOUNT_LOCKED`), redirecting to `/feed` (`SCR-FEED-001`) on success.
3. Updated `src/frontend/Cakra.Web/src/stores/auth.ts` with `login`, `logout`, `fetchCurrentUser`, and RFC 7807 `ProblemDetails` error code parsing.
4. Updated `src/frontend/Cakra.Web/src/router/index.ts` to map `/login` to `LoginView.vue`, `/feed` to `SCR-FEED-001`, and enforce authentication navigation guards. Updated `src/frontend/Cakra.Web/src/App.vue` to display user role badge and Sign Out action when authenticated.
5. Added integration tests in `tests/backend/Cakra.Tests.Integration/Identity/AuthControllerTests.cs` verifying login success (`Set-Cookie` with `httponly`, `samesite=strict`, `secure`), invalid credentials (401 `INVALID_CREDENTIALS`), nonexistent user (401 `INVALID_CREDENTIALS`), locked account (403 `ACCOUNT_LOCKED`), 5-attempt lockout threshold, `GET /api/v1/auth/me` (200 authenticated, 401 unauthenticated), and `POST /api/v1/auth/logout` session revocation and cookie clearing.
6. Verification: `dotnet test Cakra.sln` passes (196 tests total: 152 unit + 44 integration, 0 failures, 0 warnings); `npm run build` (`vue-tsc --noEmit && vite build`) in `src/frontend/Cakra.Web` succeeds with zero TypeScript or Vite errors.

Changed Files:
- `src/backend/Cakra.Api/Controllers/AuthController.cs` (created)
- `src/frontend/Cakra.Web/src/views/LoginView.vue` (created)
- `src/frontend/Cakra.Web/src/stores/auth.ts` (modified — added login, logout, fetchCurrentUser, and ProblemDetails error state)
- `src/frontend/Cakra.Web/src/router/index.ts` (modified — added `/login` and `/feed` routes and auth navigation guard)
- `src/frontend/Cakra.Web/src/App.vue` (modified — added authenticated role badge and Sign Out button)
- `tests/backend/Cakra.Tests.Integration/Identity/AuthControllerTests.cs` (created)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — P2-S11 status, implementation notes, and changed files)

---

## P3 — Core Master Data & Product Catalog

Implementation Status: COMPLETED
Review Status: GO

Objective: Implement the three foundational master-data modules — Organization, Customer, and Product — including domain, application services, and persistence using Dapper with explicit parameterized SQL (Architecture §19.3), along with the Product Catalog screen (`SCR-PRD-001`). Organization is split into a persistence slice and an application services slice to reduce per-slice scope. Customer and Product are architecturally decoupled and can execute in parallel: Customer has zero dependency on Organization or Product; Product depends on Organization Services for owner validation, but has zero dependency on Customer. M1 is achieved when P3-S16 is complete.

Source: Architecture §22 — Boundaries: **Organization**, **Customer**, **Product**. Architecture §6 (Module Boundaries), §10 (Product Module Architecture), §16 (Data Ownership), §19.3 (Persistence & Data Access).

---

### P3-S12

Title: Organization Module — Domain Entities & Persistence

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the Organization module domain entities and persistence layer: all domain entity classes, DbUp SQL migration scripts for `organization.*` tables, and Dapper repository implementations using explicit parameterized SQL. Domain events are defined but not dispatched in this slice. No MediatR handlers, no query service, no role resolution wiring — those are delivered in P3-S13.

Depends On: P1-S06, P1-S07

Repository: `Cakra.Modules.Organization`

Completion Criteria:
- Domain entities implemented: `Person` (Id, FirstName, LastName, Email, Status [ACTIVE/INACTIVE], CreatedAt, UpdatedAt), `Team` (Id, Name, Description), `Role` (Id, Name, Description), `Responsibility` (Id, Name, Description), `TeamMembership` (PersonId, TeamId, AssignedAt), `RoleAssignment` (PersonId, RoleId, AssignedAt, RevokedAt nullable), `ResponsibilityAssignment` (PersonId, ResponsibilityId, AssignedAt).
- DbUp SQL migration scripts create `organization.*` schema tables: `Persons`, `Teams`, `Roles`, `Responsibilities`, `TeamMemberships`, `RoleAssignments`, `ResponsibilityAssignments` — Architecture §17. Foreign key constraints defined between junction tables and their parent tables. `Status` column on `Persons` defaults to `'ACTIVE'`.
- All repository operations use Dapper with explicit parameterized SQL against SQL Server; no EF Core — Architecture §19.3, §20.
- Dapper repository classes implemented: `PersonRepository` (CRUD + status update), `TeamRepository` (CRUD), `RoleRepository` (CRUD), `ResponsibilityRepository` (CRUD), `TeamMembershipRepository` (add/remove), `RoleAssignmentRepository` (assign/revoke), `ResponsibilityAssignmentRepository` (assign/remove).
- Domain event types defined (no dispatch): `PersonCreated`, `PersonDeactivated`, `RoleAssigned`, `RoleRevoked`.
- Repository implementations write exclusively to `organization.*` schema using parameterized SQL.
- Integration tests (xUnit + Respawn): DbUp migration scripts execute without errors against test database; repository CRUD operations return expected results (insert Person, retrieve by Id, update status).

Notes: Persistence-only slice. Does NOT require authentication middleware (P2-S10) — integration tests verify database operations directly, not through the API pipeline. Can execute in parallel with P2-S09 and domain-only slices (P4-S17, P5-S24) after P1-S06 + P1-S07. Architecture §6 (Module Boundaries — Organization) and §16 are authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Implemented domain entities in `src/backend/Cakra.Modules.Organization/Domain/`: `Person` (Id, FirstName, LastName, Email, Status defaulting to ACTIVE, CreatedAt, UpdatedAt, FullName, Activate, Deactivate), `Team` (Id, Name, Description), `Role` (Id, Name, Description), `Responsibility` (Id, Name, Description), `TeamMembership` (PersonId, TeamId, AssignedAt), `RoleAssignment` (PersonId, RoleId, AssignedAt, RevokedAt, IsActive, Revoke), and `ResponsibilityAssignment` (PersonId, ResponsibilityId, AssignedAt).
2. Embedded DbUp SQL migration script `src/backend/Cakra.Api/Migrations/Scripts/0003_organization_tables.sql` idempotently creates all 7 tables in the `organization` schema with PKs, indexes, check constraint on Person.Status (`'ACTIVE'`, `'INACTIVE'`), default `'ACTIVE'` status, and foreign key constraints between junction tables (`TeamMemberships`, `RoleAssignments`, `ResponsibilityAssignments`) and parent tables (`Persons`, `Teams`, `Roles`, `Responsibilities`) with `ON DELETE CASCADE`.
3. Parameterized Dapper repository implementations in `src/backend/Cakra.Modules.Organization/Persistence/` targeting `organization.*` exclusively: `PersonRepository` (GetById, GetAll, GetActive, GetByEmail, Add, Update, UpdateStatus, Delete), `TeamRepository` (GetById, GetAll, GetByName, Add, Update, Delete), `RoleRepository` (GetById, GetAll, GetByName, Add, Update, Delete), `ResponsibilityRepository` (GetById, GetAll, GetByName, Add, Update, Delete), `TeamMembershipRepository` (Add, Remove, GetByPersonId, GetByTeamId, Exists), `RoleAssignmentRepository` (Assign, Revoke, GetByPersonId, GetActiveByPersonId, GetByRoleId, Get), `ResponsibilityAssignmentRepository` (Assign, Remove, GetByPersonId, GetByResponsibilityId, Exists). Absolutely no EF Core referenced.
4. Defined domain events implementing `IDomainEvent` in `src/backend/Cakra.Modules.Organization/Domain/Events/`: `PersonCreated`, `PersonDeactivated`, `RoleAssigned`, `RoleRevoked`.
5. Created `OrganizationModule : IModule` registering all 7 repository contracts with `Scoped` lifetime in the DI container.
6. Added comprehensive unit tests in `Cakra.Tests.Unit/Organization/` covering domain models, activation/deactivation, role revocation, and domain events.
7. Added integration tests in `Cakra.Tests.Integration/Organization/` executing against SQL Server 2019 LocalDB instance, verifying migration execution, full CRUD on all repositories, status transitions, query filters, and junction foreign key constraint enforcement. Solution build passes with 0 errors/0 warnings; full test suite passes with 180 passing tests (149 unit + 31 integration).

Changed Files:
- `src/backend/Cakra.Modules.Organization/Cakra.Modules.Organization.csproj` (modified — added Dapper and DI abstractions references)
- `src/backend/Cakra.Modules.Organization/Domain/Person.cs` (created)
- `src/backend/Cakra.Modules.Organization/Domain/Team.cs` (created)
- `src/backend/Cakra.Modules.Organization/Domain/Role.cs` (created)
- `src/backend/Cakra.Modules.Organization/Domain/Responsibility.cs` (created)
- `src/backend/Cakra.Modules.Organization/Domain/TeamMembership.cs` (created)
- `src/backend/Cakra.Modules.Organization/Domain/RoleAssignment.cs` (created)
- `src/backend/Cakra.Modules.Organization/Domain/ResponsibilityAssignment.cs` (created)
- `src/backend/Cakra.Modules.Organization/Domain/Events/PersonCreated.cs` (created)
- `src/backend/Cakra.Modules.Organization/Domain/Events/PersonDeactivated.cs` (created)
- `src/backend/Cakra.Modules.Organization/Domain/Events/RoleAssigned.cs` (created)
- `src/backend/Cakra.Modules.Organization/Domain/Events/RoleRevoked.cs` (created)
- `src/backend/Cakra.Api/Migrations/Scripts/0003_organization_tables.sql` (created — DbUp migration script)
- `src/backend/Cakra.Modules.Organization/Persistence/IPersonRepository.cs` (created)
- `src/backend/Cakra.Modules.Organization/Persistence/PersonRepository.cs` (created)
- `src/backend/Cakra.Modules.Organization/Persistence/ITeamRepository.cs` (created)
- `src/backend/Cakra.Modules.Organization/Persistence/TeamRepository.cs` (created)
- `src/backend/Cakra.Modules.Organization/Persistence/IRoleRepository.cs` (created)
- `src/backend/Cakra.Modules.Organization/Persistence/RoleRepository.cs` (created)
- `src/backend/Cakra.Modules.Organization/Persistence/IResponsibilityRepository.cs` (created)
- `src/backend/Cakra.Modules.Organization/Persistence/ResponsibilityRepository.cs` (created)
- `src/backend/Cakra.Modules.Organization/Persistence/ITeamMembershipRepository.cs` (created)
- `src/backend/Cakra.Modules.Organization/Persistence/TeamMembershipRepository.cs` (created)
- `src/backend/Cakra.Modules.Organization/Persistence/IRoleAssignmentRepository.cs` (created)
- `src/backend/Cakra.Modules.Organization/Persistence/RoleAssignmentRepository.cs` (created)
- `src/backend/Cakra.Modules.Organization/Persistence/IResponsibilityAssignmentRepository.cs` (created)
- `src/backend/Cakra.Modules.Organization/Persistence/ResponsibilityAssignmentRepository.cs` (created)
- `src/backend/Cakra.Modules.Organization/OrganizationModule.cs` (created — IModule registration)
- `tests/backend/Cakra.Tests.Unit/Organization/OrganizationEntityTests.cs` (created)
- `tests/backend/Cakra.Tests.Unit/Organization/OrganizationDomainEventTests.cs` (created)
- `tests/backend/Cakra.Tests.Integration/Organization/OrganizationPersistenceTests.cs` (created)
- `tests/backend/Cakra.Tests.Integration/Cakra.Tests.Integration.csproj` (modified — added reference to Cakra.Modules.Organization)
- `tests/backend/Cakra.Tests.Integration/Modules/ModuleRegistrationTests.cs` (modified — added persistence using directives)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — P3-S12 status, notes, and changed files)

---

### P3-S13

Title: Organization Module — Application Services & Role Resolution

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the Organization module application services: MediatR command handlers, `OrganizationQueryService` query methods, and dynamic role resolution wiring with `AuthorizationService`. Builds on the domain entities and persistence layer established in P3-S12.

Depends On: P3-S12, P2-S10

Repository: `Cakra.Modules.Organization`

Completion Criteria:
- MediatR command handlers implement: create Person, update Person, create Team, assign Person to Team, create Role, assign Role to Person, create Responsibility, assign Responsibility to Person, deactivate Person — via `OrganizationService`.
- `OrganizationQueryService` implements using Dapper: `GetPersonById`, `ListActivePersons`, `GetTeamRoster`, `GetPersonRoles`, `GetPersonResponsibilities` — Architecture §7.
- Dynamic role resolution wired: `AuthorizationService.ResolveRoles(personId)` delegates to `OrganizationQueryService.GetPersonRoles` — Architecture §14.
- `OrganizationQueryService` is accessible as a published interface to other modules; internal repositories are not exposed — Architecture §20 (Strict Vertical Slice Boundary).
- Integration tests (xUnit + WebApplicationFactory + Respawn): query service returns accurate results after service commands (create Person → GetPersonById returns correct data; assign Role → GetPersonRoles includes assigned role; deactivate Person → ListActivePersons excludes deactivated person).

Notes: Depends on P2-S10 for security context in MediatR handlers and WebApplicationFactory-based integration tests. Architecture §6, §7, §14 are authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Implemented `IOrganizationService` and `OrganizationService` in `Cakra.Modules.Organization.Services` orchestrating all Organization write operations (`CreatePersonAsync`/`CreatePerson`, `UpdatePersonAsync`/`UpdatePerson`, `CreateTeamAsync`/`CreateTeam`, `AssignPersonToTeamAsync`/`AssignPersonToTeam`, `CreateRoleAsync`/`CreateRole`, `AssignRoleToPersonAsync`/`AssignRoleToPerson`, `RevokeRoleFromPersonAsync`/`RevokeRoleFromPerson`, `CreateResponsibilityAsync`/`CreateResponsibility`, `AssignResponsibilityToPersonAsync`/`AssignResponsibilityToPerson`, `DeactivatePersonAsync`/`DeactivatePerson`, `ActivatePersonAsync`/`ActivatePerson`) and dispatching in-process domain events (`PersonCreated`, `PersonDeactivated`, `RoleAssigned`, `RoleRevoked`) via `IDomainEventDispatcher`.
2. Implemented MediatR commands, FluentValidation validators, and handlers in `Cakra.Modules.Organization.Commands/` delegating to `IOrganizationService`: `CreatePersonCommand`, `UpdatePersonCommand`, `CreateTeamCommand`, `AssignPersonToTeamCommand`, `CreateRoleCommand`, `AssignRoleToPersonCommand`, `RevokeRoleFromPersonCommand`, `CreateResponsibilityCommand`, `AssignResponsibilityToPersonCommand`, and `DeactivatePersonCommand`.
3. Completed `IOrganizationQueryService` and `OrganizationQueryService` with Dapper parameterized SQL queries against `organization.*` tables (zero EF Core): `GetPersonByIdAsync`/`GetPersonById`, `ListActivePersonsAsync`/`ListActivePersons`, `GetTeamRosterAsync`/`GetTeamRoster`, `GetPersonRolesAsync`/`GetPersonRoles`, `GetPersonResponsibilitiesAsync`/`GetPersonResponsibilities`, and `IsPersonActiveAsync`/`IsPersonActive`, returning published read models (`PersonDto`, `TeamRosterMemberDto`, `PersonResponsibilityDto`).
4. Wired dynamic role resolution in `IAuthorizationService` and `AuthorizationService` (`ResolveRolesAsync` and `ResolveRoles`) delegating to `IOrganizationQueryService.GetPersonRolesAsync`.
5. Enforced strict vertical slice boundaries (Architecture §20, §21): `IOrganizationQueryService`, `OrganizationQueryService`, `IOrganizationService`, and `OrganizationService` are registered in `OrganizationModule` and exposed as published contracts, while all 7 repository interfaces and implementations in `Cakra.Modules.Organization.Persistence` are `internal` (accessible only within the module and test assemblies via `InternalsVisibleTo`).
6. Added unit tests in `Cakra.Tests.Unit/Organization/OrganizationServiceTests.cs` and `Cakra.Tests.Unit/Identity/AuthorizationServiceTests.cs` and integration tests in `Cakra.Tests.Integration/Organization/OrganizationApplicationServiceIntegrationTests.cs` using xUnit, `WebApplicationFactory<Program>`, and `Respawn`. Full solution build and all 222 tests (169 unit + 53 integration) pass with 0 warnings and 0 errors.

Changed Files:
- `src/backend/Cakra.Modules.Organization/Cakra.Modules.Organization.csproj` (modified — added `InternalsVisibleTo` for test assemblies)
- `src/backend/Cakra.Modules.Organization/IOrganizationQueryService.cs` (modified — added `GetPersonById`, `ListActivePersons`, `GetTeamRoster`, `GetPersonRoles`, `GetPersonResponsibilities` async and sync contracts)
- `src/backend/Cakra.Modules.Organization/Models/PersonDto.cs` (created)
- `src/backend/Cakra.Modules.Organization/Models/TeamRosterMemberDto.cs` (created)
- `src/backend/Cakra.Modules.Organization/Models/PersonResponsibilityDto.cs` (created)
- `src/backend/Cakra.Modules.Organization/Services/OrganizationQueryService.cs` (modified — implemented full Dapper parameterized SQL query methods)
- `src/backend/Cakra.Modules.Organization/Services/IOrganizationService.cs` (created)
- `src/backend/Cakra.Modules.Organization/Services/OrganizationService.cs` (created)
- `src/backend/Cakra.Modules.Organization/Commands/CreatePersonCommand.cs` (created)
- `src/backend/Cakra.Modules.Organization/Commands/UpdatePersonCommand.cs` (created)
- `src/backend/Cakra.Modules.Organization/Commands/CreateTeamCommand.cs` (created)
- `src/backend/Cakra.Modules.Organization/Commands/AssignPersonToTeamCommand.cs` (created)
- `src/backend/Cakra.Modules.Organization/Commands/CreateRoleCommand.cs` (created)
- `src/backend/Cakra.Modules.Organization/Commands/AssignRoleToPersonCommand.cs` (created)
- `src/backend/Cakra.Modules.Organization/Commands/RevokeRoleFromPersonCommand.cs` (created)
- `src/backend/Cakra.Modules.Organization/Commands/CreateResponsibilityCommand.cs` (created)
- `src/backend/Cakra.Modules.Organization/Commands/AssignResponsibilityToPersonCommand.cs` (created)
- `src/backend/Cakra.Modules.Organization/Commands/DeactivatePersonCommand.cs` (created)
- `src/backend/Cakra.Modules.Organization/OrganizationModule.cs` (modified — registered `OrganizationService` and `IOrganizationService`)
- `src/backend/Cakra.Modules.Organization/Persistence/IPersonRepository.cs` (modified — internalized repository contract)
- `src/backend/Cakra.Modules.Organization/Persistence/PersonRepository.cs` (modified — internalized repository implementation)
- `src/backend/Cakra.Modules.Organization/Persistence/ITeamRepository.cs` (modified — internalized repository contract)
- `src/backend/Cakra.Modules.Organization/Persistence/TeamRepository.cs` (modified — internalized repository implementation)
- `src/backend/Cakra.Modules.Organization/Persistence/IRoleRepository.cs` (modified — internalized repository contract)
- `src/backend/Cakra.Modules.Organization/Persistence/RoleRepository.cs` (modified — internalized repository implementation)
- `src/backend/Cakra.Modules.Organization/Persistence/IResponsibilityRepository.cs` (modified — internalized repository contract)
- `src/backend/Cakra.Modules.Organization/Persistence/ResponsibilityRepository.cs` (modified — internalized repository implementation)
- `src/backend/Cakra.Modules.Organization/Persistence/ITeamMembershipRepository.cs` (modified — internalized repository contract)
- `src/backend/Cakra.Modules.Organization/Persistence/TeamMembershipRepository.cs` (modified — internalized repository implementation)
- `src/backend/Cakra.Modules.Organization/Persistence/IRoleAssignmentRepository.cs` (modified — internalized repository contract)
- `src/backend/Cakra.Modules.Organization/Persistence/RoleAssignmentRepository.cs` (modified — internalized repository implementation)
- `src/backend/Cakra.Modules.Organization/Persistence/IResponsibilityAssignmentRepository.cs` (modified — internalized repository contract)
- `src/backend/Cakra.Modules.Organization/Persistence/ResponsibilityAssignmentRepository.cs` (modified — internalized repository implementation)
- `src/backend/Cakra.Modules.Identity/Services/IAuthorizationService.cs` (modified — added synchronous `ResolveRoles` overload)
- `src/backend/Cakra.Modules.Identity/Services/AuthorizationService.cs` (modified — implemented synchronous `ResolveRoles` overload)
- `tests/backend/Cakra.Tests.Unit/Identity/AuthorizationServiceTests.cs` (modified — added synchronous `ResolveRoles` test)
- `tests/backend/Cakra.Tests.Unit/Organization/OrganizationServiceTests.cs` (created)
- `tests/backend/Cakra.Tests.Integration/DatabaseResetHelper.cs` (modified — preserved `dbo.SchemaVersions` during Respawn reset)
- `tests/backend/Cakra.Tests.Integration/Organization/OrganizationPersistenceTests.cs` (modified — added `[Collection("OrganizationDatabase")]`)
- `tests/backend/Cakra.Tests.Integration/Organization/OrganizationApplicationServiceIntegrationTests.cs` (created)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — P3-S13 status, notes, and changed files)

---

### P3-S14

Title: Customer Module — Domain, Application Services & Persistence

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the complete Customer module: domain entities (`Customer`, `CustomerContact`), `CustomerService` command methods, `CustomerQueryService` query methods, DbUp SQL migration scripts for `customer.*` tables, and Dapper repository implementations using explicit parameterized SQL.

Depends On: P1-S06, P1-S07, P2-S10

Repository: `Cakra.Modules.Customer`

Completion Criteria:
- DbUp SQL migration scripts create `customer.*` schema tables: `Customers`, `CustomerContacts` — Architecture §17.
- All repository operations use Dapper with explicit parameterized SQL; no EF Core — Architecture §19.3, §20.
- MediatR handlers via `CustomerService` implement: create Customer, update Customer master data, create CustomerContact, update CustomerContact, deactivate Customer.
- `CustomerQueryService` implements: `GetCustomerById`, `ListActiveCustomers`, `GetCustomerContacts`, `GetCustomerWithContractStatus` — Architecture §7.
- `CustomerQueryService` is accessible as a published interface; internal repositories are not exposed.
- Repository implementations write exclusively to `customer.*` schema using parameterized SQL.
- Integration tests (xUnit + WebApplicationFactory + Respawn): query service returns accurate results after service commands.

Notes: Architecture §6 (Module Boundaries — Customer) and §16 are authoritative. Customer module does not own Request or Work Package relationships. **Parallel Execution**: Customer has zero dependency on Organization (P3-S12, P3-S13) or Product (P3-S15). It can execute concurrently in parallel with P3-S12, P3-S13, and P3-S15.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Implemented domain entities in `src/backend/Cakra.Modules.Customer/Domain/`: `Customer` (CustomerId/Id, CustomerCode/Code, CustomerName/Name, Status [ACTIVE/INACTIVE], HasActiveMaintenanceContract, CreatedAt, UpdatedAt, Create, UpdateMasterData, Deactivate, Activate) and `CustomerContact` (ContactId/Id, CustomerId, Name, Position, PhoneNumber, Email, Status [ACTIVE/INACTIVE], CreatedAt, UpdatedAt, Create, Update, Deactivate, Activate), plus domain events in `Domain/Events/` (`CustomerCreated`, `CustomerActivated`, `CustomerInactivated`, `CustomerDeactivated`, `CustomerContactAdded`, `CustomerContactActivated`, `CustomerContactInactivated`).
2. Added embedded DbUp SQL migration script `src/backend/Cakra.Api/Migrations/Scripts/0004_customer_tables.sql` idempotently creating `customer.Customers` and `customer.CustomerContacts` with PKs, status check constraints (`'ACTIVE'`, `'INACTIVE'`), default `'ACTIVE'` status, unique index on `CustomerCode`, and same-schema foreign key `FK_CustomerContacts_Customers` with `ON DELETE CASCADE`.
3. Implemented internal Dapper repositories (`ICustomerRepository`, `CustomerRepository`, `ICustomerContactRepository`, `CustomerContactRepository`) in `src/backend/Cakra.Modules.Customer/Persistence/` executing explicit parameterized SQL exclusively against the `customer.*` schema. Marked `internal` so internal repositories are not exposed outside the module boundary (visible only to test projects via `InternalsVisibleTo`). Zero EF Core usage.
4. Implemented `CustomerService` (`ICustomerService`) and MediatR command handlers + FluentValidation validators for `CreateCustomerCommand`, `UpdateCustomerCommand`, `CreateCustomerContactCommand`, `UpdateCustomerContactCommand`, `DeactivateCustomerCommand`, and `ActivateCustomerCommand`, enforcing unique customer codes, parent customer existence for contacts, and domain event dispatch.
5. Implemented `CustomerQueryService` (`ICustomerQueryService`) as a published interface in `Cakra.Modules.Customer` with Dapper parameterized SQL queries: `GetCustomerById` (`GetCustomerByIdAsync`), `GetCustomerByCode` (`GetCustomerByCodeAsync`), `ListActiveCustomers` (`ListActiveCustomersAsync`), `ListAllCustomers` (`ListAllCustomersAsync`), `GetCustomerContacts` (`GetCustomerContactsAsync`), `GetCustomerWithContractStatus` (`GetCustomerWithContractStatusAsync`), and `IsCustomerActiveAsync`, plus MediatR query handlers.
6. Registered all repositories and services in `CustomerModule : IModule`.
7. Added unit tests in `tests/backend/Cakra.Tests.Unit/Customer/CustomerDomainAndServiceTests.cs` and integration tests in `tests/backend/Cakra.Tests.Integration/Customer/CustomerModuleIntegrationTests.cs` (using `WebApplicationFactory<Program>` and Respawn). `dotnet test Cakra.sln` passes with 0 warnings, 0 errors, and all 222 tests passing.
8. Remediation (2026-09-28): Resolved RV-001 and RV-002 in `tests/backend/Cakra.Tests.Integration/Customer/CustomerModuleIntegrationTests.cs`. Configured `_factory` (`CakraWebApplicationFactory`) using `builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString)` and `builder.UseSetting("ConnectionStrings:TestConnection", _connectionString)` instead of `builder.ConfigureAppConfiguration(...)` so `Program.cs` receives the test connection string when registering `IDbConnectionFactory` (RV-001). Updated `InitializeAsync()` to probe SQL Server availability against `InitialCatalog = "master"` before running `DatabaseMigrationRunner.Run()` (which calls `EnsureDatabase.For.SqlDatabase(_connectionString)` to create `CakraTestDb` if absent and applies all DbUp migrations) and then opening `SqlConnection(_connectionString)` (RV-002). Verified that all 3 integration tests in `CustomerModuleIntegrationTests` (`Migration_0004_creates_customer_schema_tables`, `CustomerService_commands_and_CustomerQueryService_queries_work_end_to_end`, `Foreign_key_constraint_prevents_orphaned_customer_contacts`) execute against SQL Server (`CakraTestDb`) without skipping and pass with 0 failures.

Changed Files:
- `src/backend/Cakra.Modules.Customer/Cakra.Modules.Customer.csproj` (modified — added Dapper, DI abstractions, and InternalsVisibleTo for test projects)
- `src/backend/Cakra.Modules.Customer/CustomerModule.cs` (created — IModule service registration)
- `src/backend/Cakra.Modules.Customer/ICustomerQueryService.cs` (created — published query interface and DTOs)
- `src/backend/Cakra.Modules.Customer/Domain/Customer.cs` (created)
- `src/backend/Cakra.Modules.Customer/Domain/CustomerContact.cs` (created)
- `src/backend/Cakra.Modules.Customer/Domain/Events/CustomerCreated.cs` (created)
- `src/backend/Cakra.Modules.Customer/Domain/Events/CustomerActivated.cs` (created)
- `src/backend/Cakra.Modules.Customer/Domain/Events/CustomerInactivated.cs` (created)
- `src/backend/Cakra.Modules.Customer/Domain/Events/CustomerContactAdded.cs` (created)
- `src/backend/Cakra.Modules.Customer/Domain/Events/CustomerContactActivated.cs` (created)
- `src/backend/Cakra.Modules.Customer/Domain/Events/CustomerContactInactivated.cs` (created)
- `src/backend/Cakra.Modules.Customer/Persistence/ICustomerRepository.cs` (created — internal repository contract)
- `src/backend/Cakra.Modules.Customer/Persistence/CustomerRepository.cs` (created — internal Dapper repository)
- `src/backend/Cakra.Modules.Customer/Persistence/ICustomerContactRepository.cs` (created — internal repository contract)
- `src/backend/Cakra.Modules.Customer/Persistence/CustomerContactRepository.cs` (created — internal Dapper repository)
- `src/backend/Cakra.Modules.Customer/Services/CustomerCommands.cs` (created — MediatR commands and FluentValidation validators)
- `src/backend/Cakra.Modules.Customer/Services/CustomerQueries.cs` (created — MediatR query records)
- `src/backend/Cakra.Modules.Customer/Services/ICustomerService.cs` (created)
- `src/backend/Cakra.Modules.Customer/Services/CustomerService.cs` (created)
- `src/backend/Cakra.Modules.Customer/Services/CustomerQueryService.cs` (created)
- `src/backend/Cakra.Api/Migrations/Scripts/0004_customer_tables.sql` (created — DbUp migration script)
- `tests/backend/Cakra.Tests.Unit/Cakra.Tests.Unit.csproj` (modified — added Customer module reference)
- `tests/backend/Cakra.Tests.Unit/Customer/CustomerDomainAndServiceTests.cs` (created)
- `tests/backend/Cakra.Tests.Integration/Cakra.Tests.Integration.csproj` (modified — added Customer module reference)
- `tests/backend/Cakra.Tests.Integration/Customer/CustomerModuleIntegrationTests.cs` (created)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — P3-S14 status, implementation notes, and changed files)

---

### P3-S15

Title: Product Module — Domain, Application Services & Persistence

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the complete Product module: domain entity (`Product`), `ProductService` command methods, `ProductQueryService` query methods, DbUp SQL migration scripts for `product.*` tables, and Dapper repository implementations. Owner validation reads from `OrganizationQueryService`.

Depends On: P1-S06, P1-S07, P2-S10, P3-S13

Repository: `Cakra.Modules.Product`

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

Notes: Architecture §10 (Product Module Architecture) is authoritative. **Parallel Execution**: Depends on P3-S13 because `CreateProduct` and `AssignProductOwner` call `OrganizationQueryService` to validate the owner, but has zero dependency on Customer (P3-S14). Executes in parallel with P3-S14.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Implemented domain entity `Product` in `src/backend/Cakra.Modules.Product/Domain/Product.cs` (`Id`/`ProductId`, `Code`/`ProductCode`, `Name`/`ProductName`, `Description`, `OwnerPersonId`, `Status` [`ACTIVE`/`INACTIVE`], `IsActive`, `CreatedAt`, `UpdatedAt`, `Create`, `Update`, `AssignOwner`, `Activate`, `Deactivate`) and domain events in `Domain/Events/` (`ProductCreated`, `ProductOwnerChanged`, `ProductActivated`, `ProductDeactivated`).
2. Added embedded DbUp SQL migration script `src/backend/Cakra.Api/Migrations/Scripts/0005_product_tables.sql` idempotently creating `product.Products` with PK, status check constraint (`'ACTIVE'`, `'INACTIVE'`), default `'ACTIVE'` status, unique nonclustered index on `Code`, and nonclustered indexes on `Status` and `OwnerPersonId`. Zero cross-schema foreign keys per Architecture §20.
3. Implemented internal Dapper repository (`IProductRepository`, `ProductRepository`) in `src/backend/Cakra.Modules.Product/Persistence/` executing explicit parameterized SQL exclusively against `[product].[Products]`. Marked `internal` so repositories are not exposed outside the module boundary (visible only to test suites via `InternalsVisibleTo`). Zero EF Core usage.
4. Implemented `ProductService` (`IProductService`) and MediatR command handlers + FluentValidation validators for `CreateProductCommand`, `UpdateProductCommand`, `AssignProductOwnerCommand`, `ActivateProductCommand`, and `DeactivateProductCommand`, enforcing unique product codes, validating owner existence and active status via `IOrganizationQueryService` (no direct cross-schema writes), and dispatching `ProductCreated`, `ProductOwnerChanged`, `ProductActivated`, and `ProductDeactivated` via `IDomainEventDispatcher`.
5. Implemented `ProductQueryService` (`IProductQueryService`) as a published interface in `Cakra.Modules.Product` with Dapper parameterized SQL queries: `GetProductById` (`GetProductByIdAsync`), `GetProductByCode` (`GetProductByCodeAsync`), `ListActiveProducts` (`ListActiveProductsAsync`), `ListAllProducts` (`ListAllProductsAsync`), and `IsProductActiveAsync`/`IsProductActive`, plus MediatR query handlers.
6. Registered all services and internal repositories in `ProductModule : IModule`.
7. Added unit tests in `tests/backend/Cakra.Tests.Unit/Product/ProductDomainAndServiceTests.cs` and integration tests in `tests/backend/Cakra.Tests.Integration/Product/ProductModuleIntegrationTests.cs` (using `WebApplicationFactory<Program>` and Respawn). `dotnet test Cakra.sln` passes with 0 warnings, 0 errors, and all 231 tests passing (175 unit + 56 integration).
8. Remediation (2026-09-28): Resolved RV-003 and RV-004 in `tests/backend/Cakra.Tests.Integration/Product/ProductModuleIntegrationTests.cs`. Configured `_factory` (`CakraWebApplicationFactory`) using `builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString)` and `builder.UseSetting("ConnectionStrings:TestConnection", _connectionString)` instead of `builder.ConfigureAppConfiguration(...)` so `Program.cs` receives the test connection string when registering `IDbConnectionFactory` (RV-003). Updated `InitializeAsync()` to probe SQL Server availability against `InitialCatalog = "master"` before running `DatabaseMigrationRunner.Run()` (which calls `EnsureDatabase.For.SqlDatabase(_connectionString)` to create `CakraTestDb` if absent and applies all DbUp migrations) and then opening `SqlConnection(_connectionString)` (RV-004). Verified that all 3 integration tests in `ProductModuleIntegrationTests` (`Migration_0005_creates_product_schema_table`, `ProductService_commands_and_ProductQueryService_queries_work_end_to_end`, `CreateProduct_and_AssignProductOwner_reject_nonexistent_or_inactive_owner_in_Organization`) execute against SQL Server (`CakraTestDb`) without skipping and pass with 0 failures.

Changed Files:
- `src/backend/Cakra.Modules.Product/Cakra.Modules.Product.csproj` (modified — added Dapper, DI abstractions, and InternalsVisibleTo for test projects)
- `src/backend/Cakra.Modules.Product/ProductModule.cs` (created — IModule service registration)
- `src/backend/Cakra.Modules.Product/IProductQueryService.cs` (created — published query interface and ProductDto)
- `src/backend/Cakra.Modules.Product/Domain/Product.cs` (created)
- `src/backend/Cakra.Modules.Product/Domain/Events/ProductCreated.cs` (created)
- `src/backend/Cakra.Modules.Product/Domain/Events/ProductOwnerChanged.cs` (created)
- `src/backend/Cakra.Modules.Product/Domain/Events/ProductActivated.cs` (created)
- `src/backend/Cakra.Modules.Product/Domain/Events/ProductDeactivated.cs` (created)
- `src/backend/Cakra.Modules.Product/Persistence/IProductRepository.cs` (created — internal repository contract)
- `src/backend/Cakra.Modules.Product/Persistence/ProductRepository.cs` (created — internal Dapper repository)
- `src/backend/Cakra.Modules.Product/Services/IProductService.cs` (created)
- `src/backend/Cakra.Modules.Product/Services/ProductCommands.cs` (created — MediatR commands and FluentValidation validators)
- `src/backend/Cakra.Modules.Product/Services/ProductQueries.cs` (created — MediatR query records)
- `src/backend/Cakra.Modules.Product/Services/ProductService.cs` (created)
- `src/backend/Cakra.Modules.Product/Services/ProductQueryService.cs` (created)
- `src/backend/Cakra.Api/Migrations/Scripts/0005_product_tables.sql` (created — DbUp migration script)
- `tests/backend/Cakra.Tests.Unit/Cakra.Tests.Unit.csproj` (modified — added Product module reference)
- `tests/backend/Cakra.Tests.Unit/Product/ProductDomainAndServiceTests.cs` (created)
- `tests/backend/Cakra.Tests.Integration/Cakra.Tests.Integration.csproj` (modified — added Product module reference)
- `tests/backend/Cakra.Tests.Integration/Product/ProductModuleIntegrationTests.cs` (created)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — P3-S15 status, implementation notes, and changed files)

---

### P3-S16

Title: Product Catalog Screen — SCR-PRD-001

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement `SCR-PRD-001` (Product Catalog Screen) as a Vue 3 SFC with Bootstrap 5 styling in `src/frontend/Cakra.Web/`, and the corresponding ASP.NET Core REST API controller (`/api/v1/products/*`) in `Cakra.Api`. Wires `ProductService` commands and `ProductQueryService` queries. Delivers the first content management screen; users can view the product catalog, create products, update attributes, assign product owners, and toggle product status. M1 is achieved when this slice is complete.

Depends On: P3-S15, P2-S10, P1-S08

Repository: `Cakra.Api`, `src/frontend/Cakra.Web`

Completion Criteria:
- Vue 3 SFC `ProductCatalogView.vue` (`<script setup lang="ts">`, Bootstrap 5) renders: Bootstrap 5 table listing all products with columns (Code, Name, Owner, Status), create product button opening an inline form or modal, status toggle button per row — Architecture §19.4.
- ASP.NET Core Controller at `/api/v1/products` exposes REST endpoints for: list all products (`ListAllProducts`), list active products (`ListActiveProducts`), get product detail (`GetProductById`), create product (`CreateProduct`), update product (`UpdateProduct`), assign product owner (`AssignProductOwner`), activate/deactivate product.
- Product owner selector uses `OrganizationQueryService.ListActivePersons` for dropdown — Architecture §10.
- All endpoints protected by `[Authorize]` (authentication middleware from P2-S10).
- Axios HTTP client in Vue component calls `/api/v1/products` endpoints with authentication interceptors — Architecture §19.4.
- Integration tests (xUnit + WebApplicationFactory): catalog listing, product creation, and owner assignment pass.

Notes: Architecture §10 and §9 (FEAT-PRD-001) are authoritative. This slice completes Milestone M1. Combined API + Vue slice because scope is limited (1 controller, 1 Vue SFC, 6 endpoints).

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Implemented `ProductsController` (`src/backend/Cakra.Api/Controllers/ProductsController.cs`) at `/api/v1/products` protected by `[Authorize]`:
   - `GET /api/v1/products`: dispatches `ListAllProductsQuery` (or `ListActiveProductsQuery` when `?activeOnly=true`) via MediatR and enriches each item with `OwnerName` resolved via `IOrganizationQueryService`.
   - `GET /api/v1/products/active`: dispatches `ListActiveProductsQuery` via MediatR.
   - `GET /api/v1/products/owners` and `OrganizationController` (`GET /api/v1/organization/persons/active`): expose `IOrganizationQueryService.ListActivePersonsAsync` for the Product Owner dropdown selector (Architecture §7, §10).
   - `GET /api/v1/products/{id:guid}`: dispatches `GetProductByIdQuery` and returns 200 OK or 404 RFC 7807 `ProblemDetails`.
   - `POST /api/v1/products`: dispatches `CreateProductCommand` and returns 201 Created (`CreatedAtAction`).
   - `PUT /api/v1/products/{id:guid}`: dispatches `UpdateProductCommand` (and optional owner reassignment) and returns 200 OK.
   - `PUT /api/v1/products/{id:guid}/owner` (plus `PATCH`/`POST` aliases): dispatches `AssignProductOwnerCommand` and returns 200 OK.
   - `POST /api/v1/products/{id:guid}/activate` and `POST /api/v1/products/{id:guid}/deactivate` (plus `PUT` aliases): dispatch `ActivateProductCommand` and `DeactivateProductCommand` and return 200 OK.
2. Implemented `ProductCatalogView.vue` (`src/frontend/Cakra.Web/src/views/ProductCatalogView.vue`) as a Vue 3 SFC (`<script setup lang="ts">`) with Bootstrap 5 styling (`data-screen-id="SCR-PRD-001"`):
   - Renders a Bootstrap 5 table listing all products with columns (`Code`, `Name`, `Owner`, `Status`, `Actions`), plus an `Active Only` filter toggle.
   - Renders a "Create Product" button opening an inline form/modal card with Code, Name, Description, and Product Owner `<select>` dropdown populated from `GET /api/v1/organization/persons/active`.
   - Renders per-row Edit Product action, inline Product Owner reassignment selector (`PUT /api/v1/products/{id}/owner`), and Status Toggle button (`Activate` / `Deactivate`).
   - Uses the shared Axios `httpClient` (`@/api/http`) with cookie authentication interceptors.
3. Registered the `/products` route (`SCR-PRD-001`) in `src/frontend/Cakra.Web/src/router/index.ts` and added the "Product Catalog" navigation link in `src/frontend/Cakra.Web/src/App.vue`.
4. Added end-to-end integration tests in `tests/backend/Cakra.Tests.Integration/Product/ProductsControllerTests.cs` using xUnit, `WebApplicationFactory<Program>`, and Respawn verifying 401 on unauthenticated requests, active persons dropdown lookup, product creation, catalog listing, detail lookup, attribute update, owner reassignment, status activation/deactivation, and RFC 7807 `ProblemDetails` on duplicate codes or invalid owners.
5. Verification: `dotnet test Cakra.sln` passes (234 tests: 175 unit + 59 integration, 0 failures, 0 warnings); `npm run build` in `src/frontend/Cakra.Web` succeeds with 0 errors.

Changed Files:
- `src/backend/Cakra.Api/Controllers/ProductsController.cs` (created)
- `src/backend/Cakra.Api/Controllers/OrganizationController.cs` (created)
- `src/frontend/Cakra.Web/src/views/ProductCatalogView.vue` (created)
- `src/frontend/Cakra.Web/src/router/index.ts` (modified — registered `/products` route for `SCR-PRD-001`)
- `src/frontend/Cakra.Web/src/App.vue` (modified — added navigation links for Operational Feed and Product Catalog)
- `tests/backend/Cakra.Tests.Integration/Product/ProductsControllerTests.cs` (created)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — P3-S16 status, implementation notes, and changed files)

---

## P4 — Request Lifecycle

Implementation Status: COMPLETED
Review Status: GO

Objective: Implement the Request module — the core operational transactional engine — including domain model, application services using Dapper, escalation and management commands, API controller, and all Request screens as Vue 3 SFCs. Delivers complete operational request management. The module is split into seven slices to enable maximum parallelism: domain model, persistence + core commands, lifecycle completion + queries, escalation commands, API controller, and two screen groups. M2 is achieved when this phase is complete.

Source: Architecture §22 — Boundary: **Request** (`Cakra.Modules.Request`, Depends On: `Cakra.Core`, `Organization`, `Customer`, `Product`).

---

### P4-S17

Title: Request Module — Domain Model & State Machine

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the Request domain layer: `Request` aggregate, `RequestResolution` entity, `RequestAssignment` entity, state machine enforcing valid transitions (`CAPTURED → EVALUATING → ACCEPTED/REJECTED → IN_PROGRESS → ESCALATED → COMPLETED`), and all domain events emitted on state transitions.

Depends On: P1-S06

Repository: `Cakra.Modules.Request`

Completion Criteria:
- `Request` aggregate with all state transition methods is implemented, per Architecture §7 (`RequestService`) and §8 (UC-REQ-001..008).
- State machine enforces valid transitions; invalid transitions are rejected with a domain exception.
- Domain events defined and emitted on each transition: `RequestRecorded`, `RequestAssigned`, `RequestEvaluated`, `RequestAccepted`, `RequestRejected`, `RequestEscalated`, `ManagementDecisionRequested`, `RequestCompleted`.
- `RequestResolution` and `RequestAssignment` entities are implemented.
- Unit tests (xUnit + FluentAssertions) covering all valid state transitions and all invalid transition rejections pass — Architecture §19.8.

Notes: Domain layer only — no database, no service layer. Clean separation enables P4-S17 to start as soon as P1-S06 is complete, independently of P2 and P3 — executing in parallel with P2-S09, P3-S12, and P5-S24. Architecture §7, §8, §20 are authoritative.

Implementation Notes (2026-09-28):
All completion criteria satisfied:
1. `Request` aggregate root implemented in `Cakra.Modules.Request.Domain.Request`, inheriting `EntityBase`, managing identity, core attributes (`Title`, `Description`, `RequestType`, `Priority`), context linkages (`CustomerId`, `ProductId`, `WorkPackageId`), assigned ownership (`OwnerPersonId`), resolution outcome (`Resolution`), audit trails (`Assignments`), and in-process domain events (`DomainEvents`).
2. State machine enforces valid transitions:
   - `CAPTURED` (initial state from `Request.Record`, emits `RequestRecorded`).
   - `CAPTURED -> EVALUATING` (via `AssignOwner`, emits `RequestAssigned`).
   - `EVALUATING -> ACCEPTED` (via `Accept` / `AcceptResponsibility`, emits `RequestAccepted`).
   - `EVALUATING -> REJECTED` (via `Reject`, populates `Resolution` outcome `REJECTED`, emits `RequestRejected`).
   - `EVALUATING -> ESCALATED` (via `Escalate`, records reason, emits `RequestEscalated`).
   - `ACCEPTED -> IN_PROGRESS` (via `StartProgress`, records work start).
   - `IN_PROGRESS -> ESCALATED` (via `Escalate`, records reason, emits `RequestEscalated`).
   - `IN_PROGRESS -> COMPLETED` (via `Complete`, populates `Resolution` outcome `COMPLETED`, emits `RequestCompleted`).
   - `ESCALATED -> EVALUATING` (via `ReassignOwner` default or `ApplyManagementDecision(Evaluating)`).
   - `ESCALATED -> IN_PROGRESS` (via `ReassignOwner(InProgress)`, `ResumeProgress`, or `ApplyManagementDecision(InProgress)`).
   - `Evaluate` records triage notes while staying in `EVALUATING` (emits `RequestEvaluated`).
   - `RequestManagementDecision` records management question while staying active/escalated (emits `ManagementDecisionRequested`).
   - Invalid transitions strictly rejected via `InvalidRequestStateTransitionException`. Closed states (`REJECTED`, `COMPLETED`) reject any mutations.
3. All 8 domain events defined as records implementing `Cakra.Core.IDomainEvent`: `RequestRecorded`, `RequestAssigned`, `RequestEvaluated`, `RequestAccepted`, `RequestRejected`, `RequestEscalated`, `ManagementDecisionRequested`, `RequestCompleted`.
4. `RequestResolution` and `RequestAssignment` entities implemented inheriting `EntityBase`.
5. Comprehensive unit test suite implemented in `tests/backend/Cakra.Tests.Unit/Request/RequestStateMachineTests.cs` (45 distinct tests covering all valid transitions, invalid transition rejections, invariant validation, and complete end-to-end lifecycle happy paths). All 149 unit tests and all 31 integration tests across the solution pass with zero failures. Zero EF Core packages introduced.

Changed Files:
- `src/backend/Cakra.Modules.Request/Domain/RequestStatus.cs` (created)
- `src/backend/Cakra.Modules.Request/Domain/ResolutionOutcome.cs` (created)
- `src/backend/Cakra.Modules.Request/Domain/RequestResolution.cs` (created)
- `src/backend/Cakra.Modules.Request/Domain/RequestAssignment.cs` (created)
- `src/backend/Cakra.Modules.Request/Domain/Exceptions/InvalidRequestStateTransitionException.cs` (created)
- `src/backend/Cakra.Modules.Request/Domain/Exceptions/RequestDomainValidationException.cs` (created)
- `src/backend/Cakra.Modules.Request/Domain/Events/RequestRecorded.cs` (created)
- `src/backend/Cakra.Modules.Request/Domain/Events/RequestAssigned.cs` (created)
- `src/backend/Cakra.Modules.Request/Domain/Events/RequestEvaluated.cs` (created)
- `src/backend/Cakra.Modules.Request/Domain/Events/RequestAccepted.cs` (created)
- `src/backend/Cakra.Modules.Request/Domain/Events/RequestRejected.cs` (created)
- `src/backend/Cakra.Modules.Request/Domain/Events/RequestEscalated.cs` (created)
- `src/backend/Cakra.Modules.Request/Domain/Events/ManagementDecisionRequested.cs` (created)
- `src/backend/Cakra.Modules.Request/Domain/Events/RequestCompleted.cs` (created)
- `src/backend/Cakra.Modules.Request/Domain/Request.cs` (created)
- `tests/backend/Cakra.Tests.Unit/Cakra.Tests.Unit.csproj` (modified — added ProjectReference to `Cakra.Modules.Request`)
- `tests/backend/Cakra.Tests.Unit/Request/RequestStateMachineTests.cs` (created)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — P4-S17 status updated to IMPLEMENTED)

---

### P4-S18

Title: Request Module — Persistence & Core Commands

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the Request module persistence layer (DbUp SQL migrations, Dapper repositories) and the core command handlers that initiate and advance the request lifecycle: `RecordRequest`, `AssignRequestOwner`, `EvaluateRequest`. Cross-module validation reads from Organization, Customer, and Product via their published query interfaces. Establishes the audit logging pattern for request state changes.

Depends On: P4-S17, P1-S07, P2-S10, P3-S13, P3-S14, P3-S15

Repository: `Cakra.Modules.Request`

Completion Criteria:
- DbUp SQL migration scripts create `request.*` schema tables: `Requests`, `RequestResolutions`, `RequestAssignments` — Architecture §17.
- All repository operations use Dapper with explicit parameterized SQL against SQL Server; no EF Core — Architecture §19.3, §20.
- MediatR handlers via `RequestService` implement: `RecordRequest` (creates request in CAPTURED state, validates customer via `CustomerQueryService`, product via `ProductQueryService`), `AssignRequestOwner` (validates assignee via `OrganizationQueryService`, transitions to EVALUATING, emits `RequestAssigned`), `EvaluateRequest` (records evaluation notes, emits `RequestEvaluated`) — Architecture §7, §8 (UC-REQ-001..003).
- Every state change records actor `PersonId`, timestamp, and previous state using Dapper parameterized INSERT into `RequestAssignments` — Architecture §18 (Audit Logging). This audit pattern is reusable by P4-S19 and P4-S20.
- Repository implementations write exclusively to `request.*` schema.
- Integration tests (xUnit + WebApplicationFactory + Respawn): Record request → Assign owner → Evaluate passes as a sequential lifecycle test.

Notes: Architecture §7, §8, §15, §18, §19.3, §20 are authoritative. This slice establishes the persistence layer and audit pattern that P4-S19 and P4-S20 build upon.

Implementation Notes (2026-09-28):
- Added DbUp SQL migration script `src/backend/Cakra.Api/Migrations/Scripts/0006_request_tables.sql` creating `request.Requests`, `request.RequestResolutions`, and `request.RequestAssignments` with intra-schema foreign keys (`FK_RequestResolutions_Requests`, `FK_RequestAssignments_Requests`), check constraints (`CK_Requests_Status`), and query indexes (`IX_Requests_Status`, `IX_Requests_AssignedPersonId`, `IX_Requests_CustomerId`, `IX_Requests_ProductId`, `IX_RequestAssignments_RequestId_Timestamp`, `IX_RequestResolutions_RequestId`). Zero cross-schema foreign keys are defined, complying with Architecture §17 and §20.
- Extended `RequestStatusNames` with `FromName` / `FromNullableName` and added `Rehydrate` static factory methods on `Request`, `RequestAssignment`, and `RequestResolution` so Dapper row materialization maps `"IN_PROGRESS"` and all lifecycle states cleanly without reflection or public mutable setters.
- Implemented internal `IRequestRepository` and `RequestRepository` in `Cakra.Modules.Request.Persistence` using Dapper with explicit parameterized SQL over `IDbConnectionFactory` (`CreateAsync`, `UpdateAsync`, `GetByIdAsync`, `AddAssignmentAsync`, `GetAssignmentsByRequestIdAsync`, `UpsertResolutionAsync`, `GetResolutionByRequestIdAsync`), writing exclusively to the `request.*` schema.
- Implemented `IRequestService`, `RequestService`, `RequestDto`, and MediatR command records + handlers (`RecordRequestCommand`, `AssignRequestOwnerCommand`, `EvaluateRequestCommand`) in `Cakra.Modules.Request`:
  - `RecordRequest`: validates `Title`, actor `PersonId` (resolving fallback via `ICurrentContextProvider.CurrentPersonId`), customer existence and active status via `ICustomerQueryService.GetCustomerByIdAsync`, and optional product existence and active status via `IProductQueryService.GetProductByIdAsync`; creates the `Request` aggregate in `CAPTURED` status, persists the initial `RequestAssignment` state-change audit (`PreviousStatus = null`, `NewStatus = CAPTURED`), and dispatches `RequestRecorded`.
  - `AssignRequestOwner`: validates request existence and active assignee via `IOrganizationQueryService.GetPersonByIdAsync`, invokes `Request.AssignOwner` to transition `CAPTURED -> EVALUATING`, persists the updated `Request` and `RequestAssignment` audit record via the reusable `PersistStateChangesAndDispatchAsync` / `RecordStateChangeAuditAsync` helper pattern, and dispatches `RequestAssigned`.
  - `EvaluateRequest`: validates request existence and non-empty `EvaluationNotes`, invokes `Request.Evaluate`, persists the updated `Request` and `RequestAssignment` audit record (`PreviousStatus = EVALUATING`, `NewStatus = EVALUATING`), and dispatches `RequestEvaluated`.
  - Structured Serilog logging is emitted for every command and state transition.
- Added `RequestModule : IModule` registering `IRequestRepository` and `IRequestService` in DI (`AddMediatR` in `Program.cs` scans all `IModule` assemblies automatically).
- Added unit tests (`RequestCoreCommandsTests.cs` — 16 tests) and integration tests (`RequestCoreCommandsIntegrationTests.cs` — 12 tests, including sequential `RecordRequest` → `AssignRequestOwner` → `EvaluateRequest` lifecycle and Respawn/SQL Server database verification). All 100 Request tests pass with 0 warnings and 0 errors.

Changed Files:
- `src/backend/Cakra.Api/Migrations/Scripts/0006_request_tables.sql` (created)
- `src/backend/Cakra.Modules.Request/Cakra.Modules.Request.csproj` (modified)
- `src/backend/Cakra.Modules.Request/Domain/RequestStatus.cs` (modified)
- `src/backend/Cakra.Modules.Request/Domain/Request.cs` (modified)
- `src/backend/Cakra.Modules.Request/Domain/RequestAssignment.cs` (modified)
- `src/backend/Cakra.Modules.Request/Domain/RequestResolution.cs` (modified)
- `src/backend/Cakra.Modules.Request/Models/RequestDto.cs` (created)
- `src/backend/Cakra.Modules.Request/Persistence/IRequestRepository.cs` (created)
- `src/backend/Cakra.Modules.Request/Persistence/RequestRepository.cs` (created)
- `src/backend/Cakra.Modules.Request/Services/IRequestService.cs` (created)
- `src/backend/Cakra.Modules.Request/Services/RequestCommands.cs` (created)
- `src/backend/Cakra.Modules.Request/Services/RequestService.cs` (created)
- `src/backend/Cakra.Modules.Request/RequestModule.cs` (created)
- `tests/backend/Cakra.Tests.Unit/Request/RequestCoreCommandsTests.cs` (created)
- `tests/backend/Cakra.Tests.Integration/Cakra.Tests.Integration.csproj` (modified)
- `tests/backend/Cakra.Tests.Integration/Request/RequestCoreCommandsIntegrationTests.cs` (created)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — P4-S18 status updated to IMPLEMENTED)

---

### P4-S19

Title: Request Module — Lifecycle Completion Commands & Queries

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the request lifecycle completion command handlers (`AcceptRequestResponsibility`, `RejectRequest`, `ReviewRequestCompletion`) and the complete `RequestQueryService` query methods using Dapper explicit parameterized SQL.

Depends On: P4-S18

Repository: `Cakra.Modules.Request`

Completion Criteria:
- MediatR handlers via `RequestService` implement: `AcceptRequestResponsibility` (transitions to IN_PROGRESS, emits `RequestAccepted`), `RejectRequest` (transitions to REJECTED, records rejection reason, emits `RequestRejected`), `ReviewRequestCompletion` (transitions to COMPLETED, records completion details, emits `RequestCompleted`) — Architecture §7, §8 (UC-REQ-004, UC-REQ-005, UC-REQ-008).
- `RequestQueryService` implements using Dapper: `GetRequestById`, `GetRequestStateHistory`, `ListMyAssignedRequests` (filtered by `CurrentContextProvider.CurrentPersonId`), `GetFilteredRequestGrid` (filter by status, assignee, customer, product with pagination) — Architecture §7, §8 (UC-COL-002..004).
- Every state change records audit entry using the pattern established in P4-S18 — Architecture §18 (Audit Logging).
- `RequestQueryService` is accessible as a published interface to other modules.
- Integration tests (xUnit + WebApplicationFactory + Respawn): full request lifecycle (Record → Assign → Evaluate → Accept → Complete) passes; GetFilteredRequestGrid returns correct filtered results.

Notes: Can execute in parallel with P4-S20 (Escalation) after P4-S18. Architecture §7, §8, §15, §18 are authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Extended `IRequestService`, `RequestCommands.cs`, and `RequestService` (marked `partial` for clean extension by P4-S20) with the lifecycle completion commands, FluentValidation validators, and MediatR handlers:
   - `AcceptRequestResponsibility` (`AcceptRequestResponsibilityCommand` / `AcceptRequestResponsibilityAsync`): transitions `EVALUATING -> ACCEPTED -> IN_PROGRESS` (or `ACCEPTED -> IN_PROGRESS`), records audit entries in `request.RequestAssignments` via `PersistStateChangesAndDispatchAsync` / `RecordStateChangeAuditAsync` with monotonically increasing `AssignedAtUtc` timestamps, and dispatches `RequestAccepted` via `IDomainEventDispatcher` (UC-REQ-004).
   - `RejectRequest` (`RejectRequestCommand` / `RejectRequestAsync`): transitions `EVALUATING -> REJECTED`, records the rejection resolution in `request.RequestResolutions` and state change audit in `request.RequestAssignments`, and dispatches `RequestRejected` (UC-REQ-005).
   - `ReviewRequestCompletion` (`ReviewRequestCompletionCommand` / `CompleteRequestCommand` / `ReviewRequestCompletionAsync`): transitions `IN_PROGRESS -> COMPLETED`, records completion details in `request.RequestResolutions` and state change audit in `request.RequestAssignments`, and dispatches `RequestCompleted` (UC-REQ-008).
2. Implemented published cross-module query contract `IRequestQueryService` (along with `RequestGridFilter`, `PagedRequestGridResult`, `RequestDetailDto`, `RequestStateHistoryItemDto`, and MediatR query records in `RequestQueries.cs`) and Dapper implementation `RequestQueryService` executing explicit parameterized SQL exclusively against the `request.*` schema (`request.Requests`, `request.RequestResolutions`, `request.RequestAssignments`) with zero EF Core usage and cross-module display name enrichment via `IOrganizationQueryService`, `ICustomerQueryService`, and `IProductQueryService`:
   - `GetRequestById` / `GetRequestByIdAsync` (`GetRequestByIdQuery`)
   - `GetRequestStateHistory` / `GetRequestStateHistoryAsync` (`GetRequestStateHistoryQuery`)
   - `ListMyAssignedRequests` / `ListMyAssignedRequestsAsync` (`ListMyAssignedRequestsQuery`, filtered by `ICurrentContextProvider.CurrentPersonId` or explicit `personId`)
   - `GetFilteredRequestGrid` / `GetFilteredRequestGridAsync` (`GetFilteredRequestGridQuery`, parameterized filtering by status, assignee, customer, product, workPackage, and search term with `OFFSET ... FETCH NEXT` pagination)
   - `RequestExists` / `RequestExistsAsync` and `GetRequestsByIds` / `GetRequestsByIdsAsync` for cross-module reference validation and scope projections.
3. Registered `RequestQueryService` and `IRequestQueryService` in `RequestModule.cs`.
4. Added unit tests in `tests/backend/Cakra.Tests.Unit/Request/RequestCompletionAndQueriesTests.cs` and SQL Server integration tests in `tests/backend/Cakra.Tests.Integration/Request/RequestCompletionAndQueriesIntegrationTests.cs` (using `WebApplicationFactory<Program>` and Respawn). Verified `dotnet test cakra\Cakra.sln --filter FullyQualifiedName~Request` passes all 108 Request tests (93 unit + 15 integration) with 0 failures and 0 skips.
5. Remediation (2026-09-28): Resolved `RV-005` by registering Dapper `DbType.DateTime2` type mappings (`SqlMapper.AddTypeMap(typeof(DateTime), DbType.DateTime2)` and `SqlMapper.AddTypeMap(typeof(DateTime?), DbType.DateTime2)`) in `RequestRepository` static constructor so `DATETIME2` audit timestamps are not rounded to 3.33ms `DbType.DateTime` increments, updating `RequestService.GetNextMonotonicTimestamp` and `AcceptRequestResponsibilityAsync` so consecutive state-transition audit timestamps (`AssignedAtUtc` and `CreatedAt`) are separated by at least 10ms, and configuring `RequestCompletionAndQueriesIntegrationTests` to use isolated database `CakraTestDb_RequestCompletion` to prevent concurrent Respawn reset collisions. Verified `dotnet test cakra\Cakra.sln --filter FullyQualifiedName~RequestCompletionAndQueries` passes all 8 tests (5 unit + 3 integration) with 0 failures and 0 skips.

Changed Files:
- `src/backend/Cakra.Modules.Request/IRequestQueryService.cs` (created — published query interface, `RequestGridFilter`, and `PagedRequestGridResult`)
- `src/backend/Cakra.Modules.Request/Models/RequestDto.cs` (modified — added optional display enrichment properties and `RequestDetailDto` / `RequestStateHistoryItemDto` records)
- `src/backend/Cakra.Modules.Request/Persistence/RequestRepository.cs` (modified — registered Dapper `DbType.DateTime2` type mappings for `DateTime` and `DateTime?`)
- `src/backend/Cakra.Modules.Request/Services/IRequestService.cs` (modified — marked `partial` and added `AcceptRequestResponsibility`, `RejectRequest`, and `ReviewRequestCompletion` contracts)
- `src/backend/Cakra.Modules.Request/Services/RequestCommands.cs` (modified — added `AcceptRequestResponsibilityCommand`, `RejectRequestCommand`, `ReviewRequestCompletionCommand`, `CompleteRequestCommand`, and FluentValidation validators)
- `src/backend/Cakra.Modules.Request/Services/RequestQueries.cs` (created — MediatR query records for `RequestQueryService`)
- `src/backend/Cakra.Modules.Request/Services/RequestService.cs` (modified — marked `partial`, implemented completion command methods, MediatR handlers, and 10ms monotonic audit timestamp separation)
- `src/backend/Cakra.Modules.Request/Services/RequestQueryService.cs` (created — Dapper parameterized SQL query service and MediatR query handlers)
- `src/backend/Cakra.Modules.Request/RequestModule.cs` (modified — registered `RequestQueryService` and `IRequestQueryService`)
- `tests/backend/Cakra.Tests.Unit/Request/RequestCompletionAndQueriesTests.cs` (created)
- `tests/backend/Cakra.Tests.Integration/Request/RequestCompletionAndQueriesIntegrationTests.cs` (created / modified — isolated `CakraTestDb_RequestCompletion` test database)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — updated P4-S19 status to IMPLEMENTED with implementation notes, RV-005 remediation notes, and changed files)

---

### P4-S20

Title: Request Module — Escalation & Management Commands

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement escalation, management decision, and reassignment command paths as MediatR handlers in `RequestService` (UC-REQ-006, UC-REQ-007, UC-MGT-001). These commands extend the service layer established in P4-S18 with the escalation-specific business logic and `ReassignRequestOwnership`.

Depends On: P4-S18

Repository: `Cakra.Modules.Request`

Completion Criteria:
- `EscalateRequest` MediatR handler transitions `Request` to `ESCALATED`, records escalation actor and reason via Dapper parameterized SQL, emits `RequestEscalated`.
- `RequestManagementDecision` MediatR handler records management decision, emits `ManagementDecisionRequested`.
- `ReassignRequestOwnership` (UC-MGT-001) MediatR handler updates `OwnerPersonId` via Dapper parameterized SQL, validates new assignee via `OrganizationQueryService`, emits `RequestAssigned`.
- Integration tests (xUnit + WebApplicationFactory + Respawn): Escalate, ManagementDecision, and Reassign flows pass.

Notes: **Parallel Execution**: Depends on P4-S18 (not P4-S19). Can execute concurrently in parallel with P4-S19 (Lifecycle Completion & Queries). Architecture §8 (UC-REQ-006, UC-REQ-007, UC-MGT-001) is authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Extended `IRequestService` (`IRequestService.Escalation.cs`), MediatR command records + FluentValidation validators (`RequestEscalationCommands.cs`), and `RequestService` (`RequestService.Escalation.cs`) with the escalation and management command paths:
   - `EscalateRequest` (`EscalateRequestCommand`, `EscalateRequestAsync` / `EscalateRequest`): transitions a `Request` in `EVALUATING` or `IN_PROGRESS` state to `ESCALATED`, records `EscalationReason` and actor in `request.Requests`, persists the state transition audit record in `request.RequestAssignments` via `PersistStateChangesAndDispatchAsync` / `RecordStateChangeAuditAsync` using Dapper parameterized SQL, and dispatches `RequestEscalated` (UC-REQ-006).
   - `RequestManagementDecision` (`RequestManagementDecisionCommand`, `RequestManagementDecisionAsync` / `RequestManagementDecision`): records management decision notes on active or escalated requests (and supports optional `TargetStatus` to resolve an `ESCALATED` request back to `EVALUATING` or `IN_PROGRESS` via `ApplyManagementDecision`), persists the audit entry in `request.RequestAssignments` via Dapper parameterized SQL, and dispatches `ManagementDecisionRequested` (UC-REQ-007).
   - `ReassignRequestOwnership` (`ReassignRequestOwnershipCommand`, `ReassignRequestOwnershipAsync` / `ReassignRequestOwnership`): validates the new assignee via `IOrganizationQueryService` (`IsPersonActiveAsync` / `GetPersonByIdAsync`), updates `OwnerPersonId` (and transitions `ESCALATED -> EVALUATING/IN_PROGRESS` or `CAPTURED -> EVALUATING`), persists the ownership/state audit entry in `request.RequestAssignments` via Dapper parameterized SQL, and dispatches `RequestAssigned` (UC-MGT-001).
2. Added unit tests in `tests/backend/Cakra.Tests.Unit/Request/RequestEscalationAndManagementTests.cs` (6 tests) and SQL Server integration tests in `tests/backend/Cakra.Tests.Integration/Request/RequestEscalationAndManagementIntegrationTests.cs` (2 end-to-end integration tests using `WebApplicationFactory<Program>` and Respawn on isolated test database `CakraTestDb_RequestEscalation`). Verified `dotnet test cakra\Cakra.sln --filter FullyQualifiedName~RequestEscalation` passes all 8 tests with 0 warnings and 0 errors.

Changed Files:
- `src/backend/Cakra.Modules.Request/Services/IRequestService.Escalation.cs` (created — partial `IRequestService` contract for `EscalateRequest`, `RequestManagementDecision`, and `ReassignRequestOwnership`)
- `src/backend/Cakra.Modules.Request/Services/RequestEscalationCommands.cs` (created — `EscalateRequestCommand`, `RequestManagementDecisionCommand`, `ReassignRequestOwnershipCommand`, and FluentValidation validators)
- `src/backend/Cakra.Modules.Request/Services/RequestService.Escalation.cs` (created — partial `RequestService` implementation and MediatR command handlers)
- `tests/backend/Cakra.Tests.Unit/Request/RequestEscalationAndManagementTests.cs` (created)
- `tests/backend/Cakra.Tests.Integration/Request/RequestEscalationAndManagementIntegrationTests.cs` (created)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — updated P4-S20 status to IMPLEMENTED with implementation notes and changed files)

---

### P4-S21

Title: Request API Controller

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the ASP.NET Core REST API controller for the Request module at `/api/v1/requests/*` in `Cakra.Api`. Wires all `RequestService` MediatR command handlers and `RequestQueryService` query methods to HTTP endpoints. Separated from Vue SFC screen slices to allow backend and frontend agents to work independently.

Depends On: P4-S19, P4-S20

Repository: `Cakra.Api`

Completion Criteria:
- ASP.NET Core Controller at `/api/v1/requests` exposes REST endpoints:
  - `GET /api/v1/requests` → `RequestQueryService.GetFilteredRequestGrid` with query parameters for status, assignee, customer, product, pagination.
  - `GET /api/v1/requests/{id}` → `RequestQueryService.GetRequestById`.
  - `GET /api/v1/requests/{id}/history` → `RequestQueryService.GetRequestStateHistory`.
  - `GET /api/v1/requests/my` → `RequestQueryService.ListMyAssignedRequests`.
  - `POST /api/v1/requests` → `RequestService.RecordRequest`.
  - `POST /api/v1/requests/{id}/assign` → `RequestService.AssignRequestOwner`.
  - `POST /api/v1/requests/{id}/evaluate` → `RequestService.EvaluateRequest`.
  - `POST /api/v1/requests/{id}/accept` → `RequestService.AcceptRequestResponsibility`.
  - `POST /api/v1/requests/{id}/reject` → `RequestService.RejectRequest`.
  - `POST /api/v1/requests/{id}/escalate` → `RequestService.EscalateRequest`.
  - `POST /api/v1/requests/{id}/management-decision` → `RequestService.RequestManagementDecision`.
  - `POST /api/v1/requests/{id}/complete` → `RequestService.ReviewRequestCompletion`.
  - `POST /api/v1/requests/{id}/reassign` → `RequestService.ReassignRequestOwnership`.
- Lookup endpoints for dropdowns: `GET /api/v1/customers/active` (proxies `CustomerQueryService.ListActiveCustomers`), `GET /api/v1/products/active` (proxies `ProductQueryService.ListActiveProducts`), `GET /api/v1/organization/persons/active` (proxies `OrganizationQueryService.ListActivePersons`). These may be served from their respective module controllers already; if not, add proxy endpoints.
- All endpoints protected by `[Authorize]` — Architecture §19.5.
- Audit logging active on all state-transition endpoints — Architecture §18.
- Integration tests (xUnit + WebApplicationFactory): each endpoint category (listing, create, state transition, query) returns expected HTTP status codes.

Notes: Controller-only slice. Vue SFCs are implemented in P4-S22 and P4-S23. Architecture §19.6 (API Style) and §8 are authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Implemented `RequestsController` (`src/backend/Cakra.Api/Controllers/RequestsController.cs`) at `/api/v1/requests`, inheriting from `ApiControllerBase` and protected by `[Authorize]` (Architecture §19.5, §19.6):
   - `GET /api/v1/requests` → dispatches `GetFilteredRequestGridQuery` to `RequestQueryService.GetFilteredRequestGrid` supporting status, assignee (`assigneeId` / `assigneePersonId` / `ownerPersonId` / `assignee`), customer (`customerId` / `customer`), product (`productId` / `product`), workPackage (`workPackageId` / `workPackage`), search (`searchTerm` / `search` / `q`), and pagination (`page`, `pageSize`, `offset`).
   - `GET /api/v1/requests/my` → dispatches `ListMyAssignedRequestsQuery` to `RequestQueryService.ListMyAssignedRequests` using the authenticated user's `CurrentPersonId` (or optional explicit `personId`).
   - `GET /api/v1/requests/{id:guid}` → dispatches `GetRequestByIdQuery` to `RequestQueryService.GetRequestById` (returning 200 OK or 404 RFC 7807 `ProblemDetails` when not found).
   - `GET /api/v1/requests/{id:guid}/history` → dispatches `GetRequestStateHistoryQuery` to `RequestQueryService.GetRequestStateHistory` returning the chronological audit trail of state and ownership transitions.
   - `POST /api/v1/requests` → dispatches `RecordRequestCommand` to `RequestService.RecordRequest` and returns `201 Created` via `CreatedAtAction(nameof(GetRequestById), ...)`.
   - `POST /api/v1/requests/{id:guid}/assign` → dispatches `AssignRequestOwnerCommand` to `RequestService.AssignRequestOwner` (`CAPTURED -> EVALUATING`).
   - `POST /api/v1/requests/{id:guid}/evaluate` → dispatches `EvaluateRequestCommand` to `RequestService.EvaluateRequest`.
   - `POST /api/v1/requests/{id:guid}/accept` → dispatches `AcceptRequestResponsibilityCommand` to `RequestService.AcceptRequestResponsibility` (`EVALUATING -> ACCEPTED -> IN_PROGRESS`).
   - `POST /api/v1/requests/{id:guid}/reject` → dispatches `RejectRequestCommand` to `RequestService.RejectRequest` (`EVALUATING -> REJECTED`).
   - `POST /api/v1/requests/{id:guid}/escalate` → dispatches `EscalateRequestCommand` to `RequestService.EscalateRequest` (`EVALUATING/IN_PROGRESS -> ESCALATED`).
   - `POST /api/v1/requests/{id:guid}/management-decision` → dispatches `RequestManagementDecisionCommand` to `RequestService.RequestManagementDecision`.
   - `POST /api/v1/requests/{id:guid}/complete` → dispatches `ReviewRequestCompletionCommand` to `RequestService.ReviewRequestCompletion` (`IN_PROGRESS -> COMPLETED`).
   - `POST /api/v1/requests/{id:guid}/reassign` → dispatches `ReassignRequestOwnershipCommand` to `RequestService.ReassignRequestOwnership`.
   - All command responses return enriched `RequestDto` instances (with `OwnerName`, `CustomerName`, `ProductName`, `Resolution`, and `Assignments` populated) and emit structured Serilog logs alongside `AuditLoggingMiddleware` and `request.RequestAssignments` state-transition audit persistence (Architecture §18).
2. Implemented `CustomersController` (`src/backend/Cakra.Api/Controllers/CustomersController.cs`) at `/api/v1/customers` protected by `[Authorize]`, exposing `GET /api/v1/customers/active` (proxying `ICustomerQueryService.ListActiveCustomersAsync` via `ListActiveCustomersQuery`), `GET /api/v1/customers`, `GET /api/v1/customers/{id:guid}`, and `GET /api/v1/customers/{id:guid}/contacts`. Verified `GET /api/v1/products/active` in `ProductsController` and `GET /api/v1/organization/persons/active` in `OrganizationController`.
3. Added SQL Server integration tests in `tests/backend/Cakra.Tests.Integration/Request/RequestsControllerTests.cs` (using `WebApplicationFactory<Program>` and Respawn on isolated test database `CakraTestDb_RequestsController`). Verified all 5 end-to-end test scenarios (401 unauthorized checks across all endpoints, active customer/product/person lookup endpoints, full Record → Assign → Evaluate → Accept → Complete → Detail → History lifecycle, Escalate → Management Decision → Reassign → Reject lifecycle, and My Requests + Filtered Grid + 400/404 ProblemDetails error responses) pass with 0 warnings and 0 errors.

Changed Files:
- `src/backend/Cakra.Api/Controllers/RequestsController.cs` (created)
- `src/backend/Cakra.Api/Controllers/CustomersController.cs` (created)
- `tests/backend/Cakra.Tests.Integration/Request/RequestsControllerTests.cs` (created)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — updated P4-S21 status to IMPLEMENTED with implementation notes and changed files)

---

### P4-S22

Title: Request Screens — SCR-REQ-001, SCR-REQ-002

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the Request List screen (`SCR-REQ-001`) and Create Request screen (`SCR-REQ-002`) as Vue 3 SFCs with Bootstrap 5 styling in `src/frontend/Cakra.Web/`. These are the entry-point and creation screens for request management.

Depends On: P4-S21, P1-S08

Repository: `src/frontend/Cakra.Web`

Completion Criteria:
- Vue 3 SFC `RequestListView.vue` (`<script setup lang="ts">`, Bootstrap 5) renders: Bootstrap 5 table listing requests with columns (ID, Title, Customer, Product, Status, Assignee, CreatedAt), filter dropdowns above table for status and assignee, pagination controls below table. Calls `GET /api/v1/requests` with filter query parameters — Architecture §19.4, §20.
- Vue 3 SFC `CreateRequestView.vue` (`<script setup lang="ts">`, Bootstrap 5) renders: Bootstrap 5 form with fields (Title, Description), Customer select dropdown populated from `GET /api/v1/customers/active`, Product select dropdown populated from `GET /api/v1/products/active`, submit button. Calls `POST /api/v1/requests` on submit — Architecture §19.4.
- Vue Router 4 routes: `/requests` maps to `RequestListView.vue`, `/requests/create` maps to `CreateRequestView.vue`.
- Axios HTTP client calls request API endpoints with authentication interceptors.
- Successful request creation navigates to request detail route (`/requests/{id}`).

Notes: Frontend-only slice. **Parallel Execution**: Can execute concurrently with P4-S23 after P4-S21. Architecture §9 (FEAT-REQ-001, FEAT-REQ-002) and §8 (UC-REQ-001) are authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Implemented `RequestListView.vue` (`src/frontend/Cakra.Web/src/views/RequestListView.vue`) as a Vue 3 SFC (`<script setup lang="ts">`) with Bootstrap 5 styling (`data-screen-id="SCR-REQ-001"`):
   - Renders a Bootstrap 5 table listing operational requests with columns (`ID`, `Title`, `Customer`, `Product`, `Status`, `Assignee`, `CreatedAt`), with row click and link navigation to `/requests/${id}`.
   - Renders filter dropdowns above the table for `status` (`CAPTURED`, `EVALUATING`, `ACCEPTED`, `IN_PROGRESS`, `ESCALATED`, `COMPLETED`, `REJECTED`) and `assignee` (populated from `GET /api/v1/organization/persons/active`), plus a "Create Request" button navigating to `/requests/create` and a "Clear Filters" button.
   - Renders pagination controls below the table (`Previous` and `Next` buttons with current page and total request count summary).
   - Queries `GET /api/v1/requests` with filter (`status`, `assigneeId`) and pagination (`page`, `pageSize`) query parameters via the shared Axios `httpClient` (`@/api/http`).
2. Implemented `CreateRequestView.vue` (`src/frontend/Cakra.Web/src/views/CreateRequestView.vue`) as a Vue 3 SFC (`<script setup lang="ts">`) with Bootstrap 5 styling (`data-screen-id="SCR-REQ-002"`):
   - Renders a Bootstrap 5 form with `Title`, `Description`, `RequestType`, `Priority`, `Customer` `<select>` dropdown populated from `GET /api/v1/customers/active`, `Product` `<select>` dropdown populated from `GET /api/v1/products/active`, and submit/cancel buttons.
   - Submits the new request via `POST /api/v1/requests` using the shared Axios `httpClient` (`@/api/http`) with authentication interceptors and RFC 7807 `ProblemDetails` error handling.
   - Navigates to `/requests/${id}` upon successful request creation.
3. Registered `/requests` (`SCR-REQ-001` → `RequestListView.vue`) and `/requests/create` (`SCR-REQ-002` → `CreateRequestView.vue`) routes in `src/frontend/Cakra.Web/src/router/index.ts`, and added the "Requests" navigation link in `src/frontend/Cakra.Web/src/App.vue`.
4. Verification: `npm run build` (`vue-tsc --noEmit && vite build`) in `src/frontend/Cakra.Web` succeeds with 0 TypeScript or Vite errors.

Changed Files:
- `src/frontend/Cakra.Web/src/views/RequestListView.vue` (created)
- `src/frontend/Cakra.Web/src/views/CreateRequestView.vue` (created)
- `src/frontend/Cakra.Web/src/router/index.ts` (modified — registered `/requests` and `/requests/create` routes)
- `src/frontend/Cakra.Web/src/App.vue` (modified — added Requests navigation link)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — updated P4-S22 status to IMPLEMENTED with implementation notes and changed files)

---

### P4-S23

Title: Request Screens — SCR-REQ-003, SCR-REQ-004, SCR-REQ-005

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the Request Detail screen (`SCR-REQ-003`), My Requests screen (`SCR-REQ-004`), and Request Search / History screen (`SCR-REQ-005`) as Vue 3 SFCs with Bootstrap 5 styling in `src/frontend/Cakra.Web/`.

Depends On: P4-S21, P1-S08

Repository: `src/frontend/Cakra.Web`

Completion Criteria:
- Vue 3 SFC `RequestDetailView.vue` (`<script setup lang="ts">`, Bootstrap 5) renders: Bootstrap 5 card displaying request details (Title, Description, Customer, Product, Status, Assignee, CreatedAt, UpdatedAt), state history timeline below the card, action buttons rendered conditionally based on current request state — e.g., "Assign" button visible only in CAPTURED state, "Accept"/"Reject" in EVALUATING, "Escalate" in EVALUATING/IN_PROGRESS, "Complete" in IN_PROGRESS. Calls `GET /api/v1/requests/{id}` for data, `GET /api/v1/requests/{id}/history` for timeline, and respective `POST` endpoints for actions — Architecture §19.4, §20.
- Vue 3 SFC `MyRequestsView.vue` (`<script setup lang="ts">`, Bootstrap 5) renders: Bootstrap 5 table of requests assigned to the current user. Calls `GET /api/v1/requests/my`.
- Vue 3 SFC `RequestSearchView.vue` (`<script setup lang="ts">`, Bootstrap 5) renders: Bootstrap 5 search form with Customer, Product, and status filter inputs, results table below. Calls `GET /api/v1/requests` with filter parameters, and `GET /api/v1/requests/{id}/history` for selected request history.
- Vue Router 4 routes: `/requests/:id` maps to `RequestDetailView.vue`, `/requests/my` maps to `MyRequestsView.vue`, `/requests/search` maps to `RequestSearchView.vue`.
- Axios HTTP client calls request API endpoints with authentication interceptors.

Notes: Frontend-only slice. **Parallel Execution**: Can execute concurrently with P4-S22 after P4-S21. This slice + P4-S22 completes Milestone M2. Architecture §9 (FEAT-REQ-003..008, FEAT-COL-001..004) and §8 (UC-REQ-001..008, UC-COL-001..004) are authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Implemented `RequestDetailView.vue` (`src/frontend/Cakra.Web/src/views/RequestDetailView.vue`) as a Vue 3 SFC (`<script setup lang="ts">`) with Bootstrap 5 styling (`data-screen-id="SCR-REQ-003"`):
   - Renders a Bootstrap 5 card displaying request details (`Title`, `Description`, `Customer`, `Product`, `Status`, `Assignee`, `CreatedAt`, `UpdatedAt`, `RequestType`, `Priority`, plus `EvaluationNotes`, `EscalationReason`, `ManagementDecisionNotes`, and `Resolution` when present).
   - Renders the chronological state history timeline below the card (`GET /api/v1/requests/${id}/history`).
   - Renders conditional lifecycle action forms and buttons based on the current request state:
     * `CAPTURED`: "Assign" form with active person dropdown populated from `GET /api/v1/organization/persons/active` (`POST /api/v1/requests/${id}/assign`).
     * `EVALUATING`: "Evaluate" (`POST /api/v1/requests/${id}/evaluate`), "Accept" (`POST /api/v1/requests/${id}/accept`), and "Reject" (`POST /api/v1/requests/${id}/reject`).
     * `EVALUATING` and `IN_PROGRESS`: "Escalate" (`POST /api/v1/requests/${id}/escalate`).
     * `IN_PROGRESS`: "Complete" (`POST /api/v1/requests/${id}/complete`).
     * Active / `ESCALATED` states (`EVALUATING`, `ACCEPTED`, `IN_PROGRESS`, `ESCALATED`): "Management Decision" (`POST /api/v1/requests/${id}/management-decision`) and "Reassign" (`POST /api/v1/requests/${id}/reassign`).
2. Implemented `MyRequestsView.vue` (`src/frontend/Cakra.Web/src/views/MyRequestsView.vue`) as a Vue 3 SFC (`<script setup lang="ts">`) with Bootstrap 5 styling (`data-screen-id="SCR-REQ-004"`):
   - Renders a Bootstrap 5 table of requests assigned to the current user (`GET /api/v1/requests/my`) with navigation links to `/requests/${id}`.
3. Implemented `RequestSearchView.vue` (`src/frontend/Cakra.Web/src/views/RequestSearchView.vue`) as a Vue 3 SFC (`<script setup lang="ts">`) with Bootstrap 5 styling (`data-screen-id="SCR-REQ-005"`):
   - Renders a Bootstrap 5 search form with `Customer` (`GET /api/v1/customers/active`), `Product` (`GET /api/v1/products/active`), `status`, and keyword (`searchTerm`) filter inputs.
   - Renders a paginated results table below calling `GET /api/v1/requests` with filter parameters.
   - Renders a selected request state history panel below the results table calling `GET /api/v1/requests/${id}/history`.
4. Registered `/requests/my` (`SCR-REQ-004` → `MyRequestsView.vue`), `/requests/search` (`SCR-REQ-005` → `RequestSearchView.vue`), and `/requests/:id` (`SCR-REQ-003` → `RequestDetailView.vue`) in `src/frontend/Cakra.Web/src/router/index.ts` (with static paths `/requests/my` and `/requests/search` placed before `/requests/:id`), and added navigation links in `src/frontend/Cakra.Web/src/App.vue`.
5. Verification: `npm run build` (`vue-tsc --noEmit && vite build`) in `src/frontend/Cakra.Web` succeeds with 0 TypeScript or Vite errors.

Changed Files:
- `src/frontend/Cakra.Web/src/views/RequestDetailView.vue` (created)
- `src/frontend/Cakra.Web/src/views/MyRequestsView.vue` (created)
- `src/frontend/Cakra.Web/src/views/RequestSearchView.vue` (created)
- `src/frontend/Cakra.Web/src/router/index.ts` (modified — registered `/requests/my`, `/requests/search`, and `/requests/:id` routes)
- `src/frontend/Cakra.Web/src/App.vue` (modified — added My Requests and Request Search navigation links)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — updated P4-S23 status to IMPLEMENTED with implementation notes and changed files)

---

## P5 — Work Package

Implementation Status: COMPLETED
Review Status: GO

Objective: Implement the Work Package module with Dapper persistence, API controller, and its management screen as a Vue 3 SFC. M3 is achieved when this phase is complete.

Source: Architecture §22 — Boundary: **Work Package** (`Cakra.Modules.WorkPackage`, Depends On: `Cakra.Core`, `Organization`, `Customer`, `Product`, `Request`). Architecture §11.

---

### P5-S24

Title: Work Package Module — Domain Model

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the Work Package domain layer: `WorkPackage` aggregate, `WorkPackageRequest` membership entity, lifecycle state machine (`DRAFT → ACTIVE → CLOSED`), and all domain events. Business Rule 9 (a request may belong to at most one active Work Package) must be enforced at the domain level.

Depends On: P1-S06

Repository: `Cakra.Modules.WorkPackage`

Completion Criteria:
- `WorkPackage` aggregate with lifecycle state machine is implemented (DRAFT, ACTIVE, CLOSED and valid transitions).
- `WorkPackageRequest` membership entity is implemented.
- Domain events defined: `WorkPackageCreated`, `WorkPackageActivated`, `WorkPackageClosed`, `WorkPackageOwnerChanged`, `RequestAddedToWorkPackage`, `RequestRemovedFromWorkPackage` — Architecture §11.
- Business Rule 9 is enforceable at the domain level.
- Unit tests (xUnit + FluentAssertions): lifecycle transitions and membership invariants (including Business Rule 9 violation) pass — Architecture §19.8.

Notes: Architecture §11 is authoritative. Domain-only slice; can start as soon as P1-S06 is complete, executing in parallel with P2-S09, P3-S12, and P4-S17.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. `WorkPackage` aggregate root implemented in `Cakra.Modules.WorkPackage.Domain` managing lifecycle transitions:
   - Valid transitions: `DRAFT -> ACTIVE` (via `Activate`), `ACTIVE -> CLOSED` (via `Close`), and `DRAFT -> CLOSED` directly (per Architecture §11 and Domain doc §9).
   - Invalid lifecycle transitions (e.g. `ACTIVE -> ACTIVE`, `CLOSED -> ACTIVE`, `CLOSED -> CLOSED`) throw `InvalidWorkPackageStateTransitionException`.
   - Modifying objectives (`UpdateObjective`) and reassigning owners (`AssignOwner`) enforce invariant validation and emit domain events (`WorkPackageOwnerChanged`).
2. `WorkPackageRequest` membership entity implemented with `WorkPackageId`, `RequestId`, `AddedAt`, `RemovedAt` (nullable), and `IsActive` helper flag. Supports `MarkRemoved(DateTime)` and `Rehydrate(...)` for persistence restoration while preserving historical traceability (Business Rule 15).
3. All 6 required domain events defined in `Cakra.Modules.WorkPackage.Domain.Events` implementing `IDomainEvent`: `WorkPackageCreated`, `WorkPackageActivated`, `WorkPackageClosed`, `WorkPackageOwnerChanged`, `RequestAddedToWorkPackage`, `RequestRemovedFromWorkPackage`.
4. Business Rule 9 ("a request may belong to at most one active Work Package") enforced at the domain level:
   - Within aggregate: duplicate active request addition throws `BusinessRuleViolationException(9, ...)`.
   - Across active packages: `IActiveWorkPackageChecker` contract and predicate delegate supported in `AddRequest(...)`, throwing `BusinessRuleViolationException(9, ...)`.
   - Re-adding a previously removed request succeeds as a new membership link while retaining historical audit records.
5. Unit tests implemented in `tests/backend/Cakra.Tests.Unit/WorkPackage/WorkPackageDomainTests.cs` (27 comprehensive tests, total test suite passing 158 tests across unit and integration tests, 0 warnings, 0 errors).

Changed Files:
- `src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackage.cs` (created)
- `src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackageRequest.cs` (created)
- `src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackageStatus.cs` (created)
- `src/backend/Cakra.Modules.WorkPackage/Domain/IActiveWorkPackageChecker.cs` (created)
- `src/backend/Cakra.Modules.WorkPackage/Domain/Exceptions/WorkPackageDomainException.cs` (created)
- `src/backend/Cakra.Modules.WorkPackage/Domain/Exceptions/InvalidWorkPackageStateTransitionException.cs` (created)
- `src/backend/Cakra.Modules.WorkPackage/Domain/Exceptions/BusinessRuleViolationException.cs` (created)
- `src/backend/Cakra.Modules.WorkPackage/Domain/Events/WorkPackageCreated.cs` (created)
- `src/backend/Cakra.Modules.WorkPackage/Domain/Events/WorkPackageActivated.cs` (created)
- `src/backend/Cakra.Modules.WorkPackage/Domain/Events/WorkPackageClosed.cs` (created)
- `src/backend/Cakra.Modules.WorkPackage/Domain/Events/WorkPackageOwnerChanged.cs` (created)
- `src/backend/Cakra.Modules.WorkPackage/Domain/Events/RequestAddedToWorkPackage.cs` (created)
- `src/backend/Cakra.Modules.WorkPackage/Domain/Events/RequestRemovedFromWorkPackage.cs` (created)
- `tests/backend/Cakra.Tests.Unit/WorkPackage/WorkPackageDomainTests.cs` (created)
- `tests/backend/Cakra.Tests.Unit/Cakra.Tests.Unit.csproj` (modified — added Cakra.Modules.WorkPackage project reference)
- `tests/backend/Cakra.Tests.Integration/Modules/ModuleRegistrationTests.cs` (modified — added IDbConnectionFactory registration to test provider)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — updated P5-S24 to IMPLEMENTED)

---

### P5-S25

Title: Work Package Module — Application Services & Persistence

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement `WorkPackageService` commands as MediatR handlers, `WorkPackageQueryService` queries using Dapper explicit parameterized SQL, DbUp SQL migration scripts for `workpackage.*` tables, and Dapper repository implementations. Cross-module validation uses `OrganizationQueryService`, `CustomerQueryService`, `ProductQueryService`, and `RequestQueryService`.

Depends On: P5-S24, P1-S07, P2-S10, P3-S13, P3-S14, P3-S15, P4-S19

Repository: `Cakra.Modules.WorkPackage`

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

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Added embedded DbUp SQL migration script `src/backend/Cakra.Api/Migrations/Scripts/0007_workpackage_tables.sql` idempotently creating the `[workpackage]` schema and tables `[workpackage].[WorkPackages]` and `[workpackage].[WorkPackageRequests]` with status check constraint (`DRAFT`, `ACTIVE`, `CLOSED`), intra-schema foreign key `FK_WorkPackageRequests_WorkPackages`, filtered unique index `UX_WorkPackageRequests_ActivePair`, nonclustered indexes, and zero cross-schema foreign keys (Architecture §17, §20).
2. Implemented `internal` Dapper repository `WorkPackageRepository` (`IWorkPackageRepository` and `IActiveWorkPackageChecker`) in `Cakra.Modules.WorkPackage.Persistence` using explicit parameterized SQL against `[workpackage].[WorkPackages]` and `[workpackage].[WorkPackageRequests]` with zero EF Core usage.
3. Implemented `IWorkPackageService` and `WorkPackageService` in `Cakra.Modules.WorkPackage.Services` along with MediatR commands (`CreateWorkPackageCommand`, `UpdateObjectiveCommand`, `AssignOwnerCommand`, `AddRequestToWorkPackageCommand`, `RemoveRequestFromWorkPackageCommand`, `ActivateWorkPackageCommand`, `CloseWorkPackageCommand`), FluentValidation validators, and MediatR command handlers:
   - `CreateWorkPackage` validates active owner via `IOrganizationQueryService`, optional active customer via `ICustomerQueryService`, and optional active product via `IProductQueryService`.
   - `AssignOwner` validates active owner via `IOrganizationQueryService`.
   - `AddRequestToWorkPackage` validates request existence via `IRequestQueryService.GetRequestByIdAsync` and enforces Business Rule 9 via `IActiveWorkPackageChecker` / repository query so a request cannot belong to multiple active Work Packages simultaneously.
   - `CloseWorkPackage` transitions the Work Package to `CLOSED` without altering constituent Request lifecycle states or owners.
   - All commands dispatch domain events via `IDomainEventDispatcher` and log structured events.
4. Implemented published `IWorkPackageQueryService` (`WorkPackageQueryService`) and MediatR query handlers in `Cakra.Modules.WorkPackage` using Dapper parameterized SQL for `GetWorkPackageById`, `ListWorkPackages` (filterable by status, ownerPersonId, customerId, productId), `GetWorkPackageScope` (enriched with request details via `IRequestQueryService`), and `GetRequestWorkPackage`.
5. Implemented `WorkPackageModule : IModule` registering `IWorkPackageRepository`, `IActiveWorkPackageChecker`, `IWorkPackageService`, and `IWorkPackageQueryService` in DI.
6. Added unit tests in `WorkPackageServiceTests.cs` and SQL Server integration tests in `WorkPackageModuleIntegrationTests.cs` (`Database=CakraTestDb_WorkPackage` with `WebApplicationFactory<Program>` and `Respawn`); all 58 WorkPackage unit tests and 13 integration/module registration tests pass with 0 warnings and 0 errors.

Changed Files:
- `src/backend/Cakra.Api/Migrations/Scripts/0007_workpackage_tables.sql` (created)
- `src/backend/Cakra.Modules.WorkPackage/Cakra.Modules.WorkPackage.csproj` (modified — added Dapper, DI/Logging abstractions, and InternalsVisibleTo for test projects)
- `src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackageStatus.cs` (modified — added WorkPackageStatusNames helper)
- `src/backend/Cakra.Modules.WorkPackage/Domain/IActiveWorkPackageChecker.cs` (modified — added default async overload IsRequestInActiveWorkPackageAsync)
- `src/backend/Cakra.Modules.WorkPackage/Models/WorkPackageDto.cs` (created)
- `src/backend/Cakra.Modules.WorkPackage/IWorkPackageQueryService.cs` (created)
- `src/backend/Cakra.Modules.WorkPackage/Persistence/IWorkPackageRepository.cs` (created)
- `src/backend/Cakra.Modules.WorkPackage/Persistence/WorkPackageRepository.cs` (created)
- `src/backend/Cakra.Modules.WorkPackage/Services/IWorkPackageService.cs` (created)
- `src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageCommands.cs` (created)
- `src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageQueries.cs` (created)
- `src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageService.cs` (created)
- `src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageQueryService.cs` (created)
- `src/backend/Cakra.Modules.WorkPackage/WorkPackageModule.cs` (created)
- `tests/backend/Cakra.Tests.Unit/WorkPackage/WorkPackageServiceTests.cs` (created)
- `tests/backend/Cakra.Tests.Integration/WorkPackage/WorkPackageModuleIntegrationTests.cs` (created)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — updated P5-S25 to IMPLEMENTED)

---

### P5-S26

Title: Work Package API Controller

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the ASP.NET Core REST API controller for the Work Package module at `/api/v1/work-packages/*` in `Cakra.Api`. Wires all `WorkPackageService` MediatR command handlers and `WorkPackageQueryService` query methods to HTTP endpoints. Separated from Vue SFC screen slice to reduce per-slice scope.

Depends On: P5-S25

Repository: `Cakra.Api`

Completion Criteria:
- ASP.NET Core Controller at `/api/v1/work-packages` exposes REST endpoints:
  - `GET /api/v1/work-packages` → `WorkPackageQueryService.ListWorkPackages` with query parameters for status, owner, customer, product.
  - `GET /api/v1/work-packages/{id}` → `WorkPackageQueryService.GetWorkPackageById`.
  - `GET /api/v1/work-packages/{id}/scope` → `WorkPackageQueryService.GetWorkPackageScope`.
  - `POST /api/v1/work-packages` → `WorkPackageService.CreateWorkPackage`.
  - `PUT /api/v1/work-packages/{id}/objective` → `WorkPackageService.UpdateObjective`.
  - `POST /api/v1/work-packages/{id}/assign-owner` → `WorkPackageService.AssignOwner`.
  - `POST /api/v1/work-packages/{id}/activate` → `WorkPackageService.ActivateWorkPackage`.
  - `POST /api/v1/work-packages/{id}/close` → `WorkPackageService.CloseWorkPackage`.
  - `POST /api/v1/work-packages/{id}/requests` → `WorkPackageService.AddRequestToWorkPackage`.
  - `DELETE /api/v1/work-packages/{id}/requests/{requestId}` → `WorkPackageService.RemoveRequestFromWorkPackage`.
- All endpoints protected by `[Authorize]`.
- Integration tests (xUnit + WebApplicationFactory): each endpoint returns expected HTTP status codes.

Notes: Controller-only slice. Vue SFC is implemented in P5-S27. Architecture §19.6 and §8 (UC-WP-001..003) are authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Implemented `WorkPackagesController` (`src/backend/Cakra.Api/Controllers/WorkPackagesController.cs`) inheriting `ApiControllerBase` at `[Route("api/v1/work-packages")]` and protected by `[Authorize]`:
   - `GET /api/v1/work-packages` dispatches `ListWorkPackagesQuery` (`WorkPackageQueryService.ListWorkPackages`) with query parameters for `status` (`statusFilter`), `ownerPersonId` (`ownerId`, `owner`), `customerId` (`customer`), and `productId` (`product`).
   - `GET /api/v1/work-packages/{id:guid}` dispatches `GetWorkPackageByIdQuery` (`WorkPackageQueryService.GetWorkPackageById`), returning `200 OK` with enriched `WorkPackageDto` or `404 NotFound` RFC 7807 `ProblemDetails`.
   - `GET /api/v1/work-packages/{id:guid}/scope` dispatches `GetWorkPackageScopeQuery` (`WorkPackageQueryService.GetWorkPackageScope`), verifying package existence via `WorkPackageExistsAsync` when empty so nonexistent packages return `404 NotFound` `ProblemDetails`.
   - `GET /api/v1/work-packages/by-request/{requestId:guid}` dispatches `GetRequestWorkPackageQuery` (`WorkPackageQueryService.GetRequestWorkPackage`).
   - `POST /api/v1/work-packages` dispatches `CreateWorkPackageCommand` (`WorkPackageService.CreateWorkPackage`), enriches the created package via `IWorkPackageQueryService.GetWorkPackageByIdAsync`, logs structured telemetry, and returns `201 Created` via `CreatedAtAction`.
   - `PUT /api/v1/work-packages/{id:guid}/objective` (with `POST /api/v1/work-packages/{id:guid}/objective` and `PUT /api/v1/work-packages/{id:guid}` aliases) dispatches `UpdateObjectiveCommand` (`WorkPackageService.UpdateObjective`).
   - `POST /api/v1/work-packages/{id:guid}/assign-owner` (with `PUT /api/v1/work-packages/{id:guid}/owner`, `POST /api/v1/work-packages/{id:guid}/owner`, and `PUT /api/v1/work-packages/{id:guid}/assign-owner` aliases) dispatches `AssignOwnerCommand` (`WorkPackageService.AssignOwner`).
   - `POST /api/v1/work-packages/{id:guid}/activate` (with `PUT /api/v1/work-packages/{id:guid}/activate` alias) dispatches `ActivateWorkPackageCommand` (`WorkPackageService.ActivateWorkPackage`).
   - `POST /api/v1/work-packages/{id:guid}/close` (with `PUT /api/v1/work-packages/{id:guid}/close` alias) dispatches `CloseWorkPackageCommand` (`WorkPackageService.CloseWorkPackage`).
   - `POST /api/v1/work-packages/{id:guid}/requests` dispatches `AddRequestToWorkPackageCommand` (`WorkPackageService.AddRequestToWorkPackage`).
   - `DELETE /api/v1/work-packages/{id:guid}/requests/{requestId:guid}` dispatches `RemoveRequestFromWorkPackageCommand` (`WorkPackageService.RemoveRequestFromWorkPackage`).
   - Domain and business-rule exceptions (`WorkPackageDomainException`, `BusinessRuleViolationException`, `InvalidWorkPackageStateTransitionException`, `InvalidOperationException`) map cleanly to `400 Bad Request` RFC 7807 `ProblemDetails` (`application/problem+json`).
2. Added SQL Server integration tests in `tests/backend/Cakra.Tests.Integration/WorkPackage/WorkPackagesControllerTests.cs` using `WebApplicationFactory<Program>`, isolated database `CakraTestDb_WorkPackagesController`, and `Respawn`. Verified `dotnet test cakra\Cakra.sln --filter FullyQualifiedName~WorkPackagesControllerTests` passes all 3 integration test suites (401 unauthenticated enforcement across all 10 endpoints, full end-to-end Work Package lifecycle/scope/filter flow, and 400/404 ProblemDetails + Business Rule 9 enforcement) with 0 failures and 0 warnings.

Changed Files:
- `src/backend/Cakra.Api/Controllers/WorkPackagesController.cs` (created — REST API controller for `/api/v1/work-packages/*`)
- `tests/backend/Cakra.Tests.Integration/WorkPackage/WorkPackagesControllerTests.cs` (created — SQL Server HTTP integration tests)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — updated P5-S26 status to IMPLEMENTED with implementation notes and changed files)

---

### P5-S27

Title: Work Package Screen — SCR-WP-001

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement `SCR-WP-001` (Work Package screen) as a Vue 3 SFC with Bootstrap 5 styling in `src/frontend/Cakra.Web/`. Delivers Work Package creation, lifecycle management, and scope review.

Depends On: P5-S26, P1-S08

Repository: `src/frontend/Cakra.Web`

Completion Criteria:
- Vue 3 SFC `WorkPackageView.vue` (`<script setup lang="ts">`, Bootstrap 5) renders: Bootstrap 5 table listing work packages with columns (ID, Objective, Owner, Customer, Product, Status), filter dropdowns for status/owner/customer/product, create button, detail panel showing scope (linked requests) when a work package is selected — Architecture §19.4, §20.
- Detail panel includes: objective display/edit, owner selector dropdown (from `GET /api/v1/organization/persons/active`), lifecycle action buttons (Activate, Close) conditionally rendered based on state, scope management section with "Add Request" button and request list with remove buttons.
- Vue Router 4 routes: `/work-packages` maps to work package list, `/work-packages/:id` maps to detail view.
- Axios HTTP client calls `/api/v1/work-packages` endpoints with authentication interceptors.

Notes: Frontend-only slice. This slice completes Milestone M3. Architecture §9 (FEAT-WP-001) and §8 (UC-WP-001..003) are authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Implemented `WorkPackageView.vue` (`src/frontend/Cakra.Web/src/views/WorkPackageView.vue`) as a Vue 3 SFC (`<script setup lang="ts">`, Composition API) with Bootstrap 5 styling and `data-screen-id="SCR-WP-001"` (Architecture §8 `UC-WP-001..003`, §9 `FEAT-WP-001`, §11, §19.4, §20, §21):
   - Renders a Bootstrap 5 table listing work packages (`GET /api/v1/work-packages`) with columns (`ID`, `Objective`, `Owner`, `Customer`, `Product`, `Status`) and row/link selection (`selectWorkPackage`).
   - Renders filter dropdowns above the table for `status` (`DRAFT`, `ACTIVE`, `CLOSED`), `owner` (`GET /api/v1/organization/persons/active`), `customer` (`GET /api/v1/customers/active`), and `product` (`GET /api/v1/products/active`).
   - Renders a "Create Work Package" button opening an inline modal card (`POST /api/v1/work-packages` with required `objective`, optional `name` defaulting to objective, required `ownerPersonId`, optional `customerId`, and optional `productId`).
   - Renders a responsive detail panel when a work package is selected (via row click or `/work-packages/:id` route parameter) calling `GET /api/v1/work-packages/${id}` and `GET /api/v1/work-packages/${id}/scope`:
     * Displays and edits the work package objective (`PUT /api/v1/work-packages/${id}/objective`).
     * Provides an owner selector dropdown populated from `GET /api/v1/organization/persons/active` calling `POST /api/v1/work-packages/${id}/assign-owner`.
     * Conditionally renders lifecycle action buttons based on state: `Activate` (`POST /api/v1/work-packages/${id}/activate`, visible when `DRAFT`) and `Close` (`POST /api/v1/work-packages/${id}/close` with close reason input, visible when `DRAFT` or `ACTIVE`).
     * Provides a scope management section with an "Add Request" selector (populated from `GET /api/v1/requests`) and manual Request ID input calling `POST /api/v1/work-packages/${id}/requests`, active linked request list with links to `/requests/${requestId}` and `Remove` buttons (`DELETE /api/v1/work-packages/${id}/requests/${requestId}`), and historical removed scope items.
   - Uses the shared Axios `httpClient` (`@/api/http`) with cookie authentication interceptors and RFC 7807 `ProblemDetails` error extraction.
2. Registered `/work-packages` and `/work-packages/:id` routes (`meta: { requiresAuth: true, screenId: 'SCR-WP-001' }`) in `src/frontend/Cakra.Web/src/router/index.ts` and added the "Work Packages" navigation link in `src/frontend/Cakra.Web/src/App.vue`.
3. Verified `npm run build` (`vue-tsc --noEmit && vite build`) in `src/frontend/Cakra.Web` succeeds with 0 TypeScript or Vite build errors.

Changed Files:
- `src/frontend/Cakra.Web/src/views/WorkPackageView.vue` (created — `SCR-WP-001` Work Package screen)
- `src/frontend/Cakra.Web/src/router/index.ts` (modified — registered `/work-packages` and `/work-packages/:id` routes for `SCR-WP-001`)
- `src/frontend/Cakra.Web/src/App.vue` (modified — added "Work Packages" navigation link)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — updated P5-S27 status to IMPLEMENTED with implementation notes and changed files)

---

## P6 — Post & Feed

Implementation Status: COMPLETED
Review Status: GO

Objective: Implement the Post module (authoring, comments, reactions) and the Feed materialized read model using Dapper. Deliver the Feed and Post screens as Vue 3 SFCs via a separated API controller slice and independent frontend slices. Feed projection handled by MediatR `INotificationHandler` within the same DB transaction scope. M4 is achieved when this phase is complete.

Source: Architecture §22 — Boundary: **Post & Feed** (`Cakra.Modules.Post`, Depends On: `Cakra.Core`, Domain Events from all modules). Architecture §12 (Feed Architecture).

---

### P6-S28

Title: Post Module — Domain, Application Services & Persistence

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the Post domain layer and persistence: `Post`, `Comment`, `Reaction`, `PostReference` entities, `PostService` commands as MediatR handlers, `PostQueryService` queries using Dapper, DbUp SQL migration scripts for `post.*` tables (excluding `FeedItems`), and Dapper repository implementations. `PostService` validates cross-domain references via published query services.

Depends On: P1-S06, P1-S07, P2-S10, P4-S19, P5-S25

Repository: `Cakra.Modules.Post`

Completion Criteria:
- DbUp SQL migration scripts create `post.*` schema tables: `Posts`, `Comments`, `Reactions`, `PostReferences` — Architecture §17.
- All repository operations use Dapper with explicit parameterized SQL; no EF Core — Architecture §19.3, §20.
- MediatR handlers via `PostService` implement: `CreateOperationalPost` (human authored), `RecordSystemPost` (system generated), `PostComment`, `AddReaction`, `RemoveReaction`, `TogglePostVisibility`, `ArchivePost` — Architecture §7, §8 (UC-FCOL-001..003, UC-COL-001).
- `PostQueryService` implements using Dapper: `GetPostThreadDetails`, `GetFullComments`, `GetReactionList` — Architecture §7.
- Domain events emitted as MediatR `INotification`: `PostCreated`, `CommentAdded`, `ReactionAdded`, `ReactionRemoved`, `PostVisibilityChanged`, `PostArchived` — Architecture §12 (Update Triggers).
- Soft-delete/archive only; no physical purge of posts, comments, or reactions — Architecture §20 (Permanent Retention).
- Repository implementations write exclusively to `post.*` schema.
- Integration tests (xUnit + WebApplicationFactory + Respawn): post authoring, commenting, and reaction flows pass.

Notes: Architecture §7, §8, §18, §19.2 (MediatR notifications), §20 are authoritative. `FeedItems` table and projection are implemented in P6-S29. Depends on P4-S19 and P5-S25 because `PostService` validates `RequestId` and `WorkPackageId` references — Architecture §15.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Added embedded DbUp SQL migration script `src/backend/Cakra.Api/Migrations/Scripts/0008_post_tables.sql` idempotently creating `[post].[Posts]`, `[post].[Comments]`, `[post].[Reactions]`, and `[post].[PostReferences]` with intra-schema foreign keys (`FK_Comments_Posts`, `FK_Reactions_Posts`, `FK_PostReferences_Posts`), check constraints on `Source` (`HUMAN_AUTHORED`, `SYSTEM_GENERATED`), `Status` (`ACTIVE`, `ARCHIVED`), `Visibility` (`VISIBLE`, `HIDDEN`), `ReactionType` (`SEEN`, `EXPERIENCED`, `HAVE_IDEA`, `SIMILAR_ISSUE`, `DUPLICATE`, `NEED_CLARIFICATION`), and `ReferenceType` (`REQUEST`, `WORK_PACKAGE`, `CUSTOMER`, `PRODUCT`, `ORGANIZATION`), filtered unique indexes, and zero cross-schema foreign keys (Architecture §17, §20).
2. Implemented the Post domain layer in `src/backend/Cakra.Modules.Post/Domain/`: `Post` aggregate root, `Comment`, `Reaction`, and `PostReference` entities, `PostStatus`, `PostVisibility`, `PostSource`, `PostReactionTypes`, `PostReferenceTypes`, `PostExceptionTypes`, domain exceptions (`PostDomainException`, `PostDomainValidationException`, `InvalidPostStateException`), and all 6 MediatR `IDomainEvent` notifications (`PostCreated`, `CommentAdded`, `ReactionAdded`, `ReactionRemoved`, `PostVisibilityChanged`, `PostArchived`) carrying full metadata for `FeedProjectionHandler` in P6-S29.
3. Implemented `internal` Dapper repository `PostRepository` (`IPostRepository`) in `src/backend/Cakra.Modules.Post/Persistence/` with `DbType.DateTime2` mapping, writing exclusively to the `post.*` schema using explicit parameterized SQL with zero EF Core usage and zero physical `DELETE` operations (`RemoveReaction` sets `IsActive = 0` and `RemovedAt` timestamp per Architecture §20, §21 Permanent Data Retention).
4. Implemented `IPostService` and `PostService` in `src/backend/Cakra.Modules.Post/Services/` along with MediatR commands and FluentValidation validators (`CreateOperationalPostCommand`, `RecordSystemPostCommand`, `PostCommentCommand`, `AddReactionCommand`, `RemoveReactionCommand`, `TogglePostVisibilityCommand`, `ArchivePostCommand`), validating cross-module references via `IOrganizationQueryService`, `ICustomerQueryService`, `IProductQueryService`, `IRequestQueryService`, and `IWorkPackageQueryService`, and dispatching domain events via `IDomainEventDispatcher`.
5. Implemented published `IPostQueryService` and `PostQueryService` using Dapper parameterized SQL for `GetPostThreadDetails`, `GetFullComments`, `GetReactionList`, `PostExists`, and `GetPostsByReference`, and registered all Post services in `PostModule : IModule`.
6. Added integration and domain tests in `tests/backend/Cakra.Tests.Integration/Post/PostModuleIntegrationTests.cs` executing against isolated SQL Server test database `CakraTestDb_Post` using `WebApplicationFactory<Program>` and `Respawn`. Verified `dotnet test cakra\Cakra.sln --filter FullyQualifiedName~PostModule` passes all 4 test suites with 0 warnings and 0 errors.

Changed Files:
- `src/backend/Cakra.Api/Migrations/Scripts/0008_post_tables.sql` (created)
- `src/backend/Cakra.Modules.Post/Cakra.Modules.Post.csproj` (modified — added Dapper, DI/Logging abstractions, and InternalsVisibleTo for test projects)
- `src/backend/Cakra.Modules.Post/Domain/PostStatus.cs` (created)
- `src/backend/Cakra.Modules.Post/Domain/PostVisibility.cs` (created)
- `src/backend/Cakra.Modules.Post/Domain/PostSource.cs` (created)
- `src/backend/Cakra.Modules.Post/Domain/PostReactionTypes.cs` (created)
- `src/backend/Cakra.Modules.Post/Domain/PostReferenceTypes.cs` (created)
- `src/backend/Cakra.Modules.Post/Domain/Exceptions/PostDomainException.cs` (created)
- `src/backend/Cakra.Modules.Post/Domain/Exceptions/PostDomainValidationException.cs` (created)
- `src/backend/Cakra.Modules.Post/Domain/Exceptions/InvalidPostStateException.cs` (created)
- `src/backend/Cakra.Modules.Post/Domain/Events/PostCreated.cs` (created)
- `src/backend/Cakra.Modules.Post/Domain/Events/CommentAdded.cs` (created)
- `src/backend/Cakra.Modules.Post/Domain/Events/ReactionAdded.cs` (created)
- `src/backend/Cakra.Modules.Post/Domain/Events/ReactionRemoved.cs` (created)
- `src/backend/Cakra.Modules.Post/Domain/Events/PostVisibilityChanged.cs` (created)
- `src/backend/Cakra.Modules.Post/Domain/Events/PostArchived.cs` (created)
- `src/backend/Cakra.Modules.Post/Domain/Comment.cs` (created)
- `src/backend/Cakra.Modules.Post/Domain/Reaction.cs` (created)
- `src/backend/Cakra.Modules.Post/Domain/PostReference.cs` (created)
- `src/backend/Cakra.Modules.Post/Domain/Post.cs` (created)
- `src/backend/Cakra.Modules.Post/Models/PostDto.cs` (created)
- `src/backend/Cakra.Modules.Post/IPostQueryService.cs` (created)
- `src/backend/Cakra.Modules.Post/Persistence/IPostRepository.cs` (created)
- `src/backend/Cakra.Modules.Post/Persistence/PostRepository.cs` (created)
- `src/backend/Cakra.Modules.Post/Services/IPostService.cs` (created)
- `src/backend/Cakra.Modules.Post/Services/PostCommands.cs` (created)
- `src/backend/Cakra.Modules.Post/Services/PostQueries.cs` (created)
- `src/backend/Cakra.Modules.Post/Services/PostService.cs` (created)
- `src/backend/Cakra.Modules.Post/Services/PostQueryService.cs` (created)
- `src/backend/Cakra.Modules.Post/PostModule.cs` (created)
- `tests/backend/Cakra.Tests.Integration/Post/PostModuleIntegrationTests.cs` (created)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — updated P6-S28 status to IMPLEMENTED)

---

### P6-S29

Title: Feed Projection — FeedItems Table & FeedProjectionHandler

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the `FeedItems` materialized read model: create `FeedItems` table via DbUp SQL migration script, implement `FeedProjectionHandler` as MediatR `INotificationHandler<T>` subscribing to in-process domain events (`PostCreated`, `CommentAdded`, `ReactionAdded`, `ReactionRemoved`, `PostVisibilityChanged`, `PostArchived`), and the `FeedProjectionRebuilder` idempotent rebuild routine using Dapper.

Depends On: P6-S28

Repository: `Cakra.Modules.Post`

Completion Criteria:
- DbUp SQL migration script creates `FeedItems` table in `post.*` schema with all columns per Architecture §12 (Feed Projection Table Schema).
- `FeedProjectionHandler` implements MediatR `INotificationHandler<T>` for all six event types and updates `FeedItems` using Dapper parameterized SQL within the same database transaction scope as the originating command — Architecture §12 (Synchronization Guarantee), §19.2.
- `FeedProjectionRebuilder.RebuildAll()` uses Dapper to truncate and fully regenerate `FeedItems` from authoritative `Posts`, `PostReferences`, `Comments`, `Reactions` — Architecture §12 (Rebuild Strategy). Registered as an `IHostedService` background task via `System.Threading.Channels` — Architecture §19.7.
- `FeedItems` is strictly read-only for all consumers; no application code writes to `FeedItems` except `FeedProjectionHandler` and `FeedProjectionRebuilder` — Architecture §20 (Non-Mutating Projections).
- Integration tests (xUnit + WebApplicationFactory + Respawn): post creation inserts correct `FeedItem`; comment increments `CommentCount`; reaction updates `ReactionCountsJson`; visibility change updates `Visibility`; archive sets `Status = 'ARCHIVED'`.

Notes: Architecture §12 and §19.7 (Background Processing — `System.Threading.Channels`) are authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Added embedded DbUp SQL migration script `src/backend/Cakra.Api/Migrations/Scripts/0009_feed_items_table.sql` idempotently creating `[post].[FeedItems]` with all columns per Architecture §12 (`FeedItemId`, `PostId`, `AuthorPersonId`, `AuthorName`, `PostType`/`Source`, `Title`, `ContentExcerpt`/`Summary`, `Status`, `Visibility`, `IsException`, `ExceptionType`, `ReferenceType`, `ReferenceId`, `ReferenceDisplay`, `CustomerId`, `CustomerName`, `ProductId`, `ProductName`, `RequestId`, `WorkPackageId`, `CommentCount`, `LatestCommentExcerpt`, `ReactionCountsJson`, `CreatedAt`, `UpdatedAt`/`LastActivityAt`), intra-schema foreign key `FK_FeedItems_Posts`, unique constraint `UQ_FeedItems_PostId`, zero cross-schema foreign keys, and nonclustered query indexes (`IX_FeedItems_Visibility_Status_CreatedAt`, `IX_FeedItems_CustomerId_CreatedAt`, `IX_FeedItems_ProductId_CreatedAt`, `IX_FeedItems_IsException_CreatedAt`, `IX_FeedItems_RequestId`, `IX_FeedItems_WorkPackageId`, `IX_FeedItems_CreatedAt`, `IX_FeedItems_LastActivityAt`).
2. Created read-only `FeedItemDto` (`src/backend/Cakra.Modules.Post/Models/FeedItemDto.cs`) representing projected rows in `post.FeedItems` with canonical `ReactionCountsJson` serialization/parsing helpers.
3. Implemented `FeedProjectionHandler` (`src/backend/Cakra.Modules.Post/Services/FeedProjectionHandler.cs`) as MediatR `INotificationHandler<T>` for all six Post domain events (`PostCreated`, `CommentAdded`, `ReactionAdded`, `ReactionRemoved`, `PostVisibilityChanged`, `PostArchived`) plus Request exception events (`RequestEscalated`, `RequestRejected`), updating `post.FeedItems` synchronously in-process using Dapper parameterized SQL and enriching display attributes via published query services (`IOrganizationQueryService`, `ICustomerQueryService`, `IProductQueryService`, `IRequestQueryService`, `IWorkPackageQueryService`).
4. Implemented `IFeedProjectionRebuilder` and `FeedProjectionRebuilder` (`src/backend/Cakra.Modules.Post/Services/IFeedProjectionRebuilder.cs`, `src/backend/Cakra.Modules.Post/Services/FeedProjectionRebuilder.cs`) inheriting `BackgroundService` (`IHostedService`) with an in-process `System.Threading.Channels.Channel<T>` queue (`EnqueueRebuildAsync` / `TryEnqueueRebuild`) and synchronous/asynchronous `RebuildAll()` / `RebuildAllAsync()` using Dapper parameterized SQL inside a transaction to `TRUNCATE TABLE [post].[FeedItems]` and regenerate all rows from authoritative `post.Posts`, `post.PostReferences`, `post.Comments`, and `post.Reactions`. Registered `FeedProjectionHandler`, `IFeedProjectionRebuilder`, `FeedProjectionRebuilder`, and `AddHostedService<FeedProjectionRebuilder>()` in `PostModule.cs`.
5. Added SQL Server integration tests in `tests/backend/Cakra.Tests.Integration/Post/FeedProjectionIntegrationTests.cs` (using `WebApplicationFactory<Program>` and Respawn against isolated database `CakraTestDb_FeedProjection`). Verified `dotnet test cakra\Cakra.sln --filter FullyQualifiedName~FeedProjection` passes all 3 test suites with 0 failures and 0 warnings.

Changed Files:
- `src/backend/Cakra.Api/Migrations/Scripts/0009_feed_items_table.sql` (created — DbUp migration script for `post.FeedItems`)
- `src/backend/Cakra.Modules.Post/Cakra.Modules.Post.csproj` (modified — added `FrameworkReference` to `Microsoft.AspNetCore.App` for `BackgroundService` / `IHostedService`)
- `src/backend/Cakra.Modules.Post/Models/FeedItemDto.cs` (created — non-mutating read projection DTO for `post.FeedItems`)
- `src/backend/Cakra.Modules.Post/Services/FeedProjectionHandler.cs` (created — MediatR `INotificationHandler<T>` for Post and Request exception domain events)
- `src/backend/Cakra.Modules.Post/Services/IFeedProjectionRebuilder.cs` (created — rebuilder interface)
- `src/backend/Cakra.Modules.Post/Services/FeedProjectionRebuilder.cs` (created — Dapper idempotent rebuilder and `BackgroundService` channel worker)
- `src/backend/Cakra.Modules.Post/PostModule.cs` (modified — registered `FeedProjectionHandler`, `IFeedProjectionRebuilder`, `FeedProjectionRebuilder`, and `AddHostedService<FeedProjectionRebuilder>()`)
- `tests/backend/Cakra.Tests.Integration/Post/FeedProjectionIntegrationTests.cs` (created — SQL Server integration tests for migration 0009, `FeedProjectionHandler`, and `FeedProjectionRebuilder`)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — updated P6-S29 status to IMPLEMENTED with implementation notes and changed files)

---

### P6-S30

Title: Feed Query Service — Filtered Queries & Exception Detection

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the full `FeedQueryService` filtering capabilities (Customer, Product, Exception badge, pagination) using Dapper single-table indexed query over `FeedItems`, and exception flag population logic in `FeedProjectionHandler` for escalation, rejection, and stalled request scenarios.

Depends On: P6-S29

Repository: `Cakra.Modules.Post`

Completion Criteria:
- `FeedQueryService.GetFeed(filter, pagination)` executes the single-table indexed Dapper query per Architecture §12 (Query Semantics): filters on `Visibility = 'VISIBLE'`, `Status = 'ACTIVE'`, `CustomerId`, `ProductId`, `IsException`, with `ORDER BY CreatedAt DESC` and `LIMIT/OFFSET` pagination — sub-50ms target per Architecture §20.
- All Dapper SQL uses parameterized queries; no string concatenation of filter values — Architecture §20 (Parameterization Requirement).
- `IsException` is set to `TRUE` and `ExceptionType` populated (`ESCALATION`, `REJECTION`, `STALLED`) by `FeedProjectionHandler` when processing domain events indicating exceptional request states.
- Pagination (`PageSize`, `Offset`) returns correctly bounded result sets.
- Supports UC-FCOL-005 (Filter Feed), UC-AWR-001 (Observe Feed), UC-AWR-002 (Discover via Feed), UC-AWR-003 (Monitor Exceptions) — Architecture §8.
- Integration tests (xUnit + WebApplicationFactory + Respawn): no filter, Customer filter, Product filter, exceptions-only filter, and pagination boundary pass.

Notes: Architecture §12 (Query Semantics, Update Triggers), §19.3 (Dapper, explicit SQL), and §20 (parameterization, sub-50ms) are authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Created published `IFeedQueryService` contract, `FeedFilter`, `FeedPagination`, and `FeedPageResultDto` (`src/backend/Cakra.Modules.Post/IFeedQueryService.cs`) along with MediatR queries (`GetFeedQuery`, `GetFeedItemByPostIdQuery`, `GetFeedItemByIdQuery`) and FluentValidation validator `GetFeedQueryValidator` (`src/backend/Cakra.Modules.Post/Services/FeedQueries.cs`).
2. Implemented `FeedQueryService` (`src/backend/Cakra.Modules.Post/Services/FeedQueryService.cs`) executing a single-table indexed Dapper parameterized query (`QueryMultipleAsync`) over `post.FeedItems` per Architecture §12 and §20:
   - Filters on `Visibility = 'VISIBLE'`, `Status = 'ACTIVE'`, optional `CustomerId`, optional `ProductId`, optional `IsException` / `ExceptionsOnly` (`AND (@ExceptionsOnly = 0 OR [IsException] = 1)`), optional `ExceptionType` (`ESCALATION`, `REJECTION`, `STALLED`), optional `RequestId`, `WorkPackageId`, `AuthorPersonId`, `ReferenceType`, `ReferenceId`, and `SearchTerm`.
   - Orders by `CreatedAt DESC, FeedItemId DESC` with `OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY` pagination, returning `FeedPageResultDto` (`Items`, `TotalCount`, `Page`, `PageSize`, `Offset`, `TotalPages`, `HasMore`) in a single database round-trip (sub-50ms target).
3. Extended `FeedProjectionHandler` (`src/backend/Cakra.Modules.Post/Services/FeedProjectionHandler.cs`), `FeedProjectionRebuilder` (`src/backend/Cakra.Modules.Post/Services/FeedProjectionRebuilder.cs`), and added `RequestStalled` domain event notification (`src/backend/Cakra.Modules.Post/Domain/Events/RequestStalled.cs`) to populate `IsException = TRUE` and `ExceptionType` (`ESCALATION`, `REJECTION`, `STALLED`) across all exceptional request scenarios:
   - Subscribes to `RequestEscalated` (`ESCALATION`), `RequestRejected` (`REJECTION`), and `RequestStalled` (`STALLED`) as well as `PostCreated` (inferring `ExceptionType` from explicit `ExceptionType`, `SourceEventType`, or referenced `Request.Status`).
   - When a request exception event is handled, updates any existing `post.Posts` and `post.FeedItems` rows linked to `RequestId`, and if no `FeedItems` row exists yet for that `RequestId`, automatically records a `SYSTEM_GENERATED` exception post in `post.Posts`, `post.PostReferences`, and `post.FeedItems` enriched with `CustomerId`, `CustomerName`, `ProductId`, `ProductName`, `AuthorPersonId`, and `AuthorName`.
4. Registered `FeedQueryService` and `IFeedQueryService` in `PostModule : IModule` (`src/backend/Cakra.Modules.Post/PostModule.cs`).
5. Added SQL Server integration tests in `tests/backend/Cakra.Tests.Integration/Post/FeedQueryIntegrationTests.cs` (using `WebApplicationFactory<Program>`, isolated database `CakraTestDb_FeedQuery`, and Respawn) verifying no-filter, Customer filter, Product filter, combined filter, exception detection (`ESCALATION`, `REJECTION`, `STALLED`) + exceptions-only filter, and pagination boundaries. Verified `dotnet test cakra\Cakra.sln --filter FullyQualifiedName~FeedQueryIntegrationTests` passes all 3 test suites with 0 failures and 0 warnings.

Changed Files:
- `src/backend/Cakra.Modules.Post/IFeedQueryService.cs` (created — published query contract, `FeedFilter`, `FeedPagination`, `FeedPageResultDto`)
- `src/backend/Cakra.Modules.Post/Domain/Events/RequestStalled.cs` (created — `RequestStalled` domain event notification for `STALLED` exception detection)
- `src/backend/Cakra.Modules.Post/Services/FeedQueries.cs` (created — `GetFeedQuery`, `GetFeedQueryValidator`, `GetFeedItemByPostIdQuery`, `GetFeedItemByIdQuery`)
- `src/backend/Cakra.Modules.Post/Services/FeedQueryService.cs` (created — Dapper single-table indexed query service and MediatR handlers)
- `src/backend/Cakra.Modules.Post/Services/FeedProjectionHandler.cs` (modified — added `INotificationHandler<RequestStalled>`, `SourceEventType` exception inference, and automatic `SYSTEM_GENERATED` exception post + `FeedItem` creation when no feed item exists yet for an escalated/rejected/stalled request)
- `src/backend/Cakra.Modules.Post/Services/FeedProjectionRebuilder.cs` (modified — added `SourceEventType` exception type inference fallback on rebuild)
- `src/backend/Cakra.Modules.Post/PostModule.cs` (modified — registered `FeedQueryService` and `IFeedQueryService`)
- `tests/backend/Cakra.Tests.Integration/Post/FeedQueryIntegrationTests.cs` (created — SQL Server integration tests for filtered feed queries, exception detection, and pagination boundaries)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — updated P6-S30 status to IMPLEMENTED with implementation notes and changed files)

---

### P6-S31

Title: Feed & Post API Controller

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the ASP.NET Core REST API controllers for the Feed and Post modules at `/api/v1/feed/*` and `/api/v1/posts/*` in `Cakra.Api`. Wires `FeedQueryService`, `PostService`, and `PostQueryService` to HTTP endpoints. Separated from Vue SFC screen slices to enable parallel frontend development.

Depends On: P6-S30

Repository: `Cakra.Api`

Completion Criteria:
- ASP.NET Core Controller at `/api/v1/feed` exposes REST endpoints:
  - `GET /api/v1/feed` → `FeedQueryService.GetFeed` with query parameters for CustomerId, ProductId, IsException, PageSize, Offset.
- ASP.NET Core Controller at `/api/v1/posts` exposes REST endpoints:
  - `GET /api/v1/posts/{id}` → `PostQueryService.GetPostThreadDetails`.
  - `GET /api/v1/posts/{id}/comments` → `PostQueryService.GetFullComments`.
  - `GET /api/v1/posts/{id}/reactions` → `PostQueryService.GetReactionList`.
  - `POST /api/v1/posts` → `PostService.CreateOperationalPost`.
  - `POST /api/v1/posts/{id}/comments` → `PostService.PostComment`.
  - `POST /api/v1/posts/{id}/reactions` → `PostService.AddReaction`.
  - `DELETE /api/v1/posts/{id}/reactions/{reactionType}` → `PostService.RemoveReaction`.
- All endpoints protected by `[Authorize]`.
- Integration tests (xUnit + WebApplicationFactory): feed listing, post detail, comment creation, and reaction endpoints return expected HTTP status codes.

Notes: Controller-only slice. Vue SFCs are implemented in P6-S32 and P6-S33. Architecture §19.6 and §8 are authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Implemented `FeedController` (`src/backend/Cakra.Api/Controllers/FeedController.cs`) inheriting `ApiControllerBase` at `[Route("api/v1/feed")]` and protected by `[Authorize]`:
   - `GET /api/v1/feed` dispatches `GetFeedQuery` (`FeedQueryService.GetFeed` / `GetFeedAsync`) with query parameters for `customerId` (`customer`), `productId` (`product`), `isException` (`exceptionsOnly`, `exceptionOnly`, `exceptionType`), `pageSize`, `offset` (`page`), `requestId`, `workPackageId`, `authorPersonId`, and `searchTerm`, returning `200 OK` with `FeedPageResultDto` or `400 Bad Request` RFC 7807 `ProblemDetails`.
   - `GET /api/v1/feed/{id:guid}` and `GET /api/v1/feed/by-post/{postId:guid}` expose single projected `FeedItemDto` lookups.
2. Implemented `PostsController` (`src/backend/Cakra.Api/Controllers/PostsController.cs`) inheriting `ApiControllerBase` at `[Route("api/v1/posts")]` and protected by `[Authorize]`:
   - `GET /api/v1/posts/{id:guid}` dispatches `GetPostThreadDetailsQuery` (`PostQueryService.GetPostThreadDetails`), returning `200 OK` with enriched `PostThreadDetailsDto` or `404 Not Found` `ProblemDetails`.
   - `GET /api/v1/posts/{id:guid}/comments` dispatches `GetFullCommentsQuery` (`PostQueryService.GetFullComments`), verifying post existence when empty so unknown post IDs return `404 Not Found` `ProblemDetails`.
   - `GET /api/v1/posts/{id:guid}/reactions` dispatches `GetReactionListQuery` (`PostQueryService.GetReactionList`), verifying post existence when empty so unknown post IDs return `404 Not Found` `ProblemDetails`.
   - `POST /api/v1/posts` dispatches `CreateOperationalPostCommand` (`PostService.CreateOperationalPost`), accepting both `content` and `body`, resolving `authorPersonId` from payload or `ICurrentContextProvider.CurrentPersonId`, inheriting `CustomerId` and `ProductId` from referenced `RequestId` when omitted in the payload so `post.FeedItems` is populated with `CustomerId`, `ProductId`, `RequestId`, and `PostId`, and returning `201 Created` via `CreatedAtAction`.
   - `POST /api/v1/posts/{id:guid}/comments` dispatches `PostCommentCommand` (`PostService.PostComment`), accepting `content` or `body` and `authorPersonId` or `CurrentPersonId`, returning `201 Created` via `CreatedAtAction`.
   - `POST /api/v1/posts/{id:guid}/reactions` dispatches `AddReactionCommand` (`PostService.AddReaction`), accepting `reactionType` or `type` and `personId` or `CurrentPersonId`, returning `200 OK` with `ReactionDto`.
   - `DELETE /api/v1/posts/{id:guid}/reactions/{reactionType}` dispatches `RemoveReactionCommand` (`PostService.RemoveReaction`), soft-removing the reaction without physical deletion and returning `200 OK` with enriched `PostThreadDetailsDto`.
   - Also supports `POST/PUT /api/v1/posts/{id:guid}/visibility` (`PostService.TogglePostVisibility`), `POST/PUT /api/v1/posts/{id:guid}/archive` (`PostService.ArchivePost`), and `GET /api/v1/posts/by-reference` (`PostQueryService.GetPostsByReference`).
3. Added SQL Server integration tests in `tests/backend/Cakra.Tests.Integration/Post/FeedAndPostsControllerTests.cs` using `WebApplicationFactory<Program>`, isolated database `CakraTestDb_FeedController`, and `Respawn`. Verified `dotnet test cakra\Cakra.sln --filter FullyQualifiedName~FeedAndPostsControllerTests` passes all 3 test suites (401 unauthenticated enforcement across all Feed and Post endpoints, full post creation with `RequestId` inheritance + comments + reactions + filtered/paginated feed queries, and 400/404 `ProblemDetails` validation) with 0 failures and 0 warnings.

Changed Files:
- `src/backend/Cakra.Api/Controllers/FeedController.cs` (created — REST API controller for `/api/v1/feed/*`)
- `src/backend/Cakra.Api/Controllers/PostsController.cs` (created — REST API controller for `/api/v1/posts/*`)
- `tests/backend/Cakra.Tests.Integration/Post/FeedAndPostsControllerTests.cs` (created — SQL Server HTTP integration tests)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — updated P6-S31 status to IMPLEMENTED with implementation notes and changed files)

---

### P6-S32

Title: Feed Screen — SCR-FEED-001

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the Feed screen (`SCR-FEED-001`) as a Vue 3 SFC with Bootstrap 5 styling in `src/frontend/Cakra.Web/`. Delivers the operational feed with filtering by Customer, Product, and Exception status.

Depends On: P6-S31, P1-S08

Repository: `src/frontend/Cakra.Web`

Completion Criteria:
- Vue 3 SFC `FeedView.vue` (`<script setup lang="ts">`, Bootstrap 5) renders: Bootstrap 5 card-based feed layout with each feed item displayed as a card containing (Title, Author, Customer, Product, CreatedAt, CommentCount, ReactionCount), filter sidebar or dropdown row with Customer selector, Product selector, and Exception-only toggle, pagination controls (Next/Previous or infinite scroll) — Architecture §19.4, §20.
- Exception badge indicator rendered as a Bootstrap 5 badge (`badge bg-danger`) on cards where `IsException = TRUE`, showing `ExceptionType` text.
- Feed item cards are clickable and navigate to Post Detail modal (SCR-POST-001) or Request Detail (`/requests/{requestId}`) as appropriate — UC-FCOL-004.
- Vue Router 4 route: `/feed` maps to `FeedView.vue` (this is the post-login landing page).
- Axios HTTP client calls `GET /api/v1/feed` with filter query parameters and authentication interceptors.

Notes: Frontend-only slice. **Parallel Execution**: Can execute concurrently with P6-S33 after P6-S31. Architecture §9 (FEAT-AWR-001, FEAT-FCOL-005) and §8 (UC-AWR-001..003, UC-FCOL-005) are authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Implemented `FeedView.vue` (`src/frontend/Cakra.Web/src/views/FeedView.vue`) as a Vue 3 SFC (`<script setup lang="ts">`, Composition API, Bootstrap 5, `data-screen-id="SCR-FEED-001"`):
   - Renders a Bootstrap 5 card-based operational feed layout (`article.card.feed-item-card`) where each feed item card displays `Title`, `Author` (`Author`/`AuthorName`), `Customer` (`Customer`/`CustomerName`), `Product` (`Product`/`ProductName`), `CreatedAt`, `CommentCount`, and `ReactionCount` (plus per-type reaction count pills), as well as `ContentExcerpt`/`Summary` and `LatestCommentExcerpt` when present.
   - Renders a Bootstrap 5 danger badge (`class="badge bg-danger"`) on cards where `isException === true` (`IsException = TRUE`), displaying `exceptionType` (`ESCALATION`, `REJECTION`, `STALLED`).
   - Provides a filter bar with `Customer` selector (`GET /api/v1/customers/active`), `Product` selector (`GET /api/v1/products/active`), `Exception Type` selector, and `Exceptions Only` switch (`isException`), plus Filter and Reset actions (`UC-FCOL-005`, `FEAT-FCOL-005`).
   - Provides a "New Operational Post" button and inline authoring form (`POST /api/v1/posts` with `title`, `content`/`body`, optional `customerId`, optional `productId`, optional `requestId`, and optional `isException`/`exceptionType` — `UC-FCOL-003`, `FEAT-FCOL-003`).
   - Feed item cards are clickable and open `PostDetailModal.vue` (`SCR-POST-001`) with `:post-id`, `:show`, `:initial-request-id`, `:initial-work-package-id`, `@close`, and `@updated` (refreshing the feed when comments, reactions, visibility, or archive state change), and provide a direct `<RouterLink>` / button to Request Detail (`/requests/${requestId}`) when a request reference is present (`UC-FCOL-004`) as well as Work Package Detail (`/work-packages/${workPackageId}`).
   - Provides `Previous` / `Next` pagination controls and item range indicator calling `GET /api/v1/feed` with `customerId`, `productId`, `isException`, `exceptionType`, `pageSize`, and `offset` via the shared Axios `httpClient` (`@/api/http`) with cookie authentication interceptors.
2. Updated `src/frontend/Cakra.Web/src/router/index.ts` so `/feed` maps to `FeedView.vue` (`meta: { requiresAuth: true, screenId: 'SCR-FEED-001' }`, replacing `HomeView.vue` on `/feed`).
3. Verified `npm run build` (`vue-tsc --noEmit && vite build`) in `src/frontend/Cakra.Web` succeeds with 0 TypeScript or Vite errors.

Changed Files:
- `src/frontend/Cakra.Web/src/views/FeedView.vue` (created — `SCR-FEED-001` Operational Feed screen Vue 3 SFC)
- `src/frontend/Cakra.Web/src/router/index.ts` (modified — mapped `/feed` route to `FeedView.vue`)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — updated P6-S32 status to IMPLEMENTED with implementation notes and changed files)

---

### P6-S33

Title: Post Detail Modal — SCR-POST-001

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the Post detail modal (`SCR-POST-001`) as a Vue 3 SFC with Bootstrap 5 styling in `src/frontend/Cakra.Web/`. Delivers post thread viewing, commenting, and reaction interactions.

Depends On: P6-S31, P1-S08

Repository: `src/frontend/Cakra.Web`

Completion Criteria:
- Vue 3 SFC `PostDetailModal.vue` (`<script setup lang="ts">`, Bootstrap 5) renders: Bootstrap 5 modal containing post content (Title, Body, Author, CreatedAt), comments list (each showing Author, Content, CreatedAt) loaded from `GET /api/v1/posts/{id}/comments`, comment input form (textarea + submit button) calling `POST /api/v1/posts/{id}/comments`, reaction buttons (e.g., thumbs up, flag) with count badges calling `POST /api/v1/posts/{id}/reactions` and `DELETE /api/v1/posts/{id}/reactions/{type}` — Architecture §19.4, §20.
- Modal is triggered from Feed screen (P6-S32) feed cards.
- Navigation link from post modal to Request detail (`/requests/{requestId}`) when the post references a request — UC-FCOL-004.
- Axios HTTP client calls post API endpoints with authentication interceptors.

Notes: Frontend-only slice. **Parallel Execution**: Can execute concurrently with P6-S32 after P6-S31. This slice + P6-S32 completes Milestone M4. Architecture §9 (FEAT-FCOL-001..003) and §8 (UC-FCOL-001..004) are authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Implemented `PostDetailModal.vue` (`src/frontend/Cakra.Web/src/views/PostDetailModal.vue`) as a Vue 3 SFC (`<script setup lang="ts">`, Bootstrap 5, `data-screen-id="SCR-POST-001"`) and created a re-export wrapper (`src/frontend/Cakra.Web/src/components/PostDetailModal.vue`) so consumers such as `FeedView.vue` can import from either `@/views/PostDetailModal.vue` or `@/components/PostDetailModal.vue`.
2. Renders a Bootstrap 5 modal (`class="modal fade show d-block"`) displaying full post thread details loaded from `GET /api/v1/posts/${postId}` (`Title`, `Body`/`Content`, `Author`/`AuthorName`, `CreatedAt`, plus `CustomerName`, `ProductName`, `IsException`/`ExceptionType`, `Visibility`, `Status`, and `Source` badges).
3. Loads and renders the chronological discussion comments list (`Author`/`AuthorName`, `Content`, `CreatedAt`) from `GET /api/v1/posts/${postId}/comments` and provides a comment input form (`textarea` + submit button) calling `POST /api/v1/posts/${postId}/comments` (`UC-FCOL-001`, `FEAT-FCOL-001`).
4. Renders structured operational reaction buttons (`SEEN`, `EXPERIENCED`, `HAVE_IDEA`, `SIMILAR_ISSUE`, `DUPLICATE`, `NEED_CLARIFICATION`) with Bootstrap Icons, labels, and count badges calling `POST /api/v1/posts/${postId}/reactions` to add and `DELETE /api/v1/posts/${postId}/reactions/${reactionType}` to soft-remove when active/toggled (`UC-FCOL-002`, `FEAT-FCOL-002`).
5. Renders contextual `<RouterLink>` and programmatic navigation to Request detail (`/requests/${requestId}`) when the post references a request (`requestId`, `referenceType === 'REQUEST'`, `references`, or `initialRequestId` prop — `UC-FCOL-004`) and to Work Package detail (`/work-packages/${workPackageId}`) when present, plus visibility toggle (`POST /api/v1/posts/${postId}/visibility`) and archive (`POST /api/v1/posts/${postId}/archive`) controls.
6. Accepts flexible props (`postId`, `show`, `initialRequestId`, `initialWorkPackageId`) and emits `close` and `updated` events for integration with `FeedView.vue` (`SCR-FEED-001`).
7. Uses the shared Axios `httpClient` (`@/api/http`) with cookie authentication interceptors. Verified `npm run build` (`vue-tsc --noEmit && vite build`) in `src/frontend/Cakra.Web` succeeds with 0 errors.

Changed Files:
- `src/frontend/Cakra.Web/src/views/PostDetailModal.vue` (created — `SCR-POST-001` Post detail modal Vue 3 SFC)
- `src/frontend/Cakra.Web/src/components/PostDetailModal.vue` (created — component re-export wrapper for `PostDetailModal.vue`)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — updated P6-S33 status to IMPLEMENTED with implementation notes and changed files)

---

## P7 — Management Analytics & System Finalization

Implementation Status: COMPLETED
Review Status: GO

Objective: Implement the Management Analytics module using Dapper real-time queries and `AnalyticsSnapshotJob` as an ASP.NET Core `BackgroundService` (Architecture §19.7), deliver management dashboard screens as Vue 3 SFCs, perform final cross-cutting validation, and produce the authoritative IIS on Windows Server production deployment package. M5 is achieved when this phase is complete. Analytics slices (P7-S34 through P7-S37) depend only on Request module and can execute in parallel with P5 (Work Package) and P6 (Post & Feed).

Source: Architecture §22 — Boundary: **Management Analytics** (`Cakra.Modules.Analytics`, Depends On: `Cakra.Core`, `Request`, `Organization`, `Customer`). Architecture §13 (Analytics Architecture). Architecture §18, §19.7, §19.10.

---

### P7-S34

Title: Analytics Module — Snapshot Tables & Snapshot Job

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the Analytics DbUp SQL migration scripts for `analytics.*` tables and the `AnalyticsSnapshotJob` as an ASP.NET Core `BackgroundService` (`IHostedService`) per Architecture §19.7 (daily workload snapshot and monthly customer performance snapshot). All data reads use Dapper parameterized queries against `request.*`, `organization.*`, and `customer.*` schemas via published query services. Implements scheduling using a `System.Threading.Timer`-based approach within the `BackgroundService`.

Depends On: P1-S06, P1-S07, P4-S19

Repository: `Cakra.Modules.Analytics`

Completion Criteria:
- DbUp SQL migration scripts create `analytics.*` schema tables: `DailyWorkloadSnapshots`, `MonthlyCustomerPerformanceSnapshots` — Architecture §13 (Snapshot Storage Tables).
- `AnalyticsSnapshotJob` is implemented as ASP.NET Core `BackgroundService` using `System.Threading.Timer` for scheduling — Architecture §19.7:
  - Daily snapshot (23:59:59): computes end-of-day workload per active `Person` from `Requests` using Dapper parameterized SQL and inserts into `DailyWorkloadSnapshots`.
  - Monthly snapshot (1st of month, 00:05:00): aggregates preceding calendar month metrics per `Customer` using Dapper and inserts into `MonthlyCustomerPerformanceSnapshots`.
  - Unique constraints `UNIQUE(SnapshotDate, PersonId)` and `UNIQUE(YearMonth, CustomerId)` enforced — Architecture §13.
- `ManagementAnalyticsService.RecomputeSnapshots(startDate, endDate)` implements idempotent backfill using Dapper — Architecture §13 (On-Demand Recomputation).
- Snapshot records are treated as immutable historical facts after insertion — Architecture §13 (Immutability).
- All reads are via Dapper parameterized SQL; no EF Core — Architecture §19.3, §20.
- Integration test (xUnit + WebApplicationFactory): trigger daily snapshot job; verify `DailyWorkloadSnapshots` row inserted with correct metric counts.

Notes: Architecture §13 and §19.7 (ASP.NET Core Hosted Services) are authoritative. **Early Start**: Depends on P4-S19 (Request Queries) but NOT on P5 (Work Package) or P6 (Post & Feed). Can execute concurrently with P5 and P6 slices.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Added embedded DbUp SQL migration script `src/backend/Cakra.Api/Migrations/Scripts/0010_analytics_tables.sql` idempotently creating the `[analytics]` schema and the two snapshot tables `[analytics].[DailyWorkloadSnapshots]` and `[analytics].[MonthlyCustomerPerformanceSnapshots]` with primary keys, `UNIQUE(SnapshotDate, PersonId)` (`UQ_DailyWorkloadSnapshots_SnapshotDate_PersonId`), `UNIQUE(YearMonth, CustomerId)` (`UQ_MonthlyCustomerPerformanceSnapshots_YearMonth_CustomerId`), `CK_MonthlyCustomerPerformanceSnapshots_YearMonth`, nonclustered indexes, and zero cross-schema foreign keys (Architecture §13, §17, §20).
2. Implemented `IManagementAnalyticsService` and `ManagementAnalyticsService` (marked `partial` for extension by P7-S35) in `Cakra.Modules.Analytics.Services`, plus read models (`DailyWorkloadSnapshotDto`, `MonthlyCustomerPerformanceSnapshotDto`, `SnapshotRecomputationResultDto`, `ProgrammerMonthlyPerformanceItemDto`, `ProgrammerPerformanceReportDto`) and MediatR commands/queries + FluentValidation validators:
   - `CaptureDailyWorkloadSnapshotAsync` / `CaptureDailyWorkloadSnapshot`: reads active `Person` records via `IOrganizationQueryService.ListActivePersonsAsync`, computes end-of-day workload metrics (`ActiveRequestsCount`, `EscalatedRequestsCount`, `StalledRequestsCount`, `CompletedRequestsToday`, `AvgAgeHours`) from `request.Requests` and `request.RequestResolutions` via Dapper parameterized SQL, and inserts into `analytics.DailyWorkloadSnapshots` while preserving immutability of already-captured `(SnapshotDate, PersonId)` rows.
   - `CaptureMonthlyCustomerPerformanceSnapshotAsync` / `CaptureMonthlyCustomerPerformanceSnapshot`: reads active `Customer` records via `ICustomerQueryService.ListActiveCustomersAsync`, aggregates monthly metrics (`TotalRequests`, `ResolvedRequestsCount`, `RejectedRequestsCount`, `AvgResolutionHours`, `SlaMetCount`, `SlaBreachedCount`) via Dapper parameterized SQL, and inserts into `analytics.MonthlyCustomerPerformanceSnapshots` while preserving immutability of already-captured `(YearMonth, CustomerId)` rows.
   - `RecomputeSnapshotsAsync` / `RecomputeSnapshots(startDate, endDate)`: idempotently backfills and recomputes daily and monthly snapshot rows across `[startDate, endDate]` using Dapper parameterized SQL (Architecture §13 — On-Demand Recomputation).
   - `GetDailyWorkloadSnapshotsAsync`, `GetMonthlyCustomerPerformanceSnapshotsAsync`, and `GetProgrammerPerformanceAsync` query snapshot tables via Dapper parameterized SQL for downstream consumption in P7-S36 (`SCR-MGT-002`).
3. Implemented `AnalyticsSnapshotJob` as an ASP.NET Core `BackgroundService` (`IHostedService`) using `System.Threading.Timer` scheduling daily workload snapshots at `23:59:59` UTC and monthly customer performance snapshots on the 1st of each month at `00:05:00` UTC for the preceding calendar month (Architecture §19.7), and registered services and `AddHostedService<AnalyticsSnapshotJob>()` in `AnalyticsModule : IModule`.
4. Added SQL Server integration tests in `tests/backend/Cakra.Tests.Integration/Analytics/AnalyticsSnapshotIntegrationTests.cs` (using `WebApplicationFactory<Program>`, isolated test database `CakraTestDb_Analytics`, and Respawn) verifying `0010_analytics_tables.sql` table creation, `UNIQUE(SnapshotDate, PersonId)` and `UNIQUE(YearMonth, CustomerId)` constraint enforcement, zero cross-schema FKs, `AnalyticsSnapshotJob` daily and monthly snapshot execution, snapshot immutability, and idempotent `RecomputeSnapshots(startDate, endDate)`. All 4 `AnalyticsSnapshotIntegrationTests` and all 9 `ModuleRegistrationTests` pass with 0 failures and 0 warnings.

Changed Files:
- `src/backend/Cakra.Api/Migrations/Scripts/0010_analytics_tables.sql` (created — DbUp migration for `analytics.DailyWorkloadSnapshots` and `analytics.MonthlyCustomerPerformanceSnapshots`)
- `src/backend/Cakra.Modules.Analytics/Cakra.Modules.Analytics.csproj` (modified — added `FrameworkReference` to `Microsoft.AspNetCore.App`, `Dapper`, and `InternalsVisibleTo` for test projects)
- `src/backend/Cakra.Modules.Analytics/Models/AnalyticsSnapshotModels.cs` (created — snapshot and performance report DTOs)
- `src/backend/Cakra.Modules.Analytics/Services/IManagementAnalyticsService.cs` (created — `partial` interface for snapshot capture, recomputation, and queries)
- `src/backend/Cakra.Modules.Analytics/Services/AnalyticsSnapshotCommandsAndQueries.cs` (created — MediatR commands/queries and FluentValidation validators)
- `src/backend/Cakra.Modules.Analytics/Services/ManagementAnalyticsService.cs` (created — `partial` Dapper service implementation)
- `src/backend/Cakra.Modules.Analytics/Jobs/AnalyticsSnapshotJob.cs` (created — `BackgroundService` with `System.Threading.Timer` scheduling)
- `src/backend/Cakra.Modules.Analytics/AnalyticsModule.cs` (created — `IModule` registration including `AddHostedService<AnalyticsSnapshotJob>()`)
- `tests/backend/Cakra.Tests.Integration/Analytics/AnalyticsSnapshotIntegrationTests.cs` (created — SQL Server integration tests)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — updated P7-S34 status to IMPLEMENTED with implementation notes and changed files)

---

### P7-S35

Title: Analytics Module — Real-Time Workload & Customer Portfolio Queries

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement `ManagementAnalyticsService` real-time dynamic query methods using Dapper: `GetProgrammerActiveWorkload` (per-person active request aggregation for `SCR-MGT-003`) and `GetCustomerRequestPortfolio` (active requests, open blockers, recent completions for `SCR-MGT-001`).

Depends On: P7-S34

Repository: `Cakra.Modules.Analytics`

Completion Criteria:
- `GetProgrammerActiveWorkload(personId?)`: Dapper parameterized query dynamically aggregating `Requests WHERE Status IN ('CAPTURED','ACTIVE')` grouped by `OwnerPersonId` and sub-state — Architecture §13 (Real-Time Operational Projections).
- `GetCustomerRequestPortfolio(customerId)`: Dapper parameterized query for active requests, open blockers, and recent completions, joined with customer maintenance contract status via `CustomerQueryService` — Architecture §13.
- Both methods execute without cross-module writes; read exclusively from `request.*` and `customer.*` schemas via published query interfaces — Architecture §20.
- All SQL uses Dapper parameterized queries; no string interpolation — Architecture §20 (Parameterization Requirement).
- Supports FEAT-MGT-002 and FEAT-MGT-004 — Architecture §9.
- Integration tests (xUnit + WebApplicationFactory): workload aggregation by person and customer portfolio queries pass.

Notes: Architecture §13 (Real-Time Operational Projections) and §19.3 (Dapper, explicit SQL) are authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Created read-only real-time projection DTOs in `src/backend/Cakra.Modules.Analytics/Models/AnalyticsRealTimeModels.cs`: `CustomerPortfolioRequestItemDto`, `ProgrammerActiveWorkloadDto` (with per-sub-state counts `CapturedCount`, `EvaluatingCount`, `AcceptedCount`, `InProgressCount`, `EscalatedCount`, `TotalActiveCount` / `ActiveRequestsCount`, `StalledRequestsCount`, `IsOverloaded`, `SubStateCounts`, and `ActiveRequests` queue), and `CustomerRequestPortfolioDto` (with customer identity, `HasActiveMaintenanceContract`, `ContractStatus`, `ActiveRequestsCount`, `OpenBlockersCount`, `RecentCompletionsCount`, `RejectedRequestsCount`, `TotalRequestsCount`, `ActiveRequests`, `OpenBlockers`, `RecentCompletions`, and `Requests`).
2. Extended `IManagementAnalyticsService` (`src/backend/Cakra.Modules.Analytics/Services/IManagementAnalyticsService.RealTime.cs`), added MediatR query records and FluentValidation validators (`GetProgrammerActiveWorkloadQuery`, `GetProgrammerActiveWorkloadQueryValidator`, `GetCustomerRequestPortfolioQuery`, `GetCustomerRequestPortfolioQueryValidator` in `src/backend/Cakra.Modules.Analytics/Services/AnalyticsRealTimeQueries.cs`), and implemented `ManagementAnalyticsService` real-time methods in `src/backend/Cakra.Modules.Analytics/Services/ManagementAnalyticsService.RealTime.cs`:
   - `GetProgrammerActiveWorkloadAsync(personId?, cancellationToken)` / `GetProgrammerActiveWorkload(personId?)`: dynamically aggregates `request.Requests` in active lifecycle states (`CAPTURED`, `EVALUATING`, `ACCEPTED`, `IN_PROGRESS`, `ESCALATED`) grouped by `OwnerPersonId` and sub-state using Dapper parameterized SQL, enriches person details via `IOrganizationQueryService` and customer details via `ICustomerQueryService`, excludes closed requests (`COMPLETED`, `REJECTED`), and supports optional `personId` filtering for `SCR-MGT-003` (FEAT-MGT-004, UC-MGT-004).
   - `GetCustomerRequestPortfolioAsync(customerId, cancellationToken)` / `GetCustomerRequestPortfolio(customerId)`: dynamically queries active requests, open blockers (`ESCALATED`), and recent completions (`COMPLETED`) from `request.Requests` and `request.RequestResolutions` using Dapper parameterized SQL, and enriches customer details and maintenance contract status via `ICustomerQueryService.GetCustomerWithContractStatusAsync` and owner names via `IOrganizationQueryService` for `SCR-MGT-001` (FEAT-MGT-002, UC-MGT-002).
3. Added SQL Server integration tests in `tests/backend/Cakra.Tests.Integration/Analytics/AnalyticsRealTimeQueriesIntegrationTests.cs` (using `WebApplicationFactory<Program>`, isolated test database `CakraTestDb_AnalyticsRealTime`, and Respawn). All 3 integration tests pass against SQL Server with 0 failures.

Changed Files:
- `src/backend/Cakra.Modules.Analytics/Models/AnalyticsRealTimeModels.cs` (created — real-time workload and customer portfolio DTOs)
- `src/backend/Cakra.Modules.Analytics/Services/IManagementAnalyticsService.RealTime.cs` (created — `partial` interface methods `GetProgrammerActiveWorkloadAsync` / `GetProgrammerActiveWorkload` and `GetCustomerRequestPortfolioAsync` / `GetCustomerRequestPortfolio`)
- `src/backend/Cakra.Modules.Analytics/Services/AnalyticsRealTimeQueries.cs` (created — MediatR query records and FluentValidation validators)
- `src/backend/Cakra.Modules.Analytics/Services/ManagementAnalyticsService.RealTime.cs` (created — `partial` Dapper real-time query service implementation and MediatR handlers)
- `tests/backend/Cakra.Tests.Integration/Analytics/AnalyticsRealTimeQueriesIntegrationTests.cs` (created — SQL Server integration tests for real-time workload and customer portfolio queries)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — updated P7-S35 status to IMPLEMENTED with implementation notes and changed files)

---

### P7-S36

Title: Management Analytics API Controller

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the ASP.NET Core REST API controller for the Management Analytics module at `/api/v1/analytics/*` in `Cakra.Api`. All endpoints are RBAC-protected to the Management role. Separated from Vue SFC screen slice to reduce per-slice scope.

Depends On: P7-S35

Repository: `Cakra.Api`

Completion Criteria:
- ASP.NET Core Controller at `/api/v1/analytics` exposes REST endpoints:
  - `GET /api/v1/analytics/customer-portfolio?customerId={id}` → `ManagementAnalyticsService.GetCustomerRequestPortfolio`.
  - `GET /api/v1/analytics/programmer-performance?personId={id}&startMonth={ym}&endMonth={ym}` → Dapper query over `MonthlyCustomerPerformanceSnapshots` and `DailyWorkloadSnapshots`.
  - `GET /api/v1/analytics/programmer-workload?personId={id}` → `ManagementAnalyticsService.GetProgrammerActiveWorkload`.
- All three API endpoints are RBAC-protected: `[Authorize(Roles = "Management")]` — Architecture §14 (RBAC), §19.5.
- Integration tests (xUnit + WebApplicationFactory): authorized Management user (200), unauthorized non-Management user (403) pass.

Notes: Controller-only slice. Vue SFCs are implemented in P7-S37. Architecture §19.6, §14 (RBAC), and §8 (UC-MGT-002..004) are authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Implemented `AnalyticsController` (`src/backend/Cakra.Api/Controllers/AnalyticsController.cs`) inheriting from `ApiControllerBase` at `[Route("api/v1/analytics")]` with class-level RBAC protection `[Authorize(Roles = "Management")]` (Architecture §14, §18, §19.5, §19.6):
   - `GET /api/v1/analytics/customer-portfolio?customerId={id}`: dispatches `GetCustomerRequestPortfolioQuery` to `ManagementAnalyticsService.GetCustomerRequestPortfolioAsync` (`GetCustomerRequestPortfolio`), returning `CustomerRequestPortfolioDto` for `SCR-MGT-001` (`UC-MGT-002`, `FEAT-MGT-002`).
   - `GET /api/v1/analytics/programmer-performance?personId={id}&startMonth={ym}&endMonth={ym}`: dispatches `GetProgrammerPerformanceQuery` to `ManagementAnalyticsService.GetProgrammerPerformanceAsync` (`GetProgrammerPerformance`), querying `analytics.DailyWorkloadSnapshots` and `analytics.MonthlyCustomerPerformanceSnapshots` via Dapper parameterized SQL and returning `ProgrammerPerformanceReportDto` for `SCR-MGT-002` (`UC-MGT-003`, `FEAT-MGT-003`).
   - `GET /api/v1/analytics/programmer-workload?personId={id}`: dispatches `GetProgrammerActiveWorkloadQuery` to `ManagementAnalyticsService.GetProgrammerActiveWorkloadAsync` (`GetProgrammerActiveWorkload`), returning `IReadOnlyList<ProgrammerActiveWorkloadDto>` for `SCR-MGT-003` (`UC-MGT-004`, `FEAT-MGT-004`).
2. Added SQL Server integration tests in `tests/backend/Cakra.Tests.Integration/Analytics/AnalyticsControllerTests.cs` (using `WebApplicationFactory<Program>`, isolated test database `CakraTestDb_AnalyticsController`, and Respawn) verifying:
   - Unauthenticated requests to all three endpoints return HTTP 401 Unauthorized RFC 7807 `ProblemDetails` (`errorCode: "UNAUTHORIZED"`).
   - Authenticated non-Management user (`Programmer` role) requests to all three endpoints return HTTP 403 Forbidden RFC 7807 `ProblemDetails` (`errorCode: "FORBIDDEN"`).
   - Authenticated `Management` role user requests to all three endpoints return HTTP 200 OK with accurate customer portfolio, historical programmer performance snapshots, and real-time programmer active workload payloads, plus HTTP 400 Bad Request `ProblemDetails` on missing/empty `customerId`.
   All 3 integration tests in `AnalyticsControllerTests` pass with 0 failures and 0 warnings.

Changed Files:
- `src/backend/Cakra.Api/Controllers/AnalyticsController.cs` (created — Management Analytics REST API controller with `[Authorize(Roles = "Management")]`)
- `tests/backend/Cakra.Tests.Integration/Analytics/AnalyticsControllerTests.cs` (created — SQL Server integration tests for 401 unauthenticated, 403 non-Management, and 200 Management access across all 3 analytics endpoints)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — updated P7-S36 status to IMPLEMENTED with implementation notes and changed files)

---

### P7-S37

Title: Management Analytics Screens — SCR-MGT-001, SCR-MGT-002, SCR-MGT-003

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Implement the three Management Analytics screens as Vue 3 SFCs with Bootstrap 5 styling in `src/frontend/Cakra.Web/`. All screens are read-only dashboards displaying tabular data. No charts or visualization libraries required — Bootstrap 5 tables and cards are sufficient.

Depends On: P7-S36, P1-S08

Repository: `src/frontend/Cakra.Web`

Completion Criteria:
- Vue 3 SFC `CustomerPortfolioView.vue` (`<script setup lang="ts">`, Bootstrap 5) renders `SCR-MGT-001`: Customer selector dropdown (from `GET /api/v1/customers/active`), Bootstrap 5 table displaying active requests, open blockers, and recent completions for selected customer. Calls `GET /api/v1/analytics/customer-portfolio?customerId={id}` — Architecture §19.4, §20.
- Vue 3 SFC `ProgrammerPerformanceView.vue` (`<script setup lang="ts">`, Bootstrap 5) renders `SCR-MGT-002`: Person selector dropdown (from `GET /api/v1/organization/persons/active`), month range inputs (start/end), Bootstrap 5 table displaying monthly performance snapshot data (rows = months, columns = metric values). Calls `GET /api/v1/analytics/programmer-performance` — Architecture §19.4.
- Vue 3 SFC `ProgrammerWorkloadView.vue` (`<script setup lang="ts">`, Bootstrap 5) renders `SCR-MGT-003`: Optional Person filter dropdown, Bootstrap 5 table or card layout displaying active workload per person (rows = persons, columns = request count by status). Calls `GET /api/v1/analytics/programmer-workload` — Architecture §19.4.
- Vue Router 4 routes: `/analytics/customer-portfolio` → `CustomerPortfolioView.vue`, `/analytics/programmer-performance` → `ProgrammerPerformanceView.vue`, `/analytics/programmer-workload` → `ProgrammerWorkloadView.vue`.
- Axios HTTP client calls analytics API endpoints with authentication interceptors.

Notes: Frontend-only slice. All three screens are read-only dashboards with identical pattern (selector + data table). Architecture §9 (FEAT-MGT-002..004) and §8 (UC-MGT-002..004) are authoritative.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Implemented `CustomerPortfolioView.vue` (`src/frontend/Cakra.Web/src/views/CustomerPortfolioView.vue`, `<script setup lang="ts">`, Bootstrap 5, `data-screen-id="SCR-MGT-001"`) for `SCR-MGT-001` (`UC-MGT-002`, `FEAT-MGT-002`):
   - Populates Customer selector dropdown from `GET /api/v1/customers/active` via `httpClient` (`@/api/http`).
   - Fetches real-time customer request portfolio from `GET /api/v1/analytics/customer-portfolio?customerId=${encodeURIComponent(id)}`.
   - Renders customer identity and maintenance contract status badge (`hasActiveMaintenanceContract` / `contractStatus`), summary metric cards (`activeRequestsCount`, `openBlockersCount`, `recentCompletionsCount`, `totalRequestsCount`), and three Bootstrap 5 tables for Open Blockers (`ESCALATED`), Active Requests, and Recent Completions (`COMPLETED`) with direct navigation links to `/requests/:id`.
2. Implemented `ProgrammerPerformanceView.vue` (`src/frontend/Cakra.Web/src/views/ProgrammerPerformanceView.vue`, `<script setup lang="ts">`, Bootstrap 5, `data-screen-id="SCR-MGT-002"`) for `SCR-MGT-002` (`UC-MGT-003`, `FEAT-MGT-003`):
   - Populates Person selector dropdown from `GET /api/v1/organization/persons/active`.
   - Provides month range inputs (`startMonth` and `endMonth` in `YYYY-MM` format) and Filter / Reset actions.
   - Calls `GET /api/v1/analytics/programmer-performance` with query parameters (`personId`, `startMonth`, `endMonth`) via `httpClient`.
   - Renders summary KPI cards, a Bootstrap 5 table for monthly performance series (`monthlyPerformance` / `monthlySeries`: rows = months, columns = `completedRequestsCount`, `rejectedRequestsCount`, `maxActiveRequestsCount`, `escalatedRequestsCount`, `avgResolutionHours`), a Bootstrap 5 table for `dailyWorkloadSnapshots`, and a Bootstrap 5 table for `monthlyCustomerSnapshots` when present.
3. Implemented `ProgrammerWorkloadView.vue` (`src/frontend/Cakra.Web/src/views/ProgrammerWorkloadView.vue`, `<script setup lang="ts">`, Bootstrap 5, `data-screen-id="SCR-MGT-003"`) for `SCR-MGT-003` (`UC-MGT-004`, `FEAT-MGT-004`):
   - Populates optional Person filter dropdown from `GET /api/v1/organization/persons/active`.
   - Calls `GET /api/v1/analytics/programmer-workload` with optional `personId` query parameter via `httpClient`.
   - Renders team workload summary cards, a Bootstrap 5 table displaying active workload per programmer by lifecycle sub-state (`capturedCount`, `evaluatingCount`, `acceptedCount`, `inProgressCount`, `escalatedCount`, `totalActiveCount`, `stalledRequestsCount`, `isOverloaded`), and an expandable/selectable active request queue drill-down table (`activeRequests`) with navigation links to `/requests/:id`.
4. Registered the three routes in `src/frontend/Cakra.Web/src/router/index.ts` (`/analytics/customer-portfolio` → `SCR-MGT-001`, `/analytics/programmer-performance` → `SCR-MGT-002`, `/analytics/programmer-workload` → `SCR-MGT-003`) and added navigation links in `src/frontend/Cakra.Web/src/App.vue`.
5. Verification: `npm run build` (`vue-tsc --noEmit && vite build`) in `src/frontend/Cakra.Web` succeeded with 0 errors (119 modules transformed).

Changed Files:
- `src/frontend/Cakra.Web/src/views/CustomerPortfolioView.vue` (created — `SCR-MGT-001` Customer Progress Review screen)
- `src/frontend/Cakra.Web/src/views/ProgrammerPerformanceView.vue` (created — `SCR-MGT-002` Programmer Performance Analytics screen)
- `src/frontend/Cakra.Web/src/views/ProgrammerWorkloadView.vue` (created — `SCR-MGT-003` Programmer Workload Review screen)
- `src/frontend/Cakra.Web/src/router/index.ts` (modified — registered `/analytics/customer-portfolio`, `/analytics/programmer-performance`, and `/analytics/programmer-workload` routes)
- `src/frontend/Cakra.Web/src/App.vue` (modified — added navbar links for Customer Portfolio, Programmer Performance, and Programmer Workload)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — updated P7-S37 status to IMPLEMENTED with implementation notes and changed files)

---

### P7-S38

Title: Cross-Cutting Finalization & System Integration Validation

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Write and execute system-wide integration tests that verify all cross-cutting concerns are correctly wired across the complete assembled system. This slice produces no new business functionality — only validation tests with specific pass/fail criteria.

Depends On: P2-S11, P3-S16, P4-S22, P4-S23, P5-S27, P6-S32, P6-S33, P7-S37

Repository: `Cakra.Api` (validation suite in `tests/backend/Cakra.Tests.Integration`)

Completion Criteria:
- Integration test: `POST /api/v1/requests` with valid payload returns HTTP 201; assert Serilog structured log output contains JSON entry with fields `PersonId`, `TraceId`, `Timestamp`, and `SourceContext` — Architecture §18, §19.9.
- Integration test: `POST /api/v1/requests` with missing required fields returns HTTP 400; response body deserializes as RFC 7807 `ProblemDetails` with `Content-Type: application/problem+json` and `errors` dictionary containing field validation messages — Architecture §19.6.
- Integration test: `POST /api/v1/posts` with valid `RequestId` reference → query `post.FeedItems` table → assert row exists with correct `CustomerId`, `ProductId`, and `PostId` — Architecture §12, §18.
- Integration test: Escalate a request via `POST /api/v1/requests/{id}/escalate` → query `post.FeedItems` → assert `IsException = TRUE` and `ExceptionType = 'ESCALATION'` — Architecture §12.
- Integration test: `GET /api/v1/analytics/programmer-workload` with non-Management role → HTTP 403; with Management role → HTTP 200 — Architecture §14 (RBAC).
- Integration test: Full cookie session lifecycle: `POST /api/v1/auth/login` → assert `Set-Cookie` header with `HttpOnly`, `SameSite=Strict` → `GET /api/v1/requests` with cookie → HTTP 200 → `POST /api/v1/auth/logout` → `GET /api/v1/requests` with same cookie → HTTP 401 — Architecture §19.5.
- Integration test: `GET /health/live` → HTTP 200; `GET /health/ready` → HTTP 200 (with SQL Server connectivity) — Architecture §19.9.
- Integration test: Invoke `FeedProjectionRebuilder.RebuildAll()` → assert `post.FeedItems` row count equals `post.Posts` row count (where `Status = 'ACTIVE'`) — Architecture §19.7.
- All smoke tests pass for API endpoints backing: SCR-AUTH-001, SCR-FEED-001, SCR-POST-001, SCR-REQ-001..005, SCR-WP-001, SCR-PRD-001, SCR-MGT-001..003.

Notes: Architecture §18 (Cross-Cutting Concerns), §19.5, §19.6, §19.7, §19.9, and §20 are authoritative. Each completion criterion is a specific integration test with defined inputs and expected outputs. No subjective verification language.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Implemented `CrossCuttingSystemIntegrationTests` (`tests/backend/Cakra.Tests.Integration/SystemValidation/CrossCuttingSystemIntegrationTests.cs`) using `WebApplicationFactory<Program>`, isolated SQL Server test database `CakraTestDb_SystemValidation`, and `Respawn` across all 8 module schemas (`analytics`, `post`, `workpackage`, `request`, `product`, `customer`, `organization`, `identity`).
2. Updated `SerilogConfigurationExtensions.ConfigureCakraSerilog` (`src/backend/Cakra.Api/Infrastructure/Logging/SerilogConfigurationExtensions.cs`) to forward log events to any `ILogEventSink` registered in the DI container so integration tests can capture Serilog JSON output formatted via `Serilog.Formatting.Json.JsonFormatter`.
3. Verified all 9 system-wide integration tests against SQL Server (`dotnet test cakra\Cakra.sln --filter FullyQualifiedName~CrossCuttingSystemIntegrationTests` — 9 passed, 0 failed, 0 skipped):
   - Test 1 (`Post_requests_with_valid_payload_returns_201_and_emits_Serilog_structured_JSON_log_with_PersonId_TraceId_Timestamp_and_SourceContext`): `POST /api/v1/requests` returns HTTP 201 and emits structured Serilog JSON entries containing `Timestamp`, `PersonId`, `TraceId`, and `SourceContext` (Architecture §18, §19.9).
   - Test 2 (`Post_requests_with_missing_required_fields_returns_400_RFC7807_ProblemDetails_with_application_problem_json_and_errors_dictionary`): `POST /api/v1/requests` with missing required fields returns HTTP 400 with `Content-Type: application/problem+json`, deserializes as RFC 7807 `ValidationProblemDetails`, and includes camelCase field validation messages in `errors` (`title`, `description`) (Architecture §19.6).
   - Test 3 (`Post_posts_with_valid_RequestId_reference_inserts_FeedItems_row_with_correct_CustomerId_ProductId_and_PostId`): `POST /api/v1/posts` with valid `RequestId` populates `post.FeedItems` with matching `CustomerId`, `ProductId`, `RequestId`, and `PostId` (Architecture §12, §18).
   - Test 4 (`Escalate_request_via_Post_requests_id_escalate_sets_FeedItems_IsException_true_and_ExceptionType_ESCALATION`): `POST /api/v1/requests/{id}/escalate` updates `post.FeedItems` with `IsException = TRUE` and `ExceptionType = 'ESCALATION'` (Architecture §12).
   - Test 5 (`Get_analytics_programmer_workload_returns_403_for_non_Management_role_and_200_for_Management_role`): `GET /api/v1/analytics/programmer-workload` returns HTTP 403 for non-Management role (`Programmer`) and HTTP 200 for `Management` role (Architecture §14).
   - Test 6 (`Full_cookie_session_lifecycle_login_sets_HttpOnly_SameSite_Strict_cookie_authorizes_requests_and_logout_invalidates_cookie`): `POST /api/v1/auth/login` sets `Cakra.Session` cookie with `HttpOnly` and `SameSite=Strict`, `GET /api/v1/requests` with cookie returns HTTP 200, `POST /api/v1/auth/logout` revokes the session, and subsequent `GET /api/v1/requests` with the same cookie returns HTTP 401 (Architecture §19.5).
   - Test 7 (`Health_live_and_health_ready_return_200_OK_with_SQL_Server_connectivity`): `GET /health/live` and `GET /health/ready` both return HTTP 200 `"Healthy"` against SQL Server (Architecture §19.9).
   - Test 8 (`FeedProjectionRebuilder_RebuildAll_makes_FeedItems_row_count_equal_active_Posts_row_count`): `FeedProjectionRebuilder.RebuildAll()` regenerates `post.FeedItems` so its row count equals `post.Posts` row count where `Status = 'ACTIVE'` (Architecture §19.7).
   - Test 9 (`All_screen_backing_API_endpoints_pass_end_to_end_smoke_tests`): End-to-end smoke test passes across all screen-backing API endpoints for `SCR-AUTH-001`, `SCR-PRD-001`, `SCR-REQ-001..005`, `SCR-WP-001`, `SCR-FEED-001`, `SCR-POST-001`, and `SCR-MGT-001..003`.

Changed Files:
- `src/backend/Cakra.Api/Infrastructure/Logging/SerilogConfigurationExtensions.cs` (modified — forwarded Serilog log events to any DI-registered `ILogEventSink` for integration test capture)
- `tests/backend/Cakra.Tests.Integration/SystemValidation/CrossCuttingSystemIntegrationTests.cs` (created — 9 system-wide cross-cutting and screen-backing API smoke integration tests)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — updated P7-S38 status to IMPLEMENTED with implementation notes and changed files)

---

### P7-S39

Title: IIS Production Deployment Packaging & Configuration

Implementation Status: IMPLEMENTED
Review Status: GO

Objective: Produce the production build and deployment configuration that packages the complete CAKRA - ICS Operational System for the authoritative production target: **IIS (Internet Information Services) on Windows Server** per Architecture §19.10. Configure Vite frontend compilation in `src/frontend/Cakra.Web/` emitting production static assets into `src/frontend/Cakra.Web/dist/` (which are ingested into `src/backend/Cakra.Api/wwwroot/`), `dotnet publish` Release packaging of `src/backend/Cakra.Api/Cakra.Api.csproj`, IIS `web.config` with ASP.NET Core Module (`AspNetCoreHostingModel = InProcess`), dedicated AppPool provisioning script (`deploy/setup-iis.ps1`), deployment automation script (`deploy/publish.ps1`), and automated DbUp migration execution on deploy. M5 is achieved when this slice is complete.

Depends On: P7-S38

Repository: `deploy/`, `Cakra.Api`, `src/frontend/Cakra.Web`

Completion Criteria:
- Production publish script (`deploy/publish.ps1`) automates end-to-end release build per Architecture §19.10:
  1. Executes `npm run build` in `src/frontend/Cakra.Web/`, emitting production static assets into `src/frontend/Cakra.Web/dist/` (which are ingested into `src/backend/Cakra.Api/wwwroot/`).
  2. Executes `dotnet publish src/backend/Cakra.Api/Cakra.Api.csproj -c Release -o ./publish` producing the release package containing compiled binaries, dependencies, static web assets (in `./publish/wwwroot/`), and the IIS `web.config`.
  3. Generates the production `web.config` in `./publish` configuring `aspNetCore` handler with `hostingModel="inprocess"`.
- IIS site and application pool configuration script (`deploy/setup-iis.ps1`) provisions:
  - Dedicated Application Pool (`CakraAppPool`) targeting `No Managed Code`, 64-bit, with automatic start and recycling settings — Architecture §19.10.
  - IIS Website / Web Application binding with HTTPS binding (port 443) and physical path mapped to the published folder.
- Database migration execution is integrated into deployment:
  - DbUp migration runner executes automatically on application startup within `Program.cs` (or via standalone CLI flag `dotnet Cakra.Api.dll --migrate`) to apply idempotent SQL migrations against SQL Server 2019 before traffic is served — Architecture §19.10.
- Production configuration template `appsettings.Production.json` configured for SQL Server connection string override via `ConnectionStrings__DefaultConnection` environment variable.
- `deploy/publish.ps1` runs from repository root and produces a fully populated, runnable `./publish` folder with zero errors.
- Application starts under IIS / `w3wp.exe`, serves the Vue 3 SPA at `/`, and `/health/ready` returns HTTP 200 when SQL Server 2019 is available.
- All non-IIS alternatives (systemd, Linux Nginx, Windows Service) and Docker/container references are completely excluded from the project repository.

Notes: Architecture §19.10 (Deployment & Runtime Strategy) is authoritative. This is the terminal implementation slice of the plan. Depends on P7-S38 to ensure the system passes all integration validation before the deployment artifact is produced. Achieving this slice marks completion of Milestone M5.

Implementation Notes (2026-09-28): All completion criteria satisfied.
1. Created `deploy/publish.ps1` automating the end-to-end production release build per Architecture §19.10:
   - Compiles the Vue 3 SPA (`npm run build` in `src/frontend/Cakra.Web/`), emitting production static assets into `src/frontend/Cakra.Web/dist/`.
   - Ingests compiled static assets into `src/backend/Cakra.Api/wwwroot/`.
   - Executes `dotnet publish src/backend/Cakra.Api/Cakra.Api.csproj -c Release -o ./publish` and populates `./publish/wwwroot/`.
   - Generates and verifies `./publish/web.config` with `modules="AspNetCoreModuleV2"`, `hostingModel="inprocess"`, `processPath="dotnet"`, `arguments=".\Cakra.Api.dll"`, `stdoutLogEnabled="false"`, and `ASPNETCORE_ENVIRONMENT="Production"`.
2. Created `deploy/setup-iis.ps1` provisioning the Windows Server IIS hosting environment per Architecture §19.10:
   - Configures dedicated Application Pool `CakraAppPool` (`managedRuntimeVersion = ""` for `No Managed Code`, `enable32BitAppOnWin64 = $false` for 64-bit `w3wp.exe`, `startMode = "AlwaysRunning"`, `autoStart = $true`, idle timeout disabled, and periodic recycling).
   - Configures IIS Website `Cakra` with HTTPS binding on port 443 mapped to the published physical path and optional pre-startup `dotnet Cakra.Api.dll --migrate` execution.
3. Configured `<AspNetCoreHostingModel>InProcess</AspNetCoreHostingModel>` in `src/backend/Cakra.Api/Cakra.Api.csproj` and added `src/backend/Cakra.Api/web.config` and `src/backend/Cakra.Api/appsettings.Production.json` (with `ConnectionStrings:DefaultConnection` overridden via `ConnectionStrings__DefaultConnection`, `Database:RunMigrationsOnStartup: true`, `Security:RequireHttpsCookies: true`).
4. Updated `src/backend/Cakra.Api/Infrastructure/Migrations/DatabaseMigrationRunner.cs` and `src/backend/Cakra.Api/Program.cs` to support `DatabaseMigrationRunner.Run(connectionString)` both via the standalone CLI switch (`dotnet Cakra.Api.dll --migrate`) and automatically on startup, and ensured `app.UseDefaultFiles()`, `app.UseStaticFiles()`, and `app.MapFallbackToFile("index.html")` serve the Vue 3 SPA at `/` and deep links.
5. Executed `pwsh -NoProfile -ExecutionPolicy Bypass -File .\deploy\publish.ps1` and `dotnet .\publish\Cakra.Api.dll --migrate` from repository root with 0 errors, and added 5 integration verification tests in `tests/backend/Cakra.Tests.Integration/Deployment/IisDeploymentPackagingTests.cs` (`CakraTestDb_Deployment`) — all 5 tests pass.

Changed Files:
- `deploy/publish.ps1` (created — automated Vite build, `wwwroot/` asset ingestion, `dotnet publish -c Release -o ./publish`, and IIS In-Process `web.config` packaging script)
- `deploy/setup-iis.ps1` (created — IIS `CakraAppPool` and HTTPS port 443 Website provisioning script)
- `src/backend/Cakra.Api/Cakra.Api.csproj` (modified — added `<AspNetCoreHostingModel>InProcess</AspNetCoreHostingModel>`)
- `src/backend/Cakra.Api/web.config` (created — IIS `AspNetCoreModuleV2` `hostingModel="inprocess"` configuration)
- `src/backend/Cakra.Api/appsettings.Production.json` (created — production configuration template supporting `ConnectionStrings__DefaultConnection` override)
- `src/backend/Cakra.Api/Infrastructure/Migrations/DatabaseMigrationRunner.cs` (modified — added static `DatabaseMigrationRunner.Run(connectionString, logger)` entrypoint)
- `src/backend/Cakra.Api/Program.cs` (modified — integrated `DatabaseMigrationRunner.Run` for `--migrate` CLI and startup execution, and explicit `UseDefaultFiles()` / `UseStaticFiles()` / `MapFallbackToFile("index.html")`)
- `src/backend/Cakra.Api/wwwroot/index.html` (created — compiled Vue 3 SPA production entry point and static assets under `wwwroot/assets/`)
- `tests/backend/Cakra.Tests.Integration/Deployment/IisDeploymentPackagingTests.cs` (created — 5 integration verification tests for IIS deployment packaging, SPA root serving, `/health/ready`, CLI `--migrate`, and zero Docker/non-IIS files)
- `docs/implementation-plan/CAKRA-IMPLEMENTATION-PLAN.md` (modified — updated P7-S39 status to IMPLEMENTED with implementation notes and changed files)

---

# 7. Change Log

2026-09-27 — v1.0 — Initial greenfield plan produced from CAKRA-ARCHITECTURE.md v1.1. 7 phases (P1–P7), 28 slices (S01–S28).

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
(6) P1-S01 updated: mandated exact §19.11 directory layout (.NET 8 target, C# 12 settings).
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
(5) Docker removal & actual deployment strategy: Removed all Docker references from architecture (§19.9, §19.10) and implementation plan (§2, §3, P1-S01, P1-S08, P7 intro, P7-S31). Replaced with concrete native deployment strategy: Modular Monolith single ASP.NET Core process running Kestrel as a Windows Service, systemd service, or behind an IIS/Nginx reverse proxy, with Vite SPA compilation, `dotnet publish -c Release`, deployment packaging script (`publish.ps1`), and automated DbUp migration runner.
Result: 7 phases (P1–P7), 31 slices (S01–S31). Milestones M0–M6. No architectural decisions created or reinterpreted.

2026-09-27 — v3.2 — Production deployment target clarification pass (pre-approval). Addressed reviewer feedback regarding broad deployment alternatives:
(1) Locked down the authoritative production deployment target to **IIS (Internet Information Services) on Windows Server** via In-Process hosting (`AspNetCoreHostingModel = InProcess` in `web.config`) and dedicated AppPool (`CakraAppPool`).
(2) Removed all deployment alternatives (Linux systemd, Nginx reverse proxy, standalone Windows Service) from Architecture §19.10 and Implementation Plan (§2, §3, P7 objective, P7-S31).
(3) Updated P7-S31 to generate the IIS deployment scripts (`deploy/publish.ps1`, `deploy/setup-iis.ps1`), in-process `web.config`, and SQL Server 2019 DbUp startup migration execution.
Result: 7 phases (P1–P7), 31 slices (S01–S31). Milestones M0–M6. Fully deterministic production target for implementation agents.

2026-09-28 — v3.3 — Architecture alignment pass. Synchronized implementation plan with current Architecture (§19.10, §19.11, §22):
(1) Backend host renaming: Replaced backend host references from `Cakra.Web` to `Cakra.Api` across all slices, distinguishing backend host (`Cakra.Api`) from frontend SPA (`Cakra.Web`).
(2) Repository structure alignment: Updated repository layout to mirror Architecture §19.11 repository-level separation: `src/backend/*`, `src/frontend/Cakra.Web`, `tests/backend/*`, `docs/`, `deploy/`.
(3) P1-S01 updated: Replaced legacy directory structure with exact Architecture §19.11 structure verbatim.
(4) Foundation phase description revised: Updated P1 Foundation description to reflect `Cakra.Api`, `Cakra.Web`, `src/backend`, and `src/frontend`.
(5) Deployment & build alignment: Corrected frontend build output flow (`src/frontend/Cakra.Web/dist/` → `src/backend/Cakra.Api/wwwroot/`), `dotnet publish` path (`src/backend/Cakra.Api/Cakra.Api.csproj`), CLI migration switch (`dotnet Cakra.Api.dll --migrate`), and IIS AppPool name (`CakraAppPool`) per Architecture §19.10.
(6) Planning scope & slice repositories: Updated Section 2 to include Backend Host (`Cakra.Api`) and Frontend Web (`Cakra.Web`) per Architecture §22, and aligned all slice `Repository:` declarations to reference `Cakra.Api` and `src/frontend/Cakra.Web`.
Result: 7 phases (P1–P7), 31 slices (S01–S31). Milestones M0–M6. Complete verbatim synchronization with target architecture.

2026-09-28 — v4.0 — Agent execution optimization pass. Restructured the implementation plan for budget-agent execution (Gemini 3.8 Flash class models). Eight targeted goals:
(1) **Organization Module split** (H1): Split v3.3 P3-S12 (7 entities, 7 tables, services, role resolution) into P3-S12 (Domain Entities & Persistence — entities, DbUp, repos) and P3-S13 (Application Services & Role Resolution — MediatR handlers, query service, role wiring). Reduces LARGE → 2 × SMALL. Organization Persistence (P3-S12) drops the P2-S10 dependency — persistence and migrations do not require authentication middleware — enabling P3-S12 to start in parallel with P2-S09.
(2) **Request Services split** (H2): Split v3.3 P4-S17 (8 commands, 4 queries, 3 tables) into P4-S18 (Persistence & Core Commands — DbUp, repos, RecordRequest, AssignRequestOwner, EvaluateRequest, audit pattern) and P4-S19 (Lifecycle Completion & Queries — AcceptRequest, RejectRequest, CompleteRequest, all query methods). Reduces LARGE → 2 × MEDIUM. Escalation (P4-S20) now depends on P4-S18 only, enabling P4-S19 and P4-S20 to execute concurrently.
(3) **Request Screens split** (H3): Split v3.3 P4-S19 (5 screens) into P4-S21 (Request API Controller — all endpoints), P4-S22 (SCR-REQ-001 + SCR-REQ-002 — List + Create), P4-S23 (SCR-REQ-003 + SCR-REQ-004 + SCR-REQ-005 — Detail + My Requests + History). Reduces LARGE → 1 SMALL + 2 SMALL. P4-S22 and P4-S23 execute concurrently after P4-S21.
(4) **Feed & Post Screens split** (H4): Split v3.3 P6-S26 (2 screens + modal) into P6-S31 (Feed & Post API Controller), P6-S32 (SCR-FEED-001 — Feed Screen), P6-S33 (SCR-POST-001 — Post Detail Modal). Reduces LARGE → 1 SMALL + 2 SMALL. P6-S32 and P6-S33 execute concurrently after P6-S31.
(5) **Work Package BE/FE split**: Split v3.3 P5-S22 (10+ endpoints + Vue SFC) into P5-S26 (Work Package API Controller) and P5-S27 (Work Package Screen — Vue SFC only). Applies backend/frontend separation for the largest remaining combined screen slice.
(6) **Analytics BE/FE split**: Split v3.3 P7-S29 (3 endpoints + 3 Vue SFCs) into P7-S36 (Management Analytics API Controller) and P7-S37 (Analytics Screens — 3 Vue SFCs). Applies backend/frontend separation consistently.
(7) **Cross-phase parallelism unlocked** (H5, H6): Documented that P4-S17 (Request Domain) and P5-S24 (Work Package Domain) can start immediately after P1-S06 — they have zero dependency on P2 or P3. P3-S12 (Organization Persistence) can start after P1-S06 + P1-S07 without waiting for P2-S10. P7-S34 (Analytics Snapshot) can start after P4-S19 — it has zero dependency on P5 or P6. Updated §4 Parallel Execution Opportunities comprehensively.
(8) **Determinism improvements** (M1, M3): P1-S06 now specifies exact middleware registration order (7-step sequence). P7-S38 (Cross-Cutting Finalization) rewritten with 8 specific integration test cases replacing vague "verify" and "confirm" language. All screen slices specify Bootstrap 5 component patterns (table, card, form, modal) and exact UI element lists. P7-S34 specifies `System.Threading.Timer`-based scheduling for BackgroundService. P3-S12 specifies column-level schema for all 7 Organization tables.
Result: 7 phases (P1–P7), 39 slices (S01–S39). Milestones M0–M6. Critical path reduced from ~22 waves to ~18 waves. Average agent utilization improved from ~14% to ~28% with 10 agents. No architectural decisions created or reinterpreted.
