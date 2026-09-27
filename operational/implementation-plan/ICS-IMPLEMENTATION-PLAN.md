---
Title: ICS Operational System Implementation Plan
Code: ICS
Artifact: IMPLEMENTATION-PLAN
Version: 2.0
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

| Boundary | Assembly | Architecture §21 Dependency |
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

Scope derives directly from Architecture §21 (Implementation Boundaries) and §22 (Implementation Dependency Graph). No feature sub-scope is applied; the plan covers the entire target system. Presentation screens are delivered vertically within each module's phase to enable early feedback rather than accumulating all UI work at the end.

---

# 3. Business Milestones

Milestones represent observable business-level delivery checkpoints, independent of phase numbering.

| Milestone | Achieved After | Observable Capability |
|---|---|---|
| **M0 — Foundation Ready** | P1-S06 | Infrastructure operational: database migrations run, DI container resolves, event bus dispatches, application starts and serves requests |
| **M1 — Authentication & Catalog Operational** | P3-S13 | Users can log in. Organization, Customer, and Product master data is accessible. Product catalog screen (`SCR-PRD-001`) is functional. First end-to-end user flows are possible |
| **M2 — Request Lifecycle Operational** | P4-S17 | Complete request management operational: Record, Assign, Evaluate, Accept, Reject, Escalate, Decision, Complete. All request screens (`SCR-REQ-001..005`) functional. Core business operations running |
| **M3 — Work Package Operational** | P5-S20 | Work Packages can be created, managed, activated, closed, and linked to Requests. Work Package screen (`SCR-WP-001`) functional |
| **M4 — Operational Feed Live** | P6-S24 | Feed screen (`SCR-FEED-001`) operational. Posts, comments, reactions functional. Exception badges working. Post detail modal (`SCR-POST-001`) functional. Feed filtering by Customer, Product, and Exception active |
| **M5 — Full System Operational** | P7-S28 | Management dashboards (`SCR-MGT-001..003`) operational. Analytics snapshot job running. All cross-cutting concerns verified end-to-end. System ready for UAT |

---

# 4. Dependencies

## External Dependencies

- Relational database engine (schema-segregated per Architecture §17) must be available in the target environment before any schema migration slices can execute.
- Target runtime and build toolchain must be initialised before application module slices can compile.

## Slice Dependency Rules (per skill)

- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- A dependency is satisfied when the referenced slice has implementation status `IMPLEMENTED` and its required outputs exist in the target repository.
- Review status does not participate in dependency satisfaction.
- Dependencies represent real implementation prerequisites; they are not conceptual or sequential associations.
- `Depends On: P1-S06` transitively implies all P1 slices are implemented, since P1-S06 is the terminal slice of the Foundation phase.

---

# 5. Progress Summary

| Phase | Slices | Implementation Status | Review Status | Progress |
|---|---|---|---|---|
| P1 — Foundation | S01–S06 | NOT-STARTED | NOT-REVIEWED | 0/6 |
| P2 — Core Master Data APIs | S07–S09 | NOT-STARTED | NOT-REVIEWED | 0/3 |
| P3 — Identity, Authentication & Product Catalog | S10–S13 | NOT-STARTED | NOT-REVIEWED | 0/4 |
| P4 — Request Lifecycle | S14–S17 | NOT-STARTED | NOT-REVIEWED | 0/4 |
| P5 — Work Package | S18–S20 | NOT-STARTED | NOT-REVIEWED | 0/3 |
| P6 — Post & Feed | S21–S24 | NOT-STARTED | NOT-REVIEWED | 0/4 |
| P7 — Management Analytics & System Finalization | S25–S28 | NOT-STARTED | NOT-REVIEWED | 0/4 |

---

# 6. Phases

## P1 — Foundation

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Establish the complete shared technical foundation (`ICS.Core`) that all modules depend on. Covers solution structure, shared contracts, database infrastructure, dependency injection conventions, domain event bus, and application pipeline skeleton. No business module is implemented in this phase. M0 is achieved when this phase is complete.

Source: Architecture §21 — Boundary: **Foundation** (`ICS.Core`, Depends On: None). Architecture §18 (Cross-Cutting Concerns). Architecture §5 (System Structure).

---

### P1-S01

Title: Solution Structure & Project Scaffolding

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Create the solution file, project references, and directory structure for the modular monolith. Establish the `ICS.Core` project and all `ICS.Modules.*` project skeletons (empty, buildable) and the host application project. Define inter-project reference graph matching the Architecture §21 dependency table.

Depends On: None

Repository: Solution root / `ICS.Core`

Completion Criteria:
- Solution file exists and compiles successfully with zero errors.
- `ICS.Core` project exists.
- Module project skeletons exist (empty, buildable): `ICS.Modules.Identity`, `ICS.Modules.Organization`, `ICS.Modules.Customer`, `ICS.Modules.Product`, `ICS.Modules.WorkPackage`, `ICS.Modules.Request`, `ICS.Modules.Post`, `ICS.Modules.Analytics`.
- Host application project exists and references all module projects.
- Project reference graph matches Architecture §21 dependency table with no circular references.
- `dotnet build` (or equivalent) succeeds on the solution.

Notes: Produces no business logic. Its output is the compilable project structure that all subsequent slices build into.

---

### P1-S02

Title: Core Contracts & Base Types

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Define all shared technical contracts and base types in `ICS.Core`: base entity type, value object base, domain event interface, domain event dispatcher interface, repository interface contract, system clock abstraction, audit context interface, and the `ICurrentContextProvider` interface. Interfaces only — no concrete implementations in this slice.

Depends On: P1-S01

Repository: `ICS.Core`

Completion Criteria:
- Base entity type (with `Id`, `CreatedAt`, `UpdatedAt`) is defined.
- `IDomainEvent` interface is defined.
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

Objective: Configure the migration framework, database connection management, and schema-per-module conventions. Establish the technical foundation that all module schema migrations will build on. Implement database connection factory, migration runner, and schema-segregation naming conventions (`identity.*`, `organization.*`, `customer.*`, `product.*`, `workpackage.*`, `request.*`, `post.*`, `analytics.*`) per Architecture §17.

Depends On: P1-S01

Repository: `ICS.Core` / host application

Completion Criteria:
- Migration framework is configured (e.g., FluentMigrator, EF Core Migrations, or equivalent).
- Database connection factory is implemented and configurable via application settings.
- Schema-per-module naming convention is established and documented: eight schema prefixes matching Architecture §17.
- Migration runner can apply an empty baseline migration without errors.
- Database health check confirms connectivity.
- No business tables are created in this slice; only the migration infrastructure is established.

Notes: P1-S03 depends only on P1-S01 and can execute in parallel with P1-S02. Architecture §17 (Schema Segregation) is authoritative for schema naming.

---

### P1-S04

Title: Dependency Injection & Module Registration

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the module registration pattern and DI container configuration. Each `ICS.Modules.*` project will implement `IModule` to self-register its services, repositories, and handlers. The host application discovers and invokes all `IModule` implementations at startup. Establish service lifetime conventions (Singleton, Scoped, Transient) per architectural pattern.

Depends On: P1-S02, P1-S03

Repository: `ICS.Core` / host application

Completion Criteria:
- `IModule` implementation pattern is demonstrated with a stub module that registers successfully.
- Host application startup discovers and registers all `IModule` implementations.
- Service lifetime conventions are established and applied consistently.
- DI container resolves all core interfaces (`ISystemClock`, `IAuditContext`, `ICurrentContextProvider`) after startup.
- Build and DI resolution verification tests pass (no unresolved dependencies at startup).

Notes: Depends on P1-S02 for `IModule` interface and P1-S03 for database connection registration.

---

### P1-S05

Title: Domain Event Bus Implementation

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the concrete in-process domain event bus: `DomainEventDispatcher` implementing `IDomainEventDispatcher`. The dispatcher must support synchronous in-process event publishing within the same database transaction scope as the originating command — per Architecture §18 and §12 (Synchronization Guarantee). Event handlers are discovered and registered through the DI container.

Depends On: P1-S02

Repository: `ICS.Core`

Completion Criteria:
- `DomainEventDispatcher` implements `IDomainEventDispatcher`.
- Event dispatch is synchronous and executes within the same transactional scope as the command that raised the event — per Architecture §18 ("in-process synchronous event dispatcher with transactional consistency").
- Multiple handlers registered for the same event type are all invoked.
- Event handlers are registered and discovered via the DI container.
- Unit tests: publishing an event invokes all registered handlers; missing handler does not throw; handler exception propagates correctly.

Notes: Depends on P1-S02 for the `IDomainEvent` and `IDomainEventDispatcher` interfaces. Can execute in parallel with P1-S03 after P1-S02 is implemented. Architecture §18 (Domain Event Bus) is authoritative.

---

### P1-S06

Title: Application Pipeline Foundation

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Configure the HTTP request pipeline skeleton: middleware registration order, centralized exception handling middleware, input validation pipeline, audit logging hook, and security context population middleware placeholder. Establish the application entry point, health check endpoint, and the wiring point for all modules. Application must start, serve requests, and return structured error responses.

Depends On: P1-S04, P1-S05

Repository: Host application

Completion Criteria:
- HTTP request pipeline is configured with documented middleware order.
- Centralized exception handling middleware returns consistent structured error responses (per Architecture §18 — Exception Handling & Validation).
- Input validation pipeline is in place and invoked before application service dispatch.
- Audit logging hook is registered in the pipeline (per Architecture §18 — Audit Logging).
- Security context population middleware placeholder is registered (concrete implementation supplied by P3-S11).
- Health check endpoint (`/health` or equivalent) responds with HTTP 200.
- Application starts, wires all registered `IModule` bootstrappers, and serves requests without unresolved dependency errors.

Notes: Terminal slice of P1. All subsequent phases depend on P1-S06, transitively relying on the entire Foundation phase being complete. Architecture §5 (System Structure), §18 (Cross-Cutting Concerns), and §20 (Implementation Constraints) are authoritative.

---

## P2 — Core Master Data APIs

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the three foundational master-data modules — Organization, Customer, and Product — including domain, application services, and persistence. These modules expose published query interfaces consumed by all subsequent modules. Organization and Customer can be implemented in parallel; Product depends on Organization for owner validation.

Source: Architecture §21 — Boundaries: **Organization**, **Customer**, **Product**. Architecture §6 (Module Boundaries), §16 (Data Ownership).

---

### P2-S07

Title: Organization Module — Domain, Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the complete Organization module: domain entities (`Person`, `Team`, `Role`, `Responsibility`, `TeamMembership`, `RoleAssignment`, `ResponsibilityAssignment`), `OrganizationService` command methods, `OrganizationQueryService` query methods, schema migration for `organization.*` tables, and repository implementations.

Depends On: P1-S06

Repository: `ICS.Modules.Organization`

Completion Criteria:
- `organization.*` schema tables exist and are created by migration: `Persons`, `Teams`, `Roles`, `Responsibilities`, `TeamMemberships`, `RoleAssignments`, `ResponsibilityAssignments` — Architecture §17.
- `OrganizationService` implements: create/update Person, create Team, assign Person to Team, create Role, assign Role to Person, create Responsibility, assign Responsibility to Person, deactivate Person.
- `OrganizationQueryService` implements: `GetPersonById`, `ListActivePersons`, `GetTeamRoster`, `GetPersonRoles`, `GetPersonResponsibilities` — Architecture §7.
- Domain events emitted: `PersonCreated`, `PersonDeactivated`, `RoleAssigned`, `RoleRevoked`.
- `OrganizationQueryService` is accessible as a published interface to other modules; internal repositories are not exposed — Architecture §20 (Strict Vertical Slice Boundary).
- Repository implementations write exclusively to `organization.*` schema.
- Integration tests confirming query service returns accurate results after service commands.

Notes: Architecture §6 (Module Boundaries — Organization), §7, and §16 are authoritative. No tables from other modules are written.

---

### P2-S08

Title: Customer Module — Domain, Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the complete Customer module: domain entities (`Customer`, `CustomerContact`), `CustomerService` command methods, `CustomerQueryService` query methods, schema migration for `customer.*` tables, and repository implementations.

Depends On: P1-S06

Repository: `ICS.Modules.Customer`

Completion Criteria:
- `customer.*` schema tables exist: `Customers`, `CustomerContacts` — Architecture §17.
- `CustomerService` implements: create Customer, update Customer master data, create CustomerContact, update CustomerContact, deactivate Customer.
- `CustomerQueryService` implements: `GetCustomerById`, `ListActiveCustomers`, `GetCustomerContacts`, `GetCustomerWithContractStatus` — Architecture §7.
- `CustomerQueryService` is accessible as a published interface; internal repositories are not exposed.
- Repository implementations write exclusively to `customer.*` schema.
- Integration tests confirming query service returns accurate results after service commands.

Notes: Architecture §6 (Module Boundaries — Customer) and §16 are authoritative. Customer module does not own Request or Work Package relationships. Can execute in parallel with P2-S07.

---

### P2-S09

Title: Product Module — Domain, Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the complete Product module: domain entity (`Product`), `ProductService` command methods, `ProductQueryService` query methods, schema migration for `product.*` tables, and repository implementations. Owner validation reads from `OrganizationQueryService`.

Depends On: P1-S06, P2-S07

Repository: `ICS.Modules.Product`

Completion Criteria:
- `product.*` schema table exists: `Products` — Architecture §17.
- `ProductService` implements: `CreateProduct`, `UpdateProduct`, `AssignProductOwner`, `ActivateProduct`, `DeactivateProduct` — Architecture §10.
- `ProductQueryService` implements: `GetProductById`, `GetProductByCode`, `ListActiveProducts`, `ListAllProducts` — Architecture §10.
- `CreateProduct` and `AssignProductOwner` validate the owner via `OrganizationQueryService` (no direct cross-schema write) — Architecture §15.
- Domain events emitted: `ProductCreated`, `ProductOwnerChanged`, `ProductActivated`, `ProductDeactivated`.
- `ProductQueryService` is accessible as a published interface.
- Repository implementations write exclusively to `product.*` schema.
- Integration tests pass.

Notes: Architecture §10 (Product Module Architecture) is authoritative. Depends on P2-S07 because `CreateProduct` and `AssignProductOwner` call `OrganizationQueryService` to validate the owner.

---

## P3 — Identity, Authentication & Product Catalog

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Identity & Access module, the authentication middleware pipeline, the Login screen, and the Product Catalog screen. This phase delivers the first vertical user-facing capability (log in, view and manage products). M1 is achieved when this phase is complete.

Source: Architecture §21 — Boundary: **Identity & Access** (`ICS.Modules.Identity`, Depends On: `ICS.Core`, `Organization` (Read)). Architecture §14 (Identity & Authentication Architecture). Architecture §18.

---

### P3-S10

Title: Identity Module — Domain, Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the complete Identity & Access module: `UserAccount` and `UserSession` domain entities, `AuthenticationService` (Login, Logout, ValidateSession), schema migration for `identity.*` tables, and repository implementations. Credential verification, Person status check, session creation, and token issuance per Architecture §14.

Depends On: P1-S06, P2-S07

Repository: `ICS.Modules.Identity`

Completion Criteria:
- `identity.*` schema tables exist: `UserAccounts`, `UserSessions` — Architecture §14 (IAM Persistence Tables).
- `AuthenticationService.Login(usernameOrEmail, password, clientInfo)`: verifies password hash (Argon2id or bcrypt), checks `UserAccount.Status == 'ACTIVE'`, checks linked `Person.Status == 'ACTIVE'` via `OrganizationQueryService`, creates `UserSession`, issues session token — Architecture §14.
- `AuthenticationService.Logout(sessionToken)`: marks `UserSession.IsRevoked = TRUE`.
- `AuthenticationService.ValidateSession(sessionToken)`: validates token, checks expiry and revocation, returns `SecurityContext` with `UserId` and `PersonId`.
- `AuthorizationService.ResolveRoles(personId)`: fetches active `RoleAssignments` via `OrganizationQueryService`.
- Repository implementations write exclusively to `identity.*` schema.
- Integration tests: Login success, Login failure (bad credentials), Login failure (inactive Person), ValidateSession with valid token, ValidateSession with expired token, Logout invalidates session.

Notes: Architecture §14 is authoritative for all IAM component specifications. Password hashing algorithm must be Argon2id or bcrypt per Architecture §14.

---

### P3-S11

Title: Authentication Middleware & Security Context Pipeline

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the concrete HTTP authentication middleware that intercepts every incoming request, calls `AuthenticationService.ValidateSession`, populates `CurrentContextProvider` with `CurrentUserId`, `CurrentPersonId`, and `CurrentRoles`, and rejects unauthenticated requests with HTTP 401. Implement the RBAC enforcement mechanism (`[Authorize(Roles = "...")]` or equivalent) per Architecture §18.

Depends On: P3-S10

Repository: `ICS.Modules.Identity` / host application

Completion Criteria:
- Authentication middleware is registered in the host application pipeline (fulfils the placeholder established in P1-S06).
- Every request without a valid session token returns HTTP 401.
- Every request with a valid token populates `CurrentContextProvider` with `CurrentUserId`, `CurrentPersonId`, and `CurrentRoles`.
- RBAC enforcement mechanism is in place and verified: request from a non-Management user to a Management-role endpoint returns HTTP 403.
- Integration tests for authenticated request, unauthenticated request (401), and insufficient-role request (403) pass.

Notes: Architecture §18 (Authentication & Security Context, RBAC) is authoritative.

---

### P3-S12

Title: Login Screen — SCR-AUTH-001

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement `SCR-AUTH-001` (Login Screen) and the authentication API controller. Wires `AuthenticationService.Login` and `Logout` to HTTP endpoints, sets the session cookie/token, handles login failures, and redirects to `SCR-FEED-001` on success.

Depends On: P3-S11

Repository: Host application / Presentation Layer

Completion Criteria:
- `SCR-AUTH-001` login screen renders username/password input fields.
- POST login endpoint calls `AuthenticationService.Login`, sets session cookie/token on success.
- Login failure returns distinct error messages: invalid credentials, account locked, inactive person.
- Successful login redirects to `SCR-FEED-001`.
- Logout endpoint calls `AuthenticationService.Logout` and invalidates the session cookie.
- Integration tests for login success and each login failure scenario pass.

Notes: Architecture §14 (UI Boundary — SCR-AUTH-001) is authoritative.

---

### P3-S13

Title: Product Catalog Screen — SCR-PRD-001

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement `SCR-PRD-001` (Product Catalog Screen) and its API controller. Wires `ProductService` commands and `ProductQueryService` queries to presentation endpoints. Delivers the first content management screen; users can view the product catalog, create products, update attributes, assign product owners, and toggle product status.

Depends On: P3-S11, P2-S09

Repository: Host application / Presentation Layer

Completion Criteria:
- `SCR-PRD-001` controller exposes endpoints for: list all products (`ListAllProducts`), list active products (`ListActiveProducts`), get product detail (`GetProductById`), create product (`CreateProduct`), update product (`UpdateProduct`), assign product owner (`AssignProductOwner`), activate/deactivate product.
- Product owner selector uses `OrganizationQueryService.ListActivePersons` for dropdown — Architecture §10 (ProductQueryService — `ListActiveProducts` for `SCR-REQ-002` selector reference).
- All endpoints are protected by authentication middleware (P3-S11).
- Integration tests for catalog listing, product creation, and owner assignment pass.

Notes: Architecture §10 and §9 (FEAT-PRD-001) are authoritative.

---

## P4 — Request Lifecycle

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Request module — the core operational transactional engine — including domain model, application services, escalation and management commands, and all Request screens. Delivers complete operational request management. M2 is achieved when this phase is complete.

Source: Architecture §21 — Boundary: **Request** (`ICS.Modules.Request`, Depends On: `ICS.Core`, `Organization`, `Customer`, `Product`).

---

### P4-S14

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
- Unit tests covering all valid state transitions and all invalid transition rejections pass.

Notes: Domain layer only — no database, no service layer. Clean separation enables P4-S14 to start as soon as P1-S06 is complete, independently of P2. Architecture §7, §8, §20 are authoritative.

---

### P4-S15

Title: Request Module — Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement `RequestService` application commands, `RequestQueryService` queries, schema migration for `request.*` tables, and repository implementations. Cross-module validation reads from Organization, Customer, and Product via their published query interfaces. Audit logging is applied to every state change.

Depends On: P4-S14, P2-S07, P2-S08, P2-S09

Repository: `ICS.Modules.Request`

Completion Criteria:
- `request.*` schema tables exist: `Requests`, `RequestResolutions`, `RequestAssignments` — Architecture §17.
- `RequestService` implements: `RecordRequest`, `AssignRequestOwner`, `EvaluateRequest`, `AcceptRequestResponsibility`, `RejectRequest`, `EscalateRequest`, `RequestManagementDecision`, `ReviewRequestCompletion` — Architecture §7, §8 (UC-REQ-001..008).
- `RequestQueryService` implements: `GetRequestById`, `GetRequestStateHistory`, `ListMyAssignedRequests`, `GetFilteredRequestGrid` — Architecture §7, §8 (UC-COL-002..004).
- Assignee validated via `OrganizationQueryService`; customer via `CustomerQueryService`; product via `ProductQueryService` — Architecture §15.
- Every state change records actor `PersonId`, timestamp, and previous state — Architecture §18 (Audit Logging).
- Repository implementations write exclusively to `request.*` schema.
- `RequestQueryService` is accessible as a published interface.
- Integration tests for full request lifecycle (Record → Assign → Evaluate → Accept → Complete) pass.

Notes: Architecture §7, §8, §15, §18, §20 are authoritative.

---

### P4-S16

Title: Request Module — Escalation & Management Commands

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement escalation, management decision, and reassignment command paths in `RequestService` (UC-REQ-006, UC-REQ-007, UC-MGT-001). These commands extend the service layer established in P4-S15 with the escalation-specific business logic and `ReassignRequestOwnership`.

Depends On: P4-S15

Repository: `ICS.Modules.Request`

Completion Criteria:
- `EscalateRequest` transitions `Request` to `ESCALATED`, records escalation actor and reason, emits `RequestEscalated`.
- `RequestManagementDecision` records management decision, emits `ManagementDecisionRequested`.
- `ReassignRequestOwnership` (UC-MGT-001) updates `OwnerPersonId`, validates new assignee via `OrganizationQueryService`, emits `RequestAssigned`.
- Integration tests for Escalate, ManagementDecision, and Reassign flows pass.

Notes: Architecture §8 (UC-REQ-006, UC-REQ-007, UC-MGT-001) is authoritative.

---

### P4-S17

Title: Request Screens — SCR-REQ-001..005

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement all five Request screens and their API controllers, delivering the complete request management UI. Wires `RequestService` commands and `RequestQueryService` queries. Dropdowns for Customer, Product, and Person use the respective published query services.

Depends On: P4-S16, P3-S11

Repository: Host application / Presentation Layer

Completion Criteria:
- `SCR-REQ-001` (Request List) controller: `RequestQueryService.GetFilteredRequestGrid` with status and assignment filters.
- `SCR-REQ-002` (Create Request) controller: `RequestService.RecordRequest`; Customer selector via `CustomerQueryService.ListActiveCustomers`; Product selector via `ProductQueryService.ListActiveProducts`; Person selector via `OrganizationQueryService.ListActivePersons`.
- `SCR-REQ-003` (Request Detail) controller: `RequestQueryService.GetRequestById`; all state-transition command endpoints (`Assign`, `Evaluate`, `Accept`, `Reject`, `Escalate`, `ManagementDecision`, `Complete`, `Reassign`) wired to `RequestService`.
- `SCR-REQ-004` (My Requests) controller: `RequestQueryService.ListMyAssignedRequests` using `CurrentContextProvider.CurrentPersonId`.
- `SCR-REQ-005` (Search / History) controller: `RequestQueryService.GetRequestStateHistory` and search by Customer, Product, or status.
- All endpoints protected by authentication middleware.
- Audit logging active on all state-transition endpoints.
- Integration tests for each screen endpoint (listing, create, state transition) pass.

Notes: Architecture §9 (Feature Mapping — FEAT-REQ-001..008, FEAT-COL-001..004) and §8 (UC-REQ-001..008, UC-COL-001..004) are authoritative.

---

## P5 — Work Package

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Work Package module and its management screen. M3 is achieved when this phase is complete.

Source: Architecture §21 — Boundary: **Work Package** (`ICS.Modules.WorkPackage`, Depends On: `ICS.Core`, `Organization`, `Customer`, `Product`, `Request`). Architecture §11.

---

### P5-S18

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
- Unit tests for lifecycle transitions and membership invariants (including Business Rule 9 violation) pass.

Notes: Architecture §11 is authoritative. Domain-only slice; can start as soon as P1-S06 is complete.

---

### P5-S19

Title: Work Package Module — Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement `WorkPackageService` commands, `WorkPackageQueryService` queries, schema migration for `workpackage.*` tables, and repository implementations. Cross-module validation uses `OrganizationQueryService`, `CustomerQueryService`, `ProductQueryService`, and `RequestQueryService`.

Depends On: P5-S18, P2-S07, P2-S08, P2-S09, P4-S15

Repository: `ICS.Modules.WorkPackage`

Completion Criteria:
- `workpackage.*` schema tables exist: `WorkPackages`, `WorkPackageRequests` — Architecture §17.
- `WorkPackageService` implements: `CreateWorkPackage`, `UpdateObjective`, `AssignOwner`, `AddRequestToWorkPackage`, `RemoveRequestFromWorkPackage`, `ActivateWorkPackage`, `CloseWorkPackage` — Architecture §11.
- `AddRequestToWorkPackage` validates via `RequestQueryService` that the request exists and is not already in another active package (Business Rule 9) — Architecture §11.
- `WorkPackageQueryService` implements: `GetWorkPackageById`, `ListWorkPackages`, `GetWorkPackageScope`, `GetRequestWorkPackage` — Architecture §11.
- Owner validated via `OrganizationQueryService`; customer and product references via respective query services.
- Closing a Work Package does not alter constituent Request lifecycle states — Architecture §11.
- Repository implementations write exclusively to `workpackage.*` schema.
- Integration tests for full Work Package lifecycle and request membership pass.

Notes: Architecture §11 and §15 are authoritative.

---

### P5-S20

Title: Work Package Screen — SCR-WP-001

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement `SCR-WP-001` (Work Package screen) and its API controller. Delivers Work Package creation, lifecycle management, and scope review. Wires `WorkPackageService` and `WorkPackageQueryService`.

Depends On: P5-S19, P3-S11

Repository: Host application / Presentation Layer

Completion Criteria:
- `SCR-WP-001` controller exposes endpoints for: list work packages (`ListWorkPackages` with status/owner/customer/product filters), get Work Package detail (`GetWorkPackageById`), create Work Package (`CreateWorkPackage`), update objective (`UpdateObjective`), assign owner (`AssignOwner`), activate (`ActivateWorkPackage`), close (`CloseWorkPackage`), view scope (`GetWorkPackageScope`), add request to package (`AddRequestToWorkPackage`), remove request (`RemoveRequestFromWorkPackage`).
- Owner selector uses `OrganizationQueryService.ListActivePersons`.
- Customer and Product selectors use respective query services.
- All endpoints protected by authentication middleware.
- Integration tests for Work Package creation, lifecycle transitions, and request membership management pass.

Notes: Architecture §9 (FEAT-WP-001) and §8 (UC-WP-001..003) are authoritative.

---

## P6 — Post & Feed

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Post module (authoring, comments, reactions) and the Feed materialized read model. Deliver the Feed and Post screens. M4 is achieved when this phase is complete.

Source: Architecture §21 — Boundary: **Post & Feed** (`ICS.Modules.Post`, Depends On: `ICS.Core`, Domain Events from all modules). Architecture §12 (Feed Architecture).

---

### P6-S21

Title: Post Module — Domain, Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Post domain layer and persistence: `Post`, `Comment`, `Reaction`, `PostReference` entities, `PostService` commands, `PostQueryService` queries, schema migration for `post.*` tables (excluding `FeedItems`), and repository implementations. `PostService` validates cross-domain references via published query services.

Depends On: P1-S06, P4-S15, P5-S19

Repository: `ICS.Modules.Post`

Completion Criteria:
- `post.*` schema tables exist: `Posts`, `Comments`, `Reactions`, `PostReferences` — Architecture §17.
- `PostService` implements: `CreateOperationalPost` (human authored), `RecordSystemPost` (system generated), `PostComment`, `AddReaction`, `RemoveReaction`, `TogglePostVisibility`, `ArchivePost` — Architecture §7, §8 (UC-FCOL-001..003, UC-COL-001).
- `PostQueryService` implements: `GetPostThreadDetails`, `GetFullComments`, `GetReactionList` — Architecture §7.
- Domain events emitted: `PostCreated`, `CommentAdded`, `ReactionAdded`, `ReactionRemoved`, `PostVisibilityChanged`, `PostArchived` — Architecture §12 (Update Triggers).
- Soft-delete/archive only; no physical purge of posts, comments, or reactions — Architecture §20 (Permanent Retention).
- Repository implementations write exclusively to `post.*` schema.
- Integration tests for post authoring, commenting, and reaction flows pass.

Notes: Architecture §7, §8, §18, §20 are authoritative. `FeedItems` table and projection are implemented in P6-S22. Depends on P4-S15 and P5-S19 because `PostService` validates `RequestId` and `WorkPackageId` references via `RequestQueryService` and `WorkPackageQueryService` when creating `PostReferences` — Architecture §15.

---

### P6-S22

Title: Feed Projection — FeedItems Table & FeedProjectionHandler

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the `FeedItems` materialized read model: create `FeedItems` table schema migration, implement `FeedProjectionHandler` subscribing to in-process domain events (`PostCreated`, `CommentAdded`, `ReactionAdded`, `ReactionRemoved`, `PostVisibilityChanged`, `PostArchived`), and the `FeedProjectionRebuilder` idempotent rebuild routine.

Depends On: P6-S21

Repository: `ICS.Modules.Post`

Completion Criteria:
- `FeedItems` table exists in `post.*` schema with all columns per Architecture §12 (Feed Projection Table Schema).
- `FeedProjectionHandler` handles all five event types and updates `FeedItems` within the same database transaction scope as the originating command — Architecture §12 (Synchronization Guarantee).
- `FeedProjectionRebuilder.RebuildAll()` truncates and fully regenerates `FeedItems` from authoritative `Posts`, `PostReferences`, `Comments`, `Reactions` — Architecture §12 (Rebuild Strategy).
- `FeedItems` is strictly read-only for all consumers; no application code writes to `FeedItems` except `FeedProjectionHandler` and `FeedProjectionRebuilder` — Architecture §20 (Non-Mutating Projections).
- Integration tests: post creation inserts correct `FeedItem`; comment increments `CommentCount`; reaction updates `ReactionCountsJson`; visibility change updates `Visibility`; archive sets `Status = 'ARCHIVED'`.

Notes: Architecture §12 is fully authoritative for projection table schema, update triggers, synchronization guarantee, query semantics, and rebuild strategy.

---

### P6-S23

Title: Feed Query Service — Filtered Queries & Exception Detection

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the full `FeedQueryService` filtering capabilities (Customer, Product, Exception badge, pagination) and exception flag population logic in `FeedProjectionHandler` for escalation, rejection, and stalled request scenarios.

Depends On: P6-S22

Repository: `ICS.Modules.Post`

Completion Criteria:
- `FeedQueryService.GetFeed(filter, pagination)` executes the single-table indexed query per Architecture §12 (Query Semantics): filters on `Visibility = 'VISIBLE'`, `Status = 'ACTIVE'`, `CustomerId`, `ProductId`, `IsException`, with `ORDER BY CreatedAt DESC` and `LIMIT/OFFSET` pagination.
- `IsException` is set to `TRUE` and `ExceptionType` populated (`ESCALATION`, `REJECTION`, `STALLED`) by `FeedProjectionHandler` when processing domain events indicating exceptional request states.
- Pagination (`PageSize`, `Offset`) returns correctly bounded result sets.
- Supports UC-FCOL-005 (Filter Feed), UC-AWR-001 (Observe Feed), UC-AWR-002 (Discover via Feed), UC-AWR-003 (Monitor Exceptions) — Architecture §8.
- Integration tests for: no filter (all visible active items), Customer filter, Product filter, exceptions-only filter, and pagination boundary pass.

Notes: Architecture §12 (Query Semantics, Update Triggers) and §8 are authoritative.

---

### P6-S24

Title: Feed & Post Screens — SCR-FEED-001, SCR-POST-001

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Feed screen (`SCR-FEED-001`) and Post detail modal (`SCR-POST-001`) controllers. Wire `FeedQueryService` and `PostQueryService` to presentation endpoints. Deliver post authoring, commenting, reacting, and navigation from feed card to request detail.

Depends On: P6-S23, P3-S11

Repository: Host application / Presentation Layer

Completion Criteria:
- `SCR-FEED-001` controller returns paginated `FeedItems` with filter support (Customer, Product, Exceptions-only).
- Exception badge indicator present on feed cards where `IsException = TRUE`.
- `SCR-POST-001` post detail modal controller returns full post thread via `PostQueryService.GetPostThreadDetails`, `GetFullComments`, `GetReactionList`.
- Post authoring form endpoint wires `PostService.CreateOperationalPost`.
- Comment endpoint wires `PostService.PostComment`.
- Reaction endpoint wires `PostService.AddReaction` and `PostService.RemoveReaction`.
- Navigation from feed card to Request detail (UC-FCOL-004) is wired to `SCR-REQ-003` endpoint.
- All endpoints protected by authentication middleware.
- Integration tests for feed listing, each filter dimension, post detail, comment, and reaction pass.

Notes: Architecture §9 (FEAT-AWR-001, FEAT-FCOL-001..003, FEAT-FCOL-005) and §8 (UC-AWR-001..003, UC-FCOL-001..005) are authoritative.

---

## P7 — Management Analytics & System Finalization

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Management Analytics module, deliver management dashboard screens, and perform final cross-cutting validation across the complete system. M5 is achieved when this phase is complete.

Source: Architecture §21 — Boundary: **Management Analytics** (`ICS.Modules.Analytics`, Depends On: `ICS.Core`, `Request`, `Organization`, `Customer`). Architecture §13 (Analytics Architecture). Architecture §18.

---

### P7-S25

Title: Analytics Module — Snapshot Tables & Snapshot Job

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Analytics schema migration for `analytics.*` tables and the `AnalyticsSnapshotJob` scheduled background worker (daily workload snapshot and monthly customer performance snapshot).

Depends On: P1-S06, P4-S15

Repository: `ICS.Modules.Analytics`

Completion Criteria:
- `analytics.*` schema tables exist: `DailyWorkloadSnapshots`, `MonthlyCustomerPerformanceSnapshots` — Architecture §13 (Snapshot Storage Tables).
- `AnalyticsSnapshotJob` implements:
  - Daily snapshot (23:59:59): computes end-of-day workload per active `Person` from `Requests` and inserts into `DailyWorkloadSnapshots`.
  - Monthly snapshot (1st of month, 00:05:00): aggregates preceding calendar month metrics per `Customer` and inserts into `MonthlyCustomerPerformanceSnapshots`.
  - Unique constraints `UNIQUE(SnapshotDate, PersonId)` and `UNIQUE(YearMonth, CustomerId)` enforced — Architecture §13.
- `ManagementAnalyticsService.RecomputeSnapshots(startDate, endDate)` implements idempotent backfill — Architecture §13 (On-Demand Recomputation).
- Snapshot records are treated as immutable historical facts after insertion — Architecture §13 (Immutability).
- Integration test: trigger daily snapshot job; verify `DailyWorkloadSnapshots` row inserted with correct metric counts.

Notes: Architecture §13 is authoritative. Depends on P4-S15 because snapshot queries aggregate from `Requests` (owned by Request module) and resolve `PersonId`/`CustomerId` via Organization and Customer query services (transitively covered since P4-S15 depends on P2-S07 and P2-S08).

---

### P7-S26

Title: Analytics Module — Real-Time Workload & Customer Portfolio Queries

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement `ManagementAnalyticsService` real-time dynamic query methods: `GetProgrammerActiveWorkload` (per-person active request aggregation for `SCR-MGT-003`) and `GetCustomerRequestPortfolio` (active requests, open blockers, recent completions for `SCR-MGT-001`).

Depends On: P7-S25

Repository: `ICS.Modules.Analytics`

Completion Criteria:
- `GetProgrammerActiveWorkload(personId?)`: dynamically aggregates `Requests WHERE Status IN ('CAPTURED','ACTIVE')` grouped by `OwnerPersonId` and sub-state — Architecture §13 (Real-Time Operational Projections).
- `GetCustomerRequestPortfolio(customerId)`: dynamically queries active requests, open blockers, and recent completions, joined with customer maintenance contract status via `CustomerQueryService` — Architecture §13.
- Both methods execute without cross-module writes; read exclusively from `request.*` and `customer.*` schemas via published query interfaces — Architecture §20.
- Supports FEAT-MGT-002 and FEAT-MGT-004 — Architecture §9.
- Integration tests for workload aggregation by person and customer portfolio queries pass.

Notes: Architecture §13 (Real-Time Operational Projections) is authoritative.

---

### P7-S27

Title: Management Analytics Screens — SCR-MGT-001..003

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the three Management Analytics screens and their API controllers. All management screens are RBAC-protected to the Management role.

Depends On: P7-S26, P3-S11

Repository: Host application / Presentation Layer

Completion Criteria:
- `SCR-MGT-001` (Customer Portfolio) controller: `ManagementAnalyticsService.GetCustomerRequestPortfolio(customerId)`; Customer selector from `CustomerQueryService.ListActiveCustomers`.
- `SCR-MGT-002` (Programmer Performance History) controller: queries `MonthlyCustomerPerformanceSnapshots` and `DailyWorkloadSnapshots` for historical trends per programmer.
- `SCR-MGT-003` (Programmer Workload) controller: `ManagementAnalyticsService.GetProgrammerActiveWorkload(personId?)`.
- All three endpoints are RBAC-protected to the Management role (`AuthorizationService` enforcement) — Architecture §14 (RBAC).
- Integration tests for each analytics endpoint (authorized Management user, unauthorized non-Management user returning 403) pass.

Notes: Architecture §9 (FEAT-MGT-002..004) and §8 (UC-MGT-002..004) are authoritative.

---

### P7-S28

Title: Cross-Cutting Finalization & System Integration Validation

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Verify that all cross-cutting concerns established in P1-S06 are correctly wired through the complete system. Confirm audit logging, exception handling, RBAC, session validation, domain event dispatch, and feed projection consistency across all modules end-to-end. Perform system-wide smoke tests covering all screens and primary user flows.

Depends On: P7-S27, P6-S24

Repository: Host application (validation suite)

Completion Criteria:
- Audit log entries are confirmed present for Request, WorkPackage, and Post state changes (actor `PersonId`, timestamp, previous state) across all modules — Architecture §18.
- Centralized exception handling returns consistent structured error responses for validation failures, domain rule violations, and authentication errors across all endpoints.
- Domain event dispatch confirmed: creating a post creates a `FeedItem`; escalating a request sets `IsException = TRUE` on its `FeedItem`.
- RBAC enforcement verified: `SCR-MGT-*` endpoints reject non-Management users; all other authenticated endpoints accept valid sessions.
- Session lifecycle verified end-to-end: Login → session created → ValidateSession succeeds → Logout → ValidateSession fails.
- Smoke tests pass for all screen endpoints: SCR-AUTH-001, SCR-FEED-001, SCR-POST-001, SCR-REQ-001..005, SCR-WP-001, SCR-PRD-001, SCR-MGT-001..003.
- `FeedProjectionRebuilder.RebuildAll()` executes without error and produces a consistent `FeedItems` set.

Notes: Architecture §18 (Cross-Cutting Concerns) and §20 (Implementation Constraints) are authoritative. This slice produces no new business functionality; it validates the complete assembled system.

---

# 7. Change Log

2026-09-27 — v1.0 — Initial greenfield plan produced from ICS-ARCHITECTURE.md v1.1. 7 phases (P1–P8), 20 slices (S01–S20).

2026-09-27 — v2.0 — Remediation pass (pre-approval). Three targeted goals:
(1) Foundation strengthened: P1 expanded from 2 to 6 slices by separating Database Infrastructure (P1-S03), DI & Module Registration (P1-S04), Domain Event Bus Implementation (P1-S05), and Application Pipeline Foundation (P1-S06) from Core Contracts (P1-S02). Ensures infrastructure concerns are resolved once in the Foundation phase and not repeated across modules.
(2) Business Milestones added: Section 3 (M0–M5) provides explicit business-observable delivery checkpoints aligned to phase completion.
(3) Presentation pulled vertically: screens moved from a single late P8 phase into each module's phase — Login (P3-S12), Product Catalog (P3-S13), Request Screens (P4-S17), Work Package Screen (P5-S20), Feed & Post Screens (P6-S24), Analytics Screens (P7-S27). Enables business feedback at each milestone rather than after all backend phases complete.
Result: 7 phases (P1–P7), 28 slices (S01–S28). No architectural decisions created or reinterpreted.
