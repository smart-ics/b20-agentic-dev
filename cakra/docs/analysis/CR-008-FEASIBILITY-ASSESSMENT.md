---
Title: Feasibility Assessment for Organizational Person Management (Create, Update, Activate, Deactivate)
Code: CR-008
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-05
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Feature being assessed: Manage Organizational Persons (Person Management), per request in [CR-008-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-008-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-008-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-008-ISSUE.md)
- FEATURE: [FEAT-ORG-001-manage-persons.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-ORG-001-manage-persons.md)
- DOMAIN: [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md)
- ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

## Objective

Assess the feasibility, existing technical baseline, gaps, architectural impacts, and planning readiness to:

1. Expose Person write operations (create, update, activate, deactivate) as REST API endpoints in `OrganizationController.cs` (`/api/v1/organization/persons`).
2. Secure all Person management endpoints strictly for authenticated actors possessing the `Administrator` (or `Admin`) role.
3. Build frontend views and dialog components (`SCR-ORG-001: Person Management View`, `SCR-ORG-002: Add/Edit Person Modal`) in `Cakra.Web` under route `/admin/persons`.
4. Surface a "Person Management" navigation link in the left sidebar Administration section exclusively for Administrators.
5. Enforce domain constraints: email uniqueness, Person status lifecycle (`ACTIVE` -> `INACTIVE` and back), and preservation of historical references.

---

# 2. Current State

Summarize relevant findings from current artifacts and codebase.

## Existing Behavior

1. **Database Schema & Persistence (`[organization].[Persons]`)**:
   - The table `[organization].[Persons]` is provisioned and stores `Id` (PK, GUID), `FirstName`, `LastName`, `Email`, `Status`, `CreatedAt`, `UpdatedAt`.
   - [PersonRepository.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Persistence/PersonRepository.cs) implements `GetAllAsync` (returns ALL persons regardless of status), `GetActiveAsync`, `GetByIdAsync`, `GetByEmailAsync`, `AddAsync`, `UpdateAsync`, `UpdateStatusAsync`, and `DeleteAsync`.
   - Email uniqueness is NOT enforced by a database unique constraint; it is enforced at the application service level via `GetByEmailAsync`.
2. **Backend Domain (`Cakra.Modules.Organization`)**:
   - [Person.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Domain/Person.cs) is the `Person` aggregate root with identity attributes and lifecycle methods (`Activate`, `Deactivate`). The Person lifecycle is `ACTIVE -> INACTIVE` per [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md) (§8).
   - [IOrganizationService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Services/IOrganizationService.cs) already exposes write operations: `CreatePersonAsync`, `UpdatePersonAsync`, `DeactivatePersonAsync`, `ActivatePersonAsync`, with synchronous overloads.
   - [OrganizationService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Services/OrganizationService.cs) implements all write operations. `CreatePersonAsync` checks email uniqueness (throws `InvalidOperationException` if duplicate). `DeactivatePersonAsync` and `ActivatePersonAsync` dispatch `PersonDeactivated` and (implicitly) activation domain events.
   - MediatR commands already exist with validators and handlers: `CreatePersonCommand` (first name, last name, email), `UpdatePersonCommand` (person ID + same attributes), `DeactivatePersonCommand` (person ID). No dedicated `ActivatePersonCommand` exists; activation is invoked directly through the service synchronous overload.
3. **Backend Read Layer (`IOrganizationQueryService` / `OrganizationQueryService`)**:
   - [IOrganizationQueryService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/IOrganizationQueryService.cs) exposes `ListActivePersonsAsync`, `GetPersonByIdAsync`, `IsPersonActiveAsync`, `GetTeamRosterAsync`, `GetPersonRolesAsync`, `GetPersonResponsibilitiesAsync`.
   - **No method exists to list ALL persons (including inactive)** — `ListActivePersonsAsync` only returns `ACTIVE` persons. The Person Management screen needs to display both active and inactive records.
4. **Backend REST API Layer (`Cakra.Api`)**:
   - [OrganizationController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/OrganizationController.cs) at route `/api/v1/organization` exposes **read-only** endpoints:
     - `GET /api/v1/organization/persons/active` — returns active persons ordered by name.
     - `GET /api/v1/organization/persons` — returns active persons (calls `ListActivePersonsAsync`, NOT all persons).
     - `GET /api/v1/organization/persons/{id:guid}` — returns a single person by ID.
   - The controller injects **only `IOrganizationQueryService`** — no `IMediator` or `IOrganizationService` — and contains no write endpoints.
   - **Pattern reference**: [CustomersController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/CustomersController.cs) (CR-002) demonstrates the established write-endpoint pattern using `IMediator`, MediatR commands, and RFC 7807 `ProblemDetails` error responses. [UsersController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/CustomersController.cs) (CR-007) demonstrates `[Authorize(Roles = "Administrator,Admin")]` on an administration controller.
5. **Frontend Navigation & Presentation (`Cakra.Web`)**:
   - [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue) renders an Administration sidebar section (conditionally visible when `isAdmin` is true) containing only a "User Management" link (`/admin/users`, `SCR-USR-001`). No "Person Management" link exists.
   - [router/index.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/router/index.ts) defines only `/admin/users` route; no route for Person management. The route guard checks `requiresAuth` and `requiresRole` but no Person Management route exists.
   - No `PersonManagementView.vue` or person modal component exists in `Cakra.Web`.
   - Read-only Person interaction exists only as dropdown selectors: `UserAccountModal.vue` (line 133, fetches `/organization/persons/active`), with similar selectors in `WorkPackageView.vue` and `CreateRequestModal.vue`.
   - Frontend API client pattern: [customers.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/customers.ts) provides full CRUD + activate/deactivate with typed interfaces. [users.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/users.ts) provides the administration client pattern. No `persons.ts` API client module exists.
6. **Documentation**:
   - [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md) lists "Person Management" as a Domain Capability (§3) and defines the Person Lifecycle (§8) and Person domain events (`PersonCreated`, `PersonActivated`, `PersonInactivated`) (§9).
   - [feature-catalog.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/feature-catalog.md) does **not** include a Person Management feature.
   - [feature-traceability.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/feature-traceability.md) does **not** include Person Management.
   - [screen-inventory.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/screen-inventory.md) and [00-screen-inventory.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/00-screen-inventory.md) do **not** document Person Management screens.

## Existing Constraints

1. **1-to-1 UserAccount-Person**: Exactly one `UserAccount` corresponds to one `Person` (Architecture §14). Deactivating a Person whose UserAccount is still active removes the Person from active dropdowns but does not delete the UserAccount.
2. **Email Uniqueness**: Enforced at the application service layer (`OrganizationService.CreatePersonAsync` / `UpdatePersonAsync` throw `InvalidOperationException` on duplicate email), NOT at the database level.
3. **No Physical Deletion**: Domain rule (organization-domain.md §7, rule 11) mandates that Persons are never physically deleted; lifecycle transitions are the only permitted mutations.
4. **Role Authorization**: Access to create, update, or change lifecycle status of Persons must be strictly restricted to users with `Administrator` or `Admin` role, consistent with existing administration screens (CR-007).
5. **Historical Reference Preservation**: Inactive Persons remain valid historical references (organization-domain.md §7, rule 13).
6. **Schema Isolation**: The Organization module is authoritative for `[organization].[Persons]`; other domains reference Person records rather than redefining them (organization-domain.md §7, rules 17, 18).

---

# 3. Gap Analysis

Identify gaps between the requested FEATURE (`FEAT-ORG-001`) and the current system.

| ID | Severity | Gap |
|------|------|------|
| GAP-001 | CRITICAL | No REST API write endpoints exist in `OrganizationController.cs` for creating (`POST`), updating (`PUT`), activating (`PUT /{id}/activate`), or deactivating (`PUT /{id}/deactivate`) Person records. The controller injects only `IOrganizationQueryService` and exposes GET endpoints only. |
| GAP-002 | CRITICAL | No frontend Person Management view (`PersonManagementView.vue` / `SCR-ORG-001`) or modal dialog (`PersonModal.vue` / `SCR-ORG-002`) exists in `Cakra.Web`. |
| GAP-003 | CRITICAL | No navigation entry for Person Management in `App.vue` Administration section, and no route (`/admin/persons`) defined in `router/index.ts`. |
| GAP-004 | MAJOR | No FEATURE artifact existed for Person Management prior to this assessment (now created as `FEAT-ORG-001-manage-persons.md`). |
| GAP-005 | MAJOR | Screen inventory ([screen-inventory.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/screen-inventory.md), [00-screen-inventory.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/00-screen-inventory.md)) and feature traceability ([feature-traceability.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/feature-traceability.md)) lack Person Management screen definitions. |
| GAP-006 | MAJOR | `IOrganizationQueryService` has no method to list ALL persons (including inactive) for the management UI. `ListActivePersonsAsync` returns only `ACTIVE` persons; the management screen needs to display both statuses. |
| GAP-007 | MINOR | No frontend API client module (`persons.ts`) exists, analogous to `customers.ts` and `users.ts`. |
| GAP-008 | MINOR | No automated unit or integration tests covering Person management API endpoints, authorization, or validation rules. |

---

# 4. Open Questions

Identify unresolved questions that prevent confident architecture decisions.

| ID | Question | Impact |
|------|------|------|
| OQ-001 | What screen codes and route URL should be allocated for Person Management? | Establishes router configuration, screen inventory, navigation mapping, and test identifiers. |
| OQ-002 | Should `GET /api/v1/organization/persons` be expanded to return all persons (including inactive), or should a new endpoint be introduced for the management list? | Affects `IOrganizationQueryService`, `OrganizationController`, and frontend API client. |
| OQ-003 | Should the Person Management screen display all Persons or allow status-based filtering (active/inactive/all)? | Affects query service design, API response shape, and UI table design. |
| OQ-004 | Should there be a business guard preventing deactivation of a Person currently referenced by active UserAccount, Request Owner, or Work Package Owner? | Affects lifecycle mutation validation and operational safety. |

---

# 5. Assumptions

Document assumptions made during the assessment.

| ID | Assumption |
|------|------|
| ASM-001 | The existing `IOrganizationService` write methods (`CreatePersonAsync`, `UpdatePersonAsync`, `DeactivatePersonAsync`, `ActivatePersonAsync`) and MediatR commands (`CreatePersonCommand`, `UpdatePersonCommand`, `DeactivatePersonCommand`) are sufficient for backend mutations and require no modifications. |
| ASM-002 | The existing `PersonDto` read model (Id, FirstName, LastName, Email, Status, FullName, IsActive, CreatedAt, UpdatedAt) is sufficient for both list and detail views. |
| ASM-003 | Person Management should follow the same authorization pattern as User Management: `[Authorize(Roles = "Administrator,Admin")]` on the API controller and `requiresRole` route guard on the frontend. |
| ASM-004 | The frontend API client pattern established in `customers.ts` and `users.ts` is the standard pattern for new API client modules. |
| ASM-005 | Email uniqueness is adequately enforced by the application service layer; no database-level unique constraint is currently in place, and the management UI relies on service-level 409 Conflict responses. |
| ASM-006 | The established write-endpoint pattern in `CustomersController.cs` (MediatR command dispatch, RFC 7807 `ProblemDetails`, `CreatedAtAction` for 201 responses) is the standard to be replicated for Person Management endpoints. |

---

# 6. Risks

Document identified risks.

| ID | Risk | Impact | Mitigation |
|------|------|------|------|
| RISK-001 | Unauthorized users discovering or directly accessing Person management API endpoints or routes. | High | Enforce dual-layer protection: server-side ASP.NET Core `[Authorize(Roles = "Administrator,Admin")]` returning RFC 7807 403 Forbidden, and client-side router `requiresRole` guard redirecting non-admins to `/feed`. |
| RISK-002 | Database constraint violations when inserting a Person with a duplicate email. | Medium | Pre-validate email uniqueness in the application service (already implemented via `GetByEmailAsync`); map `InvalidOperationException` to RFC 7807 409 Conflict in the controller. |
| RISK-003 | Accidental deactivation of a Person who is currently an owner of active Requests, Work Packages, or a UserAccount. | Medium | Follow domain rules: deactivation only changes `Status` to `INACTIVE`; existing operational references are preserved historically. Surface clear status indicators in the UI so Administrators understand the impact before deactivating. |
| RISK-004 | Inconsistent screen code allocation or route naming conflicting with existing conventions. | Low | Follow the established `SCR-<MODULE>-<NUMBER>` convention (e.g., `SCR-ORG-001`, `SCR-ORG-002`) and RESTful clean URLs (`/admin/persons`). |

---

# 7. Recommendations

Recommend possible approaches for addressing major gaps.

## Option A: Dedicated Person Management Screen under Administration (Recommended)

Add write endpoints to the existing `OrganizationController.cs` under `/api/v1/organization/persons`, create a dedicated `PersonManagementView.vue` (`SCR-ORG-001`) at route `/admin/persons` with an `SCR-ORG-002` modal for create/edit, and add a "Person Management" navigation link in the Administration section of `App.vue`.

### Advantages
- Clean separation of concerns adhering to the modular monolith architecture.
- Reuses existing domain service, commands, validators, and `PersonDto`.
- Follows established RESTful and frontend patterns from `CustomersController` (CR-002) and `UserManagementView` (CR-007).
- Consistent RBAC model with User Management.
- Matches the user's explicit request for a dedicated UI and navigation entry.

### Disadvantages
- Requires adding a list-all-persons query to `IOrganizationQueryService` and a new frontend API client module (`persons.ts`).

## Option B: Embed Person Creation within User Management

Reuse the existing `UserAccountModal.vue` Person dropdown to surface a "Create Person" quick-add, avoiding a dedicated management screen.

### Advantages
- No new navigation structure or route.

### Disadvantages
- Violates the domain boundary separating Person identity management (Organization domain) from UserAccount management (Identity module).
- Fails the user's explicit request for a "dedicated UI" and "dedicated navigation."
- Does not provide a full list/view of existing Persons or lifecycle management (activate/deactivate).
- Limited to creation only; no way to update or change Person lifecycle status.

---

# 8. Gap Closure

Record resolutions for gaps and open questions.

## GAP-001: REST API Write Endpoints

### Decision
Add the following write endpoints to `OrganizationController.cs` (`/api/v1/organization/persons`), guarded with a controller-level or method-level `[Authorize(Roles = "Administrator,Admin")]`:
- `POST /api/v1/organization/persons` — Creates a new Person (dispatches `CreatePersonCommand` via `IMediator`). Returns `201 Created` with the created `PersonDto`.
- `PUT /api/v1/organization/persons/{id:guid}` — Updates Person identity attributes (dispatches `UpdatePersonCommand`). Returns `200 OK` with the updated `PersonDto`.
- `PUT /api/v1/organization/persons/{id:guid}/activate` — Reactivates an inactive Person (dispatches an `ActivatePersonCommand` via `IMediator` or calls `IOrganizationService.ActivatePersonAsync`). Returns `200 OK`.
- `PUT /api/v1/organization/persons/{id:guid}/deactivate` — Deactivates an active Person (dispatches `DeactivatePersonCommand` via `IMediator`). Returns `200 OK`.

### Rationale
The `OrganizationController` already handles the `/api/v1/organization/persons` route hierarchy. Adding write endpoints to the existing controller maintains RESTful cohesion. The MediatR command pattern is already established and the commands/validators exist. The `[Authorize(Roles = "Administrator,Admin")]` pattern is proven by `UsersController.cs` (CR-007).

### Impact
Extends `OrganizationController.cs` with `IMediator` injection and four write endpoints. No new controller or module boundary changes.

### Architecture Impact
Extends the API surface in compliance with Architecture §19.5 and §19.6. Adds an `ActivatePersonCommand` if relying purely on MediatR (the command does not yet exist; a new command and handler will be required).

### Resolved By
ica-analyst

### Resolved Date
2026-10-05

---

## GAP-002: Frontend Person Management View and Modal

### Decision
Create `PersonManagementView.vue` (`SCR-ORG-001`) at route `/admin/persons` displaying a searchable table of all Persons with first name, last name, email, status badge, and Action buttons (Edit, Activate, Deactivate). Create `PersonModal.vue` (`SCR-ORG-002`) supporting both "Add Person" (create) and "Edit Person" (update) modes with fields for First Name, Last Name, and Email, and client-side validation.

### Rationale
Fulfills the user's explicit request for a dedicated Person management UI. Follows the established pattern from `UserManagementView.vue` (SCR-USR-001) and `UserAccountModal.vue` (SCR-USR-002), which are the closest analogs for an administrative CRUD screen.

### Impact
New Vue components in `Cakra.Web`: `src/views/PersonManagementView.vue`, `src/components/PersonModal.vue`, and a new `src/api/persons.ts` client module.

### Architecture Impact
Expands the UI screen inventory. No architectural boundary changes.

### Resolved By
ica-analyst

### Resolved Date
2026-10-05

---

## GAP-003: Navigation Entry and Route

### Decision
1. Add a "Person Management" link in the Administration section of `App.vue` sidebar, conditionally rendered with `v-if="isAdmin"`, linking to `/admin/persons` with `data-testid="nav-person-management-link"`.
2. Add route `{ path: '/admin/persons', name: 'person-management', component: PersonManagementView, meta: { requiresAuth: true, requiresRole: 'Administrator', screenId: 'SCR-ORG-001' } }` to `router/index.ts`.
3. Add "Person Management" to the `currentScreenTitle` computed in `App.vue`.

### Rationale
Consistent with the existing User Management navigation and route pattern. The `isAdmin` computed property and route guard already exist in `App.vue` and `router/index.ts` respectively.

### Impact
Updates to `App.vue` and `router/index.ts` in `Cakra.Web`.

### Architecture Impact
Strengthens client-side RBAC navigation architecture.

### Resolved By
ica-analyst

### Resolved Date
2026-10-05

---

## GAP-004: FEATURE Artifact

### Decision
Create `FEAT-ORG-001-manage-persons.md` as the FEATURE artifact, referencing DOMAIN `organization-domain.md`, with business outcome, operational flow, domain orchestration, constraints, exceptions, and acceptance criteria. This artifact has been created.

### Rationale
Establishes the business knowledge boundary between DOMAIN (Person, lifecycle, identity attributes) and FEATURE (management outcome, orchestration, acceptance criteria). Required as input to the feasibility assessment and consumed by downstream architecture.

### Impact
New file at `cakra/docs/features/FEAT-ORG-001-manage-persons.md`.

### Architecture Impact
None — business knowledge artifact only.

### Resolved By
ica-analyst

### Resolved Date
2026-10-05

---

## GAP-005: Documentation Gaps

### Decision
Update the following documentation:
- `screen-inventory.md`: Add `SCR-ORG-001` (Person Management View) and `SCR-ORG-002` (Add/Edit Person Modal) as Administration area screens.
- `feature-catalog.md`: Add `FEAT-ORG-001 | Manage Organizational Persons | Command | —`.
- `feature-traceability.md`: Add a traceability row mapping FEAT-ORG-001 to Organization domain, SC-ORG-001, UC-ORG-001, UJ-ORG-001, SCR-ORG-001/002.

### Rationale
Maintains consistency in the knowledge ecosystem; prevents orphan screens and undocumented capabilities.

### Impact
Updates to navigation and feature documentation artifacts.

### Architecture Impact
None — documentation only.

### Resolved By
ica-analyst

### Resolved Date
2026-10-05

---

## GAP-006: Query Service for All Persons

### Decision
Add a `ListAllPersonsAsync` method to `IOrganizationQueryService` and its implementation `OrganizationQueryService`, returning all Person records (active and inactive) ordered by last name and first name. Add a corresponding `GET /api/v1/organization/persons/all` read endpoint in `OrganizationController` for the management UI. The existing `GET /api/v1/organization/persons` (returning active persons) is preserved for existing dropdown selectors to avoid breaking changes.

### Rationale
The Person Management screen needs to display both active and inactive persons to allow administrators to review and manage the full lifecycle. The existing `ListActivePersonsAsync` is consumed by read-only dropdown selectors and must not be changed. A dedicated list-all query follows the repository pattern already established by `PersonRepository.GetAllAsync`.

### Impact
New method in `IOrganizationQueryService` and `OrganizationQueryService`; new read endpoint in `OrganizationController`; new `listAllPersons()` function in the frontend API client module (`persons.ts`).

### Architecture Impact
Extends read-model query surface within the existing modular monolith structure. No new persistence schema.

### Resolved By
ica-analyst

### Resolved Date
2026-10-05

---

## OQ-001: Screen Codes and Route

### Decision
- Screen Code: `SCR-ORG-001` (Person Management View), `SCR-ORG-002` (Add/Edit Person Modal)
- Route: `/admin/persons` (Name: `person-management`)
- Navigation Section: Administration (alongside User Management)

### Rationale
Conforms to the established CAKRA screen naming convention (`SCR-<MODULE>-<NUMBER>`). The Organization module uses the `ORG` prefix. The route follows the clean RESTful hierarchy under `/admin`.

### Impact
Updates to `router/index.ts`, `App.vue`, and `screen-inventory.md`.

### Architecture Impact
None.

### Resolved By
ica-analyst

### Resolved Date
2026-10-05

---

## OQ-002: All Persons Endpoint Strategy

### Decision
Add a new endpoint `GET /api/v1/organization/persons/all` returning all persons (active and inactive). Do NOT modify the existing `GET /api/v1/organization/persons` which currently returns only active persons and is consumed by read-only dropdown selectors.

### Rationale
Preserves backward compatibility for existing consumers of `/api/v1/organization/persons/active` and `/api/v1/organization/persons` while providing a dedicated list-all endpoint for the management UI.

### Impact
New read endpoint in `OrganizationController`; new query service method; new frontend API function.

### Architecture Impact
Adds one read endpoint; no breaking changes to existing API contracts.

### Resolved By
ica-analyst

### Resolved Date
2026-10-05

---

## OQ-003: Management Screen Filtering

### Decision
The Person Management screen (`PersonManagementView.vue` / `SCR-ORG-001`) will display ALL persons (both active and inactive) by default, with an optional status filter dropdown (All / Active / Inactive) in the table toolbar for convenience. The backend `GET /api/v1/organization/persons/all` returns all persons; client-side filtering applies status pills.

### Rationale
An administrator managing Persons needs to see the full population to make informed decisions about activation, deactivation, and updates. A client-side status filter provides UX convenience without requiring multiple backend endpoints.

### Impact
UI design decision for `PersonManagementView.vue`; no additional API endpoints required beyond `GET /api/v1/organization/persons/all`.

### Architecture Impact
None.

### Resolved By
ica-analyst

### Resolved Date
2026-10-05

---

## OQ-004: Deactivation Guard for Active References

### Decision
Do NOT add a proactive business guard in the application service preventing deactivation of a Person referenced by active UserAccount, Request Owner, or Work Package Owner. The Organization domain's stated purpose is to provide authoritative organizational knowledge as a reference; lifecycle transitions (ACTIVE -> INACTIVE) are permitted and historical references are preserved per domain rule 13 (organization-domain.md §7). The UI will surface a warning banner indicating the Person is associated with active operational objects, but will allow the Administrator to proceed with deactivation.

### Rationale
The Organization domain does not own Request, Work Package, or UserAccount lifecycle management. Enforcing cross-domain operational constraints within the Organization service would violate modular monolith boundaries (Architecture §14, §20). The downstream domains are responsible for handling inactive Person references according to their own rules.

### Impact
No backend service changes beyond the existing `DeactivatePersonAsync`. UI warning display in `PersonManagementView.vue`.

### Architecture Impact
Preserves domain boundary isolation between Organization and operational domains.

### Resolved By
ica-analyst

### Resolved Date
2026-10-05

---

# 9. Architecture Applicability

## Decision

ARCHITECTURE-REQUIRED

## Rationale

Implementation of `CR-008` introduces:

1. New REST API write and read endpoints (`POST`, `PUT`, `PUT /activate`, `PUT /deactivate`, `GET /all`) in `OrganizationController.cs` with role-based authorization.
2. A new `ActivatePersonCommand` and handler in `Cakra.Modules.Organization` (does not yet exist as a MediatR command — only the synchronous service method exists).
3. A new query method (`ListAllPersonsAsync`) in `IOrganizationQueryService` / `OrganizationQueryService` with corresponding SQL.
4. New client-side role-based routing guards and a new UI screen (route `/admin/persons`) in `Cakra.Web`.
5. New frontend API client module (`persons.ts`) and Vue components (`PersonManagementView.vue`, `PersonModal.vue`).
6. Updates to screen inventory and navigation documentation.

A formal ARCHITECTURE artifact (`CR-008-ARCHITECTURE.md`) is required to document component interactions, API contract patterns, authorization boundaries, slice structure, and integration with the existing Organization module and frontend patterns before planning.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved (GAP-001 through GAP-004 closed with decisions)
- [x] All required decisions recorded (OQ-001 through OQ-004 closed with decisions)
- [x] All blocking open questions resolved
- [x] Architecture can be finalized or updated based on the approved decisions

## Status

READY-FOR-PLANNING

## Notes

All feasibility gaps (GAP-001 through GAP-008) and open questions (OQ-001 through OQ-004) have been analyzed and resolved with recorded decisions. The analysis is complete and all blocking uncertainties have been closed.

The Architect has reviewed the approved decisions in this assessment and the realized target architecture (`CR-008-ARCHITECTURE.md`), confirming that the target architecture can be finalized from this assessment. The READY-FOR-PLANNING gate is hereby granted.

The target architecture (`CR-008-ARCHITECTURE.md`) is complete and approved. Planning may now begin.

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-008-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-008-ISSUE.md)
- FEATURE: [FEAT-ORG-001-manage-persons.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-ORG-001-manage-persons.md)
- DOMAIN: [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md)
- ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- FEASIBILITY-ASSESSMENT (reference pattern): [CR-007-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-007-FEASIBILITY-ASSESSMENT.md)
- ARCHITECTURE (reference pattern): [CR-007-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-007-ARCHITECTURE.md)
- Screen Inventory: [screen-inventory.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/screen-inventory.md)
- Feature Catalog: [feature-catalog.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/feature-catalog.md)

Referenced codebase locations:

- [Person.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Domain/Person.cs)
- [IOrganizationService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Services/IOrganizationService.cs)
- [OrganizationService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Services/OrganizationService.cs)
- [IOrganizationQueryService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/IOrganizationQueryService.cs)
- [OrganizationQueryService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Services/OrganizationQueryService.cs)
- [PersonRepository.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Persistence/PersonRepository.cs)
- [CreatePersonCommand.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Commands/CreatePersonCommand.cs)
- [UpdatePersonCommand.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Commands/UpdatePersonCommand.cs)
- [DeactivatePersonCommand.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Commands/DeactivatePersonCommand.cs)
- [PersonDto.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Models/PersonDto.cs)
- [OrganizationController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/OrganizationController.cs)
- [CustomersController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/CustomersController.cs)
- [UsersController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/UsersController.cs)
- [customers.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/customers.ts)
- [users.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/users.ts)
- [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue)
- [router/index.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/router/index.ts)
- [UserAccountModal.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/UserAccountModal.vue)
- [UserManagementView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/UserManagementView.vue)