---
Title: Register User Account
Code: FEAT-USR-002
Artifact: FEATURE
Version: 1.0
LastUpdated: 2026-10-05
---

# 1. Purpose

Enables prospective system users to submit a self-service registration request directly from the Login page without requiring prior administrative provisioning, while enforcing that newly registered accounts undergo administrative review and approval before gaining access to the system.

# 2. Business Outcome

New users can register their credentials (username, email, password) from the login card. System security is preserved by creating newly registered accounts in a Pending Admin Approval state (`PENDING` / inactive), preventing unverified access until an Administrator activates the account.

# 3. Participating Domains

- **Identity & Access**: Owns user account registration, credential validation, password hashing, initial pending-approval account status, and authentication enforcement.
- **Organization**: Provides organizational context for eventual user-person mapping upon administrative review.

# 4. Trigger

A prospective user accesses the Sign In card on the Login screen (`SCR-AUTH-001`), clicks the "Don't have an account? Sign Up" toggle link, and submits the registration form.

# 5. Preconditions

- The user is on the Login page (`SCR-AUTH-001`).
- The user does not currently hold an active session.

# 6. Operational Flow

1. The prospective user clicks the "Sign Up" toggle link on the login card.
2. The card dynamically transitions to display the Sign-Up form fields (Username, Email, Password, Confirm Password).
3. The user fills in Username, Email, Password, and Confirm Password, and submits the form.
4. The system validates input criteria (non-empty fields, valid email format, password length minimum 8 characters, matching password confirmation).
5. The system verifies that the requested Username and Email are unique across existing user accounts.
6. The system creates the user account in a Pending Admin Approval state (`PENDING` / inactive) with securely hashed credentials.
7. The system resets the form, switches the login card back to the Sign-In view, and displays a success notification alert (*"Registration submitted! Pending admin approval."*).

# 7. Domain Orchestration

- **Identity & Access**: Receives registration input, validates uniqueness and credential complexity, applies cryptographic hashing, sets initial status to `PENDING` (or inactive requiring approval), and persists the account record.
- **Organization**: Preserves option for subsequent administrative mapping to an authoritative `Person` record during administrative user account review.

# 8. Constraints

- The registration form must require only Username, Email, Password, and Confirm Password.
- Password must be at least 8 characters long and match the confirmation input.
- Username and Email must be unique across all existing accounts.
- Newly registered accounts must NOT be permitted to log in until an Administrator approves/activates the account.

# 9. Exceptions

- **Duplicate Username or Email**: If the username or email is already registered, the system displays a distinct validation error message on the registration form.
- **Validation Failure**: If fields are missing, email format is invalid, password is shorter than 8 characters, or password confirmation does not match, submission is blocked with inline validation errors.
- **Attempted Login before Approval**: If a user attempts to log in with a pending-approval account, the system rejects authentication with a clear error (*"Account is pending administrative approval."*).

# 10. Acceptance Criteria

- [ ] Login card on `SCR-AUTH-001` includes a toggle link to seamlessly switch between Sign In and Sign Up views without leaving the page.
- [ ] Sign-Up form displays inputs for Username, Email, Password, and Confirm Password.
- [ ] Submitting the form with password less than 8 characters or mismatched confirmation displays inline validation errors.
- [ ] Submitting valid registration details creates a user account in `PENDING` (inactive) status requiring Admin approval.
- [ ] Successful registration switches the card back to Sign In view with a success alert (*"Registration submitted! Pending admin approval."*).
- [ ] Attempting to sign in with a pending account returns a 400 Bad Request / RFC 7807 problem details response indicating pending approval status.
