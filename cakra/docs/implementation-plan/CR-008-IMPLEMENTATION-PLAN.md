---
Title: Person Management - Organizational Person Create, Update, Activate, Deactivate Implementation Plan
Code: CR-008
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-05
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Person Management capability per [FEAT-ORG-001-manage-persons.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-ORG-001-manage-persons.md) and realized by [CR-008-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-008-ARCHITECTURE.md).

**Planning Mode:** FEATURE-PLANNING

**Referenced artifacts:**
- FEATURE: [FEAT-ORG-001-manage-persons.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-ORG-001-manage-persons.md)
- ARCHITECTURE: [CR-008-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-008-ARCHITECTURE.md)
- FEASIBILITY-ASSESSMENT: [CR-008-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-008-FEASIBILITY-ASSESSMENT.md) (Planning Readiness: `READY-FOR-PLANNING`)

**Architecture Applicability:** ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers the implementation of the Person Management feature for organizational Person lifecycle management (create, update, activate, deactivate). The scope includes:

**Already Implemented (Backend - no further work required):**
- `ActivatePersonCommand`, `ActivatePersonCommandHandler`, `ActivatePersonCommandValidator` in `Cakra.Modules.Organization`
- `ListAllPersonsAsync` in `IOrganizationQueryService` and `OrganizationQueryService`
- All REST API write and read endpoints in `OrganizationController.cs` (`POST`, `PUT`, `PUT /activate`, `PUT /deactivate`, `GET /all`)
- Role-based authorization `[Authorize(Roles = "Administrator,Admin")]` on write endpoints
- Navigation link in `App.vue` and route `/admin/persons` in `router/index.ts` with role guard

**Remaining Work (Frontend, Testing, Documentation):**
- Frontend API client module `src/api/persons.ts`
- Full implementation of `PersonManagementView.vue` (SCR-ORG-001) replacing the stub
- New `PersonModal.vue` component (SCR-ORG-002) for create/edit operations
- Unit tests for `ActivatePersonCommandHandler` in `Cakra.Tests.Unit/Organization/`
- Integration tests for `OrganizationController` write endpoints and RBAC in `Cakra.Tests.Integration/Api/OrganizationControllerTests.cs`
- Documentation updates: `screen-inventory.md`, `00-screen-inventory.md`, `feature-catalog.md`, `feature-traceability.md`

---

# 3. Dependencies

**External Dependencies:** None

**Slice Dependencies:** See `Depends On` field in each slice definition below.

---

# 4. Progress Summary

Plan status values:
- NOT-STARTED
- IN-PROGRESS
- BLOCKED
- COMPLETED

Execution Approval values:
- PENDING
- APPROVED

Slice implementation status values:
- NOT-STARTED
- IN-PROGRESS
- IMPLEMENTED
- BLOCKED

Slice review status values:
- NOT-REVIEWED
- GO
- NO-GO

| Phase | Implementation Status | Review Status | Progress |
|---------|---------|---------|---------|
| P1 - Backend Domain & API (Complete) | IMPLEMENTED | GO | 2/2 |
| P2 - Frontend API Client & Components | IN-PROGRESS | NOT-REVIEWED | 3/3 |
| P3 - Testing | NOT-STARTED | NOT-REVIEWED | 0/2 |
| P4 - Documentation | IMPLEMENTED | NOT-REVIEWED | 1/1 |

---

# 5. Phases

## P1 - Backend Domain & API (Complete)

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

The following backend components are already implemented and verified in the current codebase:

### P1-S01

**Title:** Backend Domain Commands & Query Service Extension

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Verify all backend domain commands and query service extensions are implemented per architecture.

**Depends On:** None

**Repository:** Cakra.Modules.Organization

**Completion Criteria:**
- `ActivatePersonCommand`, `ActivatePersonCommandHandler`, `ActivatePersonCommandValidator` exist in `Commands/` - VERIFIED
- `ActivatePersonCommandHandler` registered in `OrganizationModule.cs` - VERIFIED
- `ListAllPersonsAsync` method exists in `IOrganizationQueryService` and implemented in `OrganizationQueryService` - VERIFIED
- All existing commands (`CreatePersonCommand`, `UpdatePersonCommand`, `DeactivatePersonCommand`) remain functional - VERIFIED

**Notes:** All backend domain work was completed prior to this planning cycle. No implementation work required.

---

### P1-S02

**Title:** Backend REST API Endpoints & Authorization

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Verify all REST API endpoints in `OrganizationController.cs` are implemented per architecture.

**Depends On:** P1-S01

**Repository:** Cakra.Api

**Completion Criteria:**
- `POST /api/v1/organization/persons` (CreatePerson) with `[Authorize(Roles = "Administrator,Admin")]` - VERIFIED
- `PUT /api/v1/organization/persons/{id:guid}` (UpdatePerson) with `[Authorize(Roles = "Administrator,Admin")]` - VERIFIED
- `PUT /api/v1/organization/persons/{id:guid}/activate` (ActivatePerson) with `[Authorize(Roles = "Administrator,Admin")]` - VERIFIED
- `PUT /api/v1/organization/persons/{id:guid}/deactivate` (DeactivatePerson) with `[Authorize(Roles = "Administrator,Admin")]` - VERIFIED
- `GET /api/v1/organization/persons/all` (ListAllPersons) without admin restriction - VERIFIED
- `CreatePersonRequest` and `UpdatePersonRequest` DTOs defined - VERIFIED
- RFC 7807 ProblemDetails error handling for 400, 401, 403, 404, 409 - VERIFIED
- Controller injects both `IMediator` and `IOrganizationQueryService` - VERIFIED

**Notes:** All backend API work was completed prior to this planning cycle. No implementation work required.

---

## P2 - Frontend API Client & Components

**Implementation Status:** IN-PROGRESS  
**Review Status:** NOT-REVIEWED

### P2-S03

**Title:** Frontend API Client Module - persons.ts

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Create `src/api/persons.ts` frontend API client module with typed interfaces and all CRUD + lifecycle functions matching the backend endpoints.

**Depends On:** P1-S02

**Repository:** Cakra.Web

**Completion Criteria:**
- `PersonDto` interface matching backend `PersonDto` (Id, FirstName, LastName, Email, Status, FullName, IsActive, CreatedAt, UpdatedAt)
- `CreatePersonRequest` interface (FirstName, LastName, Email)
- UpdatePersonRequest interface (FirstName, LastName, Email)
- `listAllPersons(): Promise<PersonDto[]>` → `GET /api/v1/organization/persons/all`
- `listActivePersons(): Promise<PersonDto[]>` → `GET /api/v1/organization/persons/active`
- `getPersonById(id: string): Promise<PersonDto>` → `GET /api/v1/organization/persons/{id}`
- `createPerson(payload: CreatePersonRequest): Promise<PersonDto>` → `POST /api/v1/organization/persons`
- `updatePerson(id: string, payload: UpdatePersonRequest): Promise<PersonDto>` → `PUT /api/v1/organization/persons/{id}`
- `activatePerson(id: string): Promise<PersonDto>` → `PUT /api/v1/organization/persons/{id}/activate`
- `deactivatePerson(id: string): Promise<PersonDto>` → `PUT /api/v1/organization/persons/{id}/deactivate`
- Follows `AxiosError` + RFC 7807 `ProblemDetailsPayload` error extraction pattern from `customers.ts` and `users.ts`
- Uses shared `httpClient` from `./http`

**Implementation Notes:**
- Created `src/api/persons.ts` in `src/frontend/Cakra.Web/src/api/`.
- Defined `PersonDto` interface matching backend `PersonDto` structure (`id`, `personId`, `firstName`, `lastName`, `email`, `status`, `fullName`, `isActive`, `createdAt`, `updatedAt`).
- Defined `CreatePersonRequest` and `UpdatePersonRequest` payload interfaces.
- Defined `ProblemDetailsPayload` interface and `extractErrorMessage(err, fallback)` handling `AxiosError` + RFC 7807 problem payloads.
- Implemented typed API functions using shared `httpClient` (`/organization/persons/all`, `/organization/persons/active`, `/organization/persons/{id}`, `POST /organization/persons`, `PUT /organization/persons/{id}`, `PUT /organization/persons/{id}/activate`, `PUT /organization/persons/{id}/deactivate`).
- Verified build and TypeScript type checking (`vue-tsc --noEmit && vite build`) passes cleanly with 0 errors.

**Changed Files:**
- `src/frontend/Cakra.Web/src/api/persons.ts`
- `docs/implementation-plan/CR-008-IMPLEMENTATION-PLAN.md`

**Notes:** Pattern reference: `src/api/customers.ts` and `src/api/users.ts`.

---

### P2-S04

**Title:** PersonManagementView.vue (SCR-ORG-001) - Full Implementation

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Replace the stub `PersonManagementView.vue` with full implementation per architecture.

**Depends On:** P2-S03

**Repository:** Cakra.Web

**Completion Criteria:**
- Displays screen title "Person Management" with `SCR-ORG-001` badge and `data-testid="person-management-view"`
- Metric ribbon: Total Persons, Active, Inactive counts (computed from loaded data)
- Search input (keyword filter across first name, last name, email)
- Status filter dropdown (All / Active / Inactive) - client-side filtering
- Data table with columns: First Name, Last Name, Email, Status (badge), Actions
- Status badge classes: `ACTIVE` → `badge text-bg-success`, `INACTIVE` → `badge text-bg-secondary`
- Action buttons per row: Edit (opens modal in edit mode), Activate (for inactive), Deactivate (for active)
- "Add Person" button opens `PersonModal.vue` in create mode
- Integrates `PersonModal.vue` (SCR-ORG-002) for create/edit operations
- Loads persons via `listAllPersons()` from `persons.ts` on mount
- Refresh button reloads data
- Error/success alert handling with `ProblemDetailsPayload` extraction
- Loading skeleton/spinner state
- Empty state handling
- Uses `<script setup lang="ts">`, Vue Router 4, Pinia auth store, Bootstrap 5 styling
- Consistent with `UserManagementView.vue` (SCR-USR-001) pattern

**Implementation Notes:**
- Completely replaced stub `src/views/PersonManagementView.vue` with full production implementation per SCR-ORG-001 specification.
- Configured screen header with title, `SCR-ORG-001` badge, and `data-testid="person-management-view"`.
- Implemented metric ribbon computing Total Persons, Active, and Inactive counts with respective test IDs (`stat-total-persons`, `stat-active-persons`, `stat-inactive-persons`).
- Added keyword search input (`data-testid="person-search-input"`) filtering across first name, last name, and email.
- Added status dropdown filter (`data-testid="status-filter-select"`) supporting ALL, ACTIVE, and INACTIVE client-side filtering.
- Implemented responsive data table (`data-testid="persons-table"`) displaying First Name, Last Name, Email, Status badge, and row action buttons.
- Applied badge classes: `ACTIVE` -> `badge text-bg-success`, `INACTIVE` -> `badge text-bg-secondary`.
- Implemented per-row action buttons for Edit (`data-testid="edit-person-btn"`), Activate (`data-testid="activate-person-btn"`), and Deactivate (`data-testid="deactivate-person-btn"`), guarded with admin authorization and loading indicators.
- Integrated `PersonModal.vue` (`SCR-ORG-002`) in create and edit modes, with automatic list refresh and success messaging upon `saved` emit.
- Implemented `listAllPersons()` data loading on component mount and via Refresh button (`data-testid="refresh-persons-btn"`).
- Implemented error alert (`data-testid="persons-error-alert"`) and success alert (`data-testid="persons-success-alert"`) using `extractErrorMessage` with RFC 7807 `ProblemDetailsPayload` handling.
- Added loading state with spinner (`data-testid="persons-loading-state"`) and empty state with clear filters action (`data-testid="empty-persons-state"`).
- Verified TypeScript checking and production build (`vue-tsc --noEmit && vite build`) passes with 0 errors.

**Changed Files:**
- `src/frontend/Cakra.Web/src/views/PersonManagementView.vue`
- `docs/implementation-plan/CR-008-IMPLEMENTATION-PLAN.md`

**Notes:** The existing stub at `src/views/PersonManagementView.vue` must be completely replaced. Reference: `UserManagementView.vue`.

---

### P2-S05

**Title:** PersonModal.vue (SCR-ORG-002) - Create/Edit Modal Component

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Create new `PersonModal.vue` component for creating and editing Person records.

**Depends On:** P2-S03

**Repository:** Cakra.Web

**Completion Criteria:**
- New file at `src/components/PersonModal.vue`
- Props: `show?: boolean`, `mode?: 'create' | 'edit'`, `person?: PersonDto`
- Emits: `close`, `saved` (with `PersonDto` payload)
- Form fields: First Name (required, max 100), Last Name (required, max 100), Email (required, email format, max 255)
- Client-side validation before API call (required fields, email format)
- Create mode: calls `createPerson()` from `persons.ts`
- Edit mode: calls `updatePerson(id, payload)` from `persons.ts`
- Handles `409 Conflict` (duplicate email) and generic API errors via `ProblemDetailsPayload` extraction
- Submit button disabled during submission
- Modal closes on `close` emit; parent refreshes list on `saved` emit
- Follows `CreateCustomerModal.vue` pattern for error handling and form structure
- Uses `<script setup lang="ts">`, Vue 3 Composition API, Bootstrap 5 modal styling
- `data-testid="person-modal"` for testing

**Implementation Notes:**
- Created `src/components/PersonModal.vue` conforming to SCR-ORG-002 requirements.
- Configured props `show`, `mode` (`'create' | 'edit'`), and `person` (`PersonDto | null`) with default values.
- Implemented emits `close` and `saved` (passing `PersonDto`).
- Implemented form inputs for First Name (max 100), Last Name (max 100), and Email (max 255) with client-side validation for required fields, max lengths, and email format.
- Create mode dispatches `createPerson` and edit mode dispatches `updatePerson`.
- Implemented inline error handling for RFC 7807 `ProblemDetailsPayload` and 409 conflict messages.
- Form controls and submit buttons disabled during submission with loading spinner state.
- Keyboard (Escape key) and backdrop click listeners dismiss modal when not submitting.
- Includes `data-testid="person-modal"`, `data-screen-id="SCR-ORG-002"`, and test IDs for inputs and action buttons.
- Verified TypeScript compilation and build (`vue-tsc --noEmit && vite build`) passed with 0 errors.

**Changed Files:**
- `src/frontend/Cakra.Web/src/components/PersonModal.vue`
- `docs/implementation-plan/CR-008-IMPLEMENTATION-PLAN.md`

**Notes:** Reference: `CreateCustomerModal.vue` for error handling pattern; `UserAccountModal.vue` for modal structure pattern (but simpler - no dropdowns needed).

---

## P3 - Testing

**Implementation Status:** NOT-STARTED  
**Review Status:** NOT-REVIEWED

### P3-S06

**Title:** Backend Unit Tests - ActivatePersonCommandHandler

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Add unit tests for `ActivatePersonCommandHandler` in `Cakra.Tests.Unit/Organization/`.

**Depends On:** P1-S01

**Repository:** Cakra.Tests.Unit

**Completion Criteria:**
- Test file: `Cakra.Tests.Unit/Organization/ActivatePersonCommandHandlerTests.cs`
- Test: `ActivatePersonCommand_activates_inactive_person_and_dispatches_PersonActivated_event`
- Test: `ActivatePersonCommand_rejects_empty_PersonId` (validator)
- Test: `ActivatePersonCommandHandler_throws_when_person_not_found`
- Uses existing test fakes: `InMemoryPersonRepository`, `RecordingEventDispatcher`, `FixedClock`
- Follows pattern from `OrganizationServiceTests.cs` (e.g., `DeactivatePersonCommand_marks_person_inactive_and_dispatches_PersonDeactivated`)
- Verifies `PersonActivated` domain event is dispatched

**Implementation Notes:**
- Added `Cakra.Tests.Unit/Organization/ActivatePersonCommandHandlerTests.cs` covering:
  - `ActivatePersonCommand_activates_inactive_person_and_dispatches_PersonActivated_event`
  - `ActivatePersonCommand_rejects_empty_PersonId` (validator)
  - `ActivatePersonCommandHandler_throws_when_person_not_found`
  - Null guards for handler constructor and request argument
- Added `PersonActivated` domain event record per Architecture §7 and §9 in `Cakra.Modules.Organization/Domain/Events/PersonActivated.cs` and dispatched it in `OrganizationService.ActivatePersonAsync`.
- Added `PersonActivated_populates_event_properties` test in `OrganizationDomainEventTests.cs`.
- Exposed test fakes (`InMemoryPersonRepository`, `RecordingEventDispatcher`, `FixedClock`, etc.) as internal in `Cakra.Tests.Unit.Organization` namespace for reuse across unit test fixtures.
- All 340 tests in `Cakra.Tests.Unit` pass cleanly.

**Changed Files:**
- `src/backend/Cakra.Modules.Organization/Domain/Events/PersonActivated.cs`
- `src/backend/Cakra.Modules.Organization/Services/OrganizationService.cs`
- `tests/backend/Cakra.Tests.Unit/Organization/ActivatePersonCommandHandlerTests.cs`
- `tests/backend/Cakra.Tests.Unit/Organization/OrganizationDomainEventTests.cs`
- `tests/backend/Cakra.Tests.Unit/Organization/OrganizationServiceTests.cs`
- `docs/implementation-plan/CR-008-IMPLEMENTATION-PLAN.md`

**Notes:** `CreatePersonCommandHandler` and `DeactivatePersonCommandHandler` already have tests in `OrganizationServiceTests.cs`. Only `ActivatePersonCommandHandler` needs new tests.

---

### P3-S07

**Title:** Backend Integration Tests - OrganizationController Write Endpoints & RBAC

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Add integration tests for `OrganizationController` write endpoints and role-based authorization.

**Depends On:** P1-S02

**Repository:** Cakra.Tests.Integration

**Completion Criteria:**
- Test file: `Cakra.Tests.Integration/Api/OrganizationControllerTests.cs`
- Test: `CreatePerson_returns_201_for_Administrator_role`
- Test: `CreatePerson_returns_201_for_Admin_role`
- Test: `CreatePerson_returns_403_for_non_admin_role`
- Test: `CreatePerson_returns_401_for_unauthenticated`
- Test: `CreatePerson_returns_409_for_duplicate_email`
- Test: `CreatePerson_returns_400_for_invalid_input`
- Test: `UpdatePerson_returns_200_for_Administrator_role`
- Test: `UpdatePerson_returns_403_for_non_admin_role`
- Test: `UpdatePerson_returns_404_for_not_found`
- Test: `UpdatePerson_returns_409_for_duplicate_email`
- Test: `ActivatePerson_returns_200_for_Administrator_role`
- Test: `ActivatePerson_returns_403_for_non_admin_role`
- Test: `ActivatePerson_returns_404_for_not_found`
- Test: `DeactivatePerson_returns_200_for_Administrator_role`
- Test: `DeactivatePerson_returns_403_for_non_admin_role`
- Test: `DeactivatePerson_returns_404_for_not_found`
- Test: `ListAllPersons_returns_200_for_authenticated_user` (no admin required)
- Follows pattern from `UsersControllerTests.cs` (CR-007) for RBAC testing
- Uses `CakraWebApplicationFactory`, `DatabaseResetHelper`, in-memory/stub services
- Tests execute against real HTTP pipeline with cookie-based authentication

**Implementation Notes:**
- Added `Cakra.Tests.Integration/Api/OrganizationControllerTests.cs` with in-memory service doubles (`InMemoryOrganizationService`, `InMemoryOrganizationQueryService`, `StubAuthService`, `StubAuthzService`).
- Implemented all 17 test cases verifying RBAC and endpoint responses:
  - `CreatePerson_returns_201_for_Administrator_role`
  - `CreatePerson_returns_201_for_Admin_role`
  - `CreatePerson_returns_403_for_non_admin_role` (tested across `Programmer`, `Customer`, `Implementator` roles)
  - `CreatePerson_returns_401_for_unauthenticated`
  - `CreatePerson_returns_409_for_duplicate_email`
  - `CreatePerson_returns_400_for_invalid_input`
  - `UpdatePerson_returns_200_for_Administrator_role`
  - `UpdatePerson_returns_403_for_non_admin_role`
  - `UpdatePerson_returns_404_for_not_found`
  - `UpdatePerson_returns_409_for_duplicate_email`
  - `ActivatePerson_returns_200_for_Administrator_role`
  - `ActivatePerson_returns_403_for_non_admin_role`
  - `ActivatePerson_returns_404_for_not_found`
  - `DeactivatePerson_returns_200_for_Administrator_role`
  - `DeactivatePerson_returns_403_for_non_admin_role`
  - `DeactivatePerson_returns_404_for_not_found`
  - `ListAllPersons_returns_200_for_authenticated_user`
- Handled `KeyNotFoundException` in `OrganizationController.UpdatePerson` to return standard 404 ProblemDetails consistent with `ActivatePerson` and `DeactivatePerson`.
- Added default interface implementation to `IOrganizationQueryService.ListAllPersonsAsync` and implemented `ListAllPersonsAsync` in `UsersControllerTests.cs` double.
- Verified test suite execution: all 22 test executions in `OrganizationControllerTests` passed cleanly (0 failed).

**Changed Files:**
- `tests/backend/Cakra.Tests.Integration/Api/OrganizationControllerTests.cs`
- `src/backend/Cakra.Api/Controllers/OrganizationController.cs`
- `src/backend/Cakra.Modules.Organization/IOrganizationQueryService.cs`
- `tests/backend/Cakra.Tests.Integration/Identity/UsersControllerTests.cs`
- `docs/implementation-plan/CR-008-IMPLEMENTATION-PLAN.md`

**Notes:** Reference: `UsersControllerTests.cs` for RBAC test patterns; `OrganizationApplicationServiceIntegrationTests.cs` for database integration patterns.

---

## P4 - Documentation

**Implementation Status:** IMPLEMENTED  
**Review Status:** NOT-REVIEWED

### P4-S08

**Title:** Documentation Updates - Screen Inventory, Feature Catalog, Traceability

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Update all documentation artifacts to include Person Management screens and feature.

**Depends On:** P2-S04, P2-S05

**Repository:** Documentation (cakra/docs/)

**Completion Criteria:**
- `docs/navigation/screen-inventory.md`: Add `SCR-ORG-001` (Person Management View) and `SCR-ORG-002` (Add/Edit Person Modal) to inventory summary table and add full screen definitions following `SCR-USR-001`/`SCR-USR-002` pattern
- `docs/ui-layout/00-screen-inventory.md`: Add Administration area with `SCR-ORG-001` and `SCR-ORG-002`
- `docs/features/feature-catalog.md`: Add `FEAT-ORG-001 | Manage Organizational Persons | Command | —`
- `docs/features/feature-traceability.md`: Add traceability row mapping `FEAT-ORG-001` to Organization domain, `SCR-ORG-001`, `SCR-ORG-002`, `UC-ORG-001`, `UJ-ORG-001`

**Implementation Notes:**
- Updated `docs/navigation/screen-inventory.md`:
  - Added `SCR-ORG-001` (Person Management View) and `SCR-ORG-002` (Add / Edit Person Modal) to Inventory Summary table in the Administration area.
  - Added full screen definitions for `SCR-ORG-001` and `SCR-ORG-002` covering Screen Name, Purpose, Primary Actors, Supported Use Cases, Supported User Journeys, Entry Points, and Exit / Destination following the `SCR-USR-001`/`SCR-USR-002` pattern.
- Updated `docs/ui-layout/00-screen-inventory.md`:
  - Added `Administration Area` section with `SCR-ORG-001` (Person Management View) and `SCR-ORG-002` (Add/Edit Person Modal).
- Updated `docs/features/feature-catalog.md`:
  - Added `FEAT-ORG-001 | Manage Organizational Persons | Command | —`.
- Updated `docs/features/feature-traceability.md`:
  - Added traceability matrix row mapping `FEAT-ORG-001` to Organization domain, `SC-ORG-001`, `UC-ORG-001`, `UJ-ORG-001`, and screens `SCR-ORG-001, SCR-ORG-002`.

**Changed Files:**
- `docs/navigation/screen-inventory.md`
- `docs/ui-layout/00-screen-inventory.md`
- `docs/features/feature-catalog.md`
- `docs/features/feature-traceability.md`
- `docs/implementation-plan/CR-008-IMPLEMENTATION-PLAN.md`

**Notes:** Follow existing formatting and conventions in each file. Screen definitions must include Purpose, Primary Actors, Supported Use Cases/Journeys, Entry Points, Exit/Destination.

---

# 6. Change Log

- 2026-10-05: Initial plan created based on CR-008-ARCHITECTURE.md and current codebase verification. Backend phases (P1) marked complete as all backend components already implemented. Frontend, testing, and documentation phases remain.

---