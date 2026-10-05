---
Title: Manage Organizational Persons
Code: FEAT-ORG-001
Artifact: FEATURE
Version: 1.0
LastUpdated: 2026-10-05
---

# 1. Purpose

Provides a dedicated administrative interface for creating, updating, activating, and deactivating organizational Person records, ensuring that authoritative organizational identity data is maintained within the Organization domain while remaining available for operational assignment across Requests, Work Packages, and User Accounts.

# 2. Business Outcome

Organizational Person records are created and maintained by authorized Administrators through a dedicated management screen, with full control over identity attributes and lifecycle status, while inactive Persons remain preserved as historical references for existing operational assignments.

# 3. Participating Domains

## Organization
- Owns the authoritative `Person` aggregate and its identity attributes (first name, last name, email).
- Owns the Person lifecycle state machine (`ACTIVE` -> `INACTIVE` and back).
- Provides the write operations (`CreatePersonAsync`, `UpdatePersonAsync`, `DeactivatePersonAsync`, `ActivatePersonAsync`) and read projections consumed by the management UI.

# 4. Trigger

An authenticated Administrator navigates to the Person Management screen and invokes create, edit, activate, or deactivate actions on Person records.

# 5. Preconditions

- The actor is authenticated.
- The actor possesses the `Administrator` role (or `Admin`).
- The Organization domain write service and command handlers are available.

# 6. Operational Flow

### 1. View Person Records
1. The Administrator opens the Person Management screen.
2. The system retrieves the list of all Person records (both active and inactive) with their identity attributes and status.
3. The system displays a searchable table of Persons with full name, email, status badge, and management actions.

### 2. Create New Person
1. The Administrator clicks "Add Person" to open the creation form.
2. The Administrator enters First Name, Last Name, and Email.
3. The Administrator submits the form.
4. The system validates the inputs:
   - Verifies First Name is non-empty and within maximum length.
   - Verifies Last Name is non-empty and within maximum length.
   - Verifies Email is valid format and unique across all Persons.
5. The system records the new Person in `ACTIVE` status and refreshes the Person list.

### 3. Update Person Identity
1. The Administrator clicks "Edit" on a Person row.
2. The form displays current identity attributes.
3. The Administrator updates First Name, Last Name, and/or Email.
4. The Administrator submits the changes.
5. The system validates the modified fields, verifies email uniqueness (excluding self), applies updates, and refreshes the list view.

### 4. Deactivate Person
1. The Administrator clicks "Deactivate" on an active Person row.
2. The system transitions the Person status from `ACTIVE` to `INACTIVE`.
3. The Person is removed from active assignment dropdowns but remains visible in the management list and is preserved as a historical reference.

### 5. Activate Person
1. The Administrator clicks "Activate" on an inactive Person row.
2. The system transitions the Person status from `INACTIVE` to `ACTIVE`.
3. The Person becomes available again for operational assignment.

# 7. Domain Orchestration

- **Organization**: Owns the `Person` aggregate and lifecycle. Executes create, update, activate, and deactivate mutations through its application service, enforcing email uniqueness and status transitions. Emits domain events (`PersonCreated`, `PersonDeactivated`, `PersonActivated`) for downstream consumers.

# 8. Constraints

- Only users holding the `Administrator` role may view or invoke Person management operations.
- Every `Person` must have a unique email address across the system.
- A `Person` must not be physically deleted; lifecycle transitions (`ACTIVE` <-> `INACTIVE`) are the only permitted mutations.
- Inactive Persons remain valid historical references for existing Requests, Work Packages, and User Accounts.
- The Organization domain must remain focused on organizational knowledge and must not evolve into an HR system.

# 9. Exceptions

- **Unauthorized Access Attempt**: If a non-administrator requests Person management endpoints, the system rejects the request with HTTP `403 Forbidden`.
- **Duplicate Email**: If the provided email is already registered to another Person, the system returns a validation conflict error without saving.
- **Target Person Not Found**: If an update or lifecycle transition targets a nonexistent Person ID, the system returns HTTP `404 Not Found`.

# 10. Acceptance Criteria

- [ ] Navigation menu displays "Person Management" link only when the authenticated user possesses the `Administrator` role.
- [ ] Direct URL navigation to the Person Management route by non-administrators is blocked and redirected to `/feed`.
- [ ] Backend REST API endpoints for Person administration enforce role authorization (`Administrator` / `Admin`) and reject unauthorized callers with 403 Forbidden.
- [ ] Administrator can view all existing Person records with first name, last name, email, and status.
- [ ] Administrator can trigger "Add Person" and register a new Person with required identity fields.
- [ ] Form validation prevents submission of duplicate emails.
- [ ] Administrator can trigger "Edit Person" to update identity attributes.
- [ ] Administrator can deactivate an active Person, transitioning status to `INACTIVE` while preserving the record.
- [ ] Administrator can activate an inactive Person, transitioning status back to `ACTIVE`.
- [ ] Successful mutations immediately refresh the Person list view.