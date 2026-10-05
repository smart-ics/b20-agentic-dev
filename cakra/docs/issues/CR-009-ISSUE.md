# ISSUE

## Metadata

ID: CR-009
Type: CHANGE-REQUEST
Status: OPEN
Title: Sign-Up Feature on Login Page with Pending Admin Approval

## Source

Reported By: User
Reported Date: 2026-10-05

## Description

The user requested a Sign-Up feature on the login page (`LoginView.vue`). The feature allows new users to submit a registration request directly from the login page, capturing Username, Email, Password, and Password Confirmation. Newly created accounts start in a Pending Admin Approval status and cannot log in until an administrator approves and activates them.

## Desired Outcome

1. A seamless toggle link on the login card (`SCR-AUTH-001` / `LoginView.vue`) to switch dynamically between Sign In and Sign Up views without leaving the page.
2. Registration form requesting minimal fields: Username, Email, Password, and Confirm Password.
3. Client-side and server-side input validation enforcing standard rules: minimum 8 characters for password, password confirmation matching, valid email format, and unique username/email.
4. Newly registered user accounts are assigned an initial status requiring administrative approval before authentication is permitted.
5. Post-registration user feedback displaying a clear status notification on the login card confirming registration submission and pending approval status, resetting the form and switching back to Sign In mode.

## Current Situation

1. The login screen (`SCR-AUTH-001` / `LoginView.vue`) currently supports only Sign In (`POST /api/v1/auth/login`) with username and password.
2. There is no public user registration API endpoint or self-service registration workflow in `Cakra.Modules.Identity` or `Cakra.Api`. Currently, user accounts are created only by administrators via `UserManagementView.vue` (`SCR-USR-001`) calling `POST /api/v1/users`.
3. The `UserAccount` domain entity in `Cakra.Modules.Identity` supports account statuses (`Active`, `Locked`, `Suspended`), but lacks a public registration flow and explicit pending-approval lifecycle handling for self-registered users.

## Evidence

- User directive: "I need a Sign-Up feature in login page"
- User design interview responses: Toggle link on login card, minimal fields (Username, Email, Password, Confirm Password), Pending Admin Approval status, post-registration alert notification, minimum 8-char password validation.
- Login screen frontend component: [LoginView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/LoginView.vue) (`SCR-AUTH-001`)
- Auth Pinia store: [auth.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/stores/auth.ts)
- Identity domain entity: [UserAccount.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Domain/UserAccount.cs)
- Authentication Service: [AuthenticationService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Services/AuthenticationService.cs)
- User Account Service: [UserAccountService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Services/UserAccountService.cs)

## Notes

This artifact formally captures the intake request for the Sign-Up change request in a solution-neutral manner according to Knowledge-Centric SDLC standards. Detailed feasibility assessment, domain and feature updates, architecture design, and implementation planning belong to downstream analysis and architecture stages.
