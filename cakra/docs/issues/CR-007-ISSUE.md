# ISSUE

## Metadata

ID: CR-007
Type: CHANGE-REQUEST
Status: OPEN
Title: User Management UI - Add and Update User Accounts Restricted to Administrators

## Source

Reported By: User
Reported Date: 2026-10-03

## Description

The user requested a dedicated user interface (UI) to add new user accounts and update existing user accounts. Access to this navigation menu and screen must be strictly restricted so that only users with the Administrator role can access and view it.

## Desired Outcome

1. A dedicated navigation menu item and UI screen for User Management is available in the web application.
2. Only authenticated users possessing the Administrator role can see and open the User Management menu/page; non-administrators are restricted from accessing it.
3. Administrators can add new user accounts with necessary user identity information (such as username, email, associated person, initial credentials, and status).
4. Administrators can view and update existing user accounts (such as updating email, person association, status, or resetting credentials).
5. The system provides appropriate validation and feedback during user account creation and modification operations.

## Current Situation

1. The frontend application navigation shell in [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue) provides sections for Operations, Catalog, and Management, but has no Administration navigation section or menu link for managing users.
2. The frontend router ([router/index.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/router/index.ts)) enforces general authentication (`requiresAuth`), but does not define routes or role-based guards restricting access specifically to the Administrator role for user management.
3. In the Identity module ([UserAccount.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Domain/UserAccount.cs), [UserAccountRepository.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Persistence/UserAccountRepository.cs)), low-level database operations exist, but there are no application services, commands, or REST API endpoints in [Cakra.Api](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api) to support administrative user creation, listing, and updates.
4. Screen inventories and navigation documentation ([screen-inventory.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/screen-inventory.md), [navigation-map.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/navigation-map.md)) currently do not include a dedicated User Management screen or navigation flow.

## Evidence

- User directive: "I need a UI to add or update user. Only administrator can open this menu"
- Frontend navigation shell: [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue)
- Frontend router configuration: [router/index.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/router/index.ts)
- Identity domain entity: [UserAccount.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Domain/UserAccount.cs)
- Identity repository: [UserAccountRepository.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Persistence/UserAccountRepository.cs)
- Authentication controller: [AuthController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/AuthController.cs)
- Navigation screen inventory: [screen-inventory.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/screen-inventory.md)

## Notes

This artifact formally captures the intake request in a solution-neutral manner according to Knowledge-Centric SDLC standards. Detailed feasibility assessment, domain and feature specifications, role authorization enforcement rules, screen UX workflows, architecture design, and implementation planning belong to downstream analysis and architecture stages.
