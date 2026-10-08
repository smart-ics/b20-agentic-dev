---
Title: Feasibility Assessment for Person Role Assignment in SCR-ORG-002 and Table Roles Display in SCR-ORG-001
Code: CR-027
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-08
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Feature being assessed: Manage Organizational Persons & Role Assignments, per request in [CR-027-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-027-ISSUE.md) and [FEAT-ORG-001-manage-persons.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-ORG-001-manage-persons.md) (v1.1).

Referenced artifacts:

- ISSUE: [CR-027-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-027-ISSUE.md)
- FEATURE: [FEAT-ORG-001-manage-persons.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-ORG-001-manage-persons.md)
- DOMAIN: [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- REFERENCE ARCHITECTURE: [CR-008-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-008-ARCHITECTURE.md)

## Objective

Assess the technical feasibility, architectural impact, gaps, and readiness to:

1. Expose an endpoint `GET /api/v1/organization/roles` to dynamically serve available organizational master roles.
2. Ensure master roles in `[organization].[Roles]` include `Administrator`, `Management`, and `Operational User` (in addition to existing `Programmer` and `Implementator`).
3. Update `POST /api/v1/organization/persons` and `PUT /api/v1/organization/persons/{id}` to atomically accept and synchronize `IReadOnlyList<Guid> RoleIds` in a single database transaction.
4. Prevent self-demotion by forbidding the currently logged-in administrator from revoking the `Administrator` role from their own record.
5. Enrich `PersonDto` with `IReadOnlyList<string> Roles` (or `RoleDto`) so `SCR-ORG-001` (`PersonManagementView.vue`) displays active role badges and enables keyword search across assigned roles.
6. Enrich `SCR-ORG-002` (`PersonModal.vue`) with a dedicated Role Picker using checkboxes with names and descriptions, enforcing at least one mandatory role selection.
7. Retain historical role assignments upon person deactivation so reactivating the person immediately restores operational roles.

---

# 2. Current State

## Existing Behavior

1. **Database Schema & Master Roles (`[organization].[Roles]`, `[organization].[RoleAssignments]`)**:
   - The tables `[organization].[Roles]` and `[organization].[RoleAssignments]` exist and are fully partitioned within the `organization` schema per Architecture §17.
   - `[organization].[RoleAssignments]` links `PersonId` and `RoleId` with `AssignedAt` and `RevokedAt` timestamps.
   - Seed script `seed-admin.ps1` creates `Administrator`, `Management`, `Programmer`, and `Implementator`. The role `Operational User` does not exist yet.
2. **Backend Domain & Services (`Cakra.Modules.Organization`)**:
   - [Role.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Domain/Role.cs) and [RoleAssignment.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Domain/RoleAssignment.cs) represent domain entities.
   - [OrganizationService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Services/OrganizationService.cs) contains methods `AssignRoleToPersonAsync(personId, roleId)` and `RevokeRoleFromPersonAsync(personId, roleId)`, dispatching `RoleAssigned` and `RoleRevoked` domain events.
   - MediatR commands exist: `AssignRoleToPersonCommand` and `RevokeRoleFromPersonCommand`.
   - `CreatePersonCommand` and `UpdatePersonCommand` only accept `FirstName`, `LastName`, and `Email`. They have no parameters for `RoleIds` and perform no role assignment synchronization.
3. **Backend Query Layer (`IOrganizationQueryService` / `OrganizationQueryService`)**:
   - `IOrganizationQueryService` exposes `GetPersonRolesAsync(personId)` returning active role names for a single person.
   - `ListAllPersonsAsync()` and `ListActivePersonsAsync()` project `Person` entities into `PersonDto` without loading or attaching assigned roles.
   - There is no method in `IOrganizationQueryService` to list all available master `Role` records (`ListAllRolesAsync`).
4. **Backend REST API Layer (`OrganizationController.cs`)**:
   - Controller exposes `GET persons/active`, `GET persons/all`, `GET persons/{id}`, `POST persons`, `PUT persons/{id}`, `PUT persons/{id}/activate`, and `PUT persons/{id}/deactivate`.
   - Controller does not expose any endpoint for listing roles (`GET /roles`).
   - `CreatePersonRequest` and `UpdatePersonRequest` DTOs only contain `FirstName`, `LastName`, and `Email`.
5. **Frontend UI Components (`Cakra.Web`)**:
   - `PersonModal.vue` (`SCR-ORG-002`) only renders input fields for First Name, Last Name, and Email.
   - `PersonManagementView.vue` (`SCR-ORG-001`) table renders columns: Name, Email, Status, Actions. There is no column for Roles and search does not match role names.
   - `api/persons.ts` defines `PersonDto`, `CreatePersonRequest`, `UpdatePersonRequest` without role properties.

## Existing Constraints

1. **Role Authorization**: All Person management and role assignment capabilities are strictly guarded for users possessing the `Administrator` or `Admin` role.
2. **Schema Isolation & Bounded Context**: Roles and Role Assignments belong entirely to the `Organization` module; no cross-module database joins or foreign keys to Identity tables.
3. **Multi-Role Capability**: A single `Person` may hold multiple active organizational roles simultaneously per [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md).
4. **Non-Empty Role Assignment Invariant**: Every organizational Person must hold at least one active role.
5. **Self-Demotion Guard**: An authenticated Administrator cannot revoke the `Administrator` role from their own Person record.
6. **Deactivation Invariant**: Deactivating a Person marks `Status = INACTIVE` but preserves `RoleAssignments` in history; reactivating restores those roles.

---

# 3. Gap Analysis

| ID | Severity | Gap |
|------|------|------|
| GAP-001 | CRITICAL | `CreatePersonRequest`, `UpdatePersonRequest`, `CreatePersonCommand`, and `UpdatePersonCommand` do not accept `RoleIds`, preventing atomic person and role synchronization. |
| GAP-002 | CRITICAL | No REST API endpoint exists to retrieve available master roles (`GET /api/v1/organization/roles`). |
| GAP-003 | MAJOR | `PersonDto` lacks a `Roles` property, and `ListAllPersonsAsync` does not query or project active role names. |
| GAP-004 | MAJOR | Master role `Operational User` is not seeded or provisioned in `[organization].[Roles]`. |
| GAP-005 | CRITICAL | `PersonModal.vue` (`SCR-ORG-002`) has no role fetching, no role checkboxes, no validation requiring $\ge 1$ role, and no self-demotion guard. |
| GAP-006 | MAJOR | `PersonManagementView.vue` (`SCR-ORG-001`) lacks a "Roles" column with badges and does not include role names in table search. |
| GAP-007 | MINOR | `api/persons.ts` does not define `RoleDto`, `listRoles()`, or role array fields in `CreatePersonRequest`/`UpdatePersonRequest`. |
| GAP-008 | MINOR | Automated unit and integration tests do not cover role assignment during person create/update, role query endpoints, or self-demotion protection. |

---

# 4. Open Questions

| ID | Question | Impact |
|------|------|------|
| OQ-001 | What role cardinality and validation rule should apply in SCR-ORG-002? | Determines UI multi-select vs radio buttons and form validation rules. |
| OQ-002 | How should available roles be sourced given 'Operational User' is not yet seeded? | Determines API role endpoint and database migration/seed requirements. |
| OQ-003 | What UI control should be used for role selection in SCR-ORG-002? | Determines modal visual design, layout, and component ergonomics. |
| OQ-004 | Should role assignment be atomic with person save or use separate endpoints? | Determines API contracts, database transaction scope, and error handling. |
| OQ-005 | Should the SCR-ORG-001 table display assigned roles? | Determines `PersonDto` schema and table column layout. |
| OQ-006 | Should self-demotion be prevented when an administrator edits their own record? | Determines authorization and validation invariants to prevent lockout. |
| OQ-007 | How should roles be handled when a Person is deactivated? | Determines lifecycle behavior and state consistency. |

---

# 5. Assumptions

| ID | Assumption |
|------|------|
| ASM-001 | Available roles will be retrieved via `GET /api/v1/organization/roles` cached or fetched once on modal mount. |
| ASM-002 | The active user's `PersonId` is accessible from `useAuthStore().user?.personId` to enforce the self-demotion guard. |
| ASM-003 | Standard seeded roles will include `Administrator`, `Management`, `Operational User`, `Programmer`, and `Implementator`. |
| ASM-004 | `ListAllPersonsAsync` can perform an efficient query (e.g. left join or subquery against `RoleAssignments`) to populate roles without N+1 query penalties. |
| ASM-005 | When an existing role assignment is unselected in Edit mode, it is revoked by setting `RevokedAt = UtcNow`; re-selecting it reactivates it (`RevokedAt = null`). |
| ASM-006 | Deactivation of a person retains existing role assignments unchanged in `[organization].[RoleAssignments]`. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|------|------|------|------|
| RISK-001 | Administrator accidentally removes their own `Administrator` role, locking themselves out. | High | Client-side disabled checkbox and server-side validation error forbidding self-demotion. |
| RISK-002 | Partial failure if person identity updates succeed but role assignments fail. | Medium | Execute person updates and role synchronizations within a single database transaction via MediatR handler. |
| RISK-003 | N+1 database queries when listing all persons with their active roles. | Medium | Use a single SQL query in `PersonRepository` joining `RoleAssignments` and `Roles`, grouping or aggregating role names per person. |
| RISK-004 | Creating a person without any roles creates orphaned operational users. | Medium | Enforce mandatory rule: `RoleIds` must contain at least 1 valid role in both client form and MediatR validator. |

---

# 7. Recommendations

## Option A (Recommended): Atomic Transactional Payload & Query Join
- Add `RoleIds` (`IReadOnlyList<Guid>`) to `CreatePersonCommand` and `UpdatePersonCommand`.
- Synchronize role assignments within the same transaction in `OrganizationService`.
- Enrich `PersonDto` with `IReadOnlyList<string> Roles`.
- Query persons and active roles in a single SQL query using `STRING_AGG` or Dapper multi-mapping.
- Provide `GET /api/v1/organization/roles` returning `IReadOnlyList<RoleDto>`.
- Render checkboxes in `SCR-ORG-002` with required validator ($\ge 1$ role) and self-demotion guard.

### Advantages
- Single network round-trip for create and update.
- Complete transactional atomicity; impossible to create a person without roles.
- High performance read projection without N+1 overhead.

### Disadvantages
- Minor expansion of existing person request contracts and DTOs.

## Option B: Separate Roles Sub-Resource Endpoint
- Keep `POST /persons` and `PUT /persons/{id}` unchanged.
- Expose `PUT /api/v1/organization/persons/{id}/roles` with `RoleIds`.
- Frontend calls Person API first, then Roles API second.

### Advantages
- Leaves existing Person command signatures untouched.

### Disadvantages
- Non-atomic; network failure between step 1 and step 2 creates persons with missing roles.
- Requires coordinating multi-step submissions and rollback handling on frontend.

---

# 8. Gap Closure

## GAP-001 & OQ-004 (Atomic Request Payloads)

### Decision
`CreatePersonRequest`, `UpdatePersonRequest`, `CreatePersonCommand`, and `UpdatePersonCommand` will include `IReadOnlyList<Guid> RoleIds`. The application service will create/update the person and synchronize active role assignments in a single transaction.

### Rationale
Ensures transactional integrity and prevents partial states where a person exists without assigned roles.

### Impact
Updates command signatures and handlers in `Cakra.Modules.Organization`, API request models, and frontend client calls.

### Architecture Impact
`Cakra.Modules.Organization` command handlers, `IOrganizationService.CreatePersonAsync` and `UpdatePersonAsync` signatures, and `OrganizationController.cs`.

### Resolved By
`ica-analyst` (aligned with user in `/grill-me` intake interview)

### Resolved Date
2026-10-08

---

## GAP-002 & OQ-002 (Master Roles Endpoint & Sourcing)

### Decision
Expose `GET /api/v1/organization/roles` in `OrganizationController.cs` guarded by `[Authorize]`, returning `IReadOnlyList<RoleDto>`. Add a database script/seed ensuring `Operational User`, `Administrator`, and `Management` exist in `[organization].[Roles]`.

### Rationale
Enables dynamic role population in frontend dropdowns/checkboxes without hardcoding role lists in the UI, and satisfies the required role catalog.

### Impact
Adds `RoleDto`, `ListAllRolesAsync` in `IOrganizationQueryService` and `RoleRepository`, and a new migration script.

### Architecture Impact
New API route `/api/v1/organization/roles` and database migration script.

### Resolved By
`ica-analyst` (aligned with user in `/grill-me` intake interview)

### Resolved Date
2026-10-08

---

## GAP-003, GAP-006 & OQ-005 (Roles in Table & DTO)

### Decision
`PersonDto` will be enriched with `IReadOnlyList<string> Roles`. `PersonRepository.GetAllAsync()` will aggregate active role names per person. `SCR-ORG-001` (`PersonManagementView.vue`) will render a "Roles" column with badges and include role names in keyword search.

### Rationale
Provides operational visibility so administrators can see individual roles at a glance without having to open the edit modal for each person.

### Impact
Updates `PersonDto.cs`, `PersonRepository.cs` Dapper query, and `PersonManagementView.vue` template.

### Architecture Impact
`PersonDto` schema and `PersonRepository` query projection.

### Resolved By
`ica-analyst` (aligned with user in `/grill-me` intake interview)

### Resolved Date
2026-10-08

---

## GAP-004 (Master Data Seed for Operational User)

### Decision
Add embedded SQL migration script `0019_seed_operational_user_role.sql` that idempotently inserts the `Operational User` role into `[organization].[Roles]` if it does not already exist, and update `seed-admin.ps1`.

### Rationale
Ensures consistent provisioning across all environments through DbUp migration pipeline.

### Impact
Adds new migration script in `Cakra.Api/Migrations/Scripts`.

### Architecture Impact
Database schema migration sequence.

### Resolved By
`ica-analyst` (aligned with user in `/grill-me` intake interview)

### Resolved Date
2026-10-08

---

## GAP-005, OQ-001 & OQ-003 (SCR-ORG-002 Modal Role Picker)

### Decision
Add a Roles fieldset with checkboxes displaying role names and descriptions in `PersonModal.vue`. The fieldset supports multiple roles, enforces at least 1 mandatory role selection, and includes error validation messaging.

### Rationale
Checkboxes provide high visibility and clarity for selecting multiple roles from a standard set of 3–5 organizational roles.

### Impact
Updates `PersonModal.vue` template, reactive form state, and client-side validation logic.

### Architecture Impact
Frontend modal component `SCR-ORG-002`.

### Resolved By
`ica-analyst` (aligned with user in `/grill-me` intake interview)

### Resolved Date
2026-10-08

---

## OQ-006 (Self-Demotion Guard)

### Decision
When an administrator edits their own Person record (identified via `authStore.user?.personId`), the `Administrator` checkbox is disabled in `SCR-ORG-002` with an informative tooltip/hint. The backend command validator also rejects removing the `Administrator` role if the actor's `PersonId` matches the target `PersonId`.

### Rationale
Protects against accidental administrative lockout.

### Impact
Updates `PersonModal.vue` and `UpdatePersonCommandValidator.cs`.

### Architecture Impact
Domain invariant validation and UI form guard.

### Resolved By
`ica-analyst` (aligned with user in `/grill-me` intake interview)

### Resolved Date
2026-10-08

---

## OQ-007 (Deactivation Role Handling)

### Decision
When a Person is deactivated, their role assignments in `[organization].[RoleAssignments]` are preserved in history (not revoked). Inactive persons cannot log in or be assigned active operational tasks. Reactivating a person immediately restores their role assignments.

### Rationale
Maintains historical traceability and ensures simple, seamless reactivation without requiring manual role re-assignment.

### Impact
No change to `DeactivatePersonCommand` behavior; aligns with domain rules.

### Architecture Impact
Maintains existing domain lifecycle invariants.

### Resolved By
`ica-analyst` (aligned with user in `/grill-me` intake interview)

### Resolved Date
2026-10-08

---

## GAP-007 (Frontend API Client)

### Decision
Enrich `api/persons.ts` with `RoleDto`, `listRoles()`, and updated `CreatePersonRequest`/`UpdatePersonRequest` interfaces with `roleIds: string[]`.

### Rationale
Provides strongly typed contracts for all frontend interactions.

### Impact
Updates `cakra/src/frontend/Cakra.Web/src/api/persons.ts`.

### Architecture Impact
Frontend API client tier.

### Resolved By
`ica-analyst`

### Resolved Date
2026-10-08

---

## GAP-008 (Test Coverage)

### Decision
Add comprehensive unit and integration tests verifying atomic person + role creation and update, `GET /roles` endpoint, self-demotion prevention, and validation rules.

### Rationale
Guarantees regression safety and enforces domain rules.

### Impact
New and updated tests in `Cakra.Tests.Unit` and `Cakra.Tests.Integration`.

### Architecture Impact
Test suite coverage.

### Resolved By
`ica-analyst`

### Resolved Date
2026-10-08

---

# 9. Architecture Applicability

## Decision
`ARCHITECTURE-REQUIRED`

## Rationale
Enriching Person Management with atomic role assignments introduces:
1. Modified API request/response contracts (`CreatePersonRequest`, `UpdatePersonRequest`, `PersonDto`).
2. New REST endpoint `GET /api/v1/organization/roles`.
3. Expanded MediatR command signatures and application service transactional synchronization in `Cakra.Modules.Organization`.
4. Master data database migration script (`0019_seed_operational_user_role.sql`).
5. New domain invariants (mandatory $\ge 1$ role, self-demotion prevention).

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved
- [x] All required decisions recorded
- [x] All blocking open questions resolved
- [x] Architecture can be finalized or updated

## Status
`NOT-READY`

## Notes
All technical gaps and open questions have been fully resolved with approved decisions. Status remains `NOT-READY` pending formal review and gate approval from the `ica-architect`.

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-027-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-027-ISSUE.md)
- FEATURE: [FEAT-ORG-001-manage-persons.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-ORG-001-manage-persons.md)
- DOMAIN: [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md)

Referenced codebase locations:

- Controller: [OrganizationController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/OrganizationController.cs)
- Domain: [Role.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Domain/Role.cs), [RoleAssignment.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Domain/RoleAssignment.cs), [Person.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Domain/Person.cs)
- Services: [OrganizationService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Services/OrganizationService.cs), [OrganizationQueryService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Services/OrganizationQueryService.cs)
- Frontend: [PersonModal.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/PersonModal.vue), [PersonManagementView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/PersonManagementView.vue), [api/persons.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/persons.ts)
