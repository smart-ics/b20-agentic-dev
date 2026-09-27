---
Title: ICS Operational System Implementation Plan
Code: ICS
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
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

Greenfield planning mode based on complete target architecture. Scope encompasses the entire target system architecture. The current repository is in pre-implementation state. No existing code exists for any module. The ICS-ARCHITECTURE.md is the sole authoritative technical target state. No architectural decisions are created or re-interpreted by this plan.

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

Scope derives directly from Architecture §21 (Implementation Boundaries) and §22 (Implementation Dependency Graph). No feature sub-scope is applied; the plan covers the entire target system.

---

# 3. Dependencies

## External Dependencies

- Relational database engine (schema-segregated, per §17) must be available in the target environment before any schema migration slices can execute.
- Target runtime and build toolchain must be initialised before application module slices can compile.

## Slice Dependency Rules (per skill)

- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- A dependency is satisfied when the referenced slice has implementation status `IMPLEMENTED` and its required outputs exist in the target repository.
- Review status does not participate in dependency satisfaction.
- Dependencies represent real implementation prerequisites; they are not conceptual or sequential associations.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 — Foundation | NOT-STARTED | NOT-REVIEWED | 0/2 |
| P2 — Core Master Data | NOT-STARTED | NOT-REVIEWED | 0/3 |
| P3 — Identity & Access | NOT-STARTED | NOT-REVIEWED | 0/2 |
| P4 — Request Lifecycle | NOT-STARTED | NOT-REVIEWED | 0/3 |
| P5 — Work Package | NOT-STARTED | NOT-REVIEWED | 0/2 |
| P6 — Post & Feed | NOT-STARTED | NOT-REVIEWED | 0/3 |
| P7 — Management Analytics | NOT-STARTED | NOT-REVIEWED | 0/2 |
| P8 — Presentation & Cross-Cutting | NOT-STARTED | NOT-REVIEWED | 0/3 |

---

# 5. Phases

## P1 — Foundation

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Establish the shared technical foundation (`ICS.Core`) that all modules depend on. No business module is implemented in this phase.

Source: Architecture §21 — Implementation Boundary: **Foundation** (`ICS.Core`, Depends On: None).

---

### P1-S01

Title: Solution Structure & Project Scaffolding

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Create the solution file, project references, and directory structure for the modular monolith. Establish the `ICS.Core` project and all `ICS.Modules.*` project skeletons (empty, buildable) and the host application project. Define inter-project reference graph matching the architecture §21 dependency table.

Depends On: None

Repository: `ICS.Core` / solution root

Completion Criteria:
- Solution file exists and builds successfully with zero errors.
- `ICS.Core` project exists.
- Each module project skeleton exists: `ICS.Modules.Identity`, `ICS.Modules.Organization`, `ICS.Modules.Customer`, `ICS.Modules.Product`, `ICS.Modules.WorkPackage`, `ICS.Modules.Request`, `ICS.Modules.Post`, `ICS.Modules.Analytics`.
- Host application project exists and references all module projects.
- Project reference graph matches architecture §21 dependency table (no circular references).
- `dotnet build` succeeds on the solution.

Notes: This slice produces no business logic. Its output is the compilable project structure that all subsequent slices build into.

---

### P1-S02

Title: Core Interfaces, Base Entities & Domain Event Dispatcher

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement shared technical contracts in `ICS.Core`: base entity types, value object base, domain event interfaces and in-process dispatcher, repository interface contracts, system clock abstraction, and audit logging interfaces. These are the shared technical building blocks that all domain modules implement against.

Depends On: P1-S01

Repository: `ICS.Core`

Completion Criteria:
- Base entity type (with `Id`, `CreatedAt`, `UpdatedAt`) is defined.
- Domain event interface (`IDomainEvent`) is defined.
- In-process domain event dispatcher (`IDomainEventDispatcher`, implementation) is defined and testable.
- Repository interface contract (`IRepository<T>`) or equivalent is defined.
- System clock abstraction (`ISystemClock`) is defined.
- Audit context interface (`IAuditContext`) is defined.
- `CurrentContextProvider` interface (`ICurrentContextProvider` exposing `CurrentUserId`, `CurrentPersonId`, `CurrentRoles`) is defined.
- All types are in the `ICS.Core` namespace and accessible to all module projects.
- Unit tests for the domain event dispatcher pass.

Notes: Architecture §14 (IAM), §18 (Cross-Cutting Concerns), and §7 (`CurrentContextProvider`) are the authoritative sources for required interfaces. No business logic is added.

---

## P2 — Core Master Data

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the three foundational master-data modules — Organization, Customer, and Product — which have no dependencies on each other beyond `ICS.Core`. They may be implemented in parallel.

Source: Architecture §21 — Boundaries: **Organization**, **Customer**, **Product**. All depend only on `ICS.Core` (Organization) or `ICS.Core` + `Organization` read interface (Product).

---

### P2-S03

Title: Organization Module — Domain, Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the complete Organization module: domain entities (`Person`, `Team`, `Role`, `Responsibility`, `TeamMembership`, `RoleAssignment`, `ResponsibilityAssignment`), `OrganizationService` command methods, `OrganizationQueryService` query methods, schema migration for `organization.*` tables, and repository implementations.

Depends On: P1-S02

Repository: `ICS.Modules.Organization`

Completion Criteria:
- All `organization.*` schema tables exist and are created by migration: `Persons`, `Teams`, `Roles`, `Responsibilities`, `TeamMemberships`, `RoleAssignments`, `ResponsibilityAssignments`.
- `OrganizationService` implements: create/update Person, create Team, assign Person to Team, create Role, assign Role to Person, create Responsibility, assign Responsibility to Person, deactivate Person.
- `OrganizationQueryService` implements: `GetPersonById`, `ListActivePersons`, `GetTeamRoster`, `GetPersonRoles`, `GetPersonResponsibilities` (per architecture §7).
- Domain events defined: `PersonCreated`, `PersonDeactivated`, `RoleAssigned`, `RoleRevoked`.
- Repository implementations backed by the relational database use `organization.*` schema exclusively.
- `OrganizationQueryService` is accessible as a published interface to other modules (no internal repository exposure).
- Integration tests confirming query service returns accurate results after service commands.

Notes: Architecture §6 (Module Boundaries — Organization) and §16 (Data Ownership) are authoritative. No tables from other modules are written.

---

### P2-S04

Title: Customer Module — Domain, Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the complete Customer module: domain entities (`Customer`, `CustomerContact`), `CustomerService` command methods, `CustomerQueryService` query methods, schema migration for `customer.*` tables, and repository implementations.

Depends On: P1-S02

Repository: `ICS.Modules.Customer`

Completion Criteria:
- `customer.*` schema tables exist: `Customers`, `CustomerContacts`.
- `CustomerService` implements: create Customer, update Customer master data, create CustomerContact, update CustomerContact, deactivate Customer.
- `CustomerQueryService` implements: `GetCustomerById`, `ListActiveCustomers`, `GetCustomerContacts`, `GetCustomerWithContractStatus` (per architecture §7).
- Repository implementations use `customer.*` schema exclusively.
- `CustomerQueryService` is accessible as a published interface to other modules.
- Integration tests confirming query service returns accurate results after service commands.

Notes: Architecture §6 (Module Boundaries — Customer) and §16 (Data Ownership) are authoritative. Customer module does not own Request or Work Package relationships.

---

### P2-S05

Title: Product Module — Domain, Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the complete Product module: domain entity (`Product`), `ProductService` command methods, `ProductQueryService` query methods, schema migration for `product.*` tables, and repository implementations. Product owner validation reads from `OrganizationQueryService`.

Depends On: P1-S02, P2-S03

Repository: `ICS.Modules.Product`

Completion Criteria:
- `product.*` schema table exists: `Products`.
- `ProductService` implements: `CreateProduct`, `UpdateProduct`, `AssignProductOwner`, `ActivateProduct`, `DeactivateProduct` (per architecture §10).
- `ProductQueryService` implements: `GetProductById`, `GetProductByCode`, `ListActiveProducts`, `ListAllProducts` (per architecture §10).
- `CreateProduct` and `AssignProductOwner` validate owner via `OrganizationQueryService` (no direct cross-schema write).
- Domain events defined: `ProductCreated`, `ProductOwnerChanged`, `ProductActivated`, `ProductDeactivated`.
- `ProductQueryService` is accessible as a published interface.
- Integration tests pass.

Notes: Architecture §10 (Product Module Architecture) is authoritative. Depends on P2-S03 because `AssignProductOwner` and `CreateProduct` query `OrganizationQueryService` to validate the owner exists.

---

## P3 — Identity & Access

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Identity & Access module. Depends on Organization for `Person` status validation and role resolution.

Source: Architecture §21 — Boundary: **Identity & Access** (`ICS.Modules.Identity`, Depends On: `ICS.Core`, `Organization` (Read)).

---

### P3-S06

Title: Identity Module — Domain, Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the complete Identity & Access module: `UserAccount` and `UserSession` entities, `AuthenticationService` (Login, Logout, ValidateSession), `CurrentContextProvider` implementation, schema migration for `identity.*` tables, and repository implementations.

Depends On: P1-S02, P2-S03

Repository: `ICS.Modules.Identity`

Completion Criteria:
- `identity.*` schema tables exist: `UserAccounts`, `UserSessions` (per architecture §14 schemas).
- `AuthenticationService` implements: `Login` (credential verification, Person status check via `OrganizationQueryService`, session creation, token issuance), `Logout` (session invalidation), `ValidateSession` (token validation, expiry check, returns `SecurityContext`) — per architecture §14.
- Password hashing uses Argon2id or bcrypt (per architecture §14).
- `CurrentContextProvider` implementation exposes `CurrentUserId`, `CurrentPersonId`, `CurrentRoles` as ambient per-request context.
- `AuthorizationService` resolves active Roles from `OrganizationQueryService.GetPersonRoles` and evaluates role-based permissions.
- Repository implementations use `identity.*` schema exclusively.
- Integration tests for Login success, Login failure (bad credentials), Login failure (inactive Person), and ValidateSession pass.

Notes: Architecture §14 (Identity & Authentication Architecture) and §7 (`AuthenticationService`, `AuthorizationService`, `CurrentContextProvider`) are authoritative.

---

### P3-S07

Title: Authentication Middleware & Security Context Pipeline

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the HTTP/API authentication middleware that intercepts every incoming request, calls `AuthenticationService.ValidateSession`, populates `CurrentContextProvider`, and rejects unauthenticated requests. Implement RBAC enforcement hooks (`[Authorize(Roles = "...")]` or equivalent) per architecture §18.

Depends On: P3-S06

Repository: `ICS.Modules.Identity` / host application

Completion Criteria:
- Authentication middleware is registered in the host application pipeline.
- Every request without a valid session token returns 401.
- Every request with a valid token populates `CurrentContextProvider` with `CurrentUserId`, `CurrentPersonId`, `CurrentRoles`.
- RBAC enforcement mechanism is in place and tested against Management role restriction.
- Integration tests for authenticated and unauthenticated request scenarios pass.

Notes: Architecture §18 (Cross-Cutting Concerns — Authentication & Security Context, RBAC) is authoritative.

---

## P4 — Request Lifecycle

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Request module — the core operational transactional engine. Depends on Organization, Customer, and Product for validation queries.

Source: Architecture §21 — Boundary: **Request** (`ICS.Modules.Request`, Depends On: `ICS.Core`, `Organization`, `Customer`, `Product`).

---

### P4-S08

Title: Request Module — Domain Model & State Machine

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Request domain layer: `Request` aggregate, `RequestResolution` entity, `RequestAssignment` entity, state machine transitions (`CAPTURED → EVALUATING → ACCEPTED/REJECTED → IN_PROGRESS → ESCALATED → COMPLETED`), and all domain events for state transitions.

Depends On: P1-S02

Repository: `ICS.Modules.Request`

Completion Criteria:
- `Request` aggregate with all state transition methods is implemented (per architecture §7 `RequestService` operations and use cases §8 UC-REQ-001..008).
- State machine enforces valid transitions and rejects invalid ones.
- Domain events defined and emitted: `RequestRecorded`, `RequestAssigned`, `RequestEvaluated`, `RequestAccepted`, `RequestRejected`, `RequestEscalated`, `ManagementDecisionRequested`, `RequestCompleted`.
- `RequestResolution` and `RequestAssignment` entities are implemented.
- Unit tests covering all state transitions (valid and invalid) pass.

Notes: This slice covers domain-only logic with no database or service layer. Clean separation allows parallel implementation of P4-S09.

---

### P4-S09

Title: Request Module — Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement `RequestService` application commands, `RequestQueryService` queries, schema migration for `request.*` tables, and repository implementations. Cross-module validation queries Organization, Customer, and Product via their published query interfaces.

Depends On: P4-S08, P2-S03, P2-S04, P2-S05

Repository: `ICS.Modules.Request`

Completion Criteria:
- `request.*` schema tables exist: `Requests`, `RequestResolutions`, `RequestAssignments` (architecture §17).
- `RequestService` implements: `RecordRequest`, `AssignRequestOwner`, `EvaluateRequest`, `AcceptRequestResponsibility`, `RejectRequest`, `EscalateRequest`, `RequestManagementDecision`, `ReviewRequestCompletion` — per architecture §7, §8 (UC-REQ-001..008).
- `RequestQueryService` implements: `GetRequestById`, `GetRequestStateHistory`, `ListMyAssignedRequests`, `GetFilteredRequestGrid` — per architecture §7, §8 (UC-COL-002..004).
- Cross-module validation: assignee via `OrganizationQueryService`, customer via `CustomerQueryService`, product via `ProductQueryService` — per architecture §15 Integration Design.
- Audit logging records actor `PersonId`, timestamp, and previous state on every state change — per architecture §18.
- Repository implementations use `request.*` schema exclusively.
- Integration tests for the full request lifecycle (Record → Assign → Evaluate → Accept → Complete) pass.

Notes: Architecture §7 (Component Responsibilities), §8 (Use Case Mapping), §15 (Integration Design), §18 (Audit Logging), and §20 (Implementation Constraints — Zero Shared Write Ownership) are authoritative.

---

### P4-S10

Title: Request Module — Escalation & Management Decision Commands

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the escalation, management decision, and re-assignment command paths in `RequestService` together with `ManagementAnalyticsService.GetProgrammerActiveWorkload` real-time query and `RequestService.ReassignRequestOwnership` (UC-MGT-001). These commands build on the Request persistence established in P4-S09.

Depends On: P4-S09

Repository: `ICS.Modules.Request`

Completion Criteria:
- `EscalateRequest` transitions `Request` to `ESCALATED` state, records escalation actor and reason, emits `RequestEscalated`.
- `RequestManagementDecision` records management decision and emits `ManagementDecisionRequested`.
- `ReassignRequestOwnership` (UC-MGT-001) updates `OwnerPersonId`, validates new assignee via `OrganizationQueryService`, emits `RequestAssigned`.
- `GetProgrammerActiveWorkload` real-time aggregation queries `Requests WHERE Status IN ('CAPTURED','ACTIVE')` grouped by `OwnerPersonId` (architecture §13).
- Integration tests for Escalate, ManagementDecision, and Reassign flows pass.
- `GetProgrammerActiveWorkload` returns correctly grouped counts.

Notes: Architecture §13 (Real-Time Operational Projections) and §8 (UC-MGT-001, UC-REQ-006, UC-REQ-007) are authoritative. This slice is separate from P4-S09 to keep slice size manageable; it depends on the core request persistence being in place.

---

## P5 — Work Package

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Work Package module, which depends on Organization, Customer, Product, and Request.

Source: Architecture §21 — Boundary: **Work Package** (`ICS.Modules.WorkPackage`, Depends On: `ICS.Core`, `Organization`, `Customer`, `Product`, `Request`).

---

### P5-S11

Title: Work Package Module — Domain Model

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Work Package domain layer: `WorkPackage` aggregate, `WorkPackageRequest` membership entity, lifecycle state machine (`DRAFT → ACTIVE → CLOSED`), and all domain events.

Depends On: P1-S02

Repository: `ICS.Modules.WorkPackage`

Completion Criteria:
- `WorkPackage` aggregate with lifecycle state machine is implemented (DRAFT, ACTIVE, CLOSED states and transitions).
- `WorkPackageRequest` membership entity is implemented.
- Domain events defined: `WorkPackageCreated`, `WorkPackageActivated`, `WorkPackageClosed`, `WorkPackageOwnerChanged`, `RequestAddedToWorkPackage`, `RequestRemovedFromWorkPackage` — per architecture §11.
- Business Rule 9 (a request cannot be in more than one active work package) is enforceable at the domain level.
- Unit tests for lifecycle transitions and membership invariants pass.

Notes: Architecture §11 (Work Package Module Architecture) is authoritative.

---

### P5-S12

Title: Work Package Module — Application Services & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement `WorkPackageService` commands, `WorkPackageQueryService` queries, schema migration for `workpackage.*` tables, and repository implementations.

Depends On: P5-S11, P2-S03, P2-S04, P2-S05, P4-S09

Repository: `ICS.Modules.WorkPackage`

Completion Criteria:
- `workpackage.*` schema tables exist: `WorkPackages`, `WorkPackageRequests` — architecture §17.
- `WorkPackageService` implements: `CreateWorkPackage`, `UpdateObjective`, `AssignOwner`, `AddRequestToWorkPackage`, `RemoveRequestFromWorkPackage`, `ActivateWorkPackage`, `CloseWorkPackage` — architecture §11.
- `AddRequestToWorkPackage` validates via `RequestQueryService` that the request exists and is not already in another active package (Business Rule 9).
- `WorkPackageQueryService` implements: `GetWorkPackageById`, `ListWorkPackages`, `GetWorkPackageScope`, `GetRequestWorkPackage` — architecture §11.
- Owner validated via `OrganizationQueryService`; customer/product references validated via their respective query services.
- Closing a Work Package does not alter constituent Request lifecycle states.
- Repository implementations use `workpackage.*` schema exclusively.
- Integration tests for full work package lifecycle and request membership pass.

Notes: Architecture §11 (Work Package Module Architecture) and §15 (Integration Design) are authoritative.

---

## P6 — Post & Feed

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Post module including human and system post authoring, comments, reactions, post references, and the Feed materialized read model projection.

Source: Architecture §21 — Boundary: **Post & Feed** (`ICS.Modules.Post`, Depends On: `ICS.Core`, Domain Events from all modules).

---

### P6-S13

Title: Post Module — Domain Model, Post Authoring & Persistence

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Post domain layer and persistence: `Post`, `Comment`, `Reaction`, `PostReference` entities, `PostService` commands for human post authoring and system post recording, schema migration for `post.*` tables (excluding `FeedItems`), and repository implementations.

Depends On: P1-S02, P4-S09

Repository: `ICS.Modules.Post`

Completion Criteria:
- `post.*` schema tables exist: `Posts`, `Comments`, `Reactions`, `PostReferences` — architecture §17.
- `PostService` implements: `CreateOperationalPost` (human authored), `RecordSystemPost` (system generated), `PostComment`, `AddReaction`, `RemoveReaction`, `TogglePostVisibility`, `ArchivePost` — architecture §7, §8 (UC-FCOL-001..003, UC-COL-001).
- `PostQueryService` implements: `GetPostThreadDetails`, `GetFullComments`, `GetReactionList` — architecture §7.
- Domain events defined and emitted: `PostCreated`, `CommentAdded`, `ReactionAdded`, `ReactionRemoved`, `PostVisibilityChanged`, `PostArchived` — architecture §12 (Update Triggers).
- Soft-delete/archive only; no physical purge — architecture §20 (Permanent Retention).
- Repository implementations use `post.*` schema exclusively.
- Integration tests for post authoring, commenting, and reaction flows pass.

Notes: Architecture §7, §8, §18, and §20 are authoritative. `FeedItems` projection is implemented in P6-S14.

---

### P6-S14

Title: Feed Projection — FeedItems Table & FeedProjectionHandler

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the `FeedItems` materialized read model: create `FeedItems` table schema migration, implement `FeedProjectionHandler` subscribing to in-process domain events (`PostCreated`, `CommentAdded`, `ReactionAdded`, `ReactionRemoved`, `PostVisibilityChanged`, `PostArchived`), and the `FeedProjectionRebuilder` idempotent rebuild routine.

Depends On: P6-S13

Repository: `ICS.Modules.Post`

Completion Criteria:
- `FeedItems` table exists in `post.*` schema with all columns per architecture §12 schema definition.
- `FeedProjectionHandler` handles all five event types and updates `FeedItems` correctly within the same database transaction scope as the originating command — architecture §12 (Synchronization Guarantee).
- `FeedProjectionRebuilder.RebuildAll()` truncates and fully regenerates `FeedItems` from authoritative `Posts`, `PostReferences`, `Comments`, `Reactions` — architecture §12 (Rebuild Strategy).
- `FeedQueryService.GetFeed(filter, pagination)` executes the single-table indexed query per architecture §12 (Query Semantics).
- `FeedItems` is strictly read-only for all consumers; no application code writes to `FeedItems` except `FeedProjectionHandler` and `FeedProjectionRebuilder`.
- Integration tests: post creation inserts correct `FeedItem`; comment increments `CommentCount`; reaction updates `ReactionCountsJson`; visibility change updates `Visibility`; archive sets `Status=ARCHIVED`.

Notes: Architecture §12 (Feed Architecture) is authoritative and fully specifies the projection table schema, update triggers, synchronization guarantee, query semantics, and rebuild strategy.

---

### P6-S15

Title: Feed Query Service — Filtered Feed Queries & Exception Detection

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the full `FeedQueryService` filtering capabilities: filter by Customer, Product, Team, Exception badge (`IsException`), and pagination. Implement exception flag population logic in `FeedProjectionHandler` for escalation, rejection, and stalled requests.

Depends On: P6-S14

Repository: `ICS.Modules.Post`

Completion Criteria:
- `FeedQueryService.GetFeed` supports all filter dimensions: `CustomerId`, `ProductId`, `ExceptionsOnly` — architecture §12 (Query Semantics).
- `IsException` is set to `TRUE` and `ExceptionType` populated (`ESCALATION`, `REJECTION`, `STALLED`) by `FeedProjectionHandler` when processing corresponding domain events (e.g. `RequestEscalated`, `RequestRejected`).
- Pagination (`PageSize`, `Offset`) returns correctly bounded result sets.
- `FeedQueryService` supports UC-FCOL-005 (Filter Operational Feed), UC-AWR-001 (Observe Operational Feed), UC-AWR-002 (Discover Request via Feed), UC-AWR-003 (Monitor Exceptions) — architecture §8.
- Integration tests for each filter dimension (no filter, customer filter, product filter, exceptions-only) pass.

Notes: Architecture §12 (Query Semantics, Update Triggers) and §8 (UC-FCOL-005, UC-AWR-001..003) are authoritative.

---

## P7 — Management Analytics

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Management Analytics module: snapshot tables, scheduled snapshot job, and real-time customer portfolio query.

Source: Architecture §21 — Boundary: **Management Analytics** (`ICS.Modules.Analytics`, Depends On: `ICS.Core`, `Request`, `Organization`, `Customer`).

---

### P7-S16

Title: Analytics Module — Snapshot Tables & Snapshot Job

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Analytics schema migration for `analytics.*` tables and the `AnalyticsSnapshotJob` scheduled background worker.

Depends On: P1-S02, P4-S09, P2-S03, P2-S04

Repository: `ICS.Modules.Analytics`

Completion Criteria:
- `analytics.*` schema tables exist: `DailyWorkloadSnapshots`, `MonthlyCustomerPerformanceSnapshots` — per architecture §13 storage schema.
- `AnalyticsSnapshotJob` implements:
  - Daily snapshot (23:59:59): computes end-of-day workload metrics per active `Person` from `Requests` and inserts into `DailyWorkloadSnapshots`.
  - Monthly snapshot (1st of month, 00:05:00): aggregates preceding month metrics per `Customer` and inserts into `MonthlyCustomerPerformanceSnapshots`.
  - Unique constraint per `(SnapshotDate, PersonId)` and `(YearMonth, CustomerId)` respected — architecture §13.
- `ManagementAnalyticsService.RecomputeSnapshots(startDate, endDate)` implements idempotent backfill — architecture §13.
- Snapshot records treated as immutable after insertion — architecture §13 (Immutability).
- Integration test: trigger daily job, verify `DailyWorkloadSnapshots` row inserted with correct counts.

Notes: Architecture §13 (Snapshot Storage Tables, Refresh & Execution Model) is authoritative.

---

### P7-S17

Title: Analytics Module — Real-Time Workload & Customer Portfolio Queries

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement `ManagementAnalyticsService` real-time dynamic query methods: `GetProgrammerActiveWorkload` (full per-person aggregation for SCR-MGT-003) and `GetCustomerRequestPortfolio` (active requests, open blockers, recent completions for SCR-MGT-001).

Depends On: P7-S16

Repository: `ICS.Modules.Analytics`

Completion Criteria:
- `GetProgrammerActiveWorkload(personId?)` dynamically aggregates `Requests WHERE Status IN ('CAPTURED','ACTIVE')` grouped by `OwnerPersonId` and sub-state — architecture §13 (Real-Time Operational Projections).
- `GetCustomerRequestPortfolio(customerId)` dynamically queries active requests, open blockers, and recent completions, joined with customer maintenance contract status via `CustomerQueryService` — architecture §13.
- Both methods execute in < 20ms against the operational `request.*` schema (no cross-module writes) — architecture §13 (Justification).
- Supports FEAT-MGT-002, FEAT-MGT-004 — architecture §9 (Feature Mapping).
- Integration tests for workload aggregation and customer portfolio queries pass.

Notes: Architecture §13 (Real-Time Operational Projections) is authoritative.

---

## P8 — Presentation & Cross-Cutting

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Presentation Layer (API controllers, ViewModels, UI screens) and remaining cross-cutting concerns (exception handling, validation pipeline, audit logging wire-up).

Source: Architecture §5 (System Structure — Presentation Layer), §18 (Cross-Cutting Concerns).

---

### P8-S18

Title: Authentication Presentation — Login Screen & Session API

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement `SCR-AUTH-001` (Login Screen) and the authentication API controller. Wires `AuthenticationService.Login` and `Logout` to the HTTP endpoint, sets the authentication cookie/token, handles login failures (bad credentials, inactive account), and redirects to `SCR-FEED-001` on success.

Depends On: P3-S07

Repository: host application / Presentation Layer

Completion Criteria:
- `SCR-AUTH-001` login screen renders username/password fields.
- POST login endpoint calls `AuthenticationService.Login`, sets session cookie/token on success.
- Login failure returns appropriate error message (invalid credentials vs. account locked vs. inactive person).
- Successful login redirects to `SCR-FEED-001`.
- Logout endpoint calls `AuthenticationService.Logout`, invalidates session cookie.
- Integration tests for login success and failure scenarios pass.

Notes: Architecture §14 (UI Boundary — SCR-AUTH-001) is authoritative.

---

### P8-S19

Title: Operational Feed Presentation — SCR-FEED-001 & Post Screens

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement the Feed screen (`SCR-FEED-001`) API/ViewModel layer, the Post detail modal (`SCR-POST-001`), and the feed filter controls. Wires `FeedQueryService.GetFeed` and `PostQueryService` to presentation controllers.

Depends On: P6-S15, P3-S07

Repository: host application / Presentation Layer

Completion Criteria:
- `SCR-FEED-001` feed screen controller returns paginated `FeedItems` with filter support (Customer, Product, Exceptions).
- Exception badge indicator visible on feed cards where `IsException = TRUE`.
- `SCR-POST-001` post detail modal controller returns full post thread (comments, reactions) via `PostQueryService`.
- Post authoring form (`CreateOperationalPost`) invokes `PostService.CreateOperationalPost`.
- Comment and Reaction endpoints invoke `PostService.PostComment`, `PostService.AddReaction`.
- Navigation from feed card to Request detail is wired (UC-FCOL-004) — architecture §8.
- Integration tests for feed listing, filtering, and post detail pass.

Notes: Architecture §9 (Feature Mapping — FEAT-AWR-001, FEAT-FCOL-001..003, FEAT-FCOL-005) and §8 (UC-AWR-001..003, UC-FCOL-001..005) are authoritative.

---

### P8-S20

Title: Request, Work Package, Product & Management Presentation Screens

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

Objective: Implement remaining presentation screens and API controllers: Request screens (`SCR-REQ-001..005`), Work Package screen (`SCR-WP-001`), Product catalog screen (`SCR-PRD-001`), Management analytics screens (`SCR-MGT-001..003`), and centralized exception handling and validation pipeline.

Depends On: P4-S10, P5-S12, P7-S17, P8-S18

Repository: host application / Presentation Layer

Completion Criteria:
- `SCR-REQ-001` (Request List) controller wires `RequestQueryService.GetFilteredRequestGrid`.
- `SCR-REQ-002` (Create Request) controller wires `RequestService.RecordRequest` with Customer, Product, and Organization dropdowns from their respective `QueryService` interfaces.
- `SCR-REQ-003` (Request Detail) controller wires `RequestQueryService.GetRequestById` and all state-transition commands (`Assign`, `Evaluate`, `Accept`, `Reject`, `Escalate`, `ManagementDecision`, `Complete`).
- `SCR-REQ-004` (My Requests) controller wires `RequestQueryService.ListMyAssignedRequests`.
- `SCR-REQ-005` (Search) controller wires `RequestQueryService` search.
- `SCR-WP-001` (Work Package) controller wires `WorkPackageService` and `WorkPackageQueryService`.
- `SCR-PRD-001` (Product Catalog) controller wires `ProductService` and `ProductQueryService`.
- `SCR-MGT-001` (Customer Portfolio) controller wires `ManagementAnalyticsService.GetCustomerRequestPortfolio`.
- `SCR-MGT-002` (Programmer Performance History) controller wires `ManagementAnalyticsService` snapshot queries.
- `SCR-MGT-003` (Programmer Workload) controller wires `ManagementAnalyticsService.GetProgrammerActiveWorkload`.
- `SCR-MGT-*` endpoints are RBAC-protected to Management role — architecture §14 (RBAC).
- Centralized exception handling and input validation pipeline is operational for all endpoints.
- Audit logging confirmed active for Request, WorkPackage, and Post state changes.
- Smoke tests for all screen endpoints pass.

Notes: Architecture §9 (Feature Mapping), §8 (Use Case Mapping), §17 (Screen References via Navigation/UI-Layout), and §18 (Cross-Cutting Concerns) are authoritative.

---

# 6. Change Log

2026-09-27 — v1.0 — Initial greenfield plan produced from ICS-ARCHITECTURE.md v1.1. Phases and slices derived directly from Architecture §21 (Implementation Boundaries) and §22 (Implementation Dependency Graph). No architectural decisions were created or reinterpreted by this plan.
