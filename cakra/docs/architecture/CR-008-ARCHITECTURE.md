---
Title: Person Management - Organizational Person Create, Update, Activate, Deactivate Architecture
Code: CR-008
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-05
---

# 1. Overview

This architecture defines the technical realization for Change Request `CR-008`: Organizational Person Management — enabling authenticated Administrators to create, update, activate, and deactivate organizational Person records through a dedicated UI and REST API.

It realizes [FEAT-ORG-001-manage-persons.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-ORG-001-manage-persons.md) and technical gap closures from [CR-008-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-008-FEASIBILITY-ASSESSMENT.md), establishing:
1. New REST API write and list-all endpoints in `OrganizationController.cs` (`/api/v1/organization/persons`) guarded with role-based authorization (`Administrator`, `Admin`).
2. A new `ActivatePersonCommand` (MediatR) in `Cakra.Modules.Organization`, since only the synchronous service overload exists today.
3. A new `ListAllPersonsAsync` query method in `IOrganizationQueryService` to support the management screen displaying both active and inactive persons.
4. Frontend screen `SCR-ORG-001` (`PersonManagementView.vue`) and modal `SCR-ORG-002` (`PersonModal.vue`) in `Cakra.Web`.
5. Role-gated Administration sidebar navigation in `App.vue` and a client-side route guard in `router/index.ts` for route `/admin/persons`.

---

# 2. Architectural Basis

## Business Context

- DOMAIN: [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md)
- FEATURE: [FEAT-ORG-001-manage-persons.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-ORG-001-manage-persons.md)
- ISSUE: [CR-008-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-008-ISSUE.md)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md) (Section 5: System Structure, Section 6: Module Boundaries, Section 7: Component Responsibilities, Section 14: Identity & Authentication Architecture, Section 19: API & Frontend Boundaries)
- REFERENCE ARCHITECTURE: [CR-007-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-007-ARCHITECTURE.md) (User Management pattern)
- REFERENCE ARCHITECTURE: [CR-002-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-002-ARCHITECTURE.md) (Customer Management write-endpoint pattern)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-008-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-008-FEASIBILITY-ASSESSMENT.md) (Status: `NOT-READY` — pending Architect approval)

This architecture consumes and realizes the approved decisions:
- `GAP-001`: Add `POST`, `PUT`, `PUT /activate`, `PUT /deactivate` endpoints to `OrganizationController.cs` guarded with `[Authorize(Roles = "Administrator,Admin")]`.
- `GAP-002`: Create `PersonManagementView.vue` (`SCR-ORG-001`) and `PersonModal.vue` (`SCR-ORG-002`) in `Cakra.Web`.
- `GAP-003`: Add "Person Management" navigation link and route `/admin/persons` in `App.vue` and `router/index.ts`.
- `GAP-004`: FEATURE artifact `FEAT-ORG-001` created.
- `GAP-005`: Update screen inventory and navigation docs with `SCR-ORG-001` and `SCR-ORG-002`.
- `GAP-006`: Add `ListAllPersonsAsync` to `IOrganizationQueryService`; add `GET /api/v1/organization/persons/all` endpoint; do NOT modify existing `GET /persons`.
- `GAP-007`: Create `persons.ts` frontend API client module.
- `GAP-008`: Add unit and integration tests for Person management.
- `OQ-001`: Route `/admin/persons`, screen codes `SCR-ORG-001` and `SCR-ORG-002`.
- `OQ-002`: New endpoint `GET /persons/all`; existing `GET /persons` unchanged.
- `OQ-003`: Management screen displays all persons with optional client-side status filter.
- `OQ-004`: No cross-domain deactivation guard; domain boundaries preserved.

---

# 3. Scope

## Included

1. **Backend Domain Layer (`Cakra.Modules.Organization`)**:
   - New `ActivatePersonCommand` and `ActivatePersonCommandHandler` (MediatR `IRequest<Person>` + validator).
   - Registration of the new command handler in `OrganizationModule.cs`.
2. **Backend Application / Query Layer (`Cakra.Modules.Organization`)**:
   - New `ListAllPersonsAsync` method in `IOrganizationQueryService` and `OrganizationQueryService` implementation, querying `[organization].[Persons]` for all records (active and inactive) ordered by last name, first name.
3. **Backend REST API Layer (`Cakra.Api`)**:
   - Modify `OrganizationController.cs`: inject `IMediator` alongside existing `IOrganizationQueryService`; decorate controller or specific write methods with `[Authorize(Roles = "Administrator,Admin")]`.
   - `POST /api/v1/organization/persons`: Creates a new Person (dispatches `CreatePersonCommand`).
   - `PUT /api/v1/organization/persons/{id:guid}`: Updates Person identity attributes (dispatches `UpdatePersonCommand`).
   - `PUT /api/v1/organization/persons/{id:guid}/activate`: Reactivates an inactive Person (dispatches new `ActivatePersonCommand`).
   - `PUT /api/v1/organization/persons/{id:guid}/deactivate`: Deactivates an active Person (dispatches `DeactivatePersonCommand`).
   - `GET /api/v1/organization/persons/all`: Returns all persons (active and inactive) via `ListAllPersonsAsync`.
   - Request/response DTOs: `CreatePersonRequest`, `UpdatePersonRequest` (matching command signatures).
4. **Frontend Presentation Layer (`Cakra.Web`)**:
   - API client module `src/api/persons.ts`.
   - Navigation update in `App.vue`: "Person Management" link in Administration section, `v-if="isAdmin"`, `data-testid="nav-person-management-link"`.
   - Route registration in `router/index.ts`: `/admin/persons` with `meta: { requiresAuth: true, requiresRole: 'Administrator', screenId: 'SCR-ORG-001' }`.
   - `PersonManagementView.vue` (`SCR-ORG-001`): displays searchable table of all persons with status badges and action buttons, plus a status filter dropdown.
   - `PersonModal.vue` (`SCR-ORG-002`): modal supporting Add (create) and Edit (update) modes with First Name, Last Name, Email fields and client-side validation.
5. **Automated Testing**:
   - Unit tests for `ActivatePersonCommandHandler` and `CreatePersonCommandHandler` in `Cakra.Tests.Unit/Organization/`.
   - Integration tests for `OrganizationController` write endpoints and role-based authorization in `Cakra.Tests.Integration/Api/OrganizationControllerTests.cs`.
6. **Documentation**:
   - Update [screen-inventory.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/screen-inventory.md) and [00-screen-inventory.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/00-screen-inventory.md).
   - Update [feature-catalog.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/feature-catalog.md) and [feature-traceability.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/feature-traceability.md).

## Excluded

- SQL database migrations. The `[organization].[Persons]` table already exists with all required columns (`Id`, `FirstName`, `LastName`, `Email`, `Status`, `CreatedAt`, `UpdatedAt`) and `PersonRepository` already implements all persistence methods (`GetAllAsync`, `AddAsync`, `UpdateAsync`, `UpdateStatusAsync`).
- Modifications to the `Person` aggregate entity or its lifecycle semantics.
- Modifications to existing read-only dropdown selector usage in `UserAccountModal.vue`, `WorkPackageView.vue`, `CreateRequestModal.vue`.
- Cross-domain business guards preventing deactivation of Persons referenced by active UserAccounts, Requests, or Work Packages (per OQ-004 decision).
- Changes to Identity module or UserAccount lifecycle.

---

# 4. Technical Decisions

## TD-001: Command-Based Write Endpoints via MediatR

Person write operations in `OrganizationController.cs` will be dispatched through MediatR commands, matching the pattern established by `CreatePersonCommand`, `UpdatePersonCommand`, and `DeactivatePersonCommand` (which already exist with validators and handlers). A new `ActivatePersonCommand` (and its validator and handler) will be created because activation is currently only available as a synchronous service overload — the command pattern is required for consistency with the existing write command structure.

### Command Mapping

| HTTP Method + Endpoint | MediatR Command | Result |
|---|---|---|
| `POST /api/v1/organization/persons` | `CreatePersonCommand(FirstName, LastName, Email)` | `Person` → mapped to `PersonDto` |
| `PUT /api/v1/organization/persons/{id:guid}` | `UpdatePersonCommand(PersonId, FirstName, LastName, Email)` | `Person` → mapped to `PersonDto` |
| `PUT /api/v1/organization/persons/{id:guid}/activate` | `ActivatePersonCommand(PersonId)` | `Person` → mapped to `PersonDto` |
| `PUT /api/v1/organization/persons/{id:guid}/deactivate` | `DeactivatePersonCommand(PersonId)` | `Person` → mapped to `PersonDto` |

### Rationale
Ensures all Person mutations flow through the same validation, service-layer business rules (email uniqueness, domain event dispatch), and transaction boundaries as existing domain operations. MediatR commands are the established integration seam between the API layer and domain services.

## TD-002: Dual-Layer Role Authorization

To strictly enforce that only Administrators can manage Persons (consistent with CR-007):

1. **Server-Side Authorization**: The `OrganizationController` class or the write methods are decorated with `[Authorize(Roles = "Administrator,Admin")]`. Unauthorized callers receive RFC 7807 `403 Forbidden`.
2. **Client-Side Menu Affordance**: In `App.vue`, the "Person Management" navigation link is wrapped in `v-if="isAdmin"`, where `isAdmin = computed(() => authStore.roles.includes('Administrator') || authStore.roles.includes('Admin'))`.
3. **Client-Side Route Guard**: In `router/index.ts`, the `/admin/persons` route has `meta: { requiresAuth: true, requiresRole: 'Administrator', screenId: 'SCR-ORG-001' }`, and `router.beforeEach` evaluates `to.meta.requiresRole` to redirect non-administrators to `/feed`.

### Rationale
Prevents both API-level and UI-level unauthorized access. The read-only Person lookup endpoints (`GET /api/v1/organization/persons/active`, `GET /api/v1/organization/persons`) remain accessible to all authenticated users (as they are consumed by read-only dropdown selectors in `UserAccountModal.vue` and other screens).

## TD-003: List-All-Persons Query Endpoint

A new endpoint `GET /api/v1/organization/persons/all` is added, backed by a new `ListAllPersonsAsync` method on `IOrganizationQueryService` and `OrganizationQueryService`. This returns all Person records (both `ACTIVE` and `INACTIVE`) ordered by last name and first name.

The existing `GET /api/v1/organization/persons` (which returns only active persons) is preserved unchanged to avoid breaking existing dropdown selectors.

### Rationale
The Person Management screen requires visibility into both active and inactive persons. The `[organization].[Persons]` table already supports this via `PersonRepository.GetAllAsync`, which returns all records ordered by name. Reusing the existing repository method avoids new SQL and keeps the query surface minimal.

## TD-004: Frontend Component Architecture

- `PersonManagementView.vue` (`SCR-ORG-001`) acts as the stateful orchestrator: maintains person list state, search query, status filter, and modal visibility.
- `PersonModal.vue` (`SCR-ORG-002`) receives `show`, `mode` (`'create' | 'edit'`), and optional `person` prop. It emits `close` and `saved` events.
- On modal open, `PersonModal.vue` does NOT need a Person dropdown (it captures identity attributes directly — First Name, Last Name, Email).
- Error handling follows the `AxiosError` + RFC 7807 `ProblemDetailsPayload` pattern established in `CreateCustomerModal.vue`.
- Client-side status filtering: the management table displays all persons fetched from `GET /persons/all`; an optional dropdown filter (All/Active/Inactive) filters client-side.

### Rationale
Matches the established component architecture from `UserManagementView.vue` (SCR-USR-001) and `UserAccountModal.vue` (SCR-USR-002), keeping the frontend pattern consistent.

## TD-005: PersonDto Reuse

No new DTO is introduced. The existing `PersonDto` (with `Id`, `FirstName`, `LastName`, `Email`, `Status`, `FullName`, `IsActive`, `CreatedAt`, `UpdatedAt`) is sufficient for API responses in list and detail views. The write endpoints return `PersonDto` as the response body, consistent with the `CustomersController` pattern.

### Rationale
Avoids unnecessary data model proliferation; `PersonDto` already exposes all fields needed for the management UI.

## TD-006: Email Uniqueness Enforcement

Email uniqueness is enforced at the application service layer (`OrganizationService.CreatePersonAsync` and `UpdatePersonAsync` check via `GetByEmailAsync` and throw `InvalidOperationException` on conflict). The controller maps `InvalidOperationException` to RFC 7807 `409 Conflict`. No database-level unique constraint is added (none exists currently; service-level enforcement is the established pattern).

### Rationale
Consistent with the existing codebase pattern; adding a database migration is out of scope per the Issue's description ("the gap is the REST API write surface and the frontend presentation layer").

## TD-007: Person Management Screen Placement

"Person Management" appears as a second link in the existing Administration sidebar section in `App.vue`, alongside "User Management". Both links are conditionally rendered with `v-if="isAdmin"`.

### Rationale
The Issue requests the Person management UI to be "reachable through dedicated navigation" in the Administration section "consistent with existing administration screens."

---

# 5. Component Responsibilities

| Component | Responsibility |
|---|---|
| `OrganizationController` (`Cakra.Api`) | Exposes REST API endpoints under `/api/v1/organization/persons`: read endpoints (`GET /persons/active`, `GET /persons`, `GET /persons/{id}`, new `GET /persons/all`) and write endpoints (`POST /persons`, `PUT /persons/{id}`, `PUT /persons/{id}/activate`, `PUT /persons/{id}/deactivate`). Write endpoints guarded by `[Authorize(Roles = "Administrator,Admin")]`. Returns RFC 7807 `ProblemDetails` on errors. |
| `CreatePersonCommand` + Handler | MediatR command to create a new Person with identity attributes; validates via `CreatePersonCommandValidator` and delegates to `IOrganizationService.CreatePersonAsync`. Already exists. |
| `UpdatePersonCommand` + Handler | MediatR command to update Person identity attributes; validates via `UpdatePersonCommandValidator` and delegates to `IOrganizationService.UpdatePersonAsync`. Already exists. |
| `DeactivatePersonCommand` + Handler | MediatR command to deactivate a Person (status `INACTIVE`); validates via `DeactivatePersonCommandValidator` and delegates to `IOrganizationService.DeactivatePersonAsync`. Already exists. |
| `ActivatePersonCommand` + Handler (NEW) | MediatR command to activate a Person (status `ACTIVE`); validates PersonId is non-empty and delegates to `IOrganizationService.ActivatePersonAsync`. Must be created per TD-001. |
| `IOrganizationService` / `OrganizationService` (`Cakra.Modules.Organization`) | Owns Person aggregate mutations: `CreatePersonAsync` (email uniqueness), `UpdatePersonAsync` (email uniqueness on update), `DeactivatePersonAsync`, `ActivatePersonAsync`. Already implemented. |
| `IOrganizationQueryService` / `OrganizationQueryService` (`Cakra.Modules.Organization`) | Read-only query service for Person projections. New `ListAllPersonsAsync` method to be added per TD-003. |
| `PersonRepository` (`Cakra.Modules.Organization`) | Dapper persistence for `[organization].[Persons]`. Already implements `GetAllAsync`, `GetByEmailAsync`, `AddAsync`, `UpdateAsync`, `UpdateStatusAsync`. No changes required. |
| `PersonDto` (`Cakra.Modules.Organization`) | Read-model DTO for Person (Id, FirstName, LastName, Email, Status, FullName, IsActive, CreatedAt, UpdatedAt). Already exists. No changes required. |
| `persons.ts` (`Cakra.Web/src/api`) | Frontend Axios client communicating with `/api/v1/organization/persons` and `/api/v1/organization/persons/all` endpoints. Must be created. |
| `App.vue` (`Cakra.Web`) | Renders sidebar navigation shell with Administration section; conditionally shows "Person Management" link via `v-if="isAdmin"`; renders `currentScreenTitle` for the active route. Must be updated. |
| `router/index.ts` (`Cakra.Web`) | Configures route `/admin/persons` with role guard metadata (`requiresRole: 'Administrator'`); evaluates `router.beforeEach` for unauthorized access. Must be updated. |
| `PersonManagementView.vue` (`Cakra.Web`) | Screen `SCR-ORG-001`: displays searchable table of all persons with status badges and action buttons (Edit, Activate, Deactivate); manages modal state and status filter. Must be created. |
| `PersonModal.vue` (`Cakra.Web`) | Modal dialog `SCR-ORG-002`: provides form inputs for First Name, Last Name, and Email in create/edit modes; emits `close` and `saved` events. Must be created. |

---

# 6. Integration Design

| Source | Target | Communication | Purpose |
|---|---|---|---|
| `PersonManagementView.vue` | `src/api/persons.ts` | HTTP GET | Fetches all persons from `GET /api/v1/organization/persons/all` for display. |
| `PersonManagementView.vue` | `PersonModal.vue` | Component Event (`saved`, `close`) | Triggers create/edit modal; receives saved person to refresh list. |
| `PersonModal.vue` | `src/api/persons.ts` | HTTP POST / PUT | Submits Person creation or update payload. |
| `PersonManagementView.vue` | `src/api/persons.ts` | HTTP PUT | Invokes activate/deactivate on a Person row. |
| `OrganizationController` | `IMediator` | In-Process Dispatch | Dispatches `CreatePersonCommand`, `UpdatePersonCommand`, `ActivatePersonCommand`, `DeactivatePersonCommand`. |
| `ActivatePersonCommandHandler` | `IOrganizationService` | In-Process Service Call | Delegates to `ActivatePersonAsync`. |
| `CreatePersonCommandHandler` | `IOrganizationService` | In-Process Service Call | Delegates to `CreatePersonAsync`. |
| `OrganizationController` | `IOrganizationQueryService` | In-Process Service Call | Serves `GET /persons`, `GET /persons/active`, `GET /persons/{id}`, and new `GET /persons/all` read endpoints. |
| `OrganizationQueryService` | `[organization].[Persons]` | Parameterized SQL (Dapper) | Executes SELECT queries against SQL Server for person projections. |
| `App.vue` | `stores/auth.ts` | Reactive State | Evaluates `isAdmin` to render Administration menu links. |
| `router/index.ts` | `stores/auth.ts` | In-Process Query | Evaluates user roles during `beforeEach` route transitions to `/admin/persons`. |
| `OrganizationController` (write methods) | ASP.NET Core Authorization | Policy Evaluation | Enforces `[Authorize(Roles = "Administrator,Admin")]` on write endpoints; returns 403 for unauthorized callers. |

---

# 7. Data Ownership

| Data | Authoritative Owner | Schema / Location |
|---|---|---|
| Organizational Person Identity & Lifecycle | `Organization` Module (`Cakra.Modules.Organization`) | `[organization].[Persons]` |
| Person Domain Events (`PersonCreated`, `PersonDeactivated`, `PersonActivated`) | `Organization` Module (`Cakra.Modules.Organization`) | In-process event dispatch via `IDomainEventDispatcher` |
| Navigation & Screen Metadata | `Cakra.Web` (frontend) + Documentation (`screen-inventory.md`, `navigation-map.md`) | Client-side router configuration |

No new tables. No modified tables.

---

# 8. Database Design

## New Tables

None required.

## Modified Tables

None required. Existing table `[organization].[Persons]` already has:
- `Id` (uniqueidentifier, PK)
- `FirstName` (nvarchar(100))
- `LastName` (nvarchar(100))
- `Email` (nvarchar(255))
- `Status` (nvarchar(20), default `'ACTIVE'`)
- `CreatedAt` (datetime2)
- `UpdatedAt` (datetime2, nullable)

## Relationships

- The `Person` aggregate is the authoritative entity within `[organization].[Persons]`.
- Other domains (Identity, Request, Work Package, Post) reference Person records via `PersonId` (GUID) with logical references but no cross-schema physical foreign keys (Architecture §20). Deactivating a Person only changes its `Status` to `INACTIVE`; no cascading changes to referencing tables.

## Migration Considerations

No SQL migrations required. All persistence is handled by the existing `PersonRepository` methods (`GetAllAsync`, `GetByEmailAsync`, `AddAsync`, `UpdateAsync`, `UpdateStatusAsync`), which are already implemented and registered.

---

# 9. Cross-Cutting Concerns

## Security & RBAC

- All Person write endpoints (`POST`, `PUT`, `PUT /activate`, `PUT /deactivate`) are guarded with `[Authorize(Roles = "Administrator,Admin")]`.
- Read-only endpoints (`GET /persons/active`, `GET /persons`, `GET /persons/{id}`, `GET /persons/all`) follow the existing AuthorizationController pattern: `[Authorize]` (authenticated users only).
- Non-administrators attempting API write requests receive HTTP `403 Forbidden`.
- Non-administrators attempting direct URL navigation to `/admin/persons` are redirected to `/feed` via the client-side route guard.
- Plaintext passwords and sensitive credential data are not part of this change (Person management does not involve credentials).

## Auditability & Traceability

- Person creation modifies `CreatedAt` and dispatches `PersonCreated` domain event (already implemented in `OrganizationService`).
- Person updates modify `UpdatedAt` timestamp.
- Person activation/dedeactivation modifies `UpdatedAt` and dispatches `PersonDeactivated` / `PersonActivated` domain events (already implemented for deactivation; activation currently does not dispatch an event — this is a minor domain behavior gap that does not block the architecture).

## Observability & Error Handling

- All endpoint failures return RFC 7807 `ProblemDetails` with appropriate status codes (`400 Bad Request` for validation errors, `401 Unauthorized`, `403 Forbidden`, `404 Not Found` for person not found, `409 Conflict` for duplicate email).
- Error handling pattern follows `CustomersController.cs` and `UsersController.cs` (ProblemDetails with `errorCode` and `traceId` extensions).

## Frontend Consistency

- `PersonManagementView.vue` and `PersonModal.vue` use `<script setup lang="ts">`, Vue Router 4, Pinia auth store, and Bootstrap 5 styling consistent with `UserManagementView.vue` and `CustomerPortfolioView.vue`.
- API client (`persons.ts`) follows the `AxiosError` + `ProblemDetailsPayload` error extraction pattern from `customers.ts` and `users.ts`.

---

# 10. Implementation Constraints

1. **Explicit SQL**: Persistence must continue using Dapper with explicit parameterized SQL (no EF Core). The existing `PersonRepository` already provides all required methods.
2. **Schema Isolation**: No SQL JOIN queries across schemas. Person read-only enrichment is in-process.
3. **Modular Boundary**: Person write commands must flow through `IOrganizationService`; the controller must NOT call `IPersonRepository` directly.
4. **MediatR Command Pattern**: All Person mutations must use MediatR commands (`CreatePersonCommand`, `UpdatePersonCommand`, `ActivatePersonCommand`, `DeactivatePersonCommand`) — not direct service calls — to ensure validation handlers and domain event dispatch are consistently applied.
5. **Vue 3 Composition API & TypeScript**: Frontend components must use `<script setup lang="ts">`, Vue Router 4, Pinia auth store, and Bootstrap 5 styling.
6. **REST Compatibility**: The existing `GET /api/v1/organization/persons` (returning active persons) and `GET /persons/active` endpoints must not be modified — a new `GET /persons/all` endpoint is added.
7. **RBAC Consistency**: Both `Administrator` and `Admin` role strings must be recognized (per OQ-001 decision).

---

# 11. Acceptance Conditions

1. `OrganizationController` exposes `POST /api/v1/organization/persons`, `PUT /api/v1/organization/persons/{id:guid}`, `PUT /api/v1/organization/persons/{id:guid}/activate`, `PUT /api/v1/organization/persons/{id:guid}/deactivate`, and `GET /api/v1/organization/persons/all`.
2. Write endpoints enforce `[Authorize(Roles = "Administrator,Admin")]` and reject unauthenticated requests (401) and non-admin requests (403); read endpoints remain available to all authenticated users.
3. The Administration section in `App.vue` displays "Person Management" link only when the user possesses `Administrator` or `Admin` role, with `data-testid="nav-person-management-link"`.
4. Route `/admin/persons` redirects unauthorized non-admin users to `/feed` via `router.beforeEach`.
5. Administrators can view all persons (active and inactive) with first name, last name, email, status, and management actions in `PersonManagementView.vue` (`SCR-ORG-001`).
6. Administrators can create a new Person via `PersonModal.vue` (`SCR-ORG-002`) with First Name, Last Name, and Email fields; duplicate emails return `409 Conflict`.
7. Administrators can edit a Person's identity attributes via the modal; updates return `200 OK` with the updated `PersonDto`.
8. Administrators can activate and deactivate Persons, transitioning status via `PUT /activate` and `PUT /deactivate`; inactive Persons are removed from active dropdowns in `UserAccountModal.vue` but remain visible in the management screen.
9. `ActivatePersonCommand` and its MediatR handler are created and registered in `OrganizationModule.cs`.
10. `ListAllPersonsAsync` is added to `IOrganizationQueryService` and `OrganizationQueryService`.
11. `persons.ts` frontend API client module is created with typed interfaces and CRUD + lifecycle functions.
12. Screen inventory and feature traceability documentation are updated with `SCR-ORG-001` and `SCR-ORG-002`.
13. Backend unit tests and integration tests pass; frontend type-check (`npm run type-check`) passes with 0 errors.