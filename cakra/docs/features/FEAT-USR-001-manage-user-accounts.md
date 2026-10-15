# Feature

## Identity

ID: FEAT-USR-001
Name: Manage User Accounts
Type: Command & Query

## Purpose

Enables system Administrators to maintain user accounts, credentials, and account statuses within the system while restricting access strictly from unauthorized users.

## Business Outcome

User accounts are securely created, maintained, and updated by authorized Administrators, ensuring that every operational user has an authoritative login identity linked to their organizational person record, with unauthorized actors strictly prevented from accessing identity management functions.

## Traceability

### Domains
- Identity & Access (Candidate Domain / IAM Module, Architecture §14)
- Organization ([organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md))

### Scenarios
- SC-USR-001 (Manage User Accounts)

### Use Cases
- UC-USR-001 (Manage User Accounts)

### Screens
- `SCR-USR-001` (User Management View)
- `SCR-USR-002` (Add / Edit User Modal)

## Preconditions

- The actor is authenticated.
- The actor possesses the `Administrator` role (or `Admin`).
- For creating a user account, the target `Person` must exist in the Organization domain.

## Capability

The system provides a dedicated User Management screen (`SCR-USR-001`) and interactive dialogs (`SCR-USR-002`) accessible exclusively to Administrators. The screen displays a searchable table of all user accounts with their username, email, associated person name, status (`ACTIVE`, `LOCKED`, `SUSPENDED`, `Pending`), failed attempt counter, and last login timestamp. Administrators can invoke "Add User" to register a new account tied to an unassociated Person, or "Edit User" to update email, status, credentials, and map an authoritative organizational Person for unassociated or pending self-registered accounts. All mutations and queries are guarded by strict role-based access control (RBAC).

## Operational Flow

### 1. View User Accounts
1. An Administrator selects the "User Management" item from the administration section in the sidebar.
2. The system verifies the user's role. If authorized, the system retrieves and displays the active list of user accounts with organizational person details and account statuses (including pending approvals).
3. If an unauthorized user attempts to navigate to the screen, the system refuses access and redirects to the default overview page.

### 2. Add New User Account
1. The Administrator clicks "Add User" to open the creation modal dialog (`SCR-USR-002`).
2. The Administrator selects an organizational Person, enters a unique Username and Email, sets an initial Password, and selects the initial Status (`ACTIVE` or `SUSPENDED`).
3. The Administrator submits the form.
4. The system validates the inputs:
   - Verifies Username is non-empty, alphanumeric/standard characters, and unique.
   - Verifies Email is valid format and unique.
   - Verifies Person exists and is not already associated with another user account (1-to-1 rule).
   - Verifies Password meets strength requirements.
5. The system cryptographically hashes the password, records the user account, and refreshes the user account list.

### 3. Update User Account & Approve Pending Registration
1. The Administrator clicks "Edit" on a specific user row in `SCR-USR-001`.
2. The modal dialog displays current account details.
   - If the user account is already mapped to a Person, the Person name is displayed read-only.
   - If the user account is unmapped (e.g. self-registered in `Pending` status), the modal renders an active Person selector populated from the Organization domain.
3. The Administrator updates the Email, selects an authoritative Person (if unmapped), updates Status (approving `Pending` to `ACTIVE`, or transitioning between `ACTIVE`, `LOCKED`, `SUSPENDED`, or unlocking a locked account), or optionally provides a new password to reset credentials.
4. The Administrator submits the changes.
5. The system validates the modified fields:
   - Verifies email uniqueness (excluding self).
   - If mapping an unassociated account to a Person, verifies that the Person exists, is active, and is not already linked to another active user account.
   - Requires an associated active Person before allowing transition to `ACTIVE` status.
   - Applies updates to the database, and refreshes the list view.

## Domain Orchestration

- **Identity & Access**: Owns `UserAccount` aggregate, authentication credentials, password hashing, account status lifecycle (`Active`, `Locked`, `Suspended`, `Pending`), login failure resets, and security token invalidation.
- **Organization**: Provides authoritative organizational `Person` records, active person validation, and role resolution to enforce that only users holding the `Administrator` role can execute these capabilities.

## Constraints

- Only users holding the `Administrator` role may view or invoke user management operations.
- Every active `UserAccount` must reference exactly one authoritative `PersonId` (1-to-1 association per Architecture §14). Unassociated accounts cannot authenticate or transition to `ACTIVE` without a valid Person.
- `Username` must be immutable or uniquely constrained across the entire system.
- `Email` must be unique across all active accounts.
- Plaintext passwords must never be stored, logged, or returned in API responses.

## Exceptions

- **Unauthorized Access Attempt**: If a non-administrator requests user management endpoints, the system rejects the request with HTTP `403 Forbidden`.
- **Duplicate Username or Email**: If the provided username or email is already registered, the system returns a validation conflict error without saving.
- **Duplicate Person Association**: If the selected Person is already linked to another user account, the system rejects the update/creation with a validation conflict error.
- **Unassociated Activation Attempt**: If an Administrator attempts to activate an account without linking an active Person, the system rejects the operation.
- **Target Account Not Found**: If an update targets a nonexistent user ID, the system returns HTTP `404 Not Found`.

## Acceptance Criteria

- [ ] Navigation menu displays "User Management" link only when the authenticated user possesses the `Administrator` role.
- [ ] Direct URL navigation to `/admin/users` by non-administrators is blocked and redirected to `/feed`.
- [ ] Backend REST API endpoints for user account administration enforce role authorization (`Administrator` / `Admin`) and reject unauthorized callers with 403 Forbidden.
- [ ] Administrator can view all existing user accounts with username, email, associated person name, and status (including pending accounts).
- [ ] Administrator can trigger "Add User" modal and register a new user account with required fields.
- [ ] Form validation prevents submission of duplicate usernames, duplicate emails, or persons already linked to an existing account.
- [ ] Administrator can trigger "Edit User" modal to update email, status (active/suspended/unlocked), and reset password.
- [ ] Administrator can select and link an active organizational Person when editing an unmapped/pending user account and approve it to ACTIVE.
- [ ] Passwords are stored exclusively as secure cryptographic hashes and never exposed in query responses.
