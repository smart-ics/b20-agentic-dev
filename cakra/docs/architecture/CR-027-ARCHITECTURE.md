---
Title: Person Role Assignment in SCR-ORG-002 and Management View Table Roles Display Architecture
Code: CR-027
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-08
---

# 1. Overview

This architecture defines the technical realization for Change Request `CR-027`: Person Role Assignment in `SCR-ORG-002` and Management View Table Roles Display in `SCR-ORG-001`.

It realizes [FEAT-ORG-001-manage-persons.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-ORG-001-manage-persons.md) (v1.1) and technical gap closures from [CR-027-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-027-FEASIBILITY-ASSESSMENT.md), establishing:

1. A new REST endpoint `GET /api/v1/organization/roles` in `OrganizationController.cs` providing available organizational master roles.
2. A new database migration script `0019_seed_operational_user_role.sql` guaranteeing the presence of standard master roles (`Administrator`, `Management`, `Operational User`, `Programmer`, `Implementator`) in `[organization].[Roles]`.
3. Atomic transaction handling in `CreatePersonCommand` and `UpdatePersonCommand` allowing simultaneous persistence of Person identity attributes and synchronization of `RoleAssignments`, emitting `RoleAssigned` and `RoleRevoked` domain events.
4. Self-demotion guardrails preventing an active administrator from accidentally revoking the `Administrator` role from their own record.
5. Schema expansion of `PersonDto` to include active role names (`IReadOnlyList<string> Roles`), populated via an efficient single SQL query in `PersonRepository`.
6. Frontend enrichment of `SCR-ORG-002` (`PersonModal.vue`) with a multi-selection Role Picker (checkboxes with descriptions), mandatory $\ge 1$ role validation, and self-demotion lockout.
7. Frontend enrichment of `SCR-ORG-001` (`PersonManagementView.vue`) with a "Roles" column rendering active badges, and keyword search across assigned roles.

---

# 2. Architectural Basis

## Business Context

- DOMAIN: [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md)
- FEATURE: [FEAT-ORG-001-manage-persons.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-ORG-001-manage-persons.md) (v1.1)
- ISSUE: [CR-027-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-027-ISSUE.md)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md) (Section 6: Module Boundaries, Section 7: Component Responsibilities, Section 14: Identity & Authentication Architecture, Section 17: Database Design)
- BASELINE ARCHITECTURE: [CR-008-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-008-ARCHITECTURE.md) (Organizational Person Management)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-027-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-027-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

All approved analysis decisions (`GAP-001` through `GAP-008` and `OQ-001` through `OQ-007`) are directly consumed and realized in this architecture.

---

# 3. Scope

## Included

1. **Database Master Data & Migration (`Cakra.Api/Migrations/Scripts`)**:
   - `0019_seed_operational_user_role.sql`: Idempotent script adding `Operational User` to `[organization].[Roles]`.
   - Update `cakra/deploy/seed-admin.ps1` to include `Operational User`.
2. **Backend Application Layer (`Cakra.Modules.Organization`)**:
   - Contract expansion: `CreatePersonCommand` and `UpdatePersonCommand` accept `IReadOnlyList<Guid> RoleIds`.
   - Validation: `CreatePersonCommandValidator` and `UpdatePersonCommandValidator` enforce $\ge 1$ role required and self-demotion protection.
   - Transactional synchronization in `OrganizationService.cs`: updates `CreatePersonAsync` and `UpdatePersonAsync` to synchronize `RoleAssignment` records in the same unit of work.
   - Query Service & DTO expansion: `IOrganizationQueryService.ListAllRolesAsync()`, `RoleDto` definition, and `PersonDto.Roles` list.
   - Persistence updates in `PersonRepository.cs`: single query fetching persons with aggregated active roles (`STRING_AGG` or multi-mapping).
3. **Backend REST API Layer (`Cakra.Api`)**:
   - `GET /api/v1/organization/roles`: Returns `IReadOnlyList<RoleDto>` guarded by `[Authorize]`.
   - Update `CreatePersonRequest` and `UpdatePersonRequest` to accept `IReadOnlyList<Guid> RoleIds`.
   - Pass authenticated caller's PersonId into `UpdatePersonCommand` for server-side self-demotion verification.
4. **Frontend API Client (`Cakra.Web/src/api/persons.ts`)**:
   - Export `RoleDto` interface.
   - Add `listRoles(): Promise<RoleDto[]>`.
   - Update `CreatePersonRequest`, `UpdatePersonRequest`, and `PersonDto` to include `roleIds` / `roles`.
5. **Frontend Presentation Components (`Cakra.Web`)**:
   - `PersonModal.vue` (`SCR-ORG-002`): Fetch available roles on mount, render checkbox list with descriptions, enforce at least 1 role selected, lock `Administrator` role if editing current user's person.
   - `PersonManagementView.vue` (`SCR-ORG-001`): Add "Roles" column rendering badges, expand client-side search to filter by role names.
6. **Automated Testing**:
   - Unit tests for command handlers, validators, self-demotion rules, and service methods.
   - Integration tests for `OrganizationController` covering role retrieval and atomic person + role creation/update.

## Excluded

- Modifying IAM tables (`identity.UserAccounts`, `identity.UserSessions`).
- UI for managing Team Memberships or Responsibilities (reserved for future CRs).
- Full dynamic CRUD for creating new arbitrary master Roles (master roles remain system-defined).

---

# 4. Technical Decisions

## TD-001: Atomic Transactional Creation & Update
`CreatePersonCommand` and `UpdatePersonCommand` will coordinate Person aggregate persistence and `RoleAssignment` junction synchronization within a single database transaction:
- In `CreatePersonAsync`: Person record is inserted, followed immediately by inserting `RoleAssignment` rows for all specified `RoleIds`.
- In `UpdatePersonAsync`: Person record attributes are updated, and the active `RoleAssignment` set is synchronized.
- Prevents partial failure scenarios where a Person exists without assigned roles.

## TD-002: Role Assignment Synchronization Algorithm
Role synchronization in `OrganizationService.UpdatePersonAsync` follows set reconciliation against active assignments:
1. Fetch currently active `RoleAssignment` records for `personId`.
2. Compute `toAdd = desiredRoleIds \ currentActiveRoleIds`.
3. Compute `toRevoke = currentActiveRoleIds \ desiredRoleIds`.
4. For each `roleId` in `toRevoke`: call `_roleAssignmentRepository.RevokeAsync(personId, roleId, now)`, dispatching `RoleRevoked`.
5. For each `roleId` in `toAdd`: call `_roleAssignmentRepository.AssignAsync(...)`, dispatching `RoleAssigned`.

## TD-003: Single-Query Left Join Projection for `PersonDto`
To avoid N+1 queries when loading `GET /api/v1/organization/persons/all`:
- `PersonRepository.GetAllAsync` will execute a single SQL query joining `[organization].[Persons]` with `[organization].[RoleAssignments]` (where `RevokedAt IS NULL`) and `[organization].[Roles]`.
- Active role names will be aggregated per Person using SQL Server's `STRING_AGG(r.[Name], ',') WITHIN GROUP (ORDER BY r.[Name])` or Dapper multi-mapping into `PersonDto(..., IReadOnlyList<string> Roles)`.

## TD-004: Self-Demotion Invariant Protection
Prevent administrative lockout:
- **Server-Side**: `UpdatePersonCommand` receives `ActorPersonId` (resolved from `CurrentContextProvider.CurrentPersonId`). If `ActorPersonId == TargetPersonId`, the validator or service verifies that the `Administrator` role ID remains in `RoleIds`. If absent, an `InvalidOperationException` ("Cannot remove the Administrator role from your own person record.") is thrown and returned as RFC 7807 400 Bad Request.
- **Client-Side**: In `SCR-ORG-002`, if `authStore.user?.personId === person.id`, the `Administrator` checkbox is disabled (`disabled="true"`) with an explanatory tooltip.

## TD-005: Retention of Roles on Deactivation
`DeactivatePersonCommand` only mutates `Person.Status = INACTIVE` and updates `UpdatedAt`. It does **not** invoke `RevokeAsync` on `RoleAssignments`. When `ActivatePersonCommand` transitions the status back to `ACTIVE`, the historical role assignments are already active and immediately effective.

---

# 5. Component Responsibilities

| Component | Responsibility |
|-----------|----------------|
| `0019_seed_operational_user_role.sql` | Database migration idempotently inserting `Operational User` master role. |
| `RoleDto` | Read-only DTO representing an organizational role (`Guid Id, string Name, string? Description`). |
| `PersonDto` | Enhanced DTO exposing identity attributes and `IReadOnlyList<string> Roles`. |
| `IOrganizationService` / `OrganizationService` | Transactional coordinator executing atomic person create/update, role set reconciliation, and domain event dispatching. |
| `CreatePersonCommand` / `CreatePersonCommandHandler` | MediatR command and handler for atomic person creation with role IDs. |
| `UpdatePersonCommand` / `UpdatePersonCommandHandler` | MediatR command and handler for atomic person updates with role IDs and self-demotion verification. |
| `IOrganizationQueryService` / `OrganizationQueryService` | Exposes `ListAllRolesAsync()` and `ListAllPersonsAsync()` (with role projection). |
| `PersonRepository` | Executes optimized Dapper queries projecting Persons and active aggregated roles. |
| `OrganizationController` | Exposes `GET /roles`, `POST /persons` (with roles), `PUT /persons/{id}` (with roles), guarded by `[Authorize]`. |
| `api/persons.ts` | Frontend API client defining `RoleDto`, `listRoles()`, and updated request/response interfaces. |
| `PersonModal.vue` (`SCR-ORG-002`) | Renders role checkboxes with names and descriptions, enforces $\ge 1$ role, and disables self-demotion. |
| `PersonManagementView.vue` (`SCR-ORG-001`) | Renders Roles column with badge pills, integrates role search into client-side filtering. |

---

# 6. Integration Design

| Source | Target | Purpose |
|--------|--------|---------|
| `SCR-ORG-002` (`PersonModal.vue`) | `api/persons.ts: listRoles()` | Fetch master roles to populate checkboxes. |
| `SCR-ORG-002` (`PersonModal.vue`) | `api/persons.ts: createPerson() / updatePerson()` | Send person attributes + `roleIds` array. |
| `SCR-ORG-001` (`PersonManagementView.vue`) | `api/persons.ts: listAllPersons()` | Load persons populated with active role badges. |
| `OrganizationController` | `IMediator` | Dispatch `CreatePersonCommand` and `UpdatePersonCommand`. |
| `OrganizationController` | `IOrganizationQueryService` | Call `ListAllRolesAsync()`. |
| `CreatePersonCommandHandler` | `IOrganizationService` | Invoke atomic `CreatePersonAsync(..., roleIds)`. |
| `UpdatePersonCommandHandler` | `IOrganizationService` | Invoke atomic `UpdatePersonAsync(..., roleIds, actorPersonId)`. |
| `OrganizationService` | `IPersonRepository` | Insert or update `Person` aggregate. |
| `OrganizationService` | `IRoleAssignmentRepository` | Synchronize active role junction records. |
| `OrganizationService` | `IDomainEventDispatcher` | Dispatch `PersonCreated`, `RoleAssigned`, `RoleRevoked`. |

---

# 7. Data Ownership

| Data Concept | Owning Module | Table | Read Consumers |
|--------------|---------------|-------|----------------|
| Organizational Persons | Organization | `[organization].[Persons]` | Organization, Identity, Request, WorkPackage, Customer |
| Organizational Master Roles | Organization | `[organization].[Roles]` | Organization, Identity (RBAC), UI Dropdowns |
| Role Assignments | Organization | `[organization].[RoleAssignments]` | Organization, Identity (`AuthorizationService`), UI Views |

---

# 8. Database Design

## New Tables
None.

## Modified Tables
None. Existing schema tables are preserved without structural alteration:
- `[organization].[Persons]`
- `[organization].[Roles]`
- `[organization].[RoleAssignments]`

## Relationships
- **Junction**: `[organization].[RoleAssignments]` relates `PersonId` (`Persons.Id`) to `RoleId` (`Roles.Id`), with timestamps `AssignedAt` and `RevokedAt` (`RevokedAt IS NULL` signifies active membership).

## Migration Considerations
- Script `0019_seed_operational_user_role.sql` embedded in `Cakra.Api`:
  ```sql
  IF NOT EXISTS (SELECT 1 FROM [organization].[Roles] WHERE [Name] = 'Operational User')
  BEGIN
      INSERT INTO [organization].[Roles] ([Id], [Name], [Description], [CreatedAt])
      VALUES (NEWID(), 'Operational User', 'Standard operational user participating in operational workflows', SYSUTCDATETIME());
  END;
  ```
- Fully idempotent, runs through the existing DbUp runner pipeline on application startup.

---

# 9. Cross-Cutting Concerns

## Security & Authorization
- `GET /api/v1/organization/roles`: Guarded by `[Authorize]`.
- `POST /api/v1/organization/persons`: Guarded by `[Authorize(Roles = "Administrator,Admin")]`.
- `PUT /api/v1/organization/persons/{id}`: Guarded by `[Authorize(Roles = "Administrator,Admin")]`.
- Client-side navigation to `/admin/persons` guarded by `requiresRole: 'Administrator'`.

## Domain Invariants & Validation
- **Mandatory Role**: A Person must have at least one active role (`RoleIds.Count >= 1`).
- **Self-Demotion Lockout**: An administrator cannot remove `Administrator` role from themselves.
- **Audit & Events**: All role additions and revocations emit `RoleAssigned` and `RoleRevoked` domain events.

## Performance
- Avoid N+1 queries by aggregating roles in `PersonRepository.GetAllAsync` using `STRING_AGG` or multi-mapping.
- Master roles fetched once and cached in memory or fetched on modal open since role master data is small and static.

---

# 10. Implementation Constraints

1. **Dapper Only**: Persistence operations must strictly use Dapper parameterized queries; no ORMs or EF Core.
2. **Schema Isolation**: Zero foreign keys or joins to `identity` schema.
3. **Atomic Unit of Work**: Person and RoleAssignment mutations must execute within a shared connection/transaction or atomic service boundary.
4. **Vue 3 Conventions**: Components must adhere to `<script setup lang="ts">`, utilizing Pinia `useAuthStore` for current user resolution and Bootstrap 5 styling tokens consistent with Cakra.Web.

---

# 11. Acceptance Conditions

1. `GET /api/v1/organization/roles` returns HTTP 200 with all master roles including `Administrator`, `Management`, `Operational User`, `Programmer`, and `Implementator`.
2. `POST /api/v1/organization/persons` creates a Person and assigns all specified `RoleIds` atomically, rejecting payloads with empty `RoleIds` with HTTP 400.
3. `PUT /api/v1/organization/persons/{id}` synchronizes `RoleIds`, activating newly selected roles and setting `RevokedAt` on deselected roles.
4. Attempting to remove the `Administrator` role when updating one's own Person record fails with HTTP 400 Bad Request.
5. In `SCR-ORG-002`, available roles appear as checkboxes with descriptions; submitting without any checked role displays an inline validation error.
6. In `SCR-ORG-002`, the `Administrator` checkbox is disabled with a hint when an administrator edits their own profile.
7. In `SCR-ORG-001`, each person row displays active role badges in a "Roles" column, and searching by a role name (e.g. "Management") filters the table rows accordingly.
8. Deactivating and reactivating a person preserves their assigned roles.
9. All unit and integration tests pass cleanly.
