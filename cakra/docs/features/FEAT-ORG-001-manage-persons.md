---
Title: Manage Organizational Persons & Role Assignments
Code: FEAT-ORG-001
Artifact: FEATURE
Version: 1.1
LastUpdated: 2026-10-08
---

# 1. Purpose

Provides a dedicated administrative interface for creating, updating, activating, and deactivating organizational Person records and assigning organizational roles, ensuring that authoritative organizational identity data and operational role capabilities are maintained within the Organization domain while remaining available for operational assignment across Requests, Work Packages, and User Accounts.

# 2. Business Outcome

Organizational Person records are created and maintained by authorized Administrators through a dedicated management screen, with full control over identity attributes, lifecycle status, and organizational role assignments. Inactive Persons remain preserved as historical references for existing operational assignments, and their role assignments are retained in history to be restored upon reactivation.

# 3. Participating Domains

## Organization
- Owns the authoritative `Person` aggregate and its identity attributes (first name, last name, email).
- Owns the `Role` aggregate (e.g., `Administrator`, `Management`, `Operational User`, `Programmer`, `Implementator`) and `RoleAssignment` relationships.
- Owns the Person lifecycle state machine (`ACTIVE` -> `INACTIVE` and back).
- Provides the write operations (`CreatePersonAsync`, `UpdatePersonAsync`, `DeactivatePersonAsync`, `ActivatePersonAsync`, `AssignRoleToPersonAsync`, `RevokeRoleFromPersonAsync`) and read projections consumed by the management UI.

# 4. Trigger

An authenticated Administrator navigates to the Person Management screen and invokes create, edit, activate, deactivate, or role assignment actions on Person records.

# 5. Preconditions

- The actor is authenticated.
- The actor possesses the `Administrator` role (or `Admin`).
- The Organization domain write service, query service, and command handlers are available.

# 6. Operational Flow

### 1. View Person Records & Assigned Roles
1. The Administrator opens the Person Management screen (`SCR-ORG-001`).
2. The system retrieves the list of all Person records (both active and inactive) with their identity attributes, lifecycle status, and currently assigned active roles.
3. The system displays a searchable table of Persons with full name, email, assigned role badges, status badge, and management actions.
4. The Administrator can filter/search the table by full name, email, or role name.

### 2. Create New Person with Role Assignment
1. The Administrator clicks "Add Person" to open the creation form modal (`SCR-ORG-002`).
2. The system dynamically loads the list of available organizational roles from master data.
3. The Administrator enters First Name, Last Name, and Email, and selects one or more organizational roles via role checkboxes.
4. The Administrator submits the form.
5. The system validates the inputs:
   - Verifies First Name is non-empty and within maximum length.
   - Verifies Last Name is non-empty and within maximum length.
   - Verifies Email is valid format and unique across all Persons.
   - Verifies at least one organizational role is selected.
6. The system records the new Person in `ACTIVE` status, assigns the selected roles in a single atomic transaction, dispatches domain events (`PersonCreated`, `RoleAssigned`), and refreshes the Person list.

### 3. Update Person Identity & Roles
1. The Administrator clicks "Edit" on a Person row.
2. The form modal (`SCR-ORG-002`) displays current identity attributes and pre-selects currently active roles.
3. If the logged-in administrator is editing their own record, the `Administrator` role checkbox is disabled/locked to prevent self-demotion.
4. The Administrator updates First Name, Last Name, Email, and/or role selections.
5. The Administrator submits the changes.
6. The system validates the modified fields:
   - Verifies email uniqueness (excluding self).
   - Verifies at least one role remains selected.
   - Verifies self-demotion protection rule.
7. The system applies identity updates and synchronizes role assignments atomically, dispatches domain events (`RoleAssigned`, `RoleRevoked` as appropriate), and refreshes the list view.

### 4. Deactivate Person
1. The Administrator clicks "Deactivate" on an active Person row.
2. The system transitions the Person status from `ACTIVE` to `INACTIVE`.
3. The Person is removed from active assignment dropdowns but remains visible in the management list; existing role assignments are preserved in history.

### 5. Activate Person
1. The Administrator clicks "Activate" on an inactive Person row.
2. The system transitions the Person status from `INACTIVE` to `ACTIVE`.
3. The Person and their retained roles become available again for operational assignment and login authentication.

# 7. Domain Orchestration

- **Organization**: Owns the `Person` aggregate, `Role` master data, and `RoleAssignment` relationships. Executes create, update, activate, deactivate, and role assignment mutations through its application service, enforcing email uniqueness, status transitions, and role invariants. Emits domain events (`PersonCreated`, `PersonDeactivated`, `PersonActivated`, `RoleAssigned`, `RoleRevoked`) for downstream consumers.

# 8. Constraints

- Only users holding the `Administrator` role may view or invoke Person management operations.
- Every `Person` must have a unique email address across the system.
- Every `Person` must hold at least one active organizational role upon creation and updates.
- An Administrator cannot remove the `Administrator` role from their own record (Self-demotion prevention).
- A `Person` must not be physically deleted; lifecycle transitions (`ACTIVE` <-> `INACTIVE`) are the only permitted mutations.
- Inactive Persons retain their historical role assignments; reactivating a Person restores their roles immediately.
- The Organization domain must remain focused on organizational knowledge and must not evolve into an HR system.

# 9. Exceptions

- **Unauthorized Access Attempt**: If a non-administrator requests Person management endpoints, the system rejects the request with HTTP `403 Forbidden`.
- **Duplicate Email**: If the provided email is already registered to another Person, the system returns a validation conflict error without saving.
- **Target Person Not Found**: If an update or lifecycle transition targets a nonexistent Person ID, the system returns HTTP `404 Not Found`.
- **No Roles Selected**: If no role is selected during creation or update, the system rejects the submission with a validation error.
- **Self-Demotion Attempt**: If an administrator attempts to remove their own `Administrator` role, the system rejects the request with a domain validation error.

# 10. Acceptance Criteria

- [ ] Navigation menu displays "Person Management" link only when the authenticated user possesses the `Administrator` role.
- [ ] Direct URL navigation to the Person Management route by non-administrators is blocked and redirected to `/feed`.
- [ ] Backend REST API endpoints for Person administration enforce role authorization (`Administrator` / `Admin`) and reject unauthorized callers with 403 Forbidden.
- [ ] Administrator can view all existing Person records with first name, last name, email, assigned role badges, and status.
- [ ] Person Management table search filters across first name, last name, email, and assigned role names.
- [ ] Modal dialog dynamically displays available roles fetched from the system with name and description.
- [ ] Administrator can trigger "Add Person" and register a new Person with required identity fields and at least one selected role.
- [ ] Form validation prevents submission if no roles are selected or if duplicate emails are provided.
- [ ] Administrator can trigger "Edit Person" to update identity attributes and modify role assignments.
- [ ] Self-demotion is prevented: an administrator editing themselves cannot uncheck their own `Administrator` role.
- [ ] Administrator can deactivate an active Person, transitioning status to `INACTIVE` while preserving role assignments in history.
- [ ] Administrator can activate an inactive Person, transitioning status back to `ACTIVE` and restoring operational roles.
- [ ] Successful mutations immediately refresh the Person list view.