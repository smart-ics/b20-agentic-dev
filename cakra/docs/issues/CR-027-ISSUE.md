# ISSUE

## Metadata

ID: CR-027
Type: CHANGE-REQUEST
Status: OPEN
Title: Enrich SCR-ORG-002 (Person Modal) with Role Assignment and Enhance SCR-ORG-001 with Roles Display

## Source

Reported By: User
Reported Date: 2026-10-08

## Description

The user identified that Organizational Roles (e.g., Administrator, Management, Operational User) are attached to Person entities, but there is currently no Role Picker in `SCR-ORG-002` (`PersonModal.vue`). 

The user requested enriching `SCR-ORG-002` specifically with role assignment capabilities, enabling administrators to select and assign roles when creating or editing an organizational Person, and surfacing the assigned roles directly in the `SCR-ORG-001` (`PersonManagementView.vue`) management table.

## Desired Outcome

1. **Role Picker in SCR-ORG-002 (`PersonModal.vue`)**:
   - Provide a dedicated Roles selection control in the modal using checkboxes displaying role names and brief descriptions (e.g., Administrator, Management, Operational User).
   - Support multiple role selection per Person.
   - Enforce mandatory role selection: at least one role must be selected to create or save a Person.
   - Protect against accidental self-demotion: if the currently logged-in administrator edits their own Person record, disable/lock removal of the `Administrator` role.

2. **Master Roles Availability & Dynamic Sourcing**:
   - Ensure the database master roles in `organization.Roles` include standard operational roles: `Administrator`, `Management`, `Operational User` (while preserving existing `Programmer` and `Implementator`).
   - Expose an API endpoint (e.g. `GET /api/v1/organization/roles`) so available roles are dynamically retrieved and presented in the modal.

3. **Atomic API & Transactional Assignment**:
   - Support atomic creation and updates by including `RoleIds` in the Person creation and update API contracts (`CreatePersonRequest`, `UpdatePersonRequest`).
   - Create or update person details and synchronize `organization.RoleAssignments` within a single database transaction, dispatching appropriate domain events (`RoleAssigned`, `RoleRevoked`).
   - Retain role assignments upon person deactivation so reactivating the Person immediately restores their role assignments.

4. **Enhance SCR-ORG-001 (`PersonManagementView.vue`)**:
   - Add a "Roles" column to the Person Management table rendering active role badges.
   - Include assigned role names in the table's client-side keyword search.

## Current Situation

1. `SCR-ORG-002` (`PersonModal.vue`) only captures First Name, Last Name, and Email; it has no UI controls for roles.
2. `OrganizationController.cs` provides endpoints only for person identity fields (`FirstName`, `LastName`, `Email`); it does not accept role IDs in creation or update requests and does not expose a list of available roles.
3. Roles can currently only be assigned to a Person via direct SQL queries (such as `seed-admin.ps1`) or programmatic backend service calls (`IOrganizationService.AssignRoleToPersonAsync`).
4. `SCR-ORG-001` (`PersonManagementView.vue`) table displays Name, Email, Status, and Actions, but does not display roles or search by roles.
5. The master roles seeded in the database currently consist of `Administrator`, `Management`, `Programmer`, and `Implementator`, but do not yet include `Operational User`.

## Evidence

- Modal Component: [PersonModal.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/PersonModal.vue) (`SCR-ORG-002`)
- Management View: [PersonManagementView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/PersonManagementView.vue) (`SCR-ORG-001`)
- Controller: [OrganizationController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/OrganizationController.cs)
- Backend Domain & Commands: [Role.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Domain/Role.cs), [RoleAssignment.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Domain/RoleAssignment.cs), [AssignRoleToPersonCommand.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Commands/AssignRoleToPersonCommand.cs)
- Intake Alignment Interview: Conducted on 2026-10-08 via `/grill-me`, confirming:
  - Multi-role support with at least one mandatory role.
  - Checkbox UI presentation with names and descriptions.
  - Dynamic API fetching of roles with master data migration/seed for `Operational User`.
  - Atomic payload updates in `CreatePersonRequest` and `UpdatePersonRequest`.
  - Self-demotion guard for administrators.
  - Roles column and search filter enhancement in `SCR-ORG-001`.
  - Retention of role assignments upon person deactivation.

## Notes

- Downstream workflow routing:
  - Next Stage: Discovery & Feasibility Assessment
  - Owning Role: `ica-analyst`
