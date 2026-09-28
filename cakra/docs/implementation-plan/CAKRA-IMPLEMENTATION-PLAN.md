---
Title: CAKRA - ICS Operational System Implementation Plan
Code: CAKRA
Artifact: IMPLEMENTATION-PLAN
Version: 4.0
LastUpdated: 2026-09-28
Status: NOT-STARTED
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
| P1 — Foundation | S01–S08 | NOT-STARTED | NOT-REVIEWED | 0/8 |
| P2 — Identity & Access | S09–S11 | NOT-STARTED | NOT-REVIEWED | 0/3 |
| P3 — Core Master Data & Product Catalog | S12–S16 | NOT-STARTED | NOT-REVIEWED | 0/5 |
| P4 — Request Lifecycle | S17–S23 | NOT-STARTED | NOT-REVIEWED | 0/7 |
| P5 — Work Package | S24–S27 | NOT-STARTED | NOT-REVIEWED | 0/4 |
| P6 — Post & Feed | S28–S33 | NOT-STARTED | NOT-REVIEWED | 0/6 |
| P7 — Management Analytics & System Finalization | S34–S39 | NOT-STARTED | NOT-REVIEWED | 0/6 |

---

# 6. Phases

## P1 — Foundation

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Establish the complete shared technical foundation that all modules depend on. Covers solution structure and repository-level project layout with `src/backend` and `src/frontend` (Architecture §19.11), shared contracts (`Cakra.Core`), database infrastructure using DbUp-SqlServer (Architecture §19.3), dependency injection conventions with MediatR (Architecture §19.2), in-process domain event bus, ASP.NET Core backend host application pipeline skeleton (`Cakra.Api`) with Serilog structured logging (Architecture §19.9), backend test project infrastructure under `tests/backend/` (Architecture §19.8, §19.11), and Vue 3 frontend project scaffolding (`Cakra.Web` in `src/frontend/Cakra.Web/`) per Architecture §19.4 and §19.11. No business module is implemented in this phase. M0 is achieved when P1-S06 (Application Pipeline Foundation) is complete.

Source: Architecture §22 — Boundary: **Foundation** (`Cakra.Core`, Depends On: None). Architecture §18 (Cross-Cutting Concerns). Architecture §19 (Technology Decisions). Architecture §5 (System Structure).

---

### P1-S01

Title: Solution Structure & Project Scaffolding

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P1-S02

Title: Core Contracts & Base Types

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P1-S03

Title: Database Infrastructure & Migration Framework

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P1-S04

Title: Dependency Injection & Module Registration

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P1-S05

Title: Domain Event Bus Implementation

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P1-S06

Title: Application Pipeline Foundation

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P1-S07

Title: Test Project Infrastructure

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P1-S08

Title: Vue 3 Frontend Project Scaffolding

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

## P2 — Identity & Access

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Identity & Access module, the authentication middleware pipeline, and the Login screen (`SCR-AUTH-001`). Establishes `UserAccounts` and `UserSessions` tables via DbUp, credential verification via Argon2id or ASP.NET Core `IPasswordHasher` (PBKDF2/HMAC-SHA512), secure cookie-based session management (`HttpOnly`, `SameSite=Strict`), and the concrete `CurrentContextProvider` that exposes `CurrentUserId`, `CurrentPersonId`, and `CurrentRoles` across all HTTP requests. Delivers the Login screen (`SCR-AUTH-001`) as a Vue 3 SFC. Placing Identity immediately after Foundation ensures all subsequent modules (Master Data, Requests, Work Packages, Feed, Analytics) execute with active security context, user identification, and RBAC authorization without requiring post-hoc refactoring.

Source: Architecture §22 — Boundary: **Identity & Access** (`Cakra.Modules.Identity`, Depends On: `Cakra.Core`, `Organization` (Read)). Architecture §14 (Identity & Authentication Architecture). Architecture §18, §19.5.

---

### P2-S09

Title: Identity Module — Domain, Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P2-S10

Title: Authentication Middleware & Security Context Pipeline

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P2-S11

Title: Login Screen — SCR-AUTH-001

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

## P3 — Core Master Data & Product Catalog

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the three foundational master-data modules — Organization, Customer, and Product — including domain, application services, and persistence using Dapper with explicit parameterized SQL (Architecture §19.3), along with the Product Catalog screen (`SCR-PRD-001`). Organization is split into a persistence slice and an application services slice to reduce per-slice scope. Customer and Product are architecturally decoupled and can execute in parallel: Customer has zero dependency on Organization or Product; Product depends on Organization Services for owner validation, but has zero dependency on Customer. M1 is achieved when P3-S16 is complete.

Source: Architecture §22 — Boundaries: **Organization**, **Customer**, **Product**. Architecture §6 (Module Boundaries), §10 (Product Module Architecture), §16 (Data Ownership), §19.3 (Persistence & Data Access).

---

### P3-S12

Title: Organization Module — Domain Entities & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P3-S13

Title: Organization Module — Application Services & Role Resolution

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P3-S14

Title: Customer Module — Domain, Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P3-S15

Title: Product Module — Domain, Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P3-S16

Title: Product Catalog Screen — SCR-PRD-001

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

## P4 — Request Lifecycle

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Request module — the core operational transactional engine — including domain model, application services using Dapper, escalation and management commands, API controller, and all Request screens as Vue 3 SFCs. Delivers complete operational request management. The module is split into seven slices to enable maximum parallelism: domain model, persistence + core commands, lifecycle completion + queries, escalation commands, API controller, and two screen groups. M2 is achieved when this phase is complete.

Source: Architecture §22 — Boundary: **Request** (`Cakra.Modules.Request`, Depends On: `Cakra.Core`, `Organization`, `Customer`, `Product`).

---

### P4-S17

Title: Request Module — Domain Model & State Machine

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P4-S18

Title: Request Module — Persistence & Core Commands

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P4-S19

Title: Request Module — Lifecycle Completion Commands & Queries

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P4-S20

Title: Request Module — Escalation & Management Commands

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement escalation, management decision, and reassignment command paths as MediatR handlers in `RequestService` (UC-REQ-006, UC-REQ-007, UC-MGT-001). These commands extend the service layer established in P4-S18 with the escalation-specific business logic and `ReassignRequestOwnership`.

Depends On: P4-S18

Repository: `Cakra.Modules.Request`

Completion Criteria:
- `EscalateRequest` MediatR handler transitions `Request` to `ESCALATED`, records escalation actor and reason via Dapper parameterized SQL, emits `RequestEscalated`.
- `RequestManagementDecision` MediatR handler records management decision, emits `ManagementDecisionRequested`.
- `ReassignRequestOwnership` (UC-MGT-001) MediatR handler updates `OwnerPersonId` via Dapper parameterized SQL, validates new assignee via `OrganizationQueryService`, emits `RequestAssigned`.
- Integration tests (xUnit + WebApplicationFactory + Respawn): Escalate, ManagementDecision, and Reassign flows pass.

Notes: **Parallel Execution**: Depends on P4-S18 (not P4-S19). Can execute concurrently in parallel with P4-S19 (Lifecycle Completion & Queries). Architecture §8 (UC-REQ-006, UC-REQ-007, UC-MGT-001) is authoritative.

---

### P4-S21

Title: Request API Controller

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P4-S22

Title: Request Screens — SCR-REQ-001, SCR-REQ-002

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P4-S23

Title: Request Screens — SCR-REQ-003, SCR-REQ-004, SCR-REQ-005

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

## P5 — Work Package

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Work Package module with Dapper persistence, API controller, and its management screen as a Vue 3 SFC. M3 is achieved when this phase is complete.

Source: Architecture §22 — Boundary: **Work Package** (`Cakra.Modules.WorkPackage`, Depends On: `Cakra.Core`, `Organization`, `Customer`, `Product`, `Request`). Architecture §11.

---

### P5-S24

Title: Work Package Module — Domain Model

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P5-S25

Title: Work Package Module — Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P5-S26

Title: Work Package API Controller

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P5-S27

Title: Work Package Screen — SCR-WP-001

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement `SCR-WP-001` (Work Package screen) as a Vue 3 SFC with Bootstrap 5 styling in `src/frontend/Cakra.Web/`. Delivers Work Package creation, lifecycle management, and scope review.

Depends On: P5-S26, P1-S08

Repository: `src/frontend/Cakra.Web`

Completion Criteria:
- Vue 3 SFC `WorkPackageView.vue` (`<script setup lang="ts">`, Bootstrap 5) renders: Bootstrap 5 table listing work packages with columns (ID, Objective, Owner, Customer, Product, Status), filter dropdowns for status/owner/customer/product, create button, detail panel showing scope (linked requests) when a work package is selected — Architecture §19.4, §20.
- Detail panel includes: objective display/edit, owner selector dropdown (from `GET /api/v1/organization/persons/active`), lifecycle action buttons (Activate, Close) conditionally rendered based on state, scope management section with "Add Request" button and request list with remove buttons.
- Vue Router 4 routes: `/work-packages` maps to work package list, `/work-packages/:id` maps to detail view.
- Axios HTTP client calls `/api/v1/work-packages` endpoints with authentication interceptors.

Notes: Frontend-only slice. This slice completes Milestone M3. Architecture §9 (FEAT-WP-001) and §8 (UC-WP-001..003) are authoritative.

---

## P6 — Post & Feed

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Post module (authoring, comments, reactions) and the Feed materialized read model using Dapper. Deliver the Feed and Post screens as Vue 3 SFCs via a separated API controller slice and independent frontend slices. Feed projection handled by MediatR `INotificationHandler` within the same DB transaction scope. M4 is achieved when this phase is complete.

Source: Architecture §22 — Boundary: **Post & Feed** (`Cakra.Modules.Post`, Depends On: `Cakra.Core`, Domain Events from all modules). Architecture §12 (Feed Architecture).

---

### P6-S28

Title: Post Module — Domain, Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P6-S29

Title: Feed Projection — FeedItems Table & FeedProjectionHandler

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P6-S30

Title: Feed Query Service — Filtered Queries & Exception Detection

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P6-S31

Title: Feed & Post API Controller

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P6-S32

Title: Feed Screen — SCR-FEED-001

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P6-S33

Title: Post Detail Modal — SCR-POST-001

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Post detail modal (`SCR-POST-001`) as a Vue 3 SFC with Bootstrap 5 styling in `src/frontend/Cakra.Web/`. Delivers post thread viewing, commenting, and reaction interactions.

Depends On: P6-S31, P1-S08

Repository: `src/frontend/Cakra.Web`

Completion Criteria:
- Vue 3 SFC `PostDetailModal.vue` (`<script setup lang="ts">`, Bootstrap 5) renders: Bootstrap 5 modal containing post content (Title, Body, Author, CreatedAt), comments list (each showing Author, Content, CreatedAt) loaded from `GET /api/v1/posts/{id}/comments`, comment input form (textarea + submit button) calling `POST /api/v1/posts/{id}/comments`, reaction buttons (e.g., thumbs up, flag) with count badges calling `POST /api/v1/posts/{id}/reactions` and `DELETE /api/v1/posts/{id}/reactions/{type}` — Architecture §19.4, §20.
- Modal is triggered from Feed screen (P6-S32) feed cards.
- Navigation link from post modal to Request detail (`/requests/{requestId}`) when the post references a request — UC-FCOL-004.
- Axios HTTP client calls post API endpoints with authentication interceptors.

Notes: Frontend-only slice. **Parallel Execution**: Can execute concurrently with P6-S32 after P6-S31. This slice + P6-S32 completes Milestone M4. Architecture §9 (FEAT-FCOL-001..003) and §8 (UC-FCOL-001..004) are authoritative.

---

## P7 — Management Analytics & System Finalization

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Management Analytics module using Dapper real-time queries and `AnalyticsSnapshotJob` as an ASP.NET Core `BackgroundService` (Architecture §19.7), deliver management dashboard screens as Vue 3 SFCs, perform final cross-cutting validation, and produce the authoritative IIS on Windows Server production deployment package. M5 is achieved when this phase is complete. Analytics slices (P7-S34 through P7-S37) depend only on Request module and can execute in parallel with P5 (Work Package) and P6 (Post & Feed).

Source: Architecture §22 — Boundary: **Management Analytics** (`Cakra.Modules.Analytics`, Depends On: `Cakra.Core`, `Request`, `Organization`, `Customer`). Architecture §13 (Analytics Architecture). Architecture §18, §19.7, §19.10.

---

### P7-S34

Title: Analytics Module — Snapshot Tables & Snapshot Job

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P7-S35

Title: Analytics Module — Real-Time Workload & Customer Portfolio Queries

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P7-S36

Title: Management Analytics API Controller

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P7-S37

Title: Management Analytics Screens — SCR-MGT-001, SCR-MGT-002, SCR-MGT-003

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P7-S38

Title: Cross-Cutting Finalization & System Integration Validation

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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

---

### P7-S39

Title: IIS Production Deployment Packaging & Configuration

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

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
