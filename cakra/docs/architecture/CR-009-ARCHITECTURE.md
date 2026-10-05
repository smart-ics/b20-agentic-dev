---
Title: Sign-Up Feature - User Account Registration Architecture
Code: CR-009
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-05
---

# 1. Overview

This architecture defines the technical realization for Change Request `CR-009`: Sign-Up Feature on Login Page with Pending Admin Approval — enabling prospective users to submit a self-service registration request directly from the Login screen (`LoginView.vue` / `SCR-AUTH-001`).

It realizes [FEAT-USR-002-register-user-account.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-002-register-user-account.md) and technical gap closures from [CR-009-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-009-FEASIBILITY-ASSESSMENT.md), establishing:
1. A new public REST API endpoint `POST /api/v1/auth/register` in `AuthController.cs`.
2. A new `RegisterUserAccountCommand` (MediatR) and handler in `Cakra.Modules.Identity` to validate registration credentials, hash passwords, and persist new user accounts with `Status = Pending`.
3. Support for `UserAccountStatus.Pending` (`"Pending"`) in `UserAccountStatus.cs` and authentication rejection handling in `AuthenticationService.cs` (`ACCOUNT_PENDING_APPROVAL`).
4. Dual-mode presentation in `LoginView.vue` (`SCR-AUTH-001`) with a dynamic toggle link switching between Sign In and Sign Up views, client-side validation, and post-registration alert feedback.
5. Store action `register(username, email, password)` and state management in `auth.ts` Pinia store.

---

# 2. Architectural Basis

## Business Context

- DOMAIN: Identity & Access (`Cakra.Modules.Identity`, Architecture §14)
- FEATURE: [FEAT-USR-002-register-user-account.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-002-register-user-account.md)
- ISSUE: [CR-009-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-009-ISSUE.md)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md) (Section 14: Identity & Authentication Architecture, Section 19: API & Frontend Boundaries)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-009-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-009-FEASIBILITY-ASSESSMENT.md)

This architecture consumes and realizes the approved feasibility decisions:
- `GAP-001`: Add `POST /api/v1/auth/register` public endpoint to `AuthController.cs`.
- `GAP-002`: Create `RegisterUserAccountCommand`, `RegisterUserAccountCommandValidator`, and `RegisterUserAccountCommandHandler` in `Cakra.Modules.Identity`.
- `GAP-003`: Add `UserAccountStatus.Pending` and update `AuthenticationService.cs` to return `ACCOUNT_PENDING_APPROVAL` when authenticating pending accounts.
- `GAP-004`: Update `LoginView.vue` (`SCR-AUTH-001`) with inline toggle link, registration inputs (Username, Email, Password, Confirm Password), client-side validation, and post-registration success alert.
- `GAP-005`: Update `auth.ts` Pinia store with `register(...)` action and registration state handling.
- `OQ-001`: Define status string `public const string Pending = "Pending";` in `UserAccountStatus.cs`.
- `OQ-002`: Initialize `PersonId = Guid.Empty` on initial self-registration until an Administrator links a `Person` during user account review.
- `OQ-003`: Return HTTP 400 Bad Request RFC 7807 problem details with error code `ACCOUNT_PENDING_APPROVAL` on login attempt with a pending account.

---

# 3. Scope

## Included

1. **Backend Domain & Application Layer (`Cakra.Modules.Identity`)**:
   - Add `Pending = "Pending"` status constant to [UserAccountStatus.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/UserAccountStatus.cs).
   - Create `RegisterUserAccountCommand`, `RegisterUserAccountCommandValidator`, and `RegisterUserAccountCommandHandler` in `Cakra.Modules.Identity/Commands`.
   - Update `AuthenticationService.cs` to check for `UserAccountStatus.Pending` status during authentication and return `ACCOUNT_PENDING_APPROVAL` error details when detected.
2. **Backend REST API Layer (`Cakra.Api`)**:
   - Add public endpoint `POST /api/v1/auth/register` to `AuthController.cs`.
   - Create request DTO `RegisterRequest` (Username, Email, Password).
   - Map validation conflicts (duplicate username/email) to RFC 7807 `409 Conflict` or `400 Bad Request` ProblemDetails.
3. **Frontend Presentation Layer (`Cakra.Web`)**:
   - Update `src/stores/auth.ts` Pinia store with `register(username, email, password)` method and state tracking (`registrationSuccess`, `registrationMessage`).
   - Enhance `src/views/LoginView.vue` (`SCR-AUTH-001`) with reactive `mode` toggle (`'signin' | 'signup'`), form inputs for Username, Email, Password, Confirm Password, client-side validation, and post-registration success banner (*"Registration submitted! Pending admin approval."*).
4. **Automated Testing**:
   - Unit tests for `RegisterUserAccountCommandHandler` and `AuthenticationService` pending account rejection in `Cakra.Tests.Unit/Identity/`.
   - Integration tests for `POST /api/v1/auth/register` and pending login rejection in `Cakra.Tests.Integration/Api/AuthControllerTests.cs`.
5. **Documentation**:
   - Update `feature-catalog.md` and `feature-traceability.md` with `FEAT-USR-002`.

## Excluded

- Captcha integration or email verification links (security is governed by Pending Admin Approval).
- Modifications to database schema (table `[identity].[UserAccounts]` already supports `Status` varchar and `PersonId` uniqueidentifier).
- Changes to existing active user login or token generation mechanisms.

---

# 4. Technical Decisions

## TD-001: MediatR Command for Self-Registration

Self-registration requests will be dispatched via `RegisterUserAccountCommand` through MediatR in `AuthController.cs`:

```csharp
public record RegisterUserAccountCommand(
    string Username,
    string Email,
    string Password
) : IRequest<UserAccountDto>;
```

### Handler Logic
1. `RegisterUserAccountCommandValidator` validates:
   - `Username`: Not empty, max 50 characters, alphanumeric/standard.
   - `Email`: Not empty, valid email format.
   - `Password`: Not empty, minimum 8 characters.
2. `RegisterUserAccountCommandHandler`:
   - Checks if Username is already in use via `IUserAccountRepository.GetByUsernameAsync`. Throws `InvalidOperationException("Username is already taken.")` if duplicate.
   - Checks if Email is already in use via `IUserAccountRepository.GetByEmailAsync`. Throws `InvalidOperationException("Email is already registered.")` if duplicate.
   - Hashes the password using `IPasswordHasher.HashPassword`.
   - Instantiates `UserAccount` with `Status = UserAccountStatus.Pending` and `PersonId = Guid.Empty`.
   - Saves record via `IUserAccountRepository.AddAsync`.
   - Returns mapped `UserAccountDto`.

## TD-002: Pending Approval Account Lifecycle & Status

Newly self-registered accounts are assigned `UserAccountStatus.Pending`:

```csharp
public static class UserAccountStatus
{
    public const string Active = "Active";
    public const string Locked = "Locked";
    public const string Suspended = "Suspended";
    public const string Pending = "Pending";
}
```

### Authentication Gate Behavior
In `AuthenticationService.cs`, when a user attempts to log in via `AuthenticateAsync`:
```csharp
if (string.Equals(account.Status, UserAccountStatus.Pending, StringComparison.OrdinalIgnoreCase))
{
    return LoginResult.Failed("ACCOUNT_PENDING_APPROVAL", "Your account is pending administrative approval.");
}
```

The controller maps `ACCOUNT_PENDING_APPROVAL` to HTTP `400 Bad Request` with problem details:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Account Pending Approval",
  "status": 400,
  "detail": "Your account is pending administrative approval.",
  "code": "ACCOUNT_PENDING_APPROVAL"
}
```

## TD-003: Frontend Dual-Mode Card Component Architecture

`LoginView.vue` (`SCR-AUTH-001`) is enhanced with a reactive `mode` ref (`'signin' | 'signup'`):

- When `mode === 'signin'`:
  - Renders Username/Email, Password, Sign In submit button.
  - Displays toggle link: *"Don't have an account? Sign Up"*.
- When `mode === 'signup'`:
  - Renders Username, Email, Password, Confirm Password, Sign Up submit button.
  - Displays toggle link: *"Already have an account? Sign In"*.
  - Client-side validation: Password length >= 8, Password === Confirm Password.
  - On submission: calls `authStore.register(username, email, password)`.
  - On success: sets `mode = 'signin'`, resets form fields, and displays green success banner: *"Registration submitted! Pending admin approval."*.

## TD-004: Unassociated Person Handling (`PersonId = Guid.Empty`)

Self-registered users do not have an associated `Person` record at registration time. `RegisterUserAccountCommandHandler` sets `PersonId = Guid.Empty`.

When an Administrator reviews user accounts in `UserManagementView.vue` (`SCR-USR-001`), the Admin selects an authoritative `Person` record from the dropdown and updates `Status` from `Pending` to `Active`.

---

# 5. Component Responsibilities

| Component | Layer | Responsibility |
|---|---|---|
| `AuthController.cs` | API Layer | Exposes `POST /api/v1/auth/register`, dispatches `RegisterUserAccountCommand`, maps validation/conflict exceptions to RFC 7807 ProblemDetails. |
| `RegisterUserAccountCommand` | Application | MediatR command carrying Username, Email, Password. |
| `RegisterUserAccountCommandValidator` | Application | Validates non-empty fields, valid email format, password min length 8. |
| `RegisterUserAccountCommandHandler` | Domain/App | Checks username/email uniqueness, hashes password, sets `Status = Pending` and `PersonId = Guid.Empty`, saves account. |
| `UserAccountStatus.cs` | Domain | Defines account status constants (`Active`, `Locked`, `Suspended`, `Pending`). |
| `AuthenticationService.cs` | Identity Service | Rejects authentication attempts for accounts with `Status == Pending` returning `ACCOUNT_PENDING_APPROVAL`. |
| `auth.ts` | Frontend Store | Provides `register(username, email, password)` action and registration state handling. |
| `LoginView.vue` (`SCR-AUTH-001`) | Frontend View | Renders dual-mode card (Sign In / Sign Up), client-side validation, error/success notifications. |

---

# 6. Integration Design

```text
[ LoginView.vue (SCR-AUTH-001) ]
       │
       │ authStore.register(username, email, password)
       ▼
[ auth.ts (Pinia Store) ]
       │
       │ POST /api/v1/auth/register { username, email, password }
       ▼
[ AuthController.cs ]
       │
       │ IMediator.Send(new RegisterUserAccountCommand(...))
       ▼
[ RegisterUserAccountCommandHandler ]
       │
       ├─► IUserAccountRepository.GetByUsernameAsync / GetByEmailAsync (Uniqueness Check)
       ├─► IPasswordHasher.HashPassword(password)
       └─► IUserAccountRepository.AddAsync(userAccount [Status=Pending, PersonId=Guid.Empty])
```

---

# 7. Data Ownership

| Data | Owner Component | Schema / Persistence |
|---|---|---|
| `UserAccount` credentials & status | Identity Module (`Cakra.Modules.Identity`) | `[identity].[UserAccounts]` table |

---

# 8. Database Design

## Modified Tables

| Table | Change Description |
|---|---|
| `[identity].[UserAccounts]` | No physical schema change required. `Status` column (varchar) now accepts value `'Pending'`. `PersonId` column (uniqueidentifier) accepts `00000000-0000-0000-0000-000000000000` (`Guid.Empty`). |

---

# 9. Cross-Cutting Concerns

- **Security**: Passwords are securely hashed with PBKDF2/HMAC-SHA512. Registered pending accounts cannot generate JWT/session tokens.
- **Error Handling**: Follows RFC 7807 ProblemDetails for all error responses (duplicate username/email -> 409 Conflict, validation errors -> 400 Bad Request, pending account login -> 400 Bad Request with `code: ACCOUNT_PENDING_APPROVAL`).
- **Validation**: Dual-layer validation (Vue client-side check + FluentValidation server-side check).

---

# 10. Implementation Constraints

1. Public registration endpoint must be unauthenticated (`[AllowAnonymous]`).
2. Self-registered user accounts MUST be created in `Pending` status and cannot authenticate until an Admin activates them.
3. Password strength rule: minimum 8 characters required.
4. No breaking changes to existing active user login or token issuance.

---

# 11. Acceptance Conditions

- [ ] `POST /api/v1/auth/register` creates a new user account with `Status = "Pending"` and `PersonId = Guid.Empty`.
- [ ] Submitting duplicate username or email returns 409 Conflict ProblemDetails.
- [ ] Submitting password under 8 characters returns 400 Bad Request ProblemDetails.
- [ ] Attempting to log in with a `Pending` account via `POST /api/v1/auth/login` returns 400 Bad Request with `code: "ACCOUNT_PENDING_APPROVAL"`.
- [ ] `LoginView.vue` (`SCR-AUTH-001`) toggle link smoothly switches between Sign In and Sign Up modes.
- [ ] Successful registration displays green success alert (*"Registration submitted! Pending admin approval."*), resets fields, and switches to Sign In mode.
- [ ] Unit tests for `RegisterUserAccountCommandHandler` and `AuthenticationService` pass.
- [ ] Integration tests for `POST /api/v1/auth/register` pass.
