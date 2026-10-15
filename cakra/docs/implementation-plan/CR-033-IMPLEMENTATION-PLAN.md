---
Title: Organizational Person Mapping and Account Approval Implementation Plan
Code: CR-033
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-15
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-033`: Organizational Person Mapping and Account Approval for Self-Registered Users across backend identity services, REST API, frontend client, and administrative user management screens (`SCR-USR-001`, `SCR-USR-002`).

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- ISSUE: [CR-033-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-033-ISSUE.md)
- FEASIBILITY-ASSESSMENT: [CR-033-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-033-FEASIBILITY-ASSESSMENT.md) (Planning Readiness: `READY-FOR-PLANNING`)
- ARCHITECTURE: [CR-033-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-033-ARCHITECTURE.md)
- FEATURE: [FEAT-USR-001-manage-user-accounts.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-001-manage-user-accounts.md)
- FEATURE: [FEAT-USR-002-register-user-account.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-002-register-user-account.md)

Architecture Applicability: ARCHITECTURE-REQUIRED

This implementation plan realizes the technical architecture defined in `CR-033-ARCHITECTURE.md`, enabling administrators to map self-registered accounts (`PersonId = Guid.Empty`, `Status = Pending`) to active organizational persons, enforcing identity immutability for established accounts, validating active person status and 1-to-1 uniqueness, preventing activation of unmapped accounts, and providing dedicated pending status filtering and ribbon metrics.

---

# 2. Planning Scope

This plan covers full-stack implementation across Cakra backend and frontend:

1. **Backend Application Layer (`Cakra.Modules.Identity`)**:
   - Update `UpdateUserAccountCommand` record with `Guid? PersonId = null`.
   - Update `UpdateUserAccountCommandValidator` to validate `PersonId` format (not `Guid.Empty` when specified) and valid status transitions.
   - Update `UserAccountService.UpdateUserAsync` to enforce:
     - Person immutability for established accounts (`user.PersonId != Guid.Empty`).
     - Active person validation via `IOrganizationQueryService.IsPersonActiveAsync`.
     - 1-to-1 person uniqueness via `IUserAccountRepository.GetByPersonIdAsync`.
     - Activation block when `PersonId` is `Guid.Empty` and target status is `ACTIVE`.
     - Failed login attempt reset via `user.Unlock(now)` when approving into `ACTIVE`.
2. **Backend API Layer (`Cakra.Api`)**:
   - Update `UpdateUserAccountRequest` record in `UsersController.cs` with `Guid? PersonId = null`.
   - Pass `request.PersonId` to `UpdateUserAccountCommand` in `UsersController.UpdateUser`.
   - Ensure RFC 7807 ProblemDetails mapping for validation and conflict exceptions.
3. **Frontend API Client & Presentation UI (`Cakra.Web`)**:
   - Update `UpdateUserRequest` interface in `src/api/users.ts` with `personId?: string`.
   - Update `UserAccountModal.vue` (`SCR-USR-002`):
     - Compute `isPersonUnmapped` to distinguish unmapped accounts from established accounts.
     - Conditionally render active Person dropdown and trigger `loadActivePersons()` in edit mode for unmapped accounts.
     - Enforce client-side validation requiring a Person before approving to `ACTIVE`.
     - Include `personId` in the `updateUser(...)` payload for unmapped accounts.
   - Update `UserManagementView.vue` (`SCR-USR-001`):
     - Add `PENDING` to `statusFilter` type and select options.
     - Compute `pendingCount` and render a `Pending Approval` metric item in the ribbon.
     - Add distinct styling for `PENDING` status badges (`badge text-bg-info text-dark`).
4. **Automated Testing & Verification**:
   - Backend unit tests in `Cakra.Tests.Unit/Identity/UserAccountServiceTests.cs`.
   - Backend integration tests in `Cakra.Tests.Integration/Identity/UsersControllerTests.cs`.
   - Full regression build and test suite execution.

---

# 3. Dependencies

**External Dependencies:** None

**Slice Dependencies:**
- `P1-S01` has no dependencies (`Depends On: None`).
- `P1-S02` depends on `P1-S01`.
- `P2-S03` depends on `P1-S02`.
- `P2-S04` depends on `P2-S03`.
- `P3-S05` depends on `P2-S04`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Backend Contracts, Application Service & API Layer | IMPLEMENTED | GO | 2/2 |
| P2 - Frontend API Client & Presentation UI | IMPLEMENTED | GO | 2/2 |
| P3 - Automated Testing & Verification | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Backend Contracts, Application Service & API Layer

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED

### P1-S01

Title: Backend Identity Application Commands, Validation & Invariant Enforcement

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective: Extend `UpdateUserAccountCommand` and update `UserAccountService.UpdateUserAsync` to enforce person mapping invariants, active person verification, 1-to-1 exclusivity, identity immutability, and activation prerequisites.

Depends On: None

Repository: Cakra.Modules.Identity

Completion Criteria:
- `UpdateUserAccountCommand` record in `UserAccountCommands.cs` includes `Guid? PersonId = null`.
- `UpdateUserAccountCommandValidator` validates that when `PersonId` is provided, it is not `Guid.Empty`, and allows target statuses `ACTIVE`, `LOCKED`, and `SUSPENDED`.
- `UserAccountService.UpdateUserAsync` enforces:
  - If `command.PersonId` is specified and `user.PersonId != Guid.Empty` and `user.PersonId != command.PersonId.Value`, throws `InvalidOperationException("Changing the organizational person of an already mapped user account is not permitted.")`.
  - If `command.PersonId` is specified and `command.PersonId.Value != Guid.Empty`, verifies `_organizationQueryService.IsPersonActiveAsync(command.PersonId.Value)`. If false, throws `ArgumentException("The specified person is not active or does not exist.")`.
  - Verifies `_userAccountRepository.GetByPersonIdAsync(command.PersonId.Value)`. If an existing user has a different `UserId`, throws `InvalidOperationException("The specified person is already linked to another user account.")`.
  - Assigns `user.PersonId = command.PersonId.Value`.
  - If target status is `ACTIVE` and resulting `user.PersonId == Guid.Empty`, throws `ArgumentException("An active organizational person must be linked before activating a user account.")`.
  - When transitioning from `Pending` or `Locked` to `ACTIVE`, invokes `user.Unlock(now)` resetting `FailedLoginAttempts` to 0.
- `ResolvePersonNameAsync` accurately returns the mapped Person's full name.

Implementation Notes:
- Extended `UpdateUserAccountCommand` in `UserAccountCommands.cs` with optional `Guid? PersonId = null`.
- Extended `UpdateUserAccountCommandValidator` with validation rule ensuring that when `PersonId` is provided, it is not `Guid.Empty`.
- Updated `UserAccountService.UpdateUserAsync` to:
  - Enforce person immutability for established accounts: throw `InvalidOperationException("Changing the organizational person of an already mapped user account is not permitted.")` when modifying an already assigned PersonId to a different value.
  - Verify active status of person via `_organizationQueryService.IsPersonActiveAsync`: throw `ArgumentException("The specified person is not active or does not exist.")` if false.
  - Enforce 1-to-1 person uniqueness via `_userAccountRepository.GetByPersonIdAsync`: throw `InvalidOperationException("The specified person is already linked to another user account.")` if another account is mapped to the person.
  - Assign `user.PersonId = command.PersonId.Value`.
  - Prevent activation of unmapped accounts: throw `ArgumentException("An active organizational person must be linked before activating a user account.")` when target status is ACTIVE and resulting PersonId is `Guid.Empty`.
  - Reset failed login attempts and unlock user via `user.Unlock(now)` when transitioning from `Pending` or `Locked` to `ACTIVE`.
- Updated `ResolvePersonNameAsync` to guard against `Guid.Empty` and accurately return the mapped Person's full name.
- Verified solution build succeeds and unit test suite passes (581 passed).

Changed Files:
- `cakra/src/backend/Cakra.Modules.Identity/Services/UserAccountCommands.cs`
- `cakra/src/backend/Cakra.Modules.Identity/Services/UserAccountService.cs`
- `cakra/docs/implementation-plan/CR-033-IMPLEMENTATION-PLAN.md`

---

### P1-S02

Title: Backend UsersController Request Contract & ProblemDetails Mapping

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective: Update `UsersController.cs` in `Cakra.Api` to accept `PersonId` in `UpdateUserAccountRequest`, pass it to `UpdateUserAccountCommand`, and properly map validation/conflict exceptions to RFC 7807 responses.

Depends On: P1-S01

Repository: Cakra.Api

Completion Criteria:
- `UpdateUserAccountRequest` record in `UsersController.cs` contains `Guid? PersonId = null`.
- `UsersController.UpdateUser` instantiates `UpdateUserAccountCommand` including `request.PersonId`.
- Catch blocks in `UpdateUser` map:
  - `ArgumentException` to HTTP 400 Bad Request ProblemDetails.
  - `InvalidOperationException` to HTTP 409 Conflict ProblemDetails.
  - `ValidationException` to HTTP 400 Bad Request ProblemDetails.
- Successful updates return HTTP 200 OK with enriched `UserAccountDto` containing the assigned `personId` and `personName`.

Implementation Notes:
- Updated `UpdateUserAccountRequest` record in `UsersController.cs` to add optional `Guid? PersonId = null`.
- Updated `UsersController.UpdateUser` action to pass `request.PersonId` to `UpdateUserAccountCommand`.
- Verified that RFC 7807 ProblemDetails mapping in `UsersController.UpdateUser` maps:
  - `ArgumentException` to HTTP 400 Bad Request ProblemDetails (`errorCode: BAD_REQUEST`).
  - `InvalidOperationException` to HTTP 409 Conflict ProblemDetails (`errorCode: CONFLICT`).
  - `ValidationException` to HTTP 400 Bad Request ProblemDetails (`errorCode: VALIDATION_FAILED`).
  - Successful invocation returns HTTP 200 OK with the enriched `UserAccountDto`.
- Built `Cakra.Api` with 0 errors and 0 warnings.
- Ran backend unit test suite (`dotnet test Cakra.Tests.Unit.csproj` - 581 passed, 0 failed) and integration test suite (`dotnet test Cakra.Tests.Integration.csproj` - 196 passed, 0 failed).

Changed Files:
- `cakra/src/backend/Cakra.Api/Controllers/UsersController.cs`
- `cakra/docs/implementation-plan/CR-033-IMPLEMENTATION-PLAN.md`

---

## P2 - Frontend API Client & Presentation UI

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED

### P2-S03

Title: Frontend API Client & UserAccountModal Component Update (SCR-USR-002)

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective: Update `users.ts` API client and `UserAccountModal.vue` (`SCR-USR-002`) to render active person selection for unmapped users in edit mode, validate person assignment for activation, and submit `personId`.

Depends On: P1-S02

Repository: Cakra.Web

Completion Criteria:
- `UpdateUserRequest` interface in `src/api/users.ts` includes `personId?: string`.
- `UserAccountModal.vue` defines computed property `isPersonUnmapped` returning true when `isEditMode` is true and `!props.user?.personId || props.user.personId === '00000000-0000-0000-0000-000000000000'`.
- In edit mode:
  - When `isPersonUnmapped` is true, renders `<select data-testid="select-person">` populated from `activePersons`.
  - When `isPersonUnmapped` is false, renders `<input data-testid="static-person" readonly disabled :value="props.user?.personName || '—'">`.
- In `watch(() => props.show)` and `onMounted`, calls `loadActivePersons()` if `isPersonUnmapped` is true and `activePersons.length === 0`.
- In `validate()`, if `isEditMode` and `isPersonUnmapped` and `status === 'ACTIVE'`, validates that `personId` is selected (`errors.personId = 'Please select an associated Person.'`).
- In `handleSubmit()`, includes `personId: personId.value.trim() || undefined` in `updateUser` call when `isPersonUnmapped` is true.

Implementation Notes:
- Updated `UpdateUserRequest` in `src/api/users.ts` to include optional `personId?: string`.
- Added computed property `isPersonUnmapped` in `UserAccountModal.vue` (`SCR-USR-002`) returning true when `isEditMode` is true and `!props.user?.personId || props.user.personId === '00000000-0000-0000-0000-000000000000'`.
- Updated template in `UserAccountModal.vue` to conditionally render `<select data-testid="select-person">` populated from `activePersons` when `!isEditMode || isPersonUnmapped`, and `<input data-testid="static-person" readonly disabled :value="props.user?.personName || '—'">` when in edit mode with an established mapped person (`!isPersonUnmapped`).
- Updated `resetForm` to map `Guid.Empty` (`00000000-0000-0000-0000-000000000000`) to empty selection.
- Added data loading trigger to `watch(() => props.show)`, `watch(() => props.user)`, and `onMounted` invoking `loadActivePersons()` when `(!isEditMode.value || isPersonUnmapped.value) && activePersons.value.length === 0`.
- Enforced validation in `validate()` requiring person selection (`errors.personId = 'Please select an associated Person.'`) when `isEditMode && isPersonUnmapped && status === 'ACTIVE'`.
- Included `personId: isPersonUnmapped.value ? personId.value.trim() || undefined : undefined` in `handleSubmit()` when calling `updateUser(...)`.
- Added unit tests in `tests/unit/user-account-modal.spec.ts` covering unmapped state detection, contract typing, person option resolution, and validation.
- Verified TypeScript type checking (`vue-tsc --noEmit`), unit test suite execution (`vitest run` - 7 passed test files, 178 passing tests), and production frontend build (`npm run build`).

Changed Files:
- `cakra/src/frontend/Cakra.Web/src/api/users.ts`
- `cakra/src/frontend/Cakra.Web/src/components/UserAccountModal.vue`
- `cakra/src/frontend/Cakra.Web/tests/unit/user-account-modal.spec.ts`
- `cakra/docs/implementation-plan/CR-033-IMPLEMENTATION-PLAN.md`

---

### P2-S04

Title: Frontend User Management Screen Pending Visibility & Filtering (SCR-USR-001)

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective: Update `UserManagementView.vue` (`SCR-USR-001`) with a `PENDING` status filter, a `Pending Approval` metric badge in the ribbon, and distinct status badge styling.

Depends On: P2-S03

Repository: Cakra.Web

Completion Criteria:
- `statusFilter` reactive ref type updated to `'ALL' | 'ACTIVE' | 'PENDING' | 'LOCKED' | 'SUSPENDED'`.
- `#userStatusFilter` dropdown includes `<option value="PENDING">PENDING</option>`.
- Computed `pendingCount` counts users with `status?.toUpperCase() === 'PENDING'`.
- `.op-metric-ribbon` includes a stat item for `Pending Approval:` with `data-testid="stat-pending-users"` rendering `pendingCount`.
- `statusBadgeClass(status)` maps `'PENDING'` to `'badge text-bg-info text-dark'`.
- Filtering table by `PENDING` correctly isolates accounts in `Pending` status.

Implementation Notes:
- Updated `statusFilter` reactive ref type to `'ALL' | 'ACTIVE' | 'PENDING' | 'LOCKED' | 'SUSPENDED'`.
- Added computed property `pendingCount = computed(() => users.value.filter((u) => u.status?.toUpperCase() === 'PENDING').length)`.
- Added operational stat item for `Pending Approval:` in `.op-metric-ribbon` with `data-testid="stat-pending-users"` rendering `pendingCount`.
- Added `<option value="PENDING">PENDING</option>` to the `#userStatusFilter` dropdown select.
- Updated `statusBadgeClass(status)` to map `'PENDING'` (case-insensitively) to `'badge text-bg-info text-dark'`.
- Added helper exports in `<script lang="ts">` of `UserManagementView.vue` (`statusBadgeClass`, `countPendingUsers`, `filterUsersByStatus`).
- Created unit test suite in `tests/unit/user-management-view.spec.ts` covering badge class mappings, pending counter calculation, status filtering isolation, and union type compatibility.
- Verified TypeScript type checking (`vue-tsc --noEmit`), unit test suite execution (`vitest run` - 8 passed test files, 191 passing tests), and production frontend build (`npm run build`).

Changed Files:
- `cakra/src/frontend/Cakra.Web/src/views/UserManagementView.vue`
- `cakra/src/frontend/Cakra.Web/tests/unit/user-management-view.spec.ts`
- `cakra/docs/implementation-plan/CR-033-IMPLEMENTATION-PLAN.md`

---

## P3 - Automated Testing & Verification

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED

### P3-S05

Title: Automated Unit and Integration Test Suite Execution & Verification

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective: Implement unit and integration tests verifying person mapping, immutability, conflict rejection, and activation requirements across `Cakra.Tests.Unit` and `Cakra.Tests.Integration`, and run the complete test suite.

Depends On: P2-S04

Repository: Cakra.Tests.Unit, Cakra.Tests.Integration, Cakra.Web

Completion Criteria:
- Unit tests in `UserAccountServiceTests.cs` cover:
  - Successfully mapping a Person on an unmapped user account and approving to `ACTIVE`.
  - Throwing `InvalidOperationException` when attempting to change `PersonId` on an already mapped account.
  - Throwing `ArgumentException` when specifying an inactive or nonexistent `PersonId`.
  - Throwing `InvalidOperationException` when specifying a `PersonId` already linked to another account.
  - Throwing `ArgumentException` when setting status to `ACTIVE` while `PersonId` remains `Guid.Empty`.
  - Verifying `FailedLoginAttempts` is reset to 0 upon transition to `ACTIVE`.
- Integration tests in `UsersControllerTests.cs` cover `PUT /api/v1/users/{id}` with `PersonId` payload and verify 200 OK, 400 Bad Request, and 409 Conflict responses.
- Backend test suite (`dotnet test`) passes with 0 failures.
- Frontend test suite and type check (`npm run type-check`) pass with 0 errors.

Implementation Notes:
- Added 6 comprehensive unit tests in `UserAccountServiceTests.cs`:
  - `UpdateUserAsync_successfully_maps_person_on_unmapped_account_and_approves_to_active`: maps an unmapped user to an active person, approves to ACTIVE, and verifies reset of failed login attempts.
  - `UpdateUserAsync_throws_InvalidOperationException_when_attempting_to_change_personId_on_already_mapped_account`: enforces immutability of mapped persons.
  - `UpdateUserAsync_throws_ArgumentException_when_specifying_inactive_or_nonexistent_personId`: verifies rejection when linking nonexistent or inactive person.
  - `UpdateUserAsync_throws_InvalidOperationException_when_specifying_personId_already_linked_to_another_account`: enforces 1-to-1 account-to-person uniqueness constraint.
  - `UpdateUserAsync_throws_ArgumentException_when_setting_status_to_active_while_personId_remains_empty`: prevents activation of unmapped accounts.
  - `UpdateUserAsync_resets_failed_login_attempts_upon_transition_from_pending_to_active`: verifies failed login counter reset.
- Added 6 comprehensive integration tests in `UsersControllerTests.cs`:
  - `PutUser_with_PersonId_maps_person_and_activates_unmapped_account_returning_200_OK`: tests successful person mapping and activation over HTTP PUT /api/v1/users/{id} returning 200 OK with enriched user DTO.
  - `PutUser_with_PersonId_returns_400_BadRequest_when_person_is_inactive_or_nonexistent`: tests HTTP 400 Bad Request ProblemDetails on inactive/nonexistent person.
  - `PutUser_returns_400_BadRequest_when_activating_without_person_mapping`: tests HTTP 400 Bad Request ProblemDetails when activating unmapped user.
  - `PutUser_with_empty_guid_PersonId_returns_400_BadRequest_validation_error`: tests HTTP 400 Bad Request validation error when providing empty GUID.
  - `PutUser_with_PersonId_returns_409_Conflict_when_changing_person_on_already_mapped_account`: tests HTTP 409 Conflict ProblemDetails for person immutability violations.
  - `PutUser_with_PersonId_returns_409_Conflict_when_person_is_already_linked_to_another_account`: tests HTTP 409 Conflict ProblemDetails on duplicate person linking.
- Executed complete backend test suite (`dotnet test`): 587 unit tests passed, 202 integration tests passed (0 failures).
- Executed frontend type-check (`npm run type-check`), unit test suite (`npm test` - 8 test files, 191 tests passed), and production build (`npm run build`).

Changed Files:
- `cakra/tests/backend/Cakra.Tests.Unit/Identity/UserAccountServiceTests.cs`
- `cakra/tests/backend/Cakra.Tests.Integration/Identity/UsersControllerTests.cs`
- `cakra/docs/implementation-plan/CR-033-IMPLEMENTATION-PLAN.md`

---

# 6. Change Log

- **2026-10-15:** Initial release of `CR-033-IMPLEMENTATION-PLAN.md` with Execution Approval `APPROVED`.
