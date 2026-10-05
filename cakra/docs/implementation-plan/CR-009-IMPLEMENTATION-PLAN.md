---
Title: Sign-Up Feature - User Account Registration Implementation Plan
Code: CR-009
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-05
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Sign-Up capability on the login page per [FEAT-USR-002-register-user-account.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-002-register-user-account.md) as realized by [CR-009-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-009-ARCHITECTURE.md).

**Planning Mode:** FEATURE-PLANNING

**Referenced artifacts:**
- FEATURE: [FEAT-USR-002-register-user-account.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-USR-002-register-user-account.md)
- ARCHITECTURE: [CR-009-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-009-ARCHITECTURE.md)
- FEASIBILITY-ASSESSMENT: [CR-009-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-009-FEASIBILITY-ASSESSMENT.md) (Planning Readiness: `READY-FOR-PLANNING`)

**Architecture Applicability:** ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers the implementation of public self-service user account registration with Pending Admin Approval status across backend API, identity module, frontend presentation, automated testing, and documentation.

Scope breakdown:
- Backend `Cakra.Modules.Identity`: `UserAccountStatus.Pending` constant, `RegisterUserAccountCommand`, validator, command handler, and `AuthenticationService` pending account login rejection.
- Backend `Cakra.Api`: `POST /api/v1/auth/register` endpoint in `AuthController.cs` and `RegisterRequest` DTO.
- Frontend `Cakra.Web`: `register` store action in `auth.ts`, dual-mode toggle UI on `LoginView.vue` (`SCR-AUTH-001`), client-side validation, and post-registration green success alert banner.
- Testing: Unit tests in `Cakra.Tests.Unit` and API integration tests in `Cakra.Tests.Integration`.
- Documentation: Feature catalog and feature traceability entries for `FEAT-USR-002`.

---

# 3. Dependencies

**External Dependencies:** None

**Slice Dependencies:** Listed in `Depends On` field for each slice below.

---

# 4. Progress Summary

Plan status values:
- NOT-STARTED
- IN-PROGRESS
- BLOCKED
- COMPLETED

Execution Approval values:
- PENDING
- APPROVED

Slice implementation status values:
- NOT-STARTED
- IN-PROGRESS
- IMPLEMENTED
- BLOCKED

Slice review status values:
- NOT-REVIEWED
- GO
- NO-GO

| Phase | Implementation Status | Review Status | Progress |
|---------|---------|---------|---------|
| P1 - Backend Domain & API | IMPLEMENTED | GO | 2/2 |
| P2 - Frontend Presentation & Store | IMPLEMENTED | GO | 2/2 |
| P3 - Automated Testing & Verification | IMPLEMENTED | GO | 1/1 |
| P4 - Documentation & Traceability | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Backend Domain & API

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P1-S01

**Title:** Backend Identity Pending Status & Register Command Handler

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Add `UserAccountStatus.Pending` status constant and create `RegisterUserAccountCommand`, validator, and handler in `Cakra.Modules.Identity`.

**Depends On:** None

**Repository:** Cakra.Modules.Identity

**Completion Criteria:**
- `UserAccountStatus.cs` contains `public const string Pending = "Pending";`.
- `RegisterUserAccountCommand.cs` defined with `Username`, `Email`, `Password`.
- `RegisterUserAccountCommandValidator.cs` enforces required fields, email format, and password length >= 8.
- `RegisterUserAccountCommandHandler.cs` verifies username/email uniqueness via repository, hashes password using `IPasswordHasher`, creates `UserAccount` with `Status = UserAccountStatus.Pending` and `PersonId = Guid.Empty`, and saves via `IUserAccountRepository.AddAsync`.

---

### P1-S02

**Title:** Backend Authentication Service Rejection Gate & Register REST API Endpoint

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Update `AuthenticationService.cs` to reject authentication for pending accounts and expose `POST /api/v1/auth/register` in `AuthController.cs`.

**Depends On:** P1-S01

**Repository:** Cakra.Api

**Completion Criteria:**
- `AuthenticationService.cs` checks if `account.Status == UserAccountStatus.Pending` and returns `LoginResult.Failed("ACCOUNT_PENDING_APPROVAL", "Your account is pending administrative approval.")`.
- `RegisterRequest.cs` DTO created in `Cakra.Api`.
- `AuthController.cs` has `POST /api/v1/auth/register` decorated with `[AllowAnonymous]`, dispatching `RegisterUserAccountCommand`.
- Controller maps duplicate username/email errors to HTTP 409 Conflict ProblemDetails and validation errors to 400 Bad Request ProblemDetails.

---

## P2 - Frontend Presentation & Store

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P2-S03

**Title:** Frontend Auth Pinia Store Action for Registration

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Add `register(username, email, password)` action and error/success state handling to `auth.ts` Pinia store.

**Depends On:** P1-S02

**Repository:** Cakra.Web

**Completion Criteria:**
- `auth.ts` Pinia store exports `register(username, email, password)` method.
- Invokes `POST /api/v1/auth/register`.
- Handles loading state, error state, and error message parsing from RFC 7807 responses.

---

### P2-S04

**Title:** Frontend LoginView Dual-Mode Sign-Up UI Component

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Update `LoginView.vue` (`SCR-AUTH-001`) with interactive mode toggle, registration form inputs, client-side validation, and post-registration alert banner.

**Depends On:** P2-S03

**Repository:** Cakra.Web

**Completion Criteria:**
- `LoginView.vue` manages reactive mode state (`'signin' | 'signup'`).
- Displays toggle link below form to switch seamlessly between Sign In and Sign Up views.
- In Sign Up mode, renders Username, Email, Password, and Confirm Password fields.
- Client-side validation checks password length >= 8 and password confirmation match before submitting.
- Successful registration switches mode back to Sign In, resets form inputs, and displays a green success alert banner: *"Registration submitted! Pending admin approval."*.

---

## P3 - Automated Testing & Verification

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P3-S05

**Title:** Unit & Integration Tests for Self-Registration and Pending Login Gate

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Write unit and API integration tests covering `RegisterUserAccountCommandHandler`, `AuthenticationService` pending account rejection, and `POST /api/v1/auth/register`.

**Depends On:** P1-S02

**Repository:** Cakra.Tests

**Completion Criteria:**
- Unit tests in `Cakra.Tests.Unit/Identity/RegisterUserAccountCommandHandlerTests.cs` verify valid registration, duplicate username rejection, and duplicate email rejection.
- Unit tests in `Cakra.Tests.Unit/Identity/AuthenticationServiceTests.cs` verify pending status login rejection with `ACCOUNT_PENDING_APPROVAL`.
- Integration tests in `Cakra.Tests.Integration/Api/AuthControllerTests.cs` verify `POST /api/v1/auth/register` returning 201 Created and pending account login returning 400 Bad Request ProblemDetails.

---

## P4 - Documentation & Traceability

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P4-S06

**Title:** Feature Catalog and Traceability Documentation Updates

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Update `feature-catalog.md` and `feature-traceability.md` to document `FEAT-USR-002`.

**Depends On:** P2-S04

**Repository:** cakra/docs

**Completion Criteria:**
- `feature-catalog.md` includes entry for `FEAT-USR-002 | Register User Account | Command | —`.
- `feature-traceability.md` maps `FEAT-USR-002` to Identity domain, `CR-009`, `SCR-AUTH-001`.

---

# 6. Change Log

- **2026-10-05:** Initial release of `CR-009-IMPLEMENTATION-PLAN.md` with Execution Approval `APPROVED`.
