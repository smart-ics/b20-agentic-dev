---
Title: Implementation Plan for User Management UI - Add and Update User Accounts Restricted to Administrators (CR-007)
Code: CR-007
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-03
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-007`: User Management UI - Add and Update User Accounts Restricted to Administrators. Provide a dedicated user interface to view, create, and update user accounts, with navigation, routing, and REST API access strictly restricted to users holding the `Administrator` or `Admin` role.

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- FEATURE: [FEAT-USR-001-manage-user-accounts.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-001-manage-user-accounts.md)
- ARCHITECTURE: [CR-007-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-007-ARCHITECTURE.md) (authoritative capability architecture), [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- FEASIBILITY-ASSESSMENT: [CR-007-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-007-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

Architecture Applicability: ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers all application services, REST API endpoints, frontend navigation, views, modals, automated testing, and documentation alignment required to realize `CR-007`:

1. **Backend Application Layer (`Cakra.Modules.Identity`)**:
   - `IUserAccountService` and `UserAccountService` coordinating user queries, creations, and updates.
   - `CreateUserAccountCommand` with validation (unique username, unique email, valid and unassociated Person, minimum 8-character password hashed via `IPasswordHasher<UserAccount>`).
   - `UpdateUserAccountCommand` (email, status transitions: `ACTIVE`/`LOCKED`/`SUSPENDED`, optional password reset).
   - In-memory enrichment of Person full names via `IOrganizationQueryService` preserving schema isolation.
   - Service registration in `IdentityModule.cs`.

2. **Backend API Layer (`Cakra.Api`)**:
   - `UsersController.cs` under route `/api/v1/users` guarded with `[Authorize(Roles = "Administrator,Admin")]`.
   - `GET /api/v1/users`, `GET /api/v1/users/{id}`, `POST /api/v1/users`, and `PUT /api/v1/users/{id}`.
   - RFC 7807 ProblemDetails error handling for conflicts (`409`), validation errors (`400`), unauthorized access (`403`), and unauthenticated access (`401`).

3. **Frontend Presentation Layer (`Cakra.Web`)**:
   - Typed Axios API client in `src/api/users.ts`.
   - Left sidebar navigation in `App.vue`: "Administration" section and "User Management" link displayed only when `isAdmin` (`Administrator` or `Admin`).
   - Vue Router configuration in `router/index.ts`: route `/admin/users` with `meta: { requiresAuth: true, requiresRole: 'Administrator', screenId: 'SCR-USR-001' }` and navigation guard redirecting non-admins to `/feed`.
   - `UserManagementView.vue` (`SCR-USR-001`): searchable user table with status badges and action buttons.
   - `UserAccountModal.vue` (`SCR-USR-002`): dialog for creating and editing user accounts with active Person dropdown from `/api/v1/organization/persons/active`.

4. **Testing & Documentation Alignment**:
   - Unit tests in `Cakra.Tests.Unit/Identity/UserAccountServiceTests.cs`.
   - Integration tests in `Cakra.Tests.Integration/Identity/UsersControllerTests.cs`.
   - Screen inventory registration in `screen-inventory.md` and navigation map update in `navigation-map.md`.

---

# 3. Dependencies

- .NET 8 SDK / C# 12
- Node.js & npm (Vue 3 / Vite)
- Microsoft SQL Server LocalDB / Respawn for integration tests
- `[identity].[UserAccounts]` table provisioned by `0002_identity_tables.sql`

For slice dependencies:
- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires the referenced slice to have implementation status `IMPLEMENTED`.
- Dependency satisfaction does not require review status `GO`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Backend Identity Application Service & REST API | IMPLEMENTED | GO | 2/2 |
| P2 - Frontend Shell Navigation & User Management Views | IMPLEMENTED | GO | 2/2 |
| P3 - Automated Verification & Documentation Alignment | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Backend Identity Application Service & REST API

Implementation Status: IMPLEMENTED
Review Status: GO

### P1-S01

Title: Implement User Account Application Service, Commands, Validators, and DTOs in Cakra.Modules.Identity

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Create application service contracts, commands, command validators, query DTOs, and service implementation in `Cakra.Modules.Identity.Services` to support administrative user account operations. Coordinate password hashing with `IPasswordHasher<UserAccount>`, uniqueness checks with `IUserAccountRepository`, and Person name enrichment with `IOrganizationQueryService`. Register `IUserAccountService` in `IdentityModule.cs`.

Depends On: None

Repository: `cakra`

Completion Criteria:
- `UserAccountDto` and `UserAccountSummaryDto` defined with properties: `UserId`, `PersonId`, `PersonName`, `Username`, `Email`, `Status`, `FailedLoginAttempts`, `LastLoginAt`, `CreatedAt`, `UpdatedAt`.
- `CreateUserAccountCommand` and `CreateUserAccountCommandValidator` validate required username (3-50 chars), email (valid format, max 150 chars), valid PersonId, and password (min 8 chars).
- `UpdateUserAccountCommand` and `UpdateUserAccountCommandValidator` validate optional email, allowed statuses (`ACTIVE`, `LOCKED`, `SUSPENDED`), and optional new password (min 8 chars if provided).
- `IUserAccountService` and `UserAccountService` implement methods:
  - `Task<IReadOnlyList<UserAccountSummaryDto>> GetAllUsersAsync(CancellationToken cancellationToken = default)`
  - `Task<UserAccountDto?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default)`
  - `Task<UserAccountDto> CreateUserAsync(CreateUserAccountCommand command, CancellationToken cancellationToken = default)`
  - `Task<UserAccountDto> UpdateUserAsync(Guid id, UpdateUserAccountCommand command, CancellationToken cancellationToken = default)`
- `CreateUserAsync` enforces username uniqueness, email uniqueness, verifies `PersonId` is not already associated with another user account, hashes password, and persists via `IUserAccountRepository.AddAsync`.
- `UpdateUserAsync` verifies email uniqueness (excluding current user), validates status transition, updates `PasswordHash` if a new password is provided, resets failed attempts when unlocking, and persists via `IUserAccountRepository.UpdateAsync`.
- `IdentityModule.cs` registers `IUserAccountService, UserAccountService` as scoped service.
- Backend project `Cakra.Modules.Identity` compiles with 0 errors.

Implementation Notes:
- Created `UserAccountDto.cs` and `UserAccountSummaryDto.cs` in `Cakra.Modules.Identity.Services` defining `UserId`, `PersonId`, `PersonName`, `Username`, `Email`, `Status`, `FailedLoginAttempts`, `LastLoginAt`, `CreatedAt`, and `UpdatedAt`.
- Created `UserAccountCommands.cs` defining `CreateUserAccountCommand`, `CreateUserAccountCommandValidator`, `UpdateUserAccountCommand`, and `UpdateUserAccountCommandValidator` with FluentValidation rules enforcing length, format, allowed statuses (ACTIVE, LOCKED, SUSPENDED), and password constraints.
- Created `IUserAccountService.cs` interface declaring `GetAllUsersAsync`, `GetUserByIdAsync`, `CreateUserAsync`, and `UpdateUserAsync`.
- Implemented `UserAccountService.cs` coordinating `IPasswordHasher<UserAccount>`, `IUserAccountRepository`, and in-memory `IOrganizationQueryService` person display name enrichment without cross-schema SQL joins.
- Registered `IUserAccountService, UserAccountService` as scoped service in `IdentityModule.cs`.
- Verified `Cakra.Modules.Identity.csproj` and `Cakra.Api.csproj` compile with 0 errors and all unit tests pass.

Notes:
- Must not perform SQL queries joining `identity` and `organization` schemas. Person enrichment is resolved in memory via `_organizationQueryService.ListActivePersonsAsync()`.

---

### P1-S02

Title: Implement UsersController with Role-Based Authorization in Cakra.Api

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Create `UsersController.cs` in `Cakra.Api.Controllers` providing REST endpoints under `/api/v1/users`, secured with `[Authorize(Roles = "Administrator,Admin")]`. Map incoming requests to `IUserAccountService` and format standardized RFC 7807 ProblemDetails responses for errors and conflicts.

Depends On: P1-S01

Repository: `cakra`

Completion Criteria:
- `UsersController.cs` created under `[Route("api/v1/users")]` and decorated with `[Authorize(Roles = "Administrator,Admin")]`.
- Endpoint `GET /api/v1/users` returns HTTP `200 OK` with `IReadOnlyList<UserAccountSummaryDto>`.
- Endpoint `GET /api/v1/users/{id:guid}` returns HTTP `200 OK` with `UserAccountDto` or `404 Not Found`.
- Endpoint `POST /api/v1/users` accepts `CreateUserAccountRequest`, calls `CreateUserAsync`, and returns `201 CreatedAtAction` with `UserAccountDto`.
- Endpoint `PUT /api/v1/users/{id:guid}` accepts `UpdateUserAccountRequest`, calls `UpdateUserAsync`, and returns `200 OK` with updated `UserAccountDto`.
- Exceptions (e.g. duplicate username, duplicate email, person already associated) are caught and returned as RFC 7807 `409 Conflict` or `400 Bad Request` ProblemDetails.
- Project `Cakra.Api` builds cleanly with 0 errors.

Implementation Notes:
- Created `UsersController.cs` in `Cakra.Api.Controllers` inheriting from `ApiControllerBase` and decorated with `[Authorize(Roles = "Administrator,Admin")]` and `[Route("api/v1/users")]`.
- Injected `IUserAccountService` and `ILogger<UsersController>`.
- Implemented `GET /api/v1/users` returning HTTP 200 OK with `IReadOnlyList<UserAccountSummaryDto>`.
- Implemented `GET /api/v1/users/{id:guid}` returning HTTP 200 OK with `UserAccountDto` or 404 ProblemDetails if not found.
- Implemented `POST /api/v1/users` accepting `CreateUserAccountRequest`, delegating to `_userAccountService.CreateUserAsync`, and returning HTTP 201 CreatedAtAction.
- Implemented `PUT /api/v1/users/{id:guid}` accepting `UpdateUserAccountRequest`, delegating to `_userAccountService.UpdateUserAsync`, and returning HTTP 200 OK with updated `UserAccountDto`.
- Implemented standardized RFC 7807 `ProblemDetails` handling for `ValidationException` (400 Bad Request), `ArgumentException` (400 Bad Request), `KeyNotFoundException` (404 Not Found), and `InvalidOperationException` (409 Conflict).
- Verified `Cakra.Api.csproj` builds cleanly with 0 warnings and 0 errors, and backend unit test suite passes cleanly.

Notes:
- Inherits from `ApiControllerBase`.

---

## P2 - Frontend Shell Navigation & User Management Views

Implementation Status: IMPLEMENTED
Review Status: GO

### P2-S03

Title: Implement Frontend API Client, Role Guarded Route, and Administration Sidebar Navigation in Cakra.Web

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Create the typed frontend API client `src/api/users.ts`, add the "Administration" sidebar section in `App.vue` with visibility restricted to `Administrator` and `Admin`, and configure route `/admin/users` in `router/index.ts` with navigation guard enforcement redirecting unauthorized users to `/feed`.

Depends On: P1-S02

Repository: `cakra`

Completion Criteria:
- `src/api/users.ts` created with TypeScript interfaces (`UserAccountSummary`, `UserAccountDetail`, `CreateUserRequest`, `UpdateUserRequest`) and helper functions (`listUsers`, `getUserById`, `createUser`, `updateUser`).
- `App.vue` sidebar updated:
  - Adds computed property `isAdmin = computed(() => authStore.roles.includes('Administrator') || authStore.roles.includes('Admin'))`.
  - Renders `<div class="nav-section-title" v-if="isAdmin && !isCollapsed">Administration</div>`.
  - Renders `<router-link to="/admin/users" v-if="isAdmin" data-testid="nav-user-management-link" class="sidebar-nav-item">` with icon `bi bi-person-gear` and label "User Management".
  - Updates `currentScreenTitle` to return "User Management" when route path starts with `/admin/users`.
- `router/index.ts` updated:
  - Registers route `/admin/users` pointing to lazy/imported `UserManagementView.vue` with `meta: { requiresAuth: true, requiresRole: 'Administrator', screenId: 'SCR-USR-001' }`.
  - In `router.beforeEach`, checks `to.meta.requiresRole`; if set and user does not have `Administrator` or `Admin`, redirects to `{ path: '/feed' }`.
- Frontend passes type-check (`npm run type-check`).

Implementation Notes:
- Created `src/api/users.ts` with TypeScript interfaces `UserAccountSummary`, `UserAccountDetail`, `CreateUserRequest`, `UpdateUserRequest`, and helper functions `listUsers`, `getUserById`, `createUser`, `updateUser` calling `/api/v1/users` endpoints.
- Updated `src/App.vue`: added `isAdmin` computed property based on `Administrator` or `Admin` role check in Pinia `authStore`; added conditional "Administration" nav section and router-link to `/admin/users` (`data-testid="nav-user-management-link"`) with icon `bi bi-person-gear`; mapped route path `/admin/users` to topbar title `'User Management'`.
- Updated `src/router/index.ts`: added `/admin/users` route configuration with lazy-loaded `UserManagementView.vue` and `meta: { requiresAuth: true, requiresRole: 'Administrator', screenId: 'SCR-USR-001' }`; added role check in `router.beforeEach` redirecting non-admin users to `/feed`.
- Created clean minimal stub `src/views/UserManagementView.vue` so the route bundle compiles cleanly.
- Verified frontend compilation via `npm run type-check` (0 errors) and `npm run build` (successful Vite build with code 0).

Notes:
- Ensures unauthorized users cannot view the menu item or access the route by URL.

---

### P2-S04

Title: Implement User Management View (SCR-USR-001) and User Account Modal (SCR-USR-002)

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Create `UserManagementView.vue` (`SCR-USR-001`) and `UserAccountModal.vue` (`SCR-USR-002`) in `Cakra.Web` providing complete UI workflows to view all user accounts, search and filter by status, register new user accounts with active Person selection, and update existing user accounts.

Depends On: P2-S03

Repository: `cakra`

Completion Criteria:
- `UserManagementView.vue` (`SCR-USR-001`) created in `src/views`:
  - Displays header with screen title, user account counts, and "Add User" primary button (`data-testid="add-user-btn"`).
  - Search input filtering table by username, person name, or email.
  - Status filter dropdown (`All`, `ACTIVE`, `LOCKED`, `SUSPENDED`).
  - Responsive table displaying: Username, Person Name, Email, Status badge (`ACTIVE` green, `LOCKED` red, `SUSPENDED` yellow), Failed Login Attempts, Last Login At, and "Edit" action button (`data-testid="edit-user-btn"`).
  - Loading skeleton / spinner and empty state handling.
- `UserAccountModal.vue` (`SCR-USR-002`) created in `src/components`:
  - Modal dialog with props `show`, `mode` (`'create' | 'edit'`), and `user`.
  - In Create mode: fetches active persons from `GET /api/v1/organization/persons/active` to populate Person dropdown. Provides fields for Person, Username, Email, Password, and Status.
  - In Edit mode: displays Username (read-only) and Person (read-only), allows editing Email, Status, and optional New Password.
  - Client-side validation: verifies non-empty fields, valid email format, minimum 8 characters for password.
  - Displays server validation / error messages (e.g. 409 Conflict duplicate error) inline without closing modal.
  - Emits `@saved` on successful submission, triggering parent view to reload the user table.
- Frontend builds cleanly (`npm run build`) and passes type-check (`npm run type-check`).

Implementation Notes:
- Created `src/components/UserAccountModal.vue` (`SCR-USR-002`):
  - Added props `show`, `mode` (`create` | `edit`), and `user`.
  - Implemented active person selector in 'create' mode fetching from `/organization/persons/active`.
  - Implemented form fields for Person, Username, Email, Password, Status (`ACTIVE`, `SUSPENDED`) in create mode, and read-only Person/Username, editable Email, Status (`ACTIVE`, `LOCKED`, `SUSPENDED`), and optional New Password in edit mode.
  - Implemented client-side validation for required fields, email format, and password length (min 8 chars).
  - Implemented inline server conflict / validation error extraction handling HTTP 400 and 409 ProblemDetails.
  - Wired `@saved` and `@close` events, calling `createUser` or `updateUser` from `@/api/users`.
- Implemented `src/views/UserManagementView.vue` (`SCR-USR-001`):
  - Screen header with title, screen ID badge `SCR-USR-001`, "Refresh" button (`data-testid="refresh-users-btn"`), and "Add User" button (`data-testid="add-user-btn"`).
  - High-density operational metric ribbon showing Total Accounts, Active, Locked, and Suspended counters.
  - Operational toolbar with search input (`data-testid="user-search-input"`) filtering across username, person name, and email, plus status filter dropdown (`data-testid="status-filter-select"`).
  - High-density table with Username, Person Name, Email, status badge (`ACTIVE` green, `LOCKED` red, `SUSPENDED` yellow), failed logins badge, last login timestamp, and row "Edit" action button (`data-testid="edit-user-btn"`).
  - Integrated `UserAccountModal` with reload trigger on `@saved`, and included loading spinner and empty filter states.
- Verified frontend compilation with `npm run type-check` (0 errors) and `npm run build` (successful production build).

Notes:
- Styled using Bootstrap 5 utility classes consistent with `CustomerPortfolioView.vue` and `CreateRequestModal.vue`.

---

## P3 - Automated Verification & Documentation Alignment

Implementation Status: IMPLEMENTED
Review Status: GO

### P3-S05

Title: Implement Automated Unit/Integration Tests and Documentation Alignment

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Add unit tests for `UserAccountService` and integration tests for `UsersController` verifying role authorization, CRUD operations, duplicate handling, and route gating. Update screen inventory and navigation maps.

Depends On: P1-S02, P2-S04

Repository: `cakra`

Completion Criteria:
- Unit tests created in `tests/backend/Cakra.Tests.Unit/Identity/UserAccountServiceTests.cs`:
  - Validates user creation with password hashing and duplicate prevention.
  - Validates user update and status change.
  - Validates 1-to-1 person uniqueness check.
- Integration tests created in `tests/backend/Cakra.Tests.Integration/Identity/UsersControllerTests.cs`:
  - Verifies `GET /api/v1/users`, `POST /api/v1/users`, and `PUT /api/v1/users/{id}` return 200/201 for authenticated users with role `Administrator` or `Admin`.
  - Verifies requests return HTTP `401 Unauthorized` for unauthenticated requests.
  - Verifies requests return HTTP `403 Forbidden` for authenticated users without `Administrator`/`Admin` role (e.g. `Programmer`, `Implementator`).
  - Verifies conflict error handling (409 Conflict) on duplicate username/email.
- `screen-inventory.md` updated with `SCR-USR-001` and `SCR-USR-002`.
- `navigation-map.md` updated with Administration navigation area.
- Entire backend test suite passes (`dotnet test cakra/Cakra.sln`) with 0 failures.
- Frontend type verification (`npm run type-check`) and production build (`npm run build`) succeed with 0 errors.

Implementation Notes:
- Created `tests/backend/Cakra.Tests.Unit/Identity/UserAccountServiceTests.cs`:
  - Validated user creation with password hashing via `IPasswordHasher<UserAccount>`.
  - Validated duplicate prevention for usernames (case-insensitive), emails (case-insensitive), and 1-to-1 PersonId associations.
  - Validated command input validation handling via FluentValidation (`ValidationException`).
  - Validated user updates, email uniqueness check excluding self, status transitions (`ACTIVE`, `LOCKED`, `SUSPENDED`), and unlock logic resetting failed login attempts.
  - Validated in-memory Person name resolution via `IOrganizationQueryService` with fallback to `"Unknown / Archived"`.
- Created `tests/backend/Cakra.Tests.Integration/Identity/UsersControllerTests.cs`:
  - Verified `[Authorize(Roles = "Administrator,Admin")]` RBAC enforcement across `GET /api/v1/users`, `GET /api/v1/users/{id}`, `POST /api/v1/users`, and `PUT /api/v1/users/{id}` returning HTTP 200/201 for users with `Administrator` or `Admin` roles.
  - Verified unauthenticated calls to all endpoints return HTTP 401 Unauthorized ProblemDetails (`errorCode: "UNAUTHORIZED"`).
  - Verified authenticated requests with non-admin roles (`Programmer`, `Implementator`, `Management`, `Customer`) return HTTP 403 Forbidden ProblemDetails (`errorCode: "FORBIDDEN"`).
  - Verified duplicate conflicts (username, email, PersonId association) return HTTP 409 Conflict ProblemDetails (`errorCode: "CONFLICT"`).
  - Verified payload validation failures return HTTP 400 Bad Request ProblemDetails (`errorCode: "VALIDATION_FAILED"`).
  - Verified nonexistent user query returns HTTP 404 Not Found ProblemDetails (`errorCode: "RESOURCE_NOT_FOUND"`).
- Updated `cakra/docs/navigation/screen-inventory.md`:
  - Added `SCR-USR-001` (User Management View) and `SCR-USR-002` (Add / Edit User Modal) to the Screen Inventory Summary table and Screen Definitions section under Administration navigation area.
- Updated `cakra/docs/navigation/navigation-map.md`:
  - Added Administration Area to Navigation Hierarchy Tree, Navigation Areas and Destinations (Area 3: Administration), Mermaid flowchart with navigation movement paths, movement descriptions, and the End-to-End Traceability Matrix.
- Validated test suites and build outputs:
  - `dotnet test cakra/Cakra.sln` passed 100% (334 Unit Tests, 143 Integration Tests, 0 failures).
  - `npm run type-check` in `cakra/src/frontend/Cakra.Web` passed with 0 errors.
  - `npm run build` in `cakra/src/frontend/Cakra.Web` succeeded with production bundle generation.

Notes:
- Full end-to-end regression validation across all modules.

---

# 6. Change Log

- 2026-10-03: Initial implementation plan created and approved for `CR-007`. Slices P1-S01 through P3-S05 structured with continuous IDs, single-repository boundaries, and explicit dependencies.
