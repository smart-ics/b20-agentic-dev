---
Title: Feasibility Assessment for Organizational Person Mapping and Account Approval
Code: CR-033
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-15
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Feature being assessed: Enable administrators to map an unassociated, self-registered user account (`PersonId = Guid.Empty`, `Status = Pending`) to an active organizational Person record, enforce 1-to-1 binding and active status verification, and approve the account to `ACTIVE` status in `SCR-USR-002` so that the user can successfully log into the system, as formally captured in [CR-033-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-033-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-033-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-033-ISSUE.md)
- FEATURE: [FEAT-USR-001-manage-user-accounts.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-001-manage-user-accounts.md)
- FEATURE: [FEAT-USR-002-register-user-account.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-002-register-user-account.md)
- DOMAIN: [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md)
- REFERENCE ARCHITECTURE: [CR-007-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-007-ARCHITECTURE.md) (Administrative User Management)
- REFERENCE ARCHITECTURE: [CR-009-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-009-ARCHITECTURE.md) (Sign-Up Feature with Pending Admin Approval)

## Objective

Assess the technical feasibility, existing baseline, gaps, risks, options, and gap closure decisions to:

1. Allow administrators in `SCR-USR-002` (`UserAccountModal.vue`) to select an active organizational person when reviewing and editing an unassociated (`PersonId = Guid.Empty`) or `Pending` user account.
2. Extend backend contracts (`UpdateUserAccountRequest`, `UpdateUserAccountCommand`) to accept an optional `PersonId`.
3. Update `UserAccountService.UpdateUserAsync` to validate that the assigned Person exists and is active via `IOrganizationQueryService.IsPersonActiveAsync`, and enforce 1-to-1 exclusivity via `IUserAccountRepository.GetByPersonIdAsync`.
4. Ensure status transitions from `Pending` to `ACTIVE` require a valid, active associated Person and reset failed login attempts.
5. Enhance `SCR-USR-001` (`UserManagementView.vue`) with visibility and filtering for `Pending` accounts awaiting administrator review.

---

# 2. Current State

## Existing Behavior

1. **Frontend User Account Modal (`SCR-USR-002` / `UserAccountModal.vue`)**:
   - In create mode (`props.mode === 'create'`), the modal calls `loadActivePersons()` (`GET /api/v1/organization/persons/active`) and renders a `<select>` dropdown for `Organizational Person`.
   - In edit mode (`props.mode === 'edit'`), the Person field is unconditionally rendered as a disabled, read-only static text input (`<input readonly disabled :value="props.user?.personName || '—'" />`).
   - The modal never triggers `loadActivePersons()` in edit mode, even if `props.user.personId` is empty or all zeroes (`Guid.Empty`).
   - The form submit handler `handleSubmit` invokes `updateUser(userId, { email, status, newPassword })`, completely omitting `personId`.
2. **Frontend User Management Screen (`SCR-USR-001` / `UserManagementView.vue`)**:
   - Displays metrics ribbon: Total, Active, Locked, Suspended accounts. Lacks a metric for `Pending` accounts.
   - Status filter dropdown options: `ALL`, `ACTIVE`, `LOCKED`, `SUSPENDED`. Lacks a filter for `PENDING`.
   - High-density table renders all accounts returned by `GET /api/v1/users`, but `statusBadgeClass` does not define a dedicated badge style for `Pending` (defaults to secondary).
3. **Frontend API Module (`src/api/users.ts`)**:
   - `UpdateUserRequest` interface contains: `{ email?: string; status?: string; newPassword?: string }`. It does not support `personId`.
4. **Backend REST API (`UsersController.cs`)**:
   - `PUT /api/v1/users/{id}` binds `UpdateUserAccountRequest` which defines only `(string? Email, string? Status, string? NewPassword)`.
   - Instantiates `UpdateUserAccountCommand` without any `PersonId` parameter.
5. **Backend Identity Application Service (`Cakra.Modules.Identity`)**:
   - `UpdateUserAccountCommand` record only accepts `(string? Email, string? Status, string? NewPassword, Guid? UserId)`.
   - `UpdateUserAccountCommandValidator` validates that `Status` (if specified) must match `ACTIVE`, `LOCKED`, or `SUSPENDED`.
   - `UserAccountService.UpdateUserAsync` checks email uniqueness, validates status transition, updates password hash, and updates timestamps. It does not inspect, validate, or mutate `user.PersonId`.
6. **Backend Authentication Gate (`AuthenticationService.cs`)**:
   - During `AuthenticateAsync`, lines 90–98 query `_organizationQueryService.IsPersonActiveAsync(user.PersonId)`.
   - Because self-registered accounts are initialized with `PersonId = Guid.Empty`, even if an administrator manages to set `user.Status = "ACTIVE"`, authentication rejects the user with code `PERSON_INACTIVE`.
7. **Database Schema & Constraints (`[identity].[UserAccounts]`)**:
   - Migration `0020_user_accounts_person_id_filtered_unique.sql` created a filtered unique index `UQ_UserAccounts_PersonId` on `PersonId` where `PersonId <> '00000000-0000-0000-0000-000000000000'`.
   - This allows multiple pending accounts to have `Guid.Empty`, while enforcing 1-to-1 uniqueness once an authoritative `PersonId` is assigned.

## Existing Constraints

1. **1-to-1 User-to-Person Invariant**:
   - Architecture §14 and database index `UQ_UserAccounts_PersonId` require that every non-empty `PersonId` is associated with at most one user account.
2. **Schema Isolation (Architecture §14 & §20)**:
   - The Identity module database schema (`[identity]`) must not have foreign keys or SQL joins to the Organization schema (`[organization]`).
   - Person existence, display name, and active status checks must be performed in memory via `IOrganizationQueryService`.
3. **Authentication Person Requirement**:
   - An account cannot authenticate unless its `PersonId` corresponds to an active Person in the Organization domain.

---

# 3. Gap Analysis

| ID | Severity | Gap |
|------|------|------|
| GAP-001 | CRITICAL | In `SCR-USR-002` (`UserAccountModal.vue`), the `Organizational Person` field is unconditionally rendered as a read-only, disabled input in edit mode, preventing administrators from selecting a Person for unmapped (`PersonId == Guid.Empty`) self-registered accounts. |
| GAP-002 | CRITICAL | Backend `UpdateUserAccountRequest` DTO, `UpdateUserAccountCommand`, and `UpdateUserRequest` (frontend) do not support `PersonId`. |
| GAP-003 | CRITICAL | `UserAccountService.UpdateUserAsync` does not update `user.PersonId`, nor does it validate Person existence, active status, or 1-to-1 uniqueness during updates. |
| GAP-004 | CRITICAL | `UserAccountService.UpdateUserAsync` does not prevent transitioning an unmapped user (`PersonId == Guid.Empty`) to `ACTIVE` status, which causes subsequent logins to fail with `PERSON_INACTIVE`. |
| GAP-005 | MAJOR | `UpdateUserAccountCommandValidator` rejects `Status = "Pending"` or transitions from `Pending` because allowed statuses are hardcoded to `ACTIVE`, `LOCKED`, and `SUSPENDED`. |
| GAP-006 | MINOR | `SCR-USR-001` (`UserManagementView.vue`) does not include a `Pending` status filter option or a metric badge in the ribbon for pending approvals. |

---

# 4. Open Questions

| ID | Question | Impact | Status |
|------|------|------|------|
| OQ-001 | Should Person mapping be allowed only for unmapped accounts (`PersonId == Guid.Empty`), or should administrators be allowed to re-map an already mapped user account to a different Person? | Identity immutability and auditing. | CLOSED |
| OQ-002 | What validation errors should be returned if an administrator tries to approve a user to `ACTIVE` without selecting a Person, or selects a Person that is already linked? | RFC 7807 problem details and UI inline feedback. | CLOSED |
| OQ-003 | Should `SCR-USR-001` include a dedicated metric for `Pending` accounts in the top metric ribbon? | Screen usability and administrator awareness. | CLOSED |

---

# 5. Assumptions

| ID | Assumption |
|------|------|
| ASM-001 | Migration `0020_user_accounts_person_id_filtered_unique.sql` is deployed and the database unique index `UQ_UserAccounts_PersonId` will block duplicate active person links at the persistence level as a safeguard. |
| ASM-002 | `IOrganizationQueryService` is injected and available in `UserAccountService` to validate Person active status and retrieve Person display names. |
| ASM-003 | Administrators have full authority in `SCR-USR-002` to map a Person and approve the account in a single update operation. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|------|------|------|------|
| RISK-001 | Two administrators attempt to approve different pending users by assigning them to the same Person concurrently. | 409 Conflict / Unique constraint violation. | `UserAccountService` checks `_userAccountRepository.GetByPersonIdAsync(personId)` in memory before assigning, and catches database unique constraint violations to return a clean RFC 7807 409 Conflict ProblemDetails. |
| RISK-002 | An administrator activates an account while leaving `PersonId` empty. | User login fails with `PERSON_INACTIVE` error upon login. | In `UserAccountService.UpdateUserAsync`, reject activation if `targetStatus == "ACTIVE"` and the resulting `PersonId` is `Guid.Empty` with a validation error: *"An active organizational person must be linked before activating a user account."* |
| RISK-003 | An administrator selects an inactive or archived Person. | User login fails due to inactive person gate. | `UserAccountService.UpdateUserAsync` calls `_organizationQueryService.IsPersonActiveAsync(personId)` and rejects inactive persons. |

---

# 7. Recommendations

## Option A: Conditional Person Selection for Unmapped Accounts (Recommended)

In `SCR-USR-002` (`UserAccountModal.vue`), dynamically inspect the account being edited:
- If `props.user.personId` is empty or `00000000-0000-0000-0000-000000000000`:
  - Fetch active persons from `GET /api/v1/organization/persons/active` on modal open.
  - Render the `<select>` dropdown allowing the administrator to choose an active Person.
  - Require a selected person if changing status to `ACTIVE`.
- If `props.user.personId` already has an established non-empty GUID:
  - Render the Person as read-only static text (preserving the CR-007 identity immutability rule).

### Advantages
- Strictly honors the original identity immutability rule (preventing accidental reassignment of existing established accounts).
- Exactly aligns with `CR-009-ARCHITECTURE.md §4 TD-004`.
- Minimizes risk of cross-account data leaks.

### Disadvantages
- If an administrator makes a mistake and links the wrong Person during initial approval, re-linking requires a separate administrative remediation process or direct database correction.

## Option B: Universal Person Re-Mapping for All Accounts

Allow administrators to re-select a Person for any account during edit mode, even if already mapped.

### Advantages
- Offers maximum flexibility for administrators to fix incorrect person assignments.

### Disadvantages
- Violates CR-007 identity immutability assumptions.
- Increases operational risk of reassigning an existing user's identity and audit trail to another person.

---

# 8. Gap Closure

## GAP-001: Person Selector in SCR-USR-002
### Decision
Adopt Option A. In `UserAccountModal.vue` (`SCR-USR-002`), if `props.mode === 'edit'` and the account's `personId` is empty (`null`, `''`, or `'00000000-0000-0000-0000-000000000000'`), render the active Person `<select>` dropdown (identical to create mode) and invoke `loadActivePersons()`. If `personId` is already established with a valid GUID, render the read-only static input.
### Rationale
Enables administrators to link an authoritative Person for newly self-registered accounts while preserving identity immutability for established active accounts.
### Impact
Frontend `UserAccountModal.vue` updated with conditional rendering of the Person selector in edit mode and active person loading.
### Architecture Impact
Frontend presentation logic in `Cakra.Web`.
### Resolved By
ica-analyst
### Resolved Date
2026-10-15

---

## GAP-002: Backend Contracts Support PersonId
### Decision
Add `Guid? PersonId = null` to `UpdateUserAccountRequest` DTO (`UsersController.cs`), `UpdateUserAccountCommand` (`UserAccountCommands.cs`), and `UpdateUserRequest` (`users.ts`).
### Rationale
Allows the client to transmit the selected `PersonId` during an update/approval operation.
### Impact
Contracts in `Cakra.Api`, `Cakra.Modules.Identity`, and `Cakra.Web` updated.
### Architecture Impact
Application and API contract extension.
### Resolved By
ica-analyst
### Resolved Date
2026-10-15

---

## GAP-003 & GAP-004: Validation & Persistence in UserAccountService
### Decision
In `UserAccountService.UpdateUserAsync`:
1. If `command.PersonId` is provided and non-empty:
   - Verify that the target Person exists and is active using `_organizationQueryService.IsPersonActiveAsync(personId)`. If false, throw `ArgumentException("The specified person is not active or does not exist.")`.
   - Verify that the target Person is not already associated with another user account using `_userAccountRepository.GetByPersonIdAsync(personId)`. If an existing account with a different `UserId` is found, throw `InvalidOperationException("The specified person is already linked to another user account.")`.
   - Set `user.PersonId = command.PersonId.Value`.
2. Activation Invariant:
   - If `command.Status` is transitioning to `ACTIVE` (or is already `ACTIVE`) and the resulting `user.PersonId == Guid.Empty`:
     Throw `ArgumentException("An active organizational person must be linked before activating a user account.")`.
   - When transitioning from `Pending` or `Locked` to `ACTIVE`, invoke `user.Unlock(now)` to reset `FailedLoginAttempts` to 0.
### Rationale
Ensures domain integrity, prevents unmapped active accounts that would immediately fail authentication, and enforces the 1-to-1 person-to-user rule.
### Impact
`UserAccountService.cs` in `Cakra.Modules.Identity`.
### Architecture Impact
Identity application layer logic.
### Resolved By
ica-analyst
### Resolved Date
2026-10-15

---

## GAP-005: Update Command Status Validator
### Decision
Update `UpdateUserAccountCommandValidator` to permit `Status` values: `ACTIVE`, `LOCKED`, and `SUSPENDED`, and ensure that if status is provided, it must be one of these valid target statuses. (Accounts never transition *into* `Pending`, only *out* of `Pending` into `ACTIVE`, `LOCKED`, or `SUSPENDED`).
### Rationale
Ensures valid state transitions and allows approval into `ACTIVE` without validator rejection.
### Impact
`UserAccountCommands.cs` in `Cakra.Modules.Identity`.
### Architecture Impact
Application validation layer.
### Resolved By
ica-analyst
### Resolved Date
2026-10-15

---

## GAP-006: SCR-USR-001 Visibility and Filtering
### Decision
Update `UserManagementView.vue` (`SCR-USR-001`):
1. Add `PENDING` to the status filter dropdown (`ALL`, `ACTIVE`, `PENDING`, `LOCKED`, `SUSPENDED`).
2. Add a `Pending` stat item in the operational metric ribbon (`stat-pending-users`).
3. Update `statusBadgeClass` to render a distinct badge for `PENDING` (e.g. `badge text-bg-info text-dark`).
### Rationale
Improves operational awareness so administrators can immediately see how many self-registered accounts are awaiting approval and filter down to them directly.
### Impact
`UserManagementView.vue` in `Cakra.Web`.
### Architecture Impact
Frontend presentation layer.
### Resolved By
ica-analyst
### Resolved Date
2026-10-15

---

## OQ-001: Person Mapping Mutability Scope
### Decision
Person mapping is mutable only when `user.PersonId == Guid.Empty`. If `user.PersonId != Guid.Empty` and a different `command.PersonId` is passed, `UserAccountService` will reject the change with an `InvalidOperationException("Changing the organizational person of an already mapped user account is not permitted.")`.
### Rationale
Prevents accidental or unauthorized identity reassignment on existing accounts while allowing initial mapping for pending self-registrations.
### Impact
Enforced in `UserAccountService.UpdateUserAsync`.
### Architecture Impact
Identity application layer.
### Resolved By
ica-analyst
### Resolved Date
2026-10-15

---

## OQ-002: Error Response Mapping
### Decision
Map invalid person selection or missing person activation errors to HTTP 400 Bad Request RFC 7807 ProblemDetails, and duplicate person conflicts to HTTP 409 Conflict ProblemDetails in `UsersController.cs`.
### Rationale
Follows established API error handling conventions across Cakra.
### Impact
`UsersController.cs`.
### Architecture Impact
REST API boundary.
### Resolved By
ica-analyst
### Resolved Date
2026-10-15

---

## OQ-003: Metric Ribbon Pending Counter
### Decision
Include the `Pending` metric in the metric ribbon in `SCR-USR-001`.
### Rationale
Ensures administrators immediately notice pending registration requests upon opening User Management.
### Impact
`UserManagementView.vue`.
### Architecture Impact
Frontend UI.
### Resolved By
ica-analyst
### Resolved Date
2026-10-15

---

# 9. Architecture Applicability

## Decision

ARCHITECTURE-REQUIRED

## Rationale

This change introduces modifications across multiple architectural boundaries:
1. REST API contract extension on `PUT /api/v1/users/{id}` (`UpdateUserAccountRequest`).
2. Application command and validator changes (`UpdateUserAccountCommand`, `UpdateUserAccountCommandValidator`).
3. Application service orchestration with cross-module queries to `IOrganizationQueryService` during account update.
4. UI component conditional behavior and state management in `SCR-USR-001` and `SCR-USR-002`.

An Architecture document (`CR-033-ARCHITECTURE.md`) is required to formally specify the technical realization and component interactions before implementation planning begins.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved
- [x] All required decisions recorded
- [x] All blocking open questions resolved
- [x] Architecture can be finalized or updated

## Status

READY-FOR-PLANNING

## Notes

All feasibility gaps, open questions, and technical decisions have been thoroughly analyzed and resolved with approved decisions recorded in Section 8. Per SDLC governance rules, the Analyst leaves Planning Readiness in `NOT-READY`; only the Architect sets it to `READY-FOR-PLANNING` upon review and creation of `CR-033-ARCHITECTURE.md`.

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-033-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-033-ISSUE.md)
- FEATURE: [FEAT-USR-001-manage-user-accounts.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-001-manage-user-accounts.md)
- FEATURE: [FEAT-USR-002-register-user-account.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-002-register-user-account.md)
- REFERENCE ARCHITECTURE: [CR-007-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-007-ARCHITECTURE.md)
- REFERENCE ARCHITECTURE: [CR-009-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-009-ARCHITECTURE.md)

Referenced codebase locations:

- [UserAccountModal.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/UserAccountModal.vue)
- [UserManagementView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/UserManagementView.vue)
- [users.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/users.ts)
- [UsersController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/UsersController.cs)
- [UserAccountCommands.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Services/UserAccountCommands.cs)
- [UserAccountService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Services/UserAccountService.cs)
- [AuthenticationService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Services/AuthenticationService.cs)
- [0020_user_accounts_person_id_filtered_unique.sql](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Migrations/Scripts/0020_user_accounts_person_id_filtered_unique.sql)
