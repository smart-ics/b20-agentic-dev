---
Title: Person Role Assignment in SCR-ORG-002 and Table Roles Display Implementation Plan
Code: CR-027
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-08
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Person Role Assignment in `SCR-ORG-002` and Table Roles Display in `SCR-ORG-001` per [FEAT-ORG-001-manage-persons.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-ORG-001-manage-persons.md) (v1.1) and realized by [CR-027-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-027-ARCHITECTURE.md).

**Planning Mode:** FEATURE-PLANNING

**Referenced artifacts:**
- FEATURE: [FEAT-ORG-001-manage-persons.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-ORG-001-manage-persons.md) (v1.1)
- ARCHITECTURE: [CR-027-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-027-ARCHITECTURE.md)
- FEASIBILITY-ASSESSMENT: [CR-027-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-027-FEASIBILITY-ASSESSMENT.md) (Planning Readiness: `READY-FOR-PLANNING`)

**Architecture Applicability:** ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers the end-to-end technical implementation of role assignment capabilities within organizational Person management:

1. Provisioning master data migration for `Operational User` in `[organization].[Roles]` and updating deployment seed scripts.
2. Exposing the master roles lookup API endpoint (`GET /api/v1/organization/roles`).
3. Updating Person creation and update commands (`CreatePersonCommand`, `UpdatePersonCommand`) and services to atomically synchronize `RoleAssignments` and enforce domain invariants ($\ge 1$ mandatory role, self-demotion prevention).
4. Expanding `PersonDto` and optimizing `PersonRepository` queries to aggregate active role names in a single database round-trip without N+1 queries.
5. Updating the frontend API client (`api/persons.ts`) with typed contracts.
6. Enriching `SCR-ORG-002` (`PersonModal.vue`) with multi-select role checkboxes, descriptions, validation, and self-demotion lockout.
7. Enriching `SCR-ORG-001` (`PersonManagementView.vue`) with a "Roles" column rendering active badges and integrating role names into keyword search.
8. Verifying all functionality with comprehensive unit and integration tests.

---

# 3. Dependencies

**External Dependencies:** None

**Slice Dependencies:** Slices are sequenced through explicit dependencies declaring true implementation prerequisites:
- `P1-S01` (Migration) has no dependencies.
- `P1-S02` (Roles Query & API) depends on `P1-S01`.
- `P2-S03` (Atomic Commands & Domain Synchronization) depends on `P1-S02`.
- `P2-S04` (PersonDto Role Aggregation & Controller Binding) depends on `P2-S03`.
- `P3-S05` (Frontend API Client) depends on `P2-S04`.
- `P3-S06` (PersonModal SCR-ORG-002) depends on `P3-S05`.
- `P3-S07` (PersonManagementView SCR-ORG-001) depends on `P3-S06`.
- `P4-S08` (Unit & Integration Testing) depends on `P3-S07`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|-------|-----------------------|---------------|----------|
| P1 - Master Data & Roles Query API | IMPLEMENTED | GO | 2/2 |
| P2 - Backend Domain & Transactional Synchronization | IMPLEMENTED | GO | 2/2 |
| P3 - Frontend API Client & Presentation | IMPLEMENTED | GO | 3/3 |
| P4 - Verification & Automated Testing | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Master Data & Roles Query API

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P1-S01

**Title:** Database Migration for Operational User Master Role

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Ensure standard master role `Operational User` exists in `[organization].[Roles]` alongside `Administrator`, `Management`, `Programmer`, and `Implementator`.

**Depends On:** None

**Repository:** `b20-agentic-dev`

**Completion Criteria:**
- Create `cakra/src/backend/Cakra.Api/Migrations/Scripts/0019_seed_operational_user_role.sql` with idempotent insert.
- Update `cakra/deploy/seed-admin.ps1` to include `Operational User` in standard role seeding.
- Verified that running DbUp migration applies script cleanly.

**Notes:** Created idempotent DbUp migration script `cakra/src/backend/Cakra.Api/Migrations/Scripts/0019_seed_operational_user_role.sql` inserting 'Operational User' into `[organization].[Roles]` if not already present. Updated deployment seed script `cakra/deploy/seed-admin.ps1` to include 'Operational User' in standard role seeding. Verified DbUp embedded resource inclusion in `Cakra.Api.dll` and successful build.

**Changed Files:**
- `cakra/src/backend/Cakra.Api/Migrations/Scripts/0019_seed_operational_user_role.sql`
- `cakra/deploy/seed-admin.ps1`

---

### P1-S02

**Title:** Master Roles Query Service & REST Endpoint

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Expose active master roles via `IOrganizationQueryService` and `OrganizationController.cs`.

**Depends On:** P1-S01

**Repository:** `b20-agentic-dev`

**Completion Criteria:**
- Define `RoleDto(Guid Id, string Name, string? Description)` in `Cakra.Modules.Organization.Models`.
- Add `Task<IReadOnlyList<RoleDto>> ListAllRolesAsync(CancellationToken cancellationToken = default)` to `IOrganizationQueryService` and implement in `OrganizationQueryService.cs` delegating to `IRoleRepository.GetAllAsync`.
- Expose `GET /api/v1/organization/roles` in `OrganizationController.cs` returning `IReadOnlyList<RoleDto>` guarded by `[Authorize]`.

**Notes:** Defined `RoleDto(Guid Id, string Name, string? Description)` in `Cakra.Modules.Organization.Models`. Added `ListAllRolesAsync` and synchronous convenience overload `ListAllRoles` to `IOrganizationQueryService`. Implemented `ListAllRolesAsync` in `OrganizationQueryService.cs` delegating directly to `IRoleRepository.GetAllAsync` and registered in `OrganizationModule.cs`. Exposed `GET /api/v1/organization/roles` in `OrganizationController.cs` guarded by `[Authorize]` returning `IReadOnlyList<RoleDto>`. Added unit tests in `OrganizationQueryServiceTests.cs` and integration tests in `OrganizationControllerTests.cs`. All unit (525) and integration (183) tests pass cleanly.

**Changed Files:**
- `cakra/src/backend/Cakra.Modules.Organization/Models/RoleDto.cs`
- `cakra/src/backend/Cakra.Modules.Organization/IOrganizationQueryService.cs`
- `cakra/src/backend/Cakra.Modules.Organization/Services/OrganizationQueryService.cs`
- `cakra/src/backend/Cakra.Modules.Organization/OrganizationModule.cs`
- `cakra/src/backend/Cakra.Api/Controllers/OrganizationController.cs`
- `cakra/tests/backend/Cakra.Tests.Unit/Organization/OrganizationQueryServiceTests.cs`
- `cakra/tests/backend/Cakra.Tests.Integration/Api/OrganizationControllerTests.cs`

---

## P2 - Backend Domain & Transactional Synchronization

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P2-S03

**Title:** Atomic Commands with RoleIds, Service Set Reconciliation & Self-Demotion Invariant

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Support atomic person creation and updates with role IDs, set reconciliation, and self-demotion protection in domain commands and services.

**Depends On:** P1-S02

**Repository:** `b20-agentic-dev`

**Completion Criteria:**
- Update `CreatePersonCommand` with `IReadOnlyList<Guid> RoleIds`.
- Update `CreatePersonCommandValidator`: enforce `RuleFor(x => x.RoleIds).NotEmpty().WithMessage("At least one role must be assigned.")`.
- Update `UpdatePersonCommand` with `IReadOnlyList<Guid> RoleIds` and `Guid? ActorPersonId`.
- Update `UpdatePersonCommandValidator`: enforce `NotEmpty()` for `RoleIds` and validate that if `ActorPersonId == Id`, the `Administrator` role is not removed.
- Update `IOrganizationService.CreatePersonAsync` and `OrganizationService.CreatePersonAsync`: insert Person and assign all `RoleIds` atomically, dispatching `RoleAssigned` domain events.
- Update `IOrganizationService.UpdatePersonAsync` and `OrganizationService.UpdatePersonAsync`: perform set reconciliation (compute additions and revocations against active `RoleAssignments`), update timestamps, dispatch `RoleAssigned` and `RoleRevoked` events.
- Retain existing `RoleAssignments` untouched during `DeactivatePersonCommand`.

**Notes:** Updated `CreatePersonCommand` and `UpdatePersonCommand` to include `RoleIds` and `ActorPersonId` with convenience constructors for backwards compatibility. Enforced `RuleFor(x => x.RoleIds).NotEmpty().WithMessage("At least one role must be assigned.")` on both validators. Wired self-demotion protection in `UpdatePersonCommandValidator` (checking against `IRoleRepository` lookup and optional `administratorRoleId`) and in `OrganizationService.UpdatePersonAsync` throwing `InvalidOperationException("Cannot remove the Administrator role from your own person record.")`. Implemented atomic assignment in `CreatePersonAsync` emitting `PersonCreated` and `RoleAssigned` events. Implemented set reconciliation in `UpdatePersonAsync` (computing additions and revocations against active assignments) emitting `RoleAssigned` and `RoleRevoked` domain events. Verified that `DeactivatePersonCommand` retains role assignments untouched. Registered `UpdatePersonCommandValidator` in `OrganizationModule.cs`. Updated and expanded unit tests in `OrganizationServiceTests.cs` covering multi-role creation, set reconciliation, self-demotion rejection, validator rules, and deactivation role retention.
Remediation (Iteration 0):
- Fixed blocker F-CR027-P2S03-01: Added default interface method implementations in `IOrganizationService.cs` for overloads accepting `roleIds`, and implemented both `CreatePersonAsync` and `UpdatePersonAsync` role overloads in `InMemoryOrganizationService` in `OrganizationControllerTests.cs` to track `PersonRoles`.
- Fixed minor F-CR027-P2S03-02: Enhanced `UpdatePersonCommandValidator` to accept `IRoleAssignmentRepository` for active assignment verification during self-demotion validation while retaining documented fallback behavior. Registered both dependencies in `OrganizationModule.cs`.
- Supported `IsLegacy` execution on `CreatePersonCommand` and `UpdatePersonCommand` to maintain backwards compatibility for legacy callers without role IDs.
- Verified that `dotnet build cakra/Cakra.sln` succeeds with 0 errors and all unit (531) and integration (183) tests pass cleanly.

**Changed Files:**
- `cakra/src/backend/Cakra.Modules.Organization/Commands/CreatePersonCommand.cs`
- `cakra/src/backend/Cakra.Modules.Organization/Commands/UpdatePersonCommand.cs`
- `cakra/src/backend/Cakra.Modules.Organization/Services/IOrganizationService.cs`
- `cakra/src/backend/Cakra.Modules.Organization/Services/OrganizationService.cs`
- `cakra/src/backend/Cakra.Modules.Organization/OrganizationModule.cs`
- `cakra/tests/backend/Cakra.Tests.Unit/Organization/OrganizationServiceTests.cs`
- `cakra/tests/backend/Cakra.Tests.Integration/Api/OrganizationControllerTests.cs`

---

### P2-S04

**Title:** PersonDto Role Aggregation & API Controller Binding

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Enrich `PersonDto` with assigned roles, optimize `PersonRepository` queries, and bind role payloads in `OrganizationController.cs`.

**Depends On:** P2-S03

**Repository:** `b20-agentic-dev`

**Completion Criteria:**
- Update `PersonDto` to include `IReadOnlyList<string> Roles`.
- Update `PersonRepository.GetAllAsync()` and `GetByIdAsync()` to execute a single query joining `[organization].[RoleAssignments]` (where `RevokedAt IS NULL`) and `[organization].[Roles]`, aggregating active role names into `PersonDto.Roles`.
- Update `CreatePersonRequest` and `UpdatePersonRequest` records in `OrganizationController.cs` to include `IReadOnlyList<Guid> RoleIds`.
- In `OrganizationController.UpdatePerson`, resolve actor person ID from `CurrentContextProvider` or security context and pass to `UpdatePersonCommand`.

**Notes:** Updated `PersonDto` to include `IReadOnlyList<string> Roles` defaulting to empty array for backwards compatibility. Implemented single query joining `[organization].[Persons]`, `[organization].[RoleAssignments]` (`RevokedAt IS NULL`), and `[organization].[Roles]` using SQL Server `STRING_AGG` in `OrganizationQueryService` (`GetPersonByIdAsync`, `ListActivePersonsAsync`, `ListAllPersonsAsync`) and in `IPersonRepository`/`PersonRepository` (`GetPersonDtoByIdAsync`, `GetAllPersonDtosAsync`) projected through `PersonDtoRow`. Updated `CreatePersonRequest` and `UpdatePersonRequest` in `OrganizationController.cs` to include `IReadOnlyList<Guid>? RoleIds = null`. In `OrganizationController.CreatePerson`, validated non-empty `RoleIds` and bound `RoleIds` to `CreatePersonCommand`. In `OrganizationController.UpdatePerson`, resolved actor person ID from `ICurrentContextProvider` and `User.Claims` (`personId`/`person_id`), passed to `UpdatePersonCommand`, and converted self-demotion `InvalidOperationException` to HTTP 400 Bad Request ProblemDetails. Added unit tests in `OrganizationQueryServiceTests.cs` and integration tests in `OrganizationControllerTests.cs` covering role payload binding, empty role rejection, self-demotion rejection, and aggregated role projections. Verified that `dotnet build cakra/Cakra.sln` succeeds with 0 errors and all unit (534) and integration (190) tests pass cleanly.

**Changed Files:**
- `cakra/src/backend/Cakra.Modules.Organization/Models/PersonDto.cs`
- `cakra/src/backend/Cakra.Modules.Organization/Models/PersonDtoRow.cs`
- `cakra/src/backend/Cakra.Modules.Organization/Persistence/IPersonRepository.cs`
- `cakra/src/backend/Cakra.Modules.Organization/Persistence/PersonRepository.cs`
- `cakra/src/backend/Cakra.Modules.Organization/Services/OrganizationQueryService.cs`
- `cakra/src/backend/Cakra.Api/Controllers/OrganizationController.cs`
- `cakra/tests/backend/Cakra.Tests.Unit/Organization/OrganizationQueryServiceTests.cs`
- `cakra/tests/backend/Cakra.Tests.Integration/Api/OrganizationControllerTests.cs`

---

## P3 - Frontend API Client & Presentation

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P3-S05

**Title:** Frontend API Client Module Updates

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Update TypeScript contracts and helper functions in `api/persons.ts`.

**Depends On:** P2-S04

**Repository:** `b20-agentic-dev`

**Completion Criteria:**
- Export `RoleDto` interface (`id: string, name: string, description?: string`).
- Export `listRoles(): Promise<RoleDto[]>`.
- Update `CreatePersonRequest` to include `roleIds: string[]`.
- Update `UpdatePersonRequest` to include `roleIds: string[]`.
- Update `PersonDto` to include `roles: string[]`.

**Notes:** Exported `RoleDto` interface (`id: string; name: string; description?: string | null;`) and `listRoles(): Promise<RoleDto[]>` calling `httpClient.get<RoleDto[]>('/organization/roles')`. Updated `PersonDto` to include `roles?: string[]`. Updated `CreatePersonRequest` and `UpdatePersonRequest` to include `roleIds?: string[]`. Verified frontend TypeScript typecheck (`npx vue-tsc --noEmit`) and production bundle build (`npm run build`) pass cleanly with 0 errors. Verified full backend test suite (`dotnet test cakra/Cakra.sln`) passes with 0 failures (534 unit, 190 integration).

**Changed Files:**
- `cakra/src/frontend/Cakra.Web/src/api/persons.ts`

---

### P3-S06

**Title:** SCR-ORG-002 (PersonModal.vue) Role Picker & Self-Demotion Guard

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Enrich `PersonModal.vue` with multi-select role checkboxes, mandatory role validation, and self-demotion lockout.

**Depends On:** P3-S05

**Repository:** `b20-agentic-dev`

**Completion Criteria:**
- Fetch available roles on modal mount/open via `listRoles()`.
- Render a dedicated "Roles" fieldset containing checkbox items with role names and descriptions.
- In Create mode: default selection empty; enforce at least one role must be checked before submitting.
- In Edit mode: pre-populate checkboxes with the person's active roles.
- Self-Demotion Guard: If editing the logged-in administrator's record (`authStore.user?.personId === person.id`), disable the `Administrator` checkbox with a protective badge/hint.
- Client-side error validation: display inline error if form submitted with zero roles checked.
- On submit, include `roleIds` in create/update payloads and emit `saved`.

**Notes:** Architecture §4 (TD-004), §5, §11. Fetched master roles via `listRoles()` on mount and when modal opens. Rendered dedicated "Roles" fieldset containing checkboxes with role names and descriptions underneath, with loading and retry states. In Create mode, default selection is empty; enforced mandatory role selection requiring `selectedRoleIds.length > 0` with inline validation error message (`At least one role must be assigned.`). In Edit mode, pre-populated checkboxes matching active role names from `person.roles`. Implemented Self-Demotion Guard by checking `authStore.user?.personId === resolvePersonId()`; when editing the current logged-in user and role is `Administrator`, disabled the checkbox and displayed a protective badge (`Protected: Cannot remove Administrator from your own profile`). In `auth.ts`, exposed `user` computed property aliasing `currentUser` for consistency with CR-027 specifications. Included `roleIds: [...selectedRoleIds.value]` in `createPerson` and `updatePerson` payloads on submit and emitted `saved`. Verified TypeScript typecheck (`npx vue-tsc --noEmit`) and production bundle build (`npm run build`) pass cleanly with 0 errors.

**Changed Files:**
- `cakra/src/frontend/Cakra.Web/src/components/PersonModal.vue`
- `cakra/src/frontend/Cakra.Web/src/stores/auth.ts`

---

### P3-S07

**Title:** SCR-ORG-001 (PersonManagementView.vue) Roles Column & Search Filtering

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Surface assigned role badges in the Person Management table and support searching by role name.

**Depends On:** P3-S06

**Repository:** `b20-agentic-dev`

**Completion Criteria:**
- Add a "Roles" column to the `SCR-ORG-001` data table between Email and Status.
- Render assigned roles as badge pills (e.g. `BaseBadge` or styled badge pills) displaying each role name.
- Update `filteredPersons` computed property to match the search query against assigned role names in addition to first name, last name, and email.
- Verified that creating or editing a person refreshes the table with updated role badges.

**Notes:** Architecture §4, §5, §11. Added "Roles" column header and data cells to `SCR-ORG-001` (`PersonManagementView.vue`) between Email and Status. Rendered assigned roles as badge pills using `BaseBadge` component mapped to executive role theme variants (e.g., Administrator -> purple, Management -> indigo, Operational User -> cyan, Programmer -> emerald, Implementator -> amber) with `size="sm"`. Displayed muted placeholder (`<span class="text-slate-500 text-xs italic">No roles</span>`) when a person has no assigned roles. Updated `filteredPersons` computed property to support case-insensitive substring searching across assigned role names (`(p.roles || []).some((r) => r.toLowerCase().includes(query))`), in addition to first name, last name, email, and full name. Updated search input placeholder to reflect role searching. Bound modal `@saved` event to reload the list with updated role badges via `onPersonSaved` / `loadPersons()`. Verified frontend TypeScript typecheck (`npm run type-check` / `vue-tsc --noEmit`) and production bundle build (`npm run build`) pass cleanly with 0 errors.

**Changed Files:**
- `cakra/src/frontend/Cakra.Web/src/views/PersonManagementView.vue`

---

## P4 - Verification & Automated Testing

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P4-S08

**Title:** Automated Unit & Integration Testing

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Verify all domain logic, invariants, validation rules, API endpoints, and role synchronizations through automated tests.

**Depends On:** P3-S07

**Repository:** `b20-agentic-dev`

**Completion Criteria:**
- Unit tests in `Cakra.Tests.Unit/Organization`:
  - `CreatePersonCommand` creates person and assigns roles, dispatching `RoleAssigned`.
  - `CreatePersonCommandValidator` rejects empty `RoleIds`.
  - `UpdatePersonCommand` synchronizes roles (adds newly selected, revokes unselected), dispatching `RoleAssigned` and `RoleRevoked`.
  - `UpdatePersonCommandValidator` prevents removing `Administrator` role when `ActorPersonId == TargetPersonId`.
  - `ListAllRolesAsync` returns all master roles.
- Integration tests in `Cakra.Tests.Integration/Api/OrganizationControllerTests.cs`:
  - `GET /api/v1/organization/roles` returns 200 OK with master roles.
  - `POST /api/v1/organization/persons` creates person with roles and returns 201 Created.
  - `PUT /api/v1/organization/persons/{id}` synchronizes roles and returns 200 OK.
  - Verification that full test suite (`dotnet test`) passes with 0 failures.

**Notes:** Architecture §3, §11. Verified all unit and integration test scenarios across domain commands, validators, query services, and API controllers.
- Verified unit tests in `Cakra.Tests.Unit/Organization`:
  - `CreatePersonCommand` creates person and assigns roles, emitting `PersonCreated` and `RoleAssigned` domain events (`OrganizationServiceTests.cs`).
  - `CreatePersonCommandValidator` rejects empty `RoleIds` with explicit validation rule test (`CreatePersonCommandValidator_rejects_empty_RoleIds`).
  - `UpdatePersonCommand` reconciles roles (adds newly selected, revokes unselected), emitting `RoleAssigned` and `RoleRevoked` domain events (`UpdatePersonCommand_performs_role_set_reconciliation_adding_and_revoking_roles`).
  - `UpdatePersonCommandValidator` prevents removing `Administrator` role when `ActorPersonId == TargetPersonId` (`UpdatePersonCommandValidator_validates_self_demotion_invariant` and `UpdatePersonCommandValidator_with_assignment_repository_checks_current_admin_membership`).
  - `ListAllRolesAsync` delegates to `IRoleRepository.GetAllAsync` and returns all 5 master roles (`Administrator`, `Management`, `Operational User`, `Programmer`, `Implementator`) in `OrganizationQueryServiceTests.cs`.
- Verified integration tests in `Cakra.Tests.Integration/Api/OrganizationControllerTests.cs`:
  - `GET /api/v1/organization/roles` returns 200 OK with all 5 master roles for authenticated callers and 401 for unauthenticated.
  - `POST /api/v1/organization/persons` creates person with roles and returns 201 Created, and rejects empty roles with 400 Bad Request.
  - `PUT /api/v1/organization/persons/{id}` synchronizes roles and returns 200 OK, and rejects empty roles with 400 Bad Request.
  - Attempting to remove the `Administrator` role on self-update returns 400 Bad Request ProblemDetails.
- Full backend test suite (`dotnet test cakra/Cakra.sln`): 100% pass rate with 0 failures (536 unit tests passed, 190 integration tests passed; total 726 passed).
- Frontend type-check and bundle build (`npx vue-tsc --noEmit && npm run build`): completed cleanly with 0 errors.

**Changed Files:**
- `cakra/tests/backend/Cakra.Tests.Unit/Organization/OrganizationServiceTests.cs`
- `cakra/tests/backend/Cakra.Tests.Unit/Organization/OrganizationQueryServiceTests.cs`
- `cakra/tests/backend/Cakra.Tests.Integration/Api/OrganizationControllerTests.cs`

---

# 6. Change Log

- **2026-10-08**: Initial plan created and approved by `ica-architect` for `CR-027`.
