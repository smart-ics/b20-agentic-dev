# ISSUE

## Metadata

ID: CR-008
Type: CHANGE-REQUEST
Status: OPEN
Title: Add Organizational Person Management (Create, Update, Activate, Deactivate)

## Source

Reported By: User
Reported Date: 2026-10-05

## Description

The user requested the ability to add a new organizational Person within the Cakra.Web frontend. Currently, the system exposes only read-only lookups of organizational Persons (used as dropdown selectors for Work Package owners, request assignees, and user account association), but provides no dedicated interface to create or maintain Person records themselves.

## Desired Outcome

1. A dedicated UI in the Cakra.Web frontend for managing organizational Person records, accessible to authorized administrators.
2. Administrators can create new Person records with identity attributes (first name, last name, email).
3. Administrators can view existing Person records and update their identity attributes.
4. Administrators can activate or deactivate Person records, controlling their availability for operational assignment while preserving historical references.
5. The Person management UI is reachable through dedicated navigation and restricted to users holding the Administrator role, consistent with existing administration screens.

## Current Situation

1. The Organization domain (`Cakra.Modules.Organization`) already owns the authoritative `Person` aggregate and provides a full application service (`IOrganizationService`) with write operations: `CreatePersonAsync`, `UpdatePersonAsync`, `DeactivatePersonAsync`, and `ActivatePersonAsync`, backed by MediatR commands (`CreatePersonCommand`, `UpdatePersonCommand`, `DeactivatePersonCommand`).
2. The REST API layer (`Cakra.Api.Controllers.OrganizationController`, route `/api/v1/organization`) currently exposes only read endpoints: `GET /api/v1/organization/persons/active`, `GET /api/v1/organization/persons`, and `GET /api/v1/organization/persons/{id:guid}`. No write endpoints exist for creating, updating, or changing the lifecycle status of Persons.
3. The frontend (`Cakra.Web`) has no Person management view or modal. The only Person interaction is the read-only dropdown selector in `UserAccountModal.vue` (`SCR-USR-002`) which fetches active persons from `/organization/persons/active`, and similar selectors in `WorkPackageView.vue` and `CreateRequestModal.vue`.
4. The frontend navigation shell (`App.vue`) and router (`router/index.ts`) contain an Administration section with only a "User Management" link (`/admin/users`, `SCR-USR-001`); there is no navigation entry or route for Person management.
5. The Organization domain documentation (`organization-domain.md`) lists "Person Management" as a domain capability, and the Person lifecycle is defined as `ACTIVE -> INACTIVE`, but no UI or API exposes these mutations.

## Evidence

- User directive: "How to Add PERSON in @cakra/src/frontend/Cakra.Web"
- Backend Organization domain entity: [Person.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Domain/Person.cs)
- Backend Organization write service: [IOrganizationService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Services/IOrganizationService.cs), [OrganizationService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Services/OrganizationService.cs)
- Backend Organization commands: [CreatePersonCommand.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Commands/CreatePersonCommand.cs), [UpdatePersonCommand.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Commands/UpdatePersonCommand.cs), [DeactivatePersonCommand.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Commands/DeactivatePersonCommand.cs)
- Backend Organization query DTO: [PersonDto.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Organization/Models/PersonDto.cs)
- Backend REST controller (read-only): [OrganizationController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/OrganizationController.cs)
- Frontend read-only Person selector: [UserAccountModal.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/UserAccountModal.vue) (line 133, `GET /organization/persons/active`)
- Frontend API client pattern: [customers.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/customers.ts)
- Frontend administration navigation: [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue), [router/index.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/router/index.ts)
- Organization domain documentation: [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md) (Person Management capability, Person lifecycle)
- Navigation screen inventory: [screen-inventory.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/screen-inventory.md)

## Notes

This artifact formally captures the intake request in a solution-neutral manner according to Knowledge-Centric SDLC standards. The Organization domain write service and command handlers already exist; the gap is the REST API write surface and the frontend presentation layer. Detailed feasibility assessment, feature specification, screen UX workflows, architecture design, and implementation planning belong to downstream analysis and architecture stages.