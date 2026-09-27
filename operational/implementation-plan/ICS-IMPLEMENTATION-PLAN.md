---
Title: ICS Operational System Implementation Plan
Code: ICS
Artifact: IMPLEMENTATION-PLAN
Version: 3.0
LastUpdated: 2026-09-27
Status: NOT-STARTED
Execution Approval: PENDING
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
| Organization | `ICS.Modules.Organization` | `ICS.Core` |
| Customer | `ICS.Modules.Customer` | `ICS.Core` |
| Product | `ICS.Modules.Product` | `ICS.Core`, `Organization` (Read) |
| Identity & Access | `ICS.Modules.Identity` | `ICS.Core`, `Organization` (Read) |
| Request | `ICS.Modules.Request` | `ICS.Core`, `Organization`, `Customer`, `Product` |
| Work Package | `ICS.Modules.WorkPackage` | `ICS.Core`, `Organization`, `Customer`, `Product`, `Request` |
| Post & Feed | `ICS.Modules.Post` | `ICS.Core`, Domain Events from all modules |
| Management Analytics | `ICS.Modules.Analytics` | `ICS.Core`, `Request`, `Organization`, `Customer` |

Scope derives directly from Architecture §22 (Implementation Boundaries) and §23 (Implementation Dependency Graph). No feature sub-scope is applied; the plan covers the entire target system. Presentation screens are delivered vertically within each module's phase to enable early feedback rather than accumulating all UI work at the end.

Technology stack is explicitly defined in Architecture §19 and is authoritative for all implementation phases. Key mandates:

- **Backend**: .NET 8, ASP.NET Core 8.0, C# 12, MediatR, FluentValidation, Serilog — Architecture §19.1, §19.2
- **Persistence**: Microsoft SQL Server 2019, Dapper (explicit parameterized SQL), DbUp-SqlServer for migrations. EF Core is **strictly prohibited** — Architecture §19.3, §20
- **Frontend**: Vue 3 (Composition API, `<script setup lang="ts">`), TypeScript, Bootstrap 5, Vite, Pinia, Vue Router 4 — Architecture §19.4
- **Testing**: xUnit, FluentAssertions, `WebApplicationFactory<Program>` (integration), Respawn (test isolation) — Architecture §19.8
- **Logging**: Serilog (structured JSON, enriched with TraceId, UserId, PersonId) — Architecture §19.9
- **Deployment**: Single ASP.NET Core process, Docker multi-stage build (`mcr.microsoft.com/dotnet/sdk:8.0` / `aspnet:8.0`) — Architecture §19.10

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
| **M5 — Full System Operational** | P7-S31 | Management dashboards (`SCR-MGT-001..003`) operational. Analytics snapshot job running. Docker image builds and runs. All cross-cutting concerns verified end-to-end. System ready for UAT |

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

---

# 5. Progress Summary

| Phase | Slices | Implementation Status | Review Status | Progress |
|---|---|---|---|---|
| P1 — Foundation | S01–S08 | NOT-STARTED | NOT-REVIEWED | 0/8 |
| P2 — Core Master Data APIs | S09–S11 | NOT-STARTED | NOT-REVIEWED | 0/3 |
| P3 — Identity, Authentication & Product Catalog | S12–S15 | NOT-STARTED | NOT-REVIEWED | 0/4 |
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

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Create the solution file, project references, and exact directory structure for the modular monolith as specified in Architecture §19.11. Establish `ICS.Core`, all `ICS.Modules.*` project skeletons (empty, buildable), the `ICS.Web` host application project, and the `docker/` directory. All projects target .NET 8. Define inter-project reference graph matching Architecture §22 dependency table.

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
  - `docker/` (placeholder directory for Dockerfile)
- All projects target `net8.0`. C# 12 language version, nullable reference types enabled, implicit usings enabled — Architecture §19.1.
- `ICS.Web` project references all module projects.
- Project reference graph matches Architecture §22 dependency table with no circular references.
- `dotnet build ICS.sln` succeeds on the solution with zero errors.

Notes: Produces no business logic. Its output is the compilable project structure that all subsequent slices build into. The `tests/` directory and `docker/Dockerfile` are created by P1-S07 and P7-S31 respectively; only the `docker/` placeholder directory is created here.

---

### P1-S02

Title: Core Contracts & Base Types

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P1-S03

Title: Database Infrastructure & Migration Framework

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P1-S04

Title: Dependency Injection & Module Registration

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P1-S05

Title: Domain Event Bus Implementation

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P1-S06

Title: Application Pipeline Foundation

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P1-S07

Title: Test Project Infrastructure

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P1-S08

Title: Vue 3 Frontend Project Scaffolding

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

Notes: Can execute in parallel with P1-S02 through P1-S06 (depends only on P1-S01 for project structure). The `client/` SPA is built as part of the Docker multi-stage build defined in P7-S31. Each screen implementation (P3-S14, P3-S15, P4-S19, P5-S22, P6-S26, P7-S29) adds Vue SFC components to this scaffold. Architecture §19.4 (Frontend Stack), §19.5 (Authentication), §19.10 (Deployment), and §20 ("Frontend Component Architecture") are authoritative.

---

## P2 — Core Master Data APIs

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the three foundational master-data modules — Organization, Customer, and Product — including domain, application services, and persistence using Dapper with explicit parameterized SQL (Architecture §19.3). These modules expose published query interfaces consumed by all subsequent modules. Organization and Customer can be implemented in parallel; Product depends on Organization for owner validation.

Source: Architecture §22 — Boundaries: **Organization**, **Customer**, **Product**. Architecture §6 (Module Boundaries), §16 (Data Ownership), §19.3 (Persistence & Data Access).

---

### P2-S09

Title: Organization Module — Domain, Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the complete Organization module: domain entities (`Person`, `Team`, `Role`, `Responsibility`, `TeamMembership`, `RoleAssignment`, `ResponsibilityAssignment`), `OrganizationService` command methods, `OrganizationQueryService` query methods, DbUp SQL migration scripts for `organization.*` tables, and Dapper repository implementations using explicit parameterized SQL.

Depends On: P1-S06, P1-S07

Repository: `ICS.Modules.Organization`

Completion Criteria:
- DbUp SQL migration scripts create `organization.*` schema tables: `Persons`, `Teams`, `Roles`, `Responsibilities`, `TeamMemberships`, `RoleAssignments`, `ResponsibilityAssignments` — Architecture §17.
- All repository operations use Dapper with explicit parameterized SQL against SQL Server; no EF Core — Architecture §19.3, §20.
- MediatR command and query handlers implement: create/update Person, create Team, assign Person to Team, create Role, assign Role to Person, create Responsibility, assign Responsibility to Person, deactivate Person — via `OrganizationService`.
- `OrganizationQueryService` implements: `GetPersonById`, `ListActivePersons`, `GetTeamRoster`, `GetPersonRoles`, `GetPersonResponsibilities` — Architecture §7.
- Domain events emitted: `PersonCreated`, `PersonDeactivated`, `RoleAssigned`, `RoleRevoked`.
- `OrganizationQueryService` is accessible as a published interface to other modules; internal repositories are not exposed — Architecture §20 (Strict Vertical Slice Boundary).
- Repository implementations write exclusively to `organization.*` schema using parameterized SQL.
- Integration tests (xUnit + WebApplicationFactory + Respawn): query service returns accurate results after service commands.

Notes: Architecture §6 (Module Boundaries — Organization), §7, and §16 are authoritative. No tables from other modules are written. Depends on P1-S07 for test infrastructure.

---

### P2-S10

Title: Customer Module — Domain, Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the complete Customer module: domain entities (`Customer`, `CustomerContact`), `CustomerService` command methods, `CustomerQueryService` query methods, DbUp SQL migration scripts for `customer.*` tables, and Dapper repository implementations using explicit parameterized SQL.

Depends On: P1-S06, P1-S07

Repository: `ICS.Modules.Customer`

Completion Criteria:
- DbUp SQL migration scripts create `customer.*` schema tables: `Customers`, `CustomerContacts` — Architecture §17.
- All repository operations use Dapper with explicit parameterized SQL; no EF Core — Architecture §19.3, §20.
- MediatR handlers via `CustomerService` implement: create Customer, update Customer master data, create CustomerContact, update CustomerContact, deactivate Customer.
- `CustomerQueryService` implements: `GetCustomerById`, `ListActiveCustomers`, `GetCustomerContacts`, `GetCustomerWithContractStatus` — Architecture §7.
- `CustomerQueryService` is accessible as a published interface; internal repositories are not exposed.
- Repository implementations write exclusively to `customer.*` schema using parameterized SQL.
- Integration tests (xUnit + WebApplicationFactory + Respawn): query service returns accurate results after service commands.

Notes: Architecture §6 (Module Boundaries — Customer) and §16 are authoritative. Customer module does not own Request or Work Package relationships. Can execute in parallel with P2-S09. Depends on P1-S07 for test infrastructure.

---

### P2-S11

Title: Product Module — Domain, Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the complete Product module: domain entity (`Product`), `ProductService` command methods, `ProductQueryService` query methods, DbUp SQL migration scripts for `product.*` tables, and Dapper repository implementations. Owner validation reads from `OrganizationQueryService`.

Depends On: P1-S06, P1-S07, P2-S09

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

Notes: Architecture §10 (Product Module Architecture) is authoritative. Depends on P2-S09 because `CreateProduct` and `AssignProductOwner` call `OrganizationQueryService` to validate the owner.

---

## P3 — Identity, Authentication & Product Catalog

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Identity & Access module, the authentication middleware pipeline, the Login screen (`SCR-AUTH-001`), and the Product Catalog screen (`SCR-PRD-001`). Authentication uses cookie-based session (Architecture §19.5). Screens are implemented as Vue 3 SFCs served by ASP.NET Core REST API endpoints (Architecture §19.4, §19.6). This phase delivers the first vertical user-facing capability (log in, view and manage products). M1 is achieved when this phase is complete.

Source: Architecture §22 — Boundary: **Identity & Access** (`ICS.Modules.Identity`, Depends On: `ICS.Core`, `Organization` (Read)). Architecture §14 (Identity & Authentication Architecture). Architecture §18, §19.5.

---

### P3-S12

Title: Identity Module — Domain, Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the complete Identity & Access module: `UserAccount` and `UserSession` domain entities, `AuthenticationService` (Login, Logout, ValidateSession), DbUp SQL migration scripts for `identity.*` tables, and Dapper repository implementations. Credential verification uses Argon2id or ASP.NET Core `IPasswordHasher` (PBKDF2/HMAC-SHA512), per Architecture §19.5. Session token stored in `identity.UserSessions`.

Depends On: P1-S06, P1-S07, P2-S09

Repository: `ICS.Modules.Identity`

Completion Criteria:
- DbUp SQL migration scripts create `identity.*` schema tables: `UserAccounts`, `UserSessions` — Architecture §14 (IAM Persistence Tables).
- All repository operations use Dapper with explicit parameterized SQL; no EF Core — Architecture §19.3, §20.
- `AuthenticationService.Login(usernameOrEmail, password, clientInfo)`: verifies password using Argon2id or `IPasswordHasher` (PBKDF2/HMAC-SHA512) — Architecture §19.5; checks `UserAccount.Status == 'ACTIVE'`; checks linked `Person.Status == 'ACTIVE'` via `OrganizationQueryService`; creates `UserSession`; issues `SessionToken` (stored in `identity.UserSessions`).
- `AuthenticationService.Logout(sessionToken)`: marks `UserSession.IsRevoked = TRUE` in `identity.UserSessions`.
- `AuthenticationService.ValidateSession(sessionToken)`: validates token, checks expiry and revocation, returns `SecurityContext` with `UserId` and `PersonId`.
- `AuthorizationService.ResolveRoles(personId)`: fetches active `RoleAssignments` via `OrganizationQueryService`.
- Repository implementations write exclusively to `identity.*` schema using parameterized SQL.
- Integration tests (xUnit + WebApplicationFactory + Respawn): Login success, Login failure (bad credentials), Login failure (inactive Person), ValidateSession with valid token, ValidateSession with expired token, Logout invalidates session.

Notes: Architecture §14 is authoritative for all IAM component specifications. Architecture §19.5 is authoritative for authentication mechanism (Cookie Auth, server-side session, password hashing).

---

### P3-S13

Title: Authentication Middleware & Security Context Pipeline

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the concrete HTTP authentication middleware that intercepts every incoming request, validates the session cookie via `AuthenticationService.ValidateSession`, populates `CurrentContextProvider` with `CurrentUserId`, `CurrentPersonId`, and `CurrentRoles`, and rejects unauthenticated requests with HTTP 401. Uses ASP.NET Core Cookie Authentication (`CookieAuthenticationDefaults.AuthenticationScheme`) with secure, `HttpOnly`, `SameSite=Strict` cookies per Architecture §19.5. Implement the RBAC enforcement mechanism (`[Authorize(Roles = "...")]`) backed by dynamically resolved roles from `organization.RoleAssignments` — Architecture §19.5, §18.

Depends On: P3-S12

Repository: `ICS.Modules.Identity` / `ICS.Web`

Completion Criteria:
- ASP.NET Core Cookie Authentication registered with `HttpOnly = true`, `SameSite = SameSiteMode.Strict`, `Secure = true` — Architecture §19.5.
- Authentication middleware is registered in `ICS.Web` pipeline (fulfils the placeholder established in P1-S06).
- Every request without a valid session cookie returns HTTP 401.
- Every request with a valid cookie calls `AuthenticationService.ValidateSession` and populates `CurrentContextProvider` with `CurrentUserId`, `CurrentPersonId`, and `CurrentRoles` — roles dynamically resolved from `organization.RoleAssignments` via `AuthorizationService` — Architecture §19.5.
- RBAC enforcement mechanism is in place and verified: `[Authorize(Roles = "Management")]` attribute rejects non-Management users with HTTP 403.
- Integration tests (xUnit + WebApplicationFactory): authenticated request (200), unauthenticated request (401), insufficient-role request (403) pass.

Notes: Architecture §18 (Authentication & Security Context, RBAC) and §19.5 are authoritative.

---

### P3-S14

Title: Login Screen — SCR-AUTH-001

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement `SCR-AUTH-001` (Login Screen) as a Vue 3 SFC (`<script setup lang="ts">`) with Bootstrap 5 styling, and the corresponding ASP.NET Core REST API controller (`/api/v1/auth/login`, `/api/v1/auth/logout`). Wires `AuthenticationService.Login` and `Logout`, sets the session cookie on success, handles login failures with distinct error messages, and redirects to `SCR-FEED-001` on success.

Depends On: P3-S13, P1-S08

Repository: `ICS.Web`, `ICS.Web/client/`

Completion Criteria:
- Vue 3 SFC `LoginView.vue` (`<script setup lang="ts">`, Bootstrap 5) renders username/password input fields — Architecture §19.4, §20.
- Vue Router 4 route `/login` maps to `LoginView.vue`.
- POST `/api/v1/auth/login` ASP.NET Core Controller endpoint calls `AuthenticationService.Login`; sets secure `HttpOnly` `SameSite=Strict` session cookie on success — Architecture §19.5.
- Login failure returns distinct RFC 7807 `ProblemDetails` responses: invalid credentials, account locked, inactive person — Architecture §19.6.
- Successful login redirects Vue Router to `SCR-FEED-001` route.
- POST `/api/v1/auth/logout` endpoint calls `AuthenticationService.Logout` and clears the session cookie.
- Integration tests (xUnit + WebApplicationFactory): login success and each login failure scenario pass.

Notes: Architecture §14 (UI Boundary — SCR-AUTH-001), §19.4, §19.5, §19.6 are authoritative.

---

### P3-S15

Title: Product Catalog Screen — SCR-PRD-001

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement `SCR-PRD-001` (Product Catalog Screen) as a Vue 3 SFC with Bootstrap 5 styling, and the corresponding ASP.NET Core REST API controller (`/api/v1/products/*`). Wires `ProductService` commands and `ProductQueryService` queries. Delivers the first content management screen; users can view the product catalog, create products, update attributes, assign product owners, and toggle product status.

Depends On: P3-S13, P1-S08, P2-S11

Repository: `ICS.Web`, `ICS.Web/client/`

Completion Criteria:
- Vue 3 SFC `ProductCatalogView.vue` (`<script setup lang="ts">`, Bootstrap 5) renders product catalog — Architecture §19.4.
- ASP.NET Core Controller at `/api/v1/products` exposes REST endpoints for: list all products (`ListAllProducts`), list active products (`ListActiveProducts`), get product detail (`GetProductById`), create product (`CreateProduct`), update product (`UpdateProduct`), assign product owner (`AssignProductOwner`), activate/deactivate product.
- Product owner selector uses `OrganizationQueryService.ListActivePersons` for dropdown — Architecture §10.
- All endpoints protected by `[Authorize]` (authentication middleware from P3-S13).
- Axios HTTP client in Vue component calls `/api/v1/products` endpoints with authentication interceptors — Architecture §19.4.
- Integration tests (xUnit + WebApplicationFactory): catalog listing, product creation, and owner assignment pass.

Notes: Architecture §10 and §9 (FEAT-PRD-001) are authoritative.

---

## P4 — Request Lifecycle

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Request module — the core operational transactional engine — including domain model, application services using Dapper, escalation and management commands, and all Request screens as Vue 3 SFCs. Delivers complete operational request management. M2 is achieved when this phase is complete.

Source: Architecture §22 — Boundary: **Request** (`ICS.Modules.Request`, Depends On: `ICS.Core`, `Organization`, `Customer`, `Product`).

---

### P4-S16

Title: Request Module — Domain Model & State Machine

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Request domain layer: `Request` aggregate, `RequestResolution` entity, `RequestAssignment` entity, state machine enforcing valid transitions (`CAPTURED → EVALUATING → ACCEPTED/REJECTED → IN_PROGRESS → ESCALATED → COMPLETED`), and all domain events emitted on state transitions.

Depends On: P1-S06

Repository: `ICS.Modules.Request`

Completion Criteria:
- `Request` aggregate with all state transition methods is implemented, per Architecture §7 (`RequestService`) and §8 (UC-REQ-001..008).
- State machine enforces valid transitions; invalid transitions are rejected with a domain exception.
- Domain events defined and emitted on each transition: `RequestRecorded`, `RequestAssigned`, `RequestEvaluated`, `RequestAccepted`, `RequestRejected`, `RequestEscalated`, `ManagementDecisionRequested`, `RequestCompleted`.
- `RequestResolution` and `RequestAssignment` entities are implemented.
- Unit tests (xUnit + FluentAssertions) covering all valid state transitions and all invalid transition rejections pass — Architecture §19.8.

Notes: Domain layer only — no database, no service layer. Clean separation enables P4-S16 to start as soon as P1-S06 is complete, independently of P2. Architecture §7, §8, §20 are authoritative.

---

### P4-S17

Title: Request Module — Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement `RequestService` application commands as MediatR handlers, `RequestQueryService` queries using Dapper explicit parameterized SQL, DbUp SQL migration scripts for `request.*` tables, and Dapper repository implementations. Cross-module validation reads from Organization, Customer, and Product via their published query interfaces. Audit logging is applied to every state change.

Depends On: P4-S16, P1-S07, P2-S09, P2-S10, P2-S11

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

---

### P4-S18

Title: Request Module — Escalation & Management Commands

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement escalation, management decision, and reassignment command paths as MediatR handlers in `RequestService` (UC-REQ-006, UC-REQ-007, UC-MGT-001). These commands extend the service layer established in P4-S17 with the escalation-specific business logic and `ReassignRequestOwnership`.

Depends On: P4-S17

Repository: `ICS.Modules.Request`

Completion Criteria:
- `EscalateRequest` MediatR handler transitions `Request` to `ESCALATED`, records escalation actor and reason via Dapper parameterized SQL, emits `RequestEscalated`.
- `RequestManagementDecision` MediatR handler records management decision, emits `ManagementDecisionRequested`.
- `ReassignRequestOwnership` (UC-MGT-001) MediatR handler updates `OwnerPersonId` via Dapper parameterized SQL, validates new assignee via `OrganizationQueryService`, emits `RequestAssigned`.
- Integration tests (xUnit + WebApplicationFactory + Respawn): Escalate, ManagementDecision, and Reassign flows pass.

Notes: Architecture §8 (UC-REQ-006, UC-REQ-007, UC-MGT-001) is authoritative.

---

### P4-S19

Title: Request Screens — SCR-REQ-001..005

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement all five Request screens as Vue 3 SFCs with Bootstrap 5 styling and their corresponding ASP.NET Core REST API controllers (`/api/v1/requests/*`). Delivers the complete request management UI. Dropdowns for Customer, Product, and Person use the respective published query services.

Depends On: P4-S18, P3-S13, P1-S08

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

---

## P5 — Work Package

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Work Package module with Dapper persistence and its management screen as a Vue 3 SFC. M3 is achieved when this phase is complete.

Source: Architecture §22 — Boundary: **Work Package** (`ICS.Modules.WorkPackage`, Depends On: `ICS.Core`, `Organization`, `Customer`, `Product`, `Request`). Architecture §11.

---

### P5-S20

Title: Work Package Module — Domain Model

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P5-S21

Title: Work Package Module — Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement `WorkPackageService` commands as MediatR handlers, `WorkPackageQueryService` queries using Dapper explicit parameterized SQL, DbUp SQL migration scripts for `workpackage.*` tables, and Dapper repository implementations. Cross-module validation uses `OrganizationQueryService`, `CustomerQueryService`, `ProductQueryService`, and `RequestQueryService`.

Depends On: P5-S20, P1-S07, P2-S09, P2-S10, P2-S11, P4-S17

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

---

### P5-S22

Title: Work Package Screen — SCR-WP-001

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement `SCR-WP-001` (Work Package screen) as a Vue 3 SFC with Bootstrap 5 styling and the corresponding ASP.NET Core REST API controller (`/api/v1/work-packages/*`). Delivers Work Package creation, lifecycle management, and scope review. Wires `WorkPackageService` MediatR handlers and `WorkPackageQueryService`.

Depends On: P5-S21, P3-S13, P1-S08

Repository: `ICS.Web`, `ICS.Web/client/`

Completion Criteria:
- Vue 3 SFC `WorkPackageView.vue` (`<script setup lang="ts">`, Bootstrap 5) renders Work Package management — Architecture §19.4, §20.
- ASP.NET Core Controller at `/api/v1/work-packages` exposes REST endpoints for: list work packages (`ListWorkPackages` with status/owner/customer/product filters), get Work Package detail (`GetWorkPackageById`), create Work Package (`CreateWorkPackage`), update objective (`UpdateObjective`), assign owner (`AssignOwner`), activate (`ActivateWorkPackage`), close (`CloseWorkPackage`), view scope (`GetWorkPackageScope`), add request to package (`AddRequestToWorkPackage`), remove request (`RemoveRequestFromWorkPackage`).
- Owner selector uses `OrganizationQueryService.ListActivePersons`.
- Customer and Product selectors use respective query services.
- All endpoints protected by `[Authorize]`.
- Axios HTTP client in Vue component calls endpoints with authentication interceptors.
- Integration tests (xUnit + WebApplicationFactory): Work Package creation, lifecycle transitions, and request membership management pass.

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

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Post domain layer and persistence: `Post`, `Comment`, `Reaction`, `PostReference` entities, `PostService` commands as MediatR handlers, `PostQueryService` queries using Dapper, DbUp SQL migration scripts for `post.*` tables (excluding `FeedItems`), and Dapper repository implementations. `PostService` validates cross-domain references via published query services.

Depends On: P1-S06, P1-S07, P4-S17, P5-S21

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

Notes: Architecture §7, §8, §18, §19.2 (MediatR notifications), §20 are authoritative. `FeedItems` table and projection are implemented in P6-S24. Depends on P4-S17 and P5-S21 because `PostService` validates `RequestId` and `WorkPackageId` references — Architecture §15.

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

Depends On: P6-S25, P3-S13, P1-S08

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

Objective: Implement the Management Analytics module using Dapper real-time queries and `AnalyticsSnapshotJob` as an ASP.NET Core `BackgroundService` (Architecture §19.7), deliver management dashboard screens as Vue 3 SFCs, perform final cross-cutting validation, and produce the Docker deployment artifact. M5 is achieved when this phase is complete.

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

Notes: Architecture §13 and §19.7 (ASP.NET Core Hosted Services) are authoritative. Depends on P4-S17 because snapshot queries aggregate from `Requests` and resolve `PersonId`/`CustomerId` via Organization and Customer query services (transitively covered since P4-S17 depends on P2-S09 and P2-S10).

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

Depends On: P7-S28, P3-S13, P1-S08

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

Title: Docker Deployment Configuration

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Produce the multi-stage `docker/Dockerfile` that builds the complete ICS Operational System into a single deployable Linux container image per Architecture §19.10. The Dockerfile must build the Vue 3 frontend via Vite and publish it as static assets alongside the ASP.NET Core backend.

Depends On: P7-S30

Repository: `docker/`, `ICS.Web`

Completion Criteria:
- `docker/Dockerfile` exists and implements a multi-stage build — Architecture §19.10:
  - **Stage 1 (Node build)**: Runs `vite build` inside `src/ICS.Web/client/` and produces the optimized SPA bundle.
  - **Stage 2 (.NET build)**: Uses `mcr.microsoft.com/dotnet/sdk:8.0` to restore and publish `ICS.Web` in Release configuration. Copies Vue bundle output from Stage 1 into the `wwwroot/` of the published application.
  - **Stage 3 (Runtime)**: Uses `mcr.microsoft.com/dotnet/aspnet:8.0` (Linux) as the final base image; copies published output from Stage 2.
- Container reads `ConnectionStrings__DefaultConnection` from environment variable at runtime — Architecture §19.10.
- Application is stateless: session state maintained in SQL Server `identity.UserSessions`; no local file system session state — Architecture §19.10.
- `docker build -f docker/Dockerfile -t ics-operational-system .` completes successfully with zero errors from repository root.
- Container starts and the `/health/ready` endpoint returns HTTP 200 when SQL Server is available.
- TLS/HTTPS termination is documented as the responsibility of an upstream reverse proxy or ingress controller; the container listens on HTTP — Architecture §19.10.

Notes: Architecture §19.10 (Deployment & Runtime Model) is authoritative. This is the terminal slice of the plan. Depends on P7-S30 to ensure the system passes all integration validation before the deployment artifact is produced.

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
