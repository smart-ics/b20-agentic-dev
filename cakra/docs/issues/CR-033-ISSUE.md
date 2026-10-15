# ISSUE

## Metadata

ID: CR-033
Type: CHANGE-REQUEST
Status: OPEN
Title: Enable Organizational Person Mapping and Account Approval for Self-Registered Users

## Source

Reported By: User
Reported Date: 2026-10-15

## Description

Self-service user registration allows prospective users to submit account requests from the login screen, producing accounts in a pending state with no initial person association. However, administrators are currently unable to approve or activate these accounts because there is no mechanism to bind an unmapped account to an authoritative organizational person record.

When an administrator attempts to review or approve a pending user account via the user account modal (`SCR-USR-002`), the organizational person field is locked and read-only, preventing person selection. Furthermore, even if an account's status were changed to active, the user cannot log in because authentication requires a valid, active organizational person record. A permanent mechanism is required to allow administrators to map an unassociated user account to an organizational person and complete account approval.

## Desired Outcome

1. **Organizational Person Association on Review**:
   - Administrators reviewing user accounts in the user account modal (`SCR-USR-002`) can select and link an active organizational person to an unmapped or pending user account.
   - The user account can transition from `Pending` status to `ACTIVE` as part of the approval process.

2. **Validation and Integrity**:
   - The assigned organizational person must be active in the organization context.
   - Strict 1-to-1 association between an active user account and an organizational person is maintained (preventing mapping to a person already linked to an existing active account).

3. **Authentication Enablement**:
   - Once an organizational person is mapped and the account is activated, the user can successfully authenticate and establish a session without receiving person inactivity errors.

## Current Situation

1. In `SCR-USR-002` (`UserAccountModal.vue`), the organizational person input is rendered strictly read-only and disabled in edit mode (`isEditMode`), with no dropdown available to select a person.
2. Self-registered user accounts (`POST /api/v1/auth/register`) are created with `PersonId = Guid.Empty` and `Status = "Pending"`.
3. The user account update API (`PUT /api/v1/users/{id}`) and underlying commands (`UpdateUserAccountRequest`, `UpdateUserAccountCommand`, `UserAccountService.UpdateUserAsync`) do not accept or persist a `PersonId`.
4. Authentication (`AuthenticationService.cs`) checks whether the linked person is active (`IsPersonActiveAsync(user.PersonId)`); with `PersonId = Guid.Empty`, login fails with code `PERSON_INACTIVE`.

## Evidence

- Modal UI: [UserAccountModal.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/UserAccountModal.vue#L361-L374) (Organizational Person input is disabled and read-only in edit mode).
- Registration Handler: [RegisterUserAccountCommandHandler.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Commands/RegisterUserAccountCommandHandler.cs#L61-L69) (initializes accounts with `PersonId = Guid.Empty` and `Status = Pending`).
- Authentication Service: [AuthenticationService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Services/AuthenticationService.cs#L90-L98) (rejects login if `IsPersonActiveAsync` returns false).
- Architecture Reference: [CR-009-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-009-ARCHITECTURE.md#L151-L156) §4 TD-004 (unassociated person handling specified in architecture but not realized in implementation slices).

## Notes

- This issue was captured following review of `SCR-USR-002` user approval behavior for self-registered accounts.
