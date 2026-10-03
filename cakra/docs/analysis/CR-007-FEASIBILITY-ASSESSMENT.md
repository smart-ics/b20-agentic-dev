---
Title: Feasibility Assessment for User Management UI - Add and Update User Capability Restricted to Administrators (CR-007)
Code: CR-007
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-03
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Feature being assessed: User Management UI - Add and Update User Capability Restricted to Administrators ([FEAT-USR-001-manage-user-accounts.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-001-manage-user-accounts.md)), per request in [CR-007-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-007-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-007-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-007-ISSUE.md)
- FEATURE: [FEAT-USR-001-manage-user-accounts.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-001-manage-user-accounts.md)
- DOMAIN: [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md)
- ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md) (Section 14: Identity & Authentication Architecture)

## Objective

Assess the feasibility, existing technical baseline, gap closures, architectural impacts, and planning readiness to:

1. Expose administrative user management capabilities (listing, creating, and updating user accounts) in `Cakra.Modules.Identity` and REST API endpoints in `Cakra.Api`.
2. Secure all user management endpoints and UI routes strictly for authenticated actors possessing the `Administrator` (or `Admin`) role.
3. Build frontend views and dialog components (`SCR-USR-001: User Management View`, `SCR-USR-002: Add/Edit User Modal`) in `Cakra.Web`.
4. Surface an "Administration" navigation section and "User Management" link in the left sidebar exclusively for Administrators.
5. Enforce domain constraints: 1-to-1 linkage between `UserAccount` and `Person`, username/email uniqueness, cryptographic password hashing via `IPasswordHasher<UserAccount>`, and account status transitions (`ACTIVE`, `LOCKED`, `SUSPENDED`).

---

# 2. Current State

## Existing Behavior

1. **Database Schema & Persistence (`[identity].[UserAccounts]`)**:
   - The SQL Server table `[identity].[UserAccounts]` is already provisioned by migration script [0002_identity_tables.sql](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Migrations/Scripts/0002_identity_tables.sql).
   - Table columns include: `UserId` (PK), `PersonId` (Unique), `Username` (Unique), `Email` (Unique), `PasswordHash`, `Status` (Default `'ACTIVE'`), `FailedLoginAttempts`, `LastLoginAt`, `CreatedAt`, `UpdatedAt`.
   - [UserAccountRepository.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Persistence/UserAccountRepository.cs) already implements Dapper methods: `GetByIdAsync`, `GetByUsernameOrEmailAsync`, `GetByPersonIdAsync`, `GetAllAsync`, `AddAsync`, `UpdateAsync`, and `DeleteAsync`.
2. **Backend Application Layer (`Cakra.Modules.Identity`)**:
   - [IdentityModule.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/IdentityModule.cs) registers `IUserAccountRepository`, `IUserSessionRepository`, `IPasswordHasher<UserAccount>`, `IAuthenticationService`, and `IAuthorizationService`.
   - [AuthenticationService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Services/AuthenticationService.cs) handles login verification, failed login lockout (threshold 5), session issuance, and password verification with ASP.NET Core `PasswordHasher<UserAccount>`.
   - [AuthorizationService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Services/AuthorizationService.cs) resolves dynamic active roles for a `PersonId` by delegating to `IOrganizationQueryService`.
   - **Missing**: No user account commands, validators, query service, or management service (e.g. `CreateUserAccountCommand`, `UpdateUserAccountCommand`, `IUserAccountService`) exist in `Cakra.Modules.Identity`.
3. **Backend REST API Layer (`Cakra.Api`)**:
   - [AuthController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/AuthController.cs) exposes authentication endpoints (`/api/v1/auth/login`, `/api/v1/auth/logout`, `/api/v1/auth/me`).
   - [OrganizationController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/OrganizationController.cs) exposes lookup endpoints (`GET /api/v1/organization/persons/active`) to retrieve active organizational Persons.
   - **Missing**: No REST API controller exists for user account operations (`api/v1/users`).
4. **Frontend Navigation & Presentation (`Cakra.Web`)**:
   - [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue) renders navigation sections: Operations, Catalog, Management, but lacks an Administration section.
   - [stores/auth.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/stores/auth.ts) provides reactive `roles` computed property and `currentUser` details.
   - [router/index.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/router/index.ts) has an authentication guard checking `authStore.isAuthenticated`, but lacks role-based access checking (e.g., verifying `Administrator`).
   - **Missing**: No `UserManagementView.vue` (`SCR-USR-001`) or user account dialog (`SCR-USR-002`) exists in `Cakra.Web`.
5. **Documentation**:
   - Screen Inventory ([screen-inventory.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/screen-inventory.md)) and Navigation Map ([navigation-map.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/navigation-map.md)) do not document `SCR-USR-001` or `SCR-USR-002`.

## Existing Constraints

1. **1-to-1 Association**: Exactly one `UserAccount` corresponds to one `Person` in the Organization domain (Architecture §14, database unique constraint `UQ_UserAccounts_PersonId`).
2. **Username and Email Uniqueness**: Enforced in SQL Server via `UQ_UserAccounts_Username` and `UQ_UserAccounts_Email`.
3. **No Plaintext Passwords**: Passwords must be hashed using PBKDF2/HMAC-SHA512 (`IPasswordHasher<UserAccount>`) before persistence. Plaintext passwords must never be stored, logged, or returned in API DTOs.
4. **Role Authorization**: Access to create, update, or view user accounts must be strictly restricted to users with `Administrator` or `Admin` role.
5. **Architectural Schema Isolation**: The Identity module must not perform cross-schema joins to `organization.Persons`; Person details (e.g. FullName) must be joined in memory or resolved via `IOrganizationQueryService`.

---

# 3. Gap Analysis

| ID | Severity | Gap |
|---|---|---|
| GAP-001 | CRITICAL | No application service, commands, or query models exist in `Cakra.Modules.Identity` to manage user accounts (listing, creating with validation, and updating). |
| GAP-002 | CRITICAL | No REST API controller (`UsersController.cs`) exists in `Cakra.Api` exposing `GET /api/v1/users`, `GET /api/v1/users/{id}`, `POST /api/v1/users`, and `PUT /api/v1/users/{id}` guarded with `[Authorize(Roles = "Administrator,Admin")]`. |
| GAP-003 | CRITICAL | Frontend lacks a dedicated User Management screen (`UserManagementView.vue` - `SCR-USR-001`) and modal dialog (`UserAccountModal.vue` - `SCR-USR-002`). |
| GAP-004 | CRITICAL | Frontend sidebar in `App.vue` lacks an "Administration" section with a "User Management" link, and navigation is not conditionally hidden for non-administrators. |
| GAP-005 | MAJOR | Frontend router in `router/index.ts` does not enforce role-based route guard checking for `Administrator`, allowing unauthorized direct URL visits unless guarded. |
| GAP-006 | MAJOR | Navigation maps (`navigation-map.md`) and Screen Inventory (`screen-inventory.md`) lack `SCR-USR-001` and `SCR-USR-002` documentation. |
| GAP-007 | MINOR | Automated unit and integration tests covering user management application services, role-based authorization on API endpoints, and validation rules are missing. |

---

# 4. Open Questions

| ID | Question | Impact |
|---|---|---|
| OQ-001 | Which role names should be recognized as authorized administrators for opening the menu and executing API mutations? | Determines RBAC role check configuration in backend (`[Authorize]`) and frontend (`v-if` / router guard). |
| OQ-002 | How should passwords be handled during user creation and user update? | Affects command model design, form validation, and security hashing flow. |
| OQ-003 | How should Person association be displayed and selected given architectural schema isolation rules? | Determines API response DTO structure and UI dropdown selection mechanism. |
| OQ-004 | What route URL and screen codes should be allocated for User Management? | Establishes router configuration, screen inventory, and navigation mapping. |

---

# 5. Assumptions

| ID | Assumption |
|---|---|
| ASM-001 | Existing database table `[identity].[UserAccounts]` and repository `UserAccountRepository` are fully sufficient and require no SQL schema modifications. |
| ASM-002 | `IOrganizationQueryService.ListActivePersonsAsync()` can be utilized to enrich user account listings with Person full names and provide options for Person selection in the UI. |
| ASM-003 | Passwords must satisfy basic security requirements (minimum 8 characters) and are hashed using the already registered `IPasswordHasher<UserAccount>`. |
| ASM-004 | Non-administrator users attempting to open `/admin/users` should be prevented both on the client (redirected to `/feed`) and on the server (HTTP 403 Forbidden). |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| RISK-001 | Unauthorized users discovering direct API endpoints or routes. | High | Enforce dual-layer protection: client-side router navigation guard redirecting non-admins, and server-side ASP.NET Core `[Authorize(Roles = "Administrator,Admin")]` returning RFC 7807 403 Forbidden. |
| RISK-002 | Database constraint violations when inserting duplicate Username, Email, or PersonId. | Medium | Pre-validate uniqueness in application command handler/validator, and wrap repository operations with domain conflict handling mapped to RFC 7807 409 Conflict / 400 Bad Request. |
| RISK-003 | Accidental lockout or password reset of current administrator account. | Low | Provide clear UI status indicators and validation; prevent deactivating or locking one's own account. |

---

# 7. Recommendations

## Option A: Dedicated User Management Controller and Views (Recommended)

Implement a dedicated `UsersController.cs` in `Cakra.Api` under `/api/v1/users`, backed by `IUserAccountService` and MediatR commands (`CreateUserAccountCommand`, `UpdateUserAccountCommand`) in `Cakra.Modules.Identity`. Create a dedicated `UserManagementView.vue` (`SCR-USR-001`) with modal dialog `UserAccountModal.vue` (`SCR-USR-002`) under route `/admin/users`. Enforce `Administrator` and `Admin` role checks on both frontend and backend.

### Advantages
- Clean separation of concerns adhering to modular monolith architecture.
- Reuses existing `[identity].[UserAccounts]` table and `UserAccountRepository`.
- Secure by design with server-enforced RBAC and client route guards.
- Matches established patterns from Customer Management (`FEAT-CUST-001`, `CR-002`).

### Disadvantages
- Requires adding an Administration section to navigation and router configuration.

## Option B: Embedded User Account Management inside Organization Views

Embed user credential management directly into an Organization/Person view rather than having a separate User Management page.

### Advantages
- Groups user credentials directly with Person data.

### Disadvantages
- Violates architectural boundary separating IAM (`identity` schema, credentials) from Organization (`organization` schema, business persons).
- Fails the user's specific request for a dedicated menu that only administrators can open.
- Inflexible if external or system users without person records are added in future iterations.

---

# 8. Gap Closure

## GAP-001: Application Service and Commands in Identity Module

### Decision
Implement `IUserAccountService` (or MediatR command handlers: `CreateUserAccountCommand`, `UpdateUserAccountCommand`, `GetUserAccountsQuery`) in `Cakra.Modules.Identity.Services`.
- Enforce validation: Username (required, 3-50 chars, alphanumeric/symbols), Email (required, valid format, max 150 chars), PersonId (required, valid GUID, not already linked to another account), Password (required for creation, min 8 chars; optional for update).
- Enrich queries with Person Name from `IOrganizationQueryService`.
- Support status changes (`ACTIVE`, `LOCKED`, `SUSPENDED`) and password reset.

### Rationale
Provides a robust application layer encapsulating business rules, password hashing, and uniqueness validation before persistence.

### Impact
Adds clean application service models in `Cakra.Modules.Identity`.

### Architecture Impact
Fits directly into `Cakra.Modules.Identity` without altering module boundaries.

### Resolved By
ica-analyst

### Resolved Date
2026-10-03

---

## GAP-002: REST API Controller in Cakra.Api

### Decision
Create `UsersController.cs` in `Cakra.Api.Controllers` with route `api/v1/users` decorated with `[Authorize(Roles = "Administrator,Admin")]`.
- `GET /api/v1/users`: Returns list of `UserAccountSummaryDto` (UserId, PersonId, PersonName, Username, Email, Status, FailedLoginAttempts, LastLoginAt, CreatedAt).
- `GET /api/v1/users/{id}`: Returns detailed `UserAccountDto`.
- `POST /api/v1/users`: Accepts `CreateUserAccountRequest`, creates account, returns 201 Created.
- `PUT /api/v1/users/{id}`: Accepts `UpdateUserAccountRequest` (Email, Status, optional NewPassword), updates account, returns 200 OK.

### Rationale
Ensures standardized REST API patterns and RFC 7807 error responses with server-side authorization enforcement.

### Impact
Adds `UsersController.cs` in `Cakra.Api`.

### Architecture Impact
Extends the API surface in compliance with Architecture §19.5 and §19.6.

### Resolved By
ica-analyst

### Resolved Date
2026-10-03

---

## GAP-003 & GAP-004: Frontend View, Modal, and Sidebar Navigation

### Decision
1. Add an "Administration" section in [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue) sidebar with `data-testid="nav-user-management-link"`, conditionally rendered with `v-if="isAdmin"`.
2. Create `UserManagementView.vue` (`SCR-USR-001`) at `/admin/users` displaying a searchable table with user details, status badges, and action buttons.
3. Create `UserAccountModal.vue` (`SCR-USR-002`) supporting both "Add User" and "Edit User" modes with dropdown to pick active Persons from `api/v1/organization/persons/active`.

### Rationale
Fulfills the user requirement that only administrators can see and open this menu, with intuitive feedback and modal flows matching existing screens like `CustomerPortfolioView.vue`.

### Impact
New Vue components and template updates in `Cakra.Web`.

### Architecture Impact
Expands the UI screen inventory in accordance with CAKRA UI design guidelines.

### Resolved By
ica-analyst

### Resolved Date
2026-10-03

---

## GAP-005: Frontend Role-Based Route Guard

### Decision
Update `router/index.ts` route metadata to include `requiresRole: 'Administrator'` for route `/admin/users`. Update `router.beforeEach` to check if `to.meta.requiresRole` is specified; if the authenticated user lacks the required role, redirect to `/feed`.

### Rationale
Prevents unauthorized actors from navigating directly to `/admin/users` via URL address bar.

### Impact
Enhanced route guard in `router/index.ts`.

### Architecture Impact
Strengthens client-side security navigation architecture.

### Resolved By
ica-analyst

### Resolved Date
2026-10-03

---

## OQ-001: Authorized Roles Definition

### Decision
Recognize both `Administrator` and `Admin` role strings as authorized for administrative user management operations across backend controllers (`[Authorize(Roles = "Administrator,Admin")]`) and frontend role checks.

### Rationale
Prevents permission mismatches between legacy seed data (`Admin`) and formal domain specification (`Administrator`).

### Impact
Consistent authorization checks across UI and API.

### Architecture Impact
None.

### Resolved By
ica-analyst

### Resolved Date
2026-10-03

---

## OQ-002: Password Handling on Create and Update

### Decision
During user creation, `password` is mandatory (minimum 8 characters) and immediately hashed using `IPasswordHasher<UserAccount>`. During update, `password` is optional: if omitted or blank, the existing `PasswordHash` is retained; if provided, it is validated and hashed as a password reset.

### Rationale
Provides clean account creation and password reset capability in a single unified edit workflow without risking password exposure.

### Impact
Simple, secure credential handling in `UserAccountService`.

### Architecture Impact
None.

### Resolved By
ica-analyst

### Resolved Date
2026-10-03

---

## OQ-003: Person Association Resolution

### Decision
The user list query service will query `IOrganizationQueryService.ListActivePersonsAsync()` in memory to map `PersonId` to `FullName` without cross-schema database joins. The Add User modal will fetch active persons via existing endpoint `GET /api/v1/organization/persons/active` to populate the Person selector dropdown.

### Rationale
Strictly preserves modular monolith boundaries and schema isolation conventions (Architecture §14, §20).

### Impact
Optimal reuse of existing Organization queries and endpoints.

### Architecture Impact
Zero cross-schema database coupling.

### Resolved By
ica-analyst

### Resolved Date
2026-10-03

---

## OQ-004: Screen and Route Identifiers

### Decision
- Screen Code: `SCR-USR-001` (User Management View), `SCR-USR-002` (Add / Edit User Modal)
- Route: `/admin/users` (Name: `user-management`)
- Navigation Section: "Administration"

### Rationale
Conforms to CAKRA screen naming convention (`SCR-<MODULE>-<NUMBER>`) and clean RESTful route hierarchy.

### Impact
Updates to `screen-inventory.md` and `navigation-map.md`.

### Architecture Impact
None.

### Resolved By
ica-analyst

### Resolved Date
2026-10-03

---

# 9. Architecture Applicability

## Decision

ARCHITECTURE-REQUIRED

## Rationale

Implementation of `CR-007` introduces:
1. New application service contracts, commands, validators, and queries in `Cakra.Modules.Identity`.
2. A new REST API controller (`UsersController.cs`) with role-based authorization attributes in `Cakra.Api`.
3. New client-side role-based routing guards in Vue Router.
4. New UI screen components (`SCR-USR-001`, `SCR-USR-002`) and navigation structural updates.

A formal ARCHITECTURE artifact (`CR-007-ARCHITECTURE.md`) is required to document the component interactions, security constraints, API contracts, and slice structure before planning.

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

All feasibility gaps, open questions, and implementation options have been fully analyzed and resolved with approved decisions. The Architect has verified the criteria and formally granted READY-FOR-PLANNING gate.

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-007-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-007-ISSUE.md)
- FEATURE: [FEAT-USR-001-manage-user-accounts.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-001-manage-user-accounts.md)
- DOMAIN: [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md)
- ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

Referenced codebase locations:

- [UserAccount.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Domain/UserAccount.cs)
- [UserAccountRepository.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Persistence/UserAccountRepository.cs)
- [AuthenticationService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Services/AuthenticationService.cs)
- [AuthorizationService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Services/AuthorizationService.cs)
- [AuthController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/AuthController.cs)
- [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue)
- [router/index.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/router/index.ts)
- [0002_identity_tables.sql](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Migrations/Scripts/0002_identity_tables.sql)
