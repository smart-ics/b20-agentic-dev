---
Title: Organizational Person Mapping and Account Approval Architecture
Code: CR-033
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-15
---

# 1. Overview

This architecture defines the technical realization for Change Request `CR-033`: Enabling administrators to map unassociated, self-registered user accounts (`PersonId = Guid.Empty`, `Status = Pending`) to an active organizational `Person` record, enforce active person validation and 1-to-1 uniqueness invariants, approve and activate accounts in `SCR-USR-002`, and provide dedicated operational filtering and metric tracking for pending accounts in `SCR-USR-001`.

It realizes [FEAT-USR-001-manage-user-accounts.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-001-manage-user-accounts.md), [FEAT-USR-002-register-user-account.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-002-register-user-account.md), and the approved gap-closure decisions from [CR-033-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-033-FEASIBILITY-ASSESSMENT.md).

---

# 2. Architectural Basis

## Business Context

- DOMAIN: Identity & Access (`Cakra.Modules.Identity`, Architecture §14)
- DOMAIN: Organization (`Cakra.Modules.Organization`, [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md))
- FEATURE: [FEAT-USR-001-manage-user-accounts.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-001-manage-user-accounts.md)
- FEATURE: [FEAT-USR-002-register-user-account.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-002-register-user-account.md)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md) (Section 14: Identity & Authentication Architecture, Section 19: API & Frontend Boundaries)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-033-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-033-FEASIBILITY-ASSESSMENT.md) (Planning Readiness: `READY-FOR-PLANNING`)

This architecture consumes and realizes the approved feasibility decisions:
- `GAP-001`: In `SCR-USR-002` (`UserAccountModal.vue`), conditionally render the active Person `<select>` dropdown in edit mode when the target user account has an unmapped `PersonId` (`Guid.Empty`), while retaining read-only text for established accounts.
- `GAP-002`: Extend `UpdateUserAccountRequest`, `UpdateUserAccountCommand`, and `UpdateUserRequest` contracts with optional `PersonId`.
- `GAP-003` & `GAP-004`: Update `UserAccountService.UpdateUserAsync` to validate person active status via `IOrganizationQueryService.IsPersonActiveAsync`, enforce 1-to-1 uniqueness via `IUserAccountRepository.GetByPersonIdAsync`, reject activating unmapped accounts, and reset failed login attempts upon approval to `ACTIVE`.
- `GAP-005`: Ensure `UpdateUserAccountCommandValidator` permits transition to `ACTIVE`, `LOCKED`, or `SUSPENDED` from `Pending`.
- `GAP-006`: Enhance `SCR-USR-001` (`UserManagementView.vue`) with a `PENDING` status filter option, a pending metric badge in the ribbon, and distinct badge styling.
- `OQ-001`: Preserve identity immutability by permitting Person mapping mutations only when `user.PersonId == Guid.Empty`.
- `OQ-002`: Map validation/conflict exceptions to RFC 7807 `400 Bad Request` or `409 Conflict`.
- `OQ-003`: Surface a dedicated `Pending` metric item in `UserManagementView.vue`.

---

# 3. Scope

## Included

1. **Backend API Layer (`Cakra.Api`)**:
   - Update `UpdateUserAccountRequest` record in `UsersController.cs` to include `Guid? PersonId = null`.
   - In `UsersController.UpdateUser`, pass `request.PersonId` to `UpdateUserAccountCommand`.
   - Map `InvalidOperationException` to HTTP 409 Conflict (or 400 Bad Request where appropriate) and `ArgumentException` to HTTP 400 Bad Request ProblemDetails.
2. **Backend Application Layer (`Cakra.Modules.Identity`)**:
   - Update `UpdateUserAccountCommand` record in `UserAccountCommands.cs` to include `Guid? PersonId = null`.
   - Update `UpdateUserAccountCommandValidator` to ensure `PersonId` (if provided) is not empty (`Guid.Empty`), and validate status transitions.
   - Update `UserAccountService.UpdateUserAsync`:
     - If `command.PersonId` is provided:
       - Verify that `user.PersonId == Guid.Empty`. If already assigned, throw `InvalidOperationException("Changing the organizational person of an already mapped user account is not permitted.")`.
       - Verify `_organizationQueryService.IsPersonActiveAsync(command.PersonId.Value)`. If false, throw `ArgumentException("The specified person is not active or does not exist.")`.
       - Verify `_userAccountRepository.GetByPersonIdAsync(command.PersonId.Value)`. If another account is already mapped, throw `InvalidOperationException("The specified person is already linked to another user account.")`.
       - Assign `user.PersonId = command.PersonId.Value`.
     - Activation Safeguard:
       - If target status is `ACTIVE` and `user.PersonId == Guid.Empty`: throw `ArgumentException("An active organizational person must be linked before activating a user account.")`.
       - If transitioning from `Pending` or `Locked` to `ACTIVE`, invoke `user.Unlock(now)` to reset `FailedLoginAttempts` to 0.
3. **Frontend API Client (`Cakra.Web`)**:
   - In `src/api/users.ts`, add `personId?: string` to `UpdateUserRequest`.
4. **Frontend Presentation Layer (`Cakra.Web`)**:
   - In `UserAccountModal.vue` (`SCR-USR-002`):
     - Compute `isPersonUnmapped = computed(() => !props.user?.personId || props.user.personId === '00000000-0000-0000-0000-000000000000')`.
     - In edit mode, when `isPersonUnmapped` is true, render the `<select>` dropdown populated with active persons and trigger `loadActivePersons()`.
     - In edit mode, when `isPersonUnmapped` is false, render the static read-only text input (`props.user?.personName || '—'`).
     - In `validate()`, if in edit mode and `isPersonUnmapped` is true and `status === 'ACTIVE'`, require `personId` to be selected.
     - Include `personId` in the `updateUser(...)` payload when editing an unmapped account.
   - In `UserManagementView.vue` (`SCR-USR-001`):
     - Add `PENDING` to `statusFilter` options (`ALL`, `ACTIVE`, `PENDING`, `LOCKED`, `SUSPENDED`).
     - Compute `pendingCount = computed(() => users.value.filter(u => u.status?.toUpperCase() === 'PENDING').length)`.
     - Add a `Pending` stat item in `.op-metric-ribbon` with `data-testid="stat-pending-users"`.
     - In `statusBadgeClass(status)`, map `'PENDING'` to `'badge text-bg-info text-dark'`.
5. **Automated Testing**:
   - Unit tests in `Cakra.Tests.Unit/Identity/UserAccountServiceTests.cs` verifying person mapping on unmapped user, active person validation, 1-to-1 conflict rejection, and activation block when unmapped.
   - Integration tests in `Cakra.Tests.Integration/Identity/UsersControllerTests.cs` verifying `PUT /api/v1/users/{id}` with `personId` payload and approval flow.

## Excluded

- Physical database schema changes (filtered unique index `UQ_UserAccounts_PersonId` in `0020_user_accounts_person_id_filtered_unique.sql` already provides complete schema support).
- Modifications to self-registration endpoint `POST /api/v1/auth/register` (which continues initializing accounts with `PersonId = Guid.Empty` and `Status = "Pending"`).
- Modification of Person records in the Organization domain.

---

# 4. Technical Decisions

## TD-001: API & Command Contract Extension for PersonId

To allow the administrative client to specify a Person during user review and approval, `PersonId` is added as an optional property on the update request and command contracts:

```csharp
// Cakra.Api.Controllers.UsersController.cs
public sealed record UpdateUserAccountRequest(
    string? Email = null,
    string? Status = null,
    string? NewPassword = null,
    Guid? PersonId = null);

// Cakra.Modules.Identity.Services.UserAccountCommands.cs
public sealed record UpdateUserAccountCommand(
    string? Email = null,
    string? Status = null,
    string? NewPassword = null,
    Guid? UserId = null,
    Guid? PersonId = null) : IRequest<UserAccountDto>;
```

## TD-002: In-Memory Verification and Identity Immutability Invariant

To preserve the boundary rules of Architecture §14 (IAM Schema Isolation) and maintain the immutability of established identities:
1. **Unmapped-Only Mapping**:
   If an account already has an established person (`user.PersonId != Guid.Empty`) and the incoming payload attempts to change `PersonId` to a different non-null value, `UserAccountService.UpdateUserAsync` rejects the request:
   ```csharp
   if (command.PersonId.HasValue && command.PersonId.Value != Guid.Empty)
   {
       if (user.PersonId != Guid.Empty && user.PersonId != command.PersonId.Value)
       {
           throw new InvalidOperationException("Changing the organizational person of an already mapped user account is not permitted.");
       }
       // ...
   }
   ```
2. **Cross-Module Verification**:
   When binding a new `PersonId`:
   - Checks active status via `await _organizationQueryService.IsPersonActiveAsync(command.PersonId.Value, cancellationToken)`.
   - Checks 1-to-1 exclusivity via `await _userAccountRepository.GetByPersonIdAsync(command.PersonId.Value, cancellationToken)`. If an existing account has a different `UserId`, an `InvalidOperationException` is thrown.

## TD-003: Activation Safeguard & Unlock Reset

An unassociated account cannot authenticate because `AuthenticationService.AuthenticateAsync` requires an active person. Therefore, `UserAccountService.UpdateUserAsync` enforces:
1. **Activation Block**:
   ```csharp
   var targetStatus = command.Status?.Trim().ToUpperInvariant() ?? user.Status;
   var resultingPersonId = (command.PersonId.HasValue && command.PersonId.Value != Guid.Empty) 
       ? command.PersonId.Value 
       : user.PersonId;

   if (string.Equals(targetStatus, UserAccountStatus.Active, StringComparison.OrdinalIgnoreCase) &&
       resultingPersonId == Guid.Empty)
   {
       throw new ArgumentException("An active organizational person must be linked before activating a user account.", nameof(command));
   }
   ```
2. **State Transition & Unlock**:
   Transitioning from `Pending` or `Locked` to `ACTIVE` resets failed login attempts:
   ```csharp
   if (string.Equals(targetStatus, UserAccountStatus.Active, StringComparison.OrdinalIgnoreCase))
   {
       user.Unlock(now);
   }
   else
   {
       user.Status = targetStatus;
   }
   ```

## TD-004: Frontend Conditional Person Selector in Edit Mode (`SCR-USR-002`)

In `UserAccountModal.vue`:
1. Reactive property `isPersonUnmapped`:
   ```ts
   const isPersonUnmapped = computed(() => {
     if (!isEditMode.value) return false
     const pid = props.user?.personId
     return !pid || pid === '00000000-0000-0000-0000-000000000000'
   })
   ```
2. UI Template:
   - When `!isEditMode || isPersonUnmapped`: Render the active Person `<select>` dropdown (`data-testid="select-person"`).
   - When `isEditMode && !isPersonUnmapped`: Render the static read-only `<input>` (`data-testid="static-person"`).
3. Data Loading:
   In `watch(() => props.show)` and `onMounted`, if `(!isEditMode.value || isPersonUnmapped.value) && activePersons.value.length === 0`, call `loadActivePersons()`.
4. Submission Payload:
   When `isPersonUnmapped` is true, include `personId: personId.value.trim() || undefined` in the `updateUser` call.

## TD-005: Operational Visibility for Pending Accounts in `SCR-USR-001`

In `UserManagementView.vue`:
1. Metric Ribbon:
   Add stat item:
   ```html
   <div class="op-stat-item">
     <span class="op-stat-label">Pending Approval:</span>
     <span class="op-stat-val text-info" data-testid="stat-pending-users">{{ pendingCount }}</span>
   </div>
   ```
2. Filter:
   Add `<option value="PENDING">PENDING</option>` to `#userStatusFilter`.
3. Badge Styling:
   Add `case 'PENDING': return 'badge text-bg-info text-dark'` to `statusBadgeClass`.

---

# 5. Component Responsibilities

| Component | Layer | Responsibility |
|---|---|---|
| `UsersController.cs` | API Layer (`Cakra.Api`) | Exposes `PUT /api/v1/users/{id}`, binds `UpdateUserAccountRequest` with `PersonId`, maps domain exceptions to RFC 7807 ProblemDetails. |
| `UpdateUserAccountRequest.cs` | API Contract (`Cakra.Api`) | Payload DTO with `Email`, `Status`, `NewPassword`, and `PersonId`. |
| `UpdateUserAccountCommand.cs` | Application Contract (`Cakra.Modules.Identity`) | MediatR command with `UserId`, `Email`, `Status`, `NewPassword`, and `PersonId`. |
| `UpdateUserAccountCommandValidator.cs` | Application Validation (`Cakra.Modules.Identity`) | Validates formatting, non-empty `PersonId` when provided, and status values. |
| `UserAccountService.cs` | Identity Application Service (`Cakra.Modules.Identity`) | Coordinates user updates, queries `IOrganizationQueryService` to verify active person, enforces 1-to-1 uniqueness and activation invariants. |
| `users.ts` | Frontend API Client (`Cakra.Web`) | Defines `UpdateUserRequest` interface with `personId?: string` and invokes `PUT /users/{id}`. |
| `UserAccountModal.vue` (`SCR-USR-002`) | Frontend Presentation (`Cakra.Web`) | Renders active person dropdown for unmapped accounts in edit mode, validates required person for activation, and submits update. |
| `UserManagementView.vue` (`SCR-USR-001`) | Frontend Presentation (`Cakra.Web`) | Displays `Pending` counts in the metric ribbon, provides `PENDING` filter option, and formats pending status badges. |

---

# 6. Integration Design

```text
[ Administrator on SCR-USR-001 / SCR-USR-002 ]
                     │
                     ├─► (If PersonId unmapped) GET /api/v1/organization/persons/active
                     │                          │
                     │                          └─► Returns active Persons dropdown options
                     │
                     ├─► Submits Edit: PUT /api/v1/users/{id}
                     │   { email, status: "ACTIVE", personId: "<guid>" }
                     ▼
[ UsersController.UpdateUser ]
                     │
                     │ Dispatches UpdateUserAccountCommand(..., personId)
                     ▼
[ UserAccountService.UpdateUserAsync ]
                     │
                     ├─► 1. Check user.PersonId == Guid.Empty (Immutability check)
                     ├─► 2. IOrganizationQueryService.IsPersonActiveAsync(personId)
                     ├─► 3. IUserAccountRepository.GetByPersonIdAsync(personId) (1-to-1 uniqueness)
                     ├─► 4. Verify resulting PersonId != Guid.Empty for Status = "ACTIVE"
                     ├─► 5. user.Unlock(now) and update user.PersonId
                     ├─► 6. IUserAccountRepository.UpdateAsync(user)
                     ▼
[ Database: [identity].[UserAccounts] ]
                     │
                     └─► Updated with PersonId, Status = 'ACTIVE', FailedLoginAttempts = 0
```

---

# 7. Data Ownership

| Data | Authoritative Owner | Schema / Location |
|---|---|---|
| User Credentials, Status, & Person Link (`PersonId`) | `Identity` Module (`Cakra.Modules.Identity`) | `[identity].[UserAccounts]` |
| Person Master Data (`FullName`, `IsActive`) | `Organization` Module (`Cakra.Modules.Organization`) | `[organization].[Persons]` |

---

# 8. Database Design

## Modified Tables

| Table | Change Description |
|---|---|
| `[identity].[UserAccounts]` | **No physical schema changes required.** The filtered unique index `UQ_UserAccounts_PersonId` on `PersonId` (`WHERE [PersonId] <> '00000000-0000-0000-0000-000000000000'`) created in migration `0020` natively supports transitioning from `Guid.Empty` to an authoritative `PersonId` while enforcing 1-to-1 uniqueness. |

---

# 9. Cross-Cutting Concerns

- **Security & Authorization**: `PUT /api/v1/users/{id}` remains guarded by `[Authorize(Roles = "Administrator,Admin")]`.
- **Error Handling**: Follows RFC 7807 ProblemDetails:
  - Attempting to re-map an already mapped account -> 409 Conflict ProblemDetails.
  - Attempting to map a person already bound to another account -> 409 Conflict ProblemDetails.
  - Attempting to activate an account without linking an active person -> 400 Bad Request ProblemDetails.
  - Attempting to link an inactive person -> 400 Bad Request ProblemDetails.
- **Auditability**: `UpdatedAt` timestamp is refreshed upon approval.

---

# 10. Implementation Constraints

1. **Schema Boundary**: No foreign key constraints or SQL joins between `[identity].[UserAccounts]` and `[organization].[Persons]`. All person validation must occur via `IOrganizationQueryService`.
2. **Identity Immutability**: Once an account is bound to a non-empty `PersonId`, that binding cannot be modified via `PUT /api/v1/users/{id}`.
3. **Activation Prerequisite**: A user account cannot be activated (`Status = "ACTIVE"`) unless it possesses a valid, non-empty `PersonId`.
4. **Backward Compatibility**: Existing clients invoking `PUT /api/v1/users/{id}` without `personId` must continue functioning normally.

---

# 11. Acceptance Conditions

- [ ] `PUT /api/v1/users/{id}` accepts `personId` in the request body.
- [ ] If `personId` is provided for an unmapped user (`PersonId == Guid.Empty`), `UserAccountService` verifies the person is active and unassociated, and binds `PersonId`.
- [ ] If `personId` is provided for an already mapped user (`PersonId != Guid.Empty`), `PUT /api/v1/users/{id}` rejects the update with 409 Conflict ProblemDetails.
- [ ] If `status` is set to `ACTIVE` while `PersonId` remains `Guid.Empty`, `PUT /api/v1/users/{id}` rejects the update with 400 Bad Request ProblemDetails.
- [ ] Transitioning a user from `Pending` to `ACTIVE` successfully resets `FailedLoginAttempts` to 0.
- [ ] Once approved and linked, the user can successfully log in via `POST /api/v1/auth/login` without triggering `PERSON_INACTIVE`.
- [ ] `SCR-USR-002` displays an active Person `<select>` dropdown in edit mode when the user account is unmapped (`PersonId == Guid.Empty`).
- [ ] `SCR-USR-002` displays the Person as read-only static text when the account is already mapped.
- [ ] `SCR-USR-001` includes a `Pending Approval` counter in the ribbon, a `PENDING` filter option in the dropdown, and renders pending badges.
- [ ] Unit and integration tests for person mapping and pending account approval pass.
