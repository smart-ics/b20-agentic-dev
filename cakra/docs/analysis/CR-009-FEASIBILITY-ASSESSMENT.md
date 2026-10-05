---
Title: Feasibility Assessment for Sign-Up Feature on Login Page with Pending Admin Approval
Code: CR-009
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-05
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Feature being assessed: Register User Account (`FEAT-USR-002-register-user-account.md`), per request in [CR-009-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-009-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-009-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-009-ISSUE.md)
- FEATURE: [FEAT-USR-002-register-user-account.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-002-register-user-account.md)
- FEATURE (Admin): [FEAT-USR-001-manage-user-accounts.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-001-manage-user-accounts.md)
- DOMAIN: [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md)
- ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

## Objective

Assess the feasibility, existing technical baseline, gaps, architectural impacts, and planning readiness to:

1. Add a public self-service user registration REST API endpoint `POST /api/v1/auth/register` in `AuthController.cs`.
2. Introduce a public registration command `RegisterUserAccountCommand` in `Cakra.Modules.Identity` that validates input, hashes passwords, and creates accounts in `Pending` status.
3. Update `UserAccountStatus.cs` and authentication logic to support `Pending` account status and return appropriate error codes when pending users attempt to log in.
4. Enhance `LoginView.vue` (`SCR-AUTH-001`) with a toggle link to seamlessly switch between Sign In and Sign Up modes, minimal inputs (Username, Email, Password, Confirm Password), client-side validation, and post-registration success feedback.
5. Update `auth.ts` Pinia store with `register(...)` action and registration error/success state handling.

---

# 2. Current State

Summarize relevant findings from current artifacts and codebase.

## Existing Behavior

1. **Frontend Login Screen (`SCR-AUTH-001` / `LoginView.vue`)**:
   - [LoginView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/LoginView.vue) contains a single card displaying username/email and password fields and a "Sign In" button.
   - Invokes `authStore.login(username, password)`.
   - Displays error alerts for invalid credentials or locked accounts.
   - Has no toggle link or form state for Sign Up / Registration.
2. **Frontend Auth Store (`auth.ts`)**:
   - [auth.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/stores/auth.ts) manages authentication state (`user`, `token`, `isLoading`, `error`, `errorCode`).
   - Exposes `login(username, password)` calling `POST /api/v1/auth/login`.
   - Lacks a `register(...)` action or registration success state handling.
3. **Backend REST API (`AuthController.cs`)**:
   - [AuthController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/AuthController.cs) at `/api/v1/auth` exposes endpoints: `POST /api/v1/auth/login` and `POST /api/v1/auth/logout`.
   - Does NOT expose a registration endpoint (`POST /api/v1/auth/register`).
4. **Backend Identity Module (`Cakra.Modules.Identity`)**:
   - Domain entity [UserAccount.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Domain/UserAccount.cs) defines identity properties and status (`Active`, `Locked`, `Suspended` per [UserAccountStatus.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/UserAccountStatus.cs)). `Pending` status is not currently defined.
   - [UserAccountService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Services/UserAccountService.cs) supports `CreateUserAccountAsync` via `CreateUserAccountCommand`, which requires an authoritative `PersonId` upfront and is invoked exclusively by Administrators via `UsersController.cs`.
   - No self-service registration command or service method exists for public registration without an upfront `PersonId`.

## Existing Constraints

1. **UserAccount - Person Link**: Architecture §14 specifies a 1-to-1 association between `UserAccount` and `Person`. For self-registered users, a `Person` record does not yet exist at registration time, requiring `PersonId` to be nullable or assigned `Guid.Empty` until an Administrator links the account during review.
2. **Authentication Gate**: Publicly registered accounts MUST NOT be allowed to log in until activated by an Administrator.
3. **Password Hashing**: Passwords must be hashed using PBKDF2 / HMAC-SHA512 per Architecture §19.5.

---

# 3. Gap Analysis

Identify gaps between the requested FEATURE (`FEAT-USR-002`) and the current system.

| ID | Severity | Gap |
|------|------|------|
| GAP-001 | CRITICAL | Public REST API endpoint `POST /api/v1/auth/register` does not exist in `AuthController.cs`. |
| GAP-002 | CRITICAL | No public self-registration command (`RegisterUserAccountCommand`) or service method exists in `Cakra.Modules.Identity`. |
| GAP-003 | CRITICAL | `UserAccountStatus` does not include `Pending` status, and authentication service does not handle pending-approval account login rejection. |
| GAP-004 | CRITICAL | Frontend `LoginView.vue` (`SCR-AUTH-001`) lacks a toggle link to switch between Sign In and Sign Up, registration form fields, and success feedback. |
| GAP-005 | MAJOR | `auth.ts` Pinia store lacks `register(...)` action and registration state handling. |
| GAP-006 | MINOR | `UserAccount` entity `PersonId` property requires supporting empty or optional association until Admin links a Person. |

---

# 4. Open Questions

Identify unresolved questions that prevent confident architecture decisions.

| ID | Question | Impact |
|------|------|------|
| OQ-001 | What exact status string constant should be added to `UserAccountStatus.cs` for pending accounts? | Determines domain status representation and database value. |
| OQ-002 | How should `PersonId` be handled for newly self-registered accounts before an Admin associates a `Person` record? | Determines entity schema and validation rules in `UserAccount`. |
| OQ-003 | What error code and message should be returned when a pending user attempts to log in via `POST /api/v1/auth/login`? | Determines RFC 7807 problem details response and frontend error alert text. |

---

# 5. Assumptions

Document assumptions made during the assessment.

| ID | Assumption |
|------|------|
| ASM-001 | Public registration requires no captcha or email verification code in this version; account security is enforced via Pending Admin Approval status. |
| ASM-002 | Existing PasswordHasher utility in `Cakra.Modules.Identity` will be reused for hashing registration passwords. |
| ASM-003 | Existing `UserManagementView.vue` (`SCR-USR-001`) and `UsersController.cs` will be used by Administrators to view pending user accounts, assign a `Person`, and change status to `Active`. |

---

# 6. Risks

Document identified risks.

| ID | Risk | Impact | Mitigation |
|------|------|------|------|
| RISK-001 | High volume of automated/spam account registrations. | Medium | Newly created accounts are held in `Pending` status and cannot access any system features or endpoints until Admin approval. |
| RISK-002 | Foreign key or database constraint issues if `PersonId` is empty or non-existent during registration. | Medium | Treat `PersonId` as optional (`Guid.Empty` or nullable) in registration handlers and update repository/entity configuration accordingly. |

---

# 7. Recommendations

## Option A: Integrated Card Toggle on LoginView with Public Register API (Recommended)

Add `POST /api/v1/auth/register` to `AuthController.cs`, create `RegisterUserAccountCommand` in `Cakra.Modules.Identity`, add `Pending` to `UserAccountStatus`, and implement dynamic view toggle in `LoginView.vue`.

### Advantages

- Meets all user requirements from the design interview.
- Clean integration with existing auth store and login component.
- Maintains strict security via Pending Admin Approval workflow.

### Disadvantages

- Requires minor adjustments to `UserAccount` domain entity to support pending status and optional initial `PersonId`.

---

# 8. Gap Closure

Record resolutions for gaps and open questions.

## GAP-001: REST API Register Endpoint

### Decision

Add `POST /api/v1/auth/register` endpoint to `AuthController.cs`. Accepts `RegisterRequest` (Username, Email, Password), dispatches `RegisterUserAccountCommand` via `IMediator`, and returns `201 Created` with success message.

### Rationale

Follows established API controller pattern in `Cakra.Api`.

### Impact

Modifies `AuthController.cs` and introduces `RegisterRequest` DTO.

### Architecture Impact

Extends `AuthController` public route hierarchy.

### Resolved By

ica-analyst

### Resolved Date

2026-10-05

---

## GAP-002 & GAP-003: Registration Command & Pending Account Status

### Decision

1. Add `Pending = "Pending"` to `UserAccountStatus.cs`.
2. Create `RegisterUserAccountCommand`, `RegisterUserAccountCommandValidator`, and `RegisterUserAccountCommandHandler` in `Cakra.Modules.Identity`.
3. Validation enforces: Username required & unique, Email required, valid format & unique, Password required & minimum 8 characters.
4. Handler creates `UserAccount` with `Status = UserAccountStatus.Pending` and `PersonId = Guid.Empty`.
5. Update `AuthenticationService.cs`: when authenticating a user whose status is `Pending`, fail authentication and return `LoginResult.Failed("ACCOUNT_PENDING_APPROVAL", "Your account is pending administrative approval.")`.

### Rationale

Ensures self-registered accounts are created safely in `Pending` status and cannot authenticate until an Admin updates their status to `Active`.

### Impact

Updates `UserAccountStatus.cs`, `AuthenticationService.cs`, and adds registration command files in `Cakra.Modules.Identity`.

### Architecture Impact

Extends identity domain commands and status machine.

### Resolved By

ica-analyst

### Resolved Date

2026-10-05

---

## GAP-004 & GAP-005: Frontend LoginView Toggle & Auth Store Action

### Decision

1. Add `register(username, email, password)` action to `auth.ts` Pinia store calling `POST /api/v1/auth/register`.
2. In `LoginView.vue`, add a reactive `mode` ref (`'signin' | 'signup'`).
3. Add a toggle link below the form: *"Don't have an account? Sign Up"* (in sign-in mode) and *"Already have an account? Sign In"* (in sign-up mode).
4. Render Confirm Password field and Email field when in `'signup'` mode.
5. Client-side validation checks password length >= 8 and password confirmation match before dispatching `authStore.register`.
6. Upon successful registration, display a green success alert (*"Registration submitted! Pending admin approval."*), reset form fields, and switch mode back to `'signin'`.

### Rationale

Delivers the exact UX agreed upon during the user design interview without requiring separate routes or modals.

### Impact

Updates `LoginView.vue` and `auth.ts` store.

### Architecture Impact

Enhances frontend login screen component `SCR-AUTH-001`.

### Resolved By

ica-analyst

### Resolved Date

2026-10-05

---

## OQ-001: Pending Status Representation

### Decision

Add constant `public const string Pending = "Pending";` to `UserAccountStatus.cs`.

### Rationale

Consistent with existing status values (`Active = "Active"`, `Locked = "Locked"`, `Suspended = "Suspended"`).

### Impact

Modifies `UserAccountStatus.cs`.

### Architecture Impact

None.

### Resolved By

ica-analyst

### Resolved Date

2026-10-05

---

## OQ-002: PersonId Handling for Unapproved Accounts

### Decision

Set `PersonId = Guid.Empty` upon self-registration. When an Administrator reviews pending accounts in `UserManagementView.vue` (`SCR-USR-001`), the Admin selects an authoritative `Person` record and changes status to `Active`.

### Rationale

Preserves 1-to-1 relationship domain rule while allowing initial unassociated registration state.

### Impact

No database schema change required (`Guid` type supports `Guid.Empty`).

### Architecture Impact

None.

### Resolved By

ica-analyst

### Resolved Date

2026-10-05

---

## OQ-003: Login Error for Pending Accounts

### Decision

When authenticating a pending account, `AuthenticationService.cs` returns `LoginResult` with `IsSuccess = false`, `ErrorCode = "ACCOUNT_PENDING_APPROVAL"`, and `ErrorMessage = "Your account is pending administrative approval."`. `AuthController` returns HTTP `400 Bad Request` RFC 7807 ProblemDetails.

### Rationale

Provides clear, actionable error feedback to the user on `LoginView.vue`.

### Impact

Updates `AuthenticationService.cs` and `LoginView.vue` error alert display logic.

### Architecture Impact

None.

### Resolved By

ica-analyst

### Resolved Date

2026-10-05

---

# 9. Architecture Applicability

## Decision

ARCHITECTURE-REQUIRED

## Rationale

Implementation of `CR-009` introduces a new public REST API endpoint (`POST /api/v1/auth/register`), a new `Pending` account status in `UserAccountStatus`, a new `RegisterUserAccountCommand` in `Cakra.Modules.Identity`, updates to authentication login failure rules for pending accounts, and new frontend registration actions and UI states. An architecture update (`CR-009-ARCHITECTURE.md`) is required before implementation planning.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved (GAP-001 through GAP-005 closed with decisions)
- [x] All required decisions recorded (OQ-001 through OQ-003 closed with decisions)
- [x] All blocking open questions resolved
- [x] Architecture can be finalized or updated based on the approved decisions

## Status

READY-FOR-PLANNING

## Notes

All feasibility gaps and open questions have been analyzed and resolved with recorded decisions. The target architecture document ([CR-009-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-009-ARCHITECTURE.md)) has been created and approved by the Architect. The READY-FOR-PLANNING gate is hereby granted.

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-009-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-009-ISSUE.md)
- FEATURE: [FEAT-USR-002-register-user-account.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-002-register-user-account.md)
- FEATURE (Admin): [FEAT-USR-001-manage-user-accounts.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-001-manage-user-accounts.md)
- DOMAIN: [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md)
- ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

Referenced codebase locations:

- [LoginView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/LoginView.vue)
- [auth.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/stores/auth.ts)
- [AuthController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/AuthController.cs)
- [UserAccount.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Domain/UserAccount.cs)
- [UserAccountStatus.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/UserAccountStatus.cs)
- [AuthenticationService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Services/AuthenticationService.cs)
- [UserAccountService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Services/UserAccountService.cs)
