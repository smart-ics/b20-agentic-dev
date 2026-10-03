---
Title: User Management UI - Add and Update User Accounts Architecture
Code: CR-007
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-03
---

# 1. Overview

This architecture defines the technical realization for Change Request `CR-007`: User Management UI - Add and Update User Accounts Restricted to Administrators.

It realizes [FEAT-USR-001-manage-user-accounts.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-001-manage-user-accounts.md) and technical gap closures from [CR-007-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-007-FEASIBILITY-ASSESSMENT.md), establishing:
1. Application layer commands, queries, validators, and service in `Cakra.Modules.Identity`.
2. REST API endpoints in `Cakra.Api` guarded with role-based authorization (`Administrator`, `Admin`).
3. Frontend screen `SCR-USR-001` (`UserManagementView.vue`) and modal `SCR-USR-002` (`UserAccountModal.vue`) in `Cakra.Web`.
4. Role-gated Administration sidebar navigation in [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue) and client-side route guards in [router/index.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/router/index.ts).

---

# 2. Architectural Basis

## Business Context

- DOMAIN: [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md)
- FEATURE: [FEAT-USR-001-manage-user-accounts.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-001-manage-user-accounts.md)
- ISSUE: [CR-007-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-007-ISSUE.md)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md) (Section 14: Identity & Authentication Architecture)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-007-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-007-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

This architecture consumes and realizes the approved decisions:
- `GAP-001`: Implement `IUserAccountService`, commands (`CreateUserAccountCommand`, `UpdateUserAccountCommand`), command validators, and query models in `Cakra.Modules.Identity`.
- `GAP-002`: Implement `UsersController.cs` in `Cakra.Api` under `api/v1/users` guarded with `[Authorize(Roles = "Administrator,Admin")]`.
- `GAP-003` & `GAP-004`: Implement `UserManagementView.vue` (`SCR-USR-001`) and `UserAccountModal.vue` (`SCR-USR-002`) in `Cakra.Web` and surface an "Administration" sidebar section in `App.vue` visible only to Administrators.
- `GAP-005`: Implement role-based navigation guard in `router/index.ts` redirecting unauthorized non-administrators to `/feed`.
- `GAP-006`: Document screens `SCR-USR-001` and `SCR-USR-002` in navigation and screen inventory.
- `GAP-007`: Add automated unit and integration test suites covering the application service, authorization rules, and endpoints.
- `OQ-001`: Recognize both `Administrator` and `Admin` role names for administrative user management operations.
- `OQ-002`: Enforce password hashing with `IPasswordHasher<UserAccount>`; mandatory on creation (min 8 chars), optional on update (password reset).
- `OQ-003`: Resolve Person details in memory via `IOrganizationQueryService` to maintain architectural schema isolation.
- `OQ-004`: Route path `/admin/users`, screen code `SCR-USR-001`, and modal `SCR-USR-002`.

---

# 3. Scope

## Included

1. **Backend Application Layer (`Cakra.Modules.Identity`)**:
   - `IUserAccountService` and `UserAccountService` in `Cakra.Modules.Identity.Services`.
   - `CreateUserAccountCommand`, `CreateUserAccountCommandValidator`.
   - `UpdateUserAccountCommand`, `UpdateUserAccountCommandValidator`.
   - Query DTOs: `UserAccountDto`, `UserAccountSummaryDto`.
   - Service registration in `IdentityModule.cs`.
2. **Backend API Layer (`Cakra.Api`)**:
   - `UsersController.cs` in `Cakra.Api.Controllers` (`[Route("api/v1/users")]`, `[Authorize(Roles = "Administrator,Admin")]`).
   - `GET /api/v1/users`: List all user accounts enriched with person names.
   - `GET /api/v1/users/{id:guid}`: Retrieve single user account details.
   - `POST /api/v1/users`: Create user account.
   - `PUT /api/v1/users/{id:guid}`: Update user account attributes, status, and optional password reset.
3. **Frontend Presentation Layer (`Cakra.Web`)**:
   - API client module `src/api/users.ts`.
   - Navigation updates in `App.vue`: "Administration" section and "User Management" link rendered with `v-if="isAdmin"`.
   - Route registration in `router/index.ts` with `meta: { requiresAuth: true, requiresRole: 'Administrator', screenId: 'SCR-USR-001' }` and guard enforcement in `router.beforeEach`.
   - `UserManagementView.vue` (`SCR-USR-001`): Searchable table, status filter, user metrics, and action triggers.
   - `UserAccountModal.vue` (`SCR-USR-002`): Modal supporting Add and Edit modes, person selection dropdown, password setup, and status toggle.
4. **Automated Testing**:
   - Unit tests in `Cakra.Tests.Unit/Identity/UserAccountServiceTests.cs`.
   - Integration tests in `Cakra.Tests.Integration/Identity/UsersControllerTests.cs`.
5. **Documentation**:
   - Screen Inventory (`screen-inventory.md`) and Navigation Map (`navigation-map.md`).

## Excluded

- SQL database migrations (table `[identity].[UserAccounts]` in `0002_identity_tables.sql` already provides complete schema).
- Modifications to Organization module internal persistence or domain entities.

---

# 4. Technical Decisions

## TD-001: Modular Boundary & In-Memory Person Enrichment

To uphold Architecture §14 and §20 (strict schema isolation with no cross-schema database foreign keys or joins):
- `UserAccountRepository` queries exclusively against `[identity].[UserAccounts]`.
- `UserAccountService` injects `IOrganizationQueryService` to query `ListActivePersonsAsync()` in memory.
- `UserAccountSummaryDto` combines `UserAccount` attributes (`UserId`, `Username`, `Email`, `Status`, `FailedLoginAttempts`, `LastLoginAt`, `CreatedAt`) with `PersonName` resolved from the Organization query service.
- If a referenced Person is inactive or cannot be found, `PersonName` defaults to `Unknown / Archived`.

## TD-002: Dual-Layer Role Authorization

To strictly enforce that "Only administrator can open this menu":
1. **Server-Side Authorization**: `UsersController` is decorated with `[Authorize(Roles = "Administrator,Admin")]`. Any unauthorized caller receives RFC 7807 `403 Forbidden`.
2. **Client-Side Menu Affordance**: In `App.vue`, the "Administration" section and its navigation link are wrapped in `v-if="isAdmin"`, where `isAdmin = computed(() => authStore.roles.includes('Administrator') || authStore.roles.includes('Admin'))`.
3. **Client-Side Route Guard**: In `router/index.ts`, `router.beforeEach` evaluates `to.meta.requiresRole`. If set to `'Administrator'` and `!authStore.roles.some(r => r === 'Administrator' || r === 'Admin')`, navigation is redirected to `/feed`.

## TD-003: Credential Management & Password Hashing

- All password hashing is performed using ASP.NET Core `IPasswordHasher<UserAccount>` (already configured in `IdentityModule.cs`).
- **User Creation**: `Password` is required and must contain at least 8 characters. `PasswordHash` is computed prior to calling `IUserAccountRepository.AddAsync`.
- **User Update**: `NewPassword` is optional. If provided and non-empty, it is validated (min 8 chars) and rehashed onto `PasswordHash`. If empty or null, the existing hash is untouched.
- `PasswordHash` is strictly excluded from query DTOs and API responses.

## TD-004: Validation & Conflict Handling

- **Username Uniqueness**: Validated prior to insert; case-insensitive comparison. Duplicates return validation conflict (`409 Conflict` or RFC 7807 validation error).
- **Email Uniqueness**: Validated prior to insert/update (excluding current user on update). Duplicates return validation conflict.
- **Person 1-to-1 Constraint**: Verified via `_userAccountRepository.GetByPersonIdAsync(personId)`. If an active account is already bound to that Person, creation is rejected.
- **Account Status Transition**: Allowed statuses are `ACTIVE`, `LOCKED`, and `SUSPENDED` (defined in `UserAccountStatus.cs`). Transitioning from `LOCKED` to `ACTIVE` automatically resets `FailedLoginAttempts` to 0.

## TD-005: Frontend Component Architecture

- `UserManagementView.vue` (`SCR-USR-001`) acts as the stateful orchestrator, maintaining user list state, search query, status filter, and modal visibility.
- `UserAccountModal.vue` (`SCR-USR-002`) receives `show`, `mode` (`'create' | 'edit'`), and optional `user` prop. It emits `close` and `saved` events.
- On mount or modal open in create mode, it retrieves active persons from `GET /api/v1/organization/persons/active` to populate the Person dropdown.
- Success and error toasts/alerts are displayed inline within the modal and view using Bootstrap 5 utility styles matching existing views (e.g. `CustomerPortfolioView.vue`).

---

# 5. Component Responsibilities

| Component | Responsibility |
|---|---|
| `UsersController` (`Cakra.Api`) | Exposes REST API endpoints under `/api/v1/users`, guarded by `[Authorize(Roles = "Administrator,Admin")]`. Maps HTTP requests to application services and returns standardized RFC 7807 responses. |
| `IUserAccountService` / `UserAccountService` (`Cakra.Modules.Identity`) | Coordinates user account operations, enforces validation, executes password hashing with `IPasswordHasher<UserAccount>`, enriches accounts with `IOrganizationQueryService`, and delegates to `IUserAccountRepository`. |
| `CreateUserAccountCommandValidator` / `UpdateUserAccountCommandValidator` (`Cakra.Modules.Identity`) | FluentValidation rules enforcing format, string lengths, and password complexity. |
| `UserAccountRepository` (`Cakra.Modules.Identity`) | Executes explicit parameterized SQL queries against `[identity].[UserAccounts]`. |
| `users.ts` (`Cakra.Web/src/api`) | Frontend Axios client communicating with `/api/v1/users`. |
| `App.vue` (`Cakra.Web`) | Renders the sidebar navigation shell with the "Administration" section conditionally displayed for Administrators. |
| `router/index.ts` (`Cakra.Web`) | Configures route `/admin/users` and enforces role-based navigation guards. |
| `UserManagementView.vue` (`Cakra.Web`) | Screen `SCR-USR-001`: displays user account table with search, status filters, and triggers for create/edit. |
| `UserAccountModal.vue` (`Cakra.Web`) | Modal dialog `SCR-USR-002`: provides form inputs for Person, Username, Email, Password, and Status. |

---

# 6. Integration Design

| Source | Target | Communication | Purpose |
|---|---|---|---|
| `App.vue` | `stores/auth.ts` | Reactive State | Evaluates `isAdmin` to render Administration menu link. |
| `router/index.ts` | `stores/auth.ts` | In-Process Query | Evaluates user roles during `beforeEach` route transitions to `/admin/users`. |
| `UserManagementView.vue` | `src/api/users.ts` | HTTP GET | Fetches user account summaries for display. |
| `UserAccountModal.vue` | `/api/v1/organization/persons/active` | HTTP GET | Fetches active organizational persons for dropdown selector. |
| `UserAccountModal.vue` | `src/api/users.ts` | HTTP POST / PUT | Submits user account creation or modification payload. |
| `UsersController` | `IUserAccountService` | Dependency Injection | Dispatches user management operations. |
| `UserAccountService` | `IOrganizationQueryService` | In-Process Service Call | Enriches account records with Person display names. |
| `UserAccountService` | `IPasswordHasher<UserAccount>` | In-Process Service Call | Hashes passwords with PBKDF2/HMAC-SHA512. |
| `UserAccountService` | `IUserAccountRepository` | In-Process Service Call | Persists user account entities to database. |
| `UserAccountRepository` | `[identity].[UserAccounts]` | Parameterized SQL | Executes Dapper CRUD queries against SQL Server. |

---

# 7. Data Ownership

| Data | Authoritative Owner | Schema / Location |
|---|---|---|
| User Credentials & Account Status | `Identity` Module (`Cakra.Modules.Identity`) | `[identity].[UserAccounts]` |
| Active Login Sessions | `Identity` Module (`Cakra.Modules.Identity`) | `[identity].[UserSessions]` |
| Organizational Person Identity | `Organization` Module (`Cakra.Modules.Organization`) | `[organization].[Persons]` |
| Organizational Role Assignments | `Organization` Module (`Cakra.Modules.Organization`) | `[organization].[RoleAssignments]` |

---

# 8. Database Design

## New Tables
None required.

## Modified Tables
None required. Existing table `[identity].[UserAccounts]` already has:
- `UserId` (UNIQUEIDENTIFIER, PK)
- `PersonId` (UNIQUEIDENTIFIER, UQ)
- `Username` (VARCHAR(50), UQ)
- `Email` (VARCHAR(150), UQ)
- `PasswordHash` (VARCHAR(255))
- `Status` (VARCHAR(20))
- `FailedLoginAttempts` (INT)
- `LastLoginAt` (DATETIME2)
- `CreatedAt` (DATETIME2)
- `UpdatedAt` (DATETIME2)

## Relationships
- Logical 1-to-1 relationship between `identity.UserAccounts.PersonId` and `organization.Persons.PersonId` maintained without cross-schema physical foreign keys (Architecture §20).
- 1-to-N relationship between `identity.UserAccounts.UserId` and `identity.UserSessions.UserId`.

## Migration Considerations
No SQL migrations required.

---

# 9. Cross-Cutting Concerns

## Security & RBAC
- Access to all user management operations is restricted to authenticated users holding `Administrator` or `Admin` roles.
- Non-administrators attempting API requests receive HTTP `403 Forbidden`.
- Non-administrators attempting direct URL navigation in the browser are redirected to `/feed`.
- Plaintext passwords are never logged, stored, or returned in API responses.

## Auditability & Traceability
- Updates to user accounts modify `UpdatedAt` timestamp.
- User creation and modifications are logged via structured ASP.NET Core `ILogger`.

## Observability & Error Handling
- All endpoint failures return RFC 7807 `ProblemDetails` with appropriate status codes (`400 Bad Request`, `401 Unauthorized`, `403 Forbidden`, `404 Not Found`, `409 Conflict`).

---

# 10. Implementation Constraints

1. **Explicit SQL**: Persistence must continue using Dapper with explicit parameterized SQL against `[identity].[UserAccounts]` (no EF Core).
2. **Schema Isolation**: No SQL JOIN queries between `identity` and `organization` schemas. Person enrichment must occur via `IOrganizationQueryService`.
3. **Password Security**: Passwords must be hashed using ASP.NET Core `PasswordHasher<UserAccount>`.
4. **Vue 3 Composition API & TypeScript**: Frontend components must use `<script setup lang="ts">`, Vue Router 4, Pinia auth store, and Bootstrap 5 styling consistent with existing views.
5. **Clean Testing**: Backend integration tests must use `WebApplicationFactory<Program>` and assert both 200/201 success for administrators and 403 failure for non-administrators.

---

# 11. Acceptance Conditions

1. `UsersController` is operational with endpoints `GET /api/v1/users`, `GET /api/v1/users/{id}`, `POST /api/v1/users`, and `PUT /api/v1/users/{id}`.
2. Endpoints enforce `[Authorize(Roles = "Administrator,Admin")]` and reject unauthenticated requests (401) and non-admin requests (403).
3. The Administration section and User Management link in `App.vue` are visible only when the logged-in user possesses the `Administrator` or `Admin` role.
4. Route `/admin/users` redirects unauthorized non-admin users to `/feed`.
5. Administrators can view all user accounts with associated person names and statuses in `UserManagementView.vue` (`SCR-USR-001`).
6. Administrators can create a user account via `UserAccountModal.vue` (`SCR-USR-002`), picking an unlinked Person, specifying username, email, initial password, and status.
7. Administrators can edit a user account to update email, status (active, locked, suspended), or reset password.
8. Validation prevents duplicate usernames, duplicate emails, and linking a person already tied to an account.
9. All automated backend unit and integration tests, as well as frontend type-checks (`npm run type-check`), pass with 0 errors.
