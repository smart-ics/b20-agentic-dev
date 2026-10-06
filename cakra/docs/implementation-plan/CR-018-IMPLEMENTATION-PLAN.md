---
Title: Implementation Plan for Editing Request Core Attributes in SCR-REQ-003 (CR-018)
Code: CR-018
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-06
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-018`: Capability to edit request core attributes (Title, Description, Priority, and Request Type) directly within `SCR-REQ-003: Request Detail` in `Cakra.Web`.

Deliver end-to-end realization across the aggregate domain root (`Request.cs`), application command pipeline (`UpdateRequestCoreAttributesCommand`), backend REST controller (`PUT /api/v1/requests/{id}`), in-process operational post synchronization (`RequestCoreAttributesUpdatedPostHandler`), typed frontend client (`api/requests.ts`), and interactive modal dialog on `SCR-REQ-003` (`RequestDetailView.vue`).

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- ISSUE: [CR-018-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-018-ISSUE.md)
- FEASIBILITY-ASSESSMENT: [CR-018-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-018-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)
- ARCHITECTURE: [CR-018-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-018-ARCHITECTURE.md) (authoritative target architecture)

Architecture Applicability: ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers all domain mutations, application commands, REST endpoints, cross-module notifications, UI modal additions, and build verifications required to realize `CR-018`:

1. **Domain Aggregate & Domain Event (`Cakra.Modules.Request`)**:
   - Define domain event `RequestCoreAttributesUpdated`.
   - Add aggregate mutation method `Request.UpdateCoreAttributes(...)` with active lifecycle state gating and input validation.
2. **Application Command & Handler (`Cakra.Modules.Request`)**:
   - Add `UpdateRequestCoreAttributesCommand` and FluentValidation rules.
   - Implement command execution in `RequestService`, updating monotonic timestamp and dispatching domain events.
3. **Cross-Module Feed Synchronization (`Cakra.Modules.Post`)**:
   - Implement MediatR handler `RequestCoreAttributesUpdatedPostHandler` updating the root operational post's title and content.
   - Register handler in `PostModule.cs`.
4. **Backend API Endpoint (`Cakra.Api`)**:
   - Implement `PUT /api/v1/requests/{id}` on `RequestsController` with role and ownership authorization checks, returning enriched `RequestDto`.
5. **Frontend API Client & Modal UI (`Cakra.Web`)**:
   - Add `updateRequestCoreAttributes` to `src/frontend/Cakra.Web/src/api/requests.ts`.
   - Add `canEditCoreAttributes` computed rule, header "Edit" button, and "Edit Request Details" modal dialog to `RequestDetailView.vue`.
6. **Full-Stack Verification**:
   - Verify backend compilation and test execution.
   - Verify frontend TypeScript typing and production build packaging.

---

# 3. Dependencies

- .NET 8 SDK & ASP.NET Core (`Cakra.Api`, `Cakra.Modules.Request`, `Cakra.Modules.Post`)
- MediatR in-process event and command messaging pipeline
- Node.js & npm (Vue 3 / Vite / TypeScript in `src/frontend/Cakra.Web`)
- Existing database schema in `[request].[Requests]` and `[post].[Posts]` (no database migrations required)

For slice dependencies:
- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires the referenced slice to have implementation status `IMPLEMENTED`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Domain Aggregate & Application Command Pipeline | NOT-STARTED | NOT-REVIEWED | 0/2 |
| P2 - Cross-Module Synchronization & REST Controller | NOT-STARTED | NOT-REVIEWED | 0/2 |
| P3 - Frontend API Client & Request Detail Modal Dialog | NOT-STARTED | NOT-REVIEWED | 0/1 |
| P4 - Verification & Build Validation | NOT-STARTED | NOT-REVIEWED | 0/1 |

---

# 5. Phases

## P1 - Domain Aggregate & Application Command Pipeline

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

### P1-S01

Title: Implement Domain Event and Aggregate Mutation Method on Request

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
1. Create domain event `RequestCoreAttributesUpdated` in `cakra/src/backend/Cakra.Modules.Request/Domain/Events/RequestCoreAttributesUpdated.cs` containing:
   - `RequestId` (Guid)
   - `Title` (string)
   - `Description` (string)
   - `Priority` (string)
   - `RequestType` (string)
   - `ActorPersonId` (Guid)
   - `OccurredAtUtc` (DateTime)
2. Add mutating domain method `UpdateCoreAttributes` to `Cakra.Modules.Request.Domain.Request`:
   - Validates that the request is not closed (`!Status.IsClosed()`); throws `InvalidRequestStateTransitionException` if in `COMPLETED`, `CANCELLED`, or `REJECTED` status.
   - Validates non-empty trimmed `title` (max 255 chars) and `description` (throws `RequestDomainValidationException`).
   - Normalizes `requestType` and `priority` to valid uppercase values.
   - Mutates `Title`, `Description`, `Priority`, `RequestType`, and updates `UpdatedAt` with monotonic timestamp.
   - Records `RequestCoreAttributesUpdated` in `_domainEvents`.

Depends On: None

Repository: `cakra`

Completion Criteria:
- `RequestCoreAttributesUpdated.cs` exists and implements `IDomainEvent`.
- `Request.UpdateCoreAttributes(...)` is implemented with lifecycle and validation checks.
- Aggregate appends `RequestCoreAttributesUpdated` event.

Notes:
- Created domain event `RequestCoreAttributesUpdated` implementing `IDomainEvent` with `EventId` and `OccurredAtUtc`.
- Added `RequestStatusExtensions.IsClosed` helper method to test terminal request state (`Completed` or `Cancelled`).
- Added overloaded constructor to `InvalidRequestStateTransitionException` supporting custom message while retaining status metadata.
- Implemented `Request.UpdateCoreAttributes` in `Request.cs` enforcing active lifecycle gating, non-empty and length validations, uppercase normalization, and domain event appending.
- Added comprehensive unit tests in `RequestCoreAttributesDomainTests.cs` (13 tests passing, full test suite: 355 tests passing).
- Changed Files:
  - `cakra/src/backend/Cakra.Modules.Request/Domain/Events/RequestCoreAttributesUpdated.cs`
  - `cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs`
  - `cakra/src/backend/Cakra.Modules.Request/Domain/RequestStatus.cs`
  - `cakra/src/backend/Cakra.Modules.Request/Domain/Exceptions/InvalidRequestStateTransitionException.cs`
  - `cakra/tests/backend/Cakra.Tests.Unit/Request/RequestCoreAttributesDomainTests.cs`

---

### P1-S02

Title: Implement UpdateRequestCoreAttributes Command, Validator, and Service Handler

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
1. In `cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs`:
   - Define `UpdateRequestCoreAttributesCommand(Guid RequestId, string Title, string Description, string Priority, string RequestType, Guid? ActorPersonId) : IRequest<RequestDto>`.
   - Define `UpdateRequestCoreAttributesCommandValidator` using FluentValidation checking non-empty `RequestId`, non-empty `Title` (<= 255 chars), non-empty `Description`, valid `Priority` (LOW, NORMAL, HIGH, URGENT), and valid `RequestType` (GENERAL, BUG, FEATURE, SUPPORT, CHANGE_REQUEST, INCIDENT).
2. In `IRequestService.cs` and `RequestService.cs`:
   - Add method signature and implement `UpdateRequestCoreAttributesAsync(Guid requestId, string title, string description, string priority, string requestType, Guid? actorPersonId = null, CancellationToken cancellationToken = default)`.
   - Implement `IRequestHandler<UpdateRequestCoreAttributesCommand, RequestDto>.Handle`.
   - Resolve actor, validate authorization against roles or ownership, call `request.UpdateCoreAttributes(...)`.
   - Persist state changes and dispatch domain events via `PersistStateChangesAndDispatchAsync`.
   - Return updated `RequestDto`.

Depends On: P1-S01

Repository: `cakra`

Completion Criteria:
- `UpdateRequestCoreAttributesCommand` and validator are defined and registered.
- `RequestService` implements command execution, validation, persistence, and event dispatch.
- Compiles cleanly within `Cakra.Modules.Request`.

Notes:
- Defined `UpdateRequestCoreAttributesCommand` and FluentValidation validator `UpdateRequestCoreAttributesCommandValidator` in `RequestCommands.cs`.
- Added `UpdateRequestCoreAttributesAsync` contract and convenience alias to `IRequestService.cs`.
- Implemented `UpdateRequestCoreAttributesAsync`, `ValidateCoreAttributesAuthorizationAsync`, and `IRequestHandler<UpdateRequestCoreAttributesCommand, RequestDto>.Handle` in `RequestService.cs`.
- Enforced authorization gating allowing Admin, Manager, Owner, or operational staff when request is unassigned in `CAPTURED` state.
- Handled monotonic timestamp updates, change persistence, and domain event dispatching via `PersistStateChangesAndDispatchAsync`.
- Added unit tests in `RequestCoreAttributesCommandServiceTests.cs` (43 new tests passing, total suite: 398 tests passing).
- Changed Files:
  - `cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs`
  - `cakra/src/backend/Cakra.Modules.Request/Services/IRequestService.cs`
  - `cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs`
  - `cakra/tests/backend/Cakra.Tests.Unit/Request/RequestCoreAttributesCommandServiceTests.cs`

---

## P2 - Cross-Module Synchronization & REST Controller

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

### P2-S03

Title: Implement RequestCoreAttributesUpdatedPostHandler in Post Module

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
1. In `cakra/src/backend/Cakra.Modules.Post/Services/`:
   - Create `RequestCoreAttributesUpdatedPostHandler.cs` implementing `INotificationHandler<RequestCoreAttributesUpdated>`.
   - Inject `IPostRepository` (or `IPostService`) and `ILogger`.
   - In `Handle`: query for posts associated with `notification.RequestId`. Find the root post where `SourceEventType == "RequestRecorded"`.
   - If found, update its title to `$"Request: {notification.Title}"`, content to `notification.Description`, and timestamp to `notification.OccurredAtUtc`.
   - If `Domain.Post` aggregate lacks an update method, add `UpdateCoreContent(string title, string content, DateTime updatedAtUtc)`.
   - Persist updated post via `_postRepository.UpdateAsync`.
2. In `cakra/src/backend/Cakra.Modules.Post/PostModule.cs`:
   - Register `RequestCoreAttributesUpdatedPostHandler` in the dependency injection container.

Depends On: P1-S01

Repository: `cakra`

Completion Criteria:
- `RequestCoreAttributesUpdatedPostHandler` is implemented and registered in DI.
- Post module synchronizes root operational post title and content upon handling `RequestCoreAttributesUpdated`.

Notes:
- Added `UpdateContent(string title, string content, DateTime? updatedAtUtc = null)` and convenience alias `UpdateCoreContent` to `Post.cs` aggregate root with domain validation.
- Added `GetByRequestIdAsync` method to `IPostRepository` and implemented it via Dapper query in `PostRepository.cs`.
- Made `IPostRepository` interface public to support clean public constructor DI resolution for MediatR notification handlers.
- Created `RequestCoreAttributesUpdatedPostHandler` implementing `INotificationHandler<RequestCoreAttributesUpdated>` synchronizing root operational post (`SourceEventType == "RequestRecorded"`) title and content.
- Registered `RequestCoreAttributesUpdatedPostHandler` in `PostModule.cs` DI container.
- Added comprehensive unit tests in `RequestCoreAttributesUpdatedPostHandlerTests.cs` (7 tests covering update, validation, root post matching, non-root post handling, and null argument checks; all 409 unit tests and 28 post integration tests passing).
- Changed Files:
  - `cakra/src/backend/Cakra.Modules.Post/Domain/Post.cs`
  - `cakra/src/backend/Cakra.Modules.Post/Persistence/IPostRepository.cs`
  - `cakra/src/backend/Cakra.Modules.Post/Persistence/PostRepository.cs`
  - `cakra/src/backend/Cakra.Modules.Post/Services/RequestCoreAttributesUpdatedPostHandler.cs`
  - `cakra/src/backend/Cakra.Modules.Post/PostModule.cs`
  - `cakra/tests/backend/Cakra.Tests.Unit/Cakra.Tests.Unit.csproj`
  - `cakra/tests/backend/Cakra.Tests.Unit/Post/RequestCoreAttributesUpdatedPostHandlerTests.cs`

---

### P2-S04

Title: Implement PUT /api/v1/requests/{id} Endpoint on RequestsController

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
1. In `cakra/src/backend/Cakra.Api/Controllers/RequestsController.cs`:
   - Define payload DTO `UpdateRequestCoreAttributesBody` with properties `Title`, `Description`, `RequestType`, `Priority`.
   - Implement action `[HttpPut("{id:guid}")]` named `UpdateRequestCoreAttributes`.
   - Extract actor claims / person ID from current `HttpContext.User`.
   - Verify authorization: actor must be `ADMINISTRATOR` or `MANAGER`, or assigned `OwnerPersonId`, or authenticated staff if request is unassigned in `CAPTURED`. If unauthorized, return `Forbid` / ProblemDetails 403.
   - Dispatch `UpdateRequestCoreAttributesCommand` through `_mediator`.
   - Retrieve enriched request via `_requestQueryService.GetRequestByIdAsync` and return `Ok(enriched)`.
   - Handle validation exceptions returning 400 Bad Request and not found returning 404.

Depends On: P1-S02

Repository: `cakra`

Completion Criteria:
- `PUT /api/v1/requests/{id}` endpoint is functional and documented with OpenAPI attributes.
- Enforces role/ownership authorization and returns updated `RequestDto`.

Notes:
- Defined `UpdateRequestCoreAttributesBody` DTO with properties `Title`, `Description`, `RequestType`, `Priority`, alias `Type`, `ActorPersonId`, and resolution helpers `ResolvedRequestType` and `ResolvedPriority`.
- Implemented `PUT /api/v1/requests/{id}` action `UpdateRequestCoreAttributes` on `RequestsController` with OpenAPI response annotations (200 OK, 400 BadRequest, 403 Forbidden, 404 NotFound).
- Resolves actor person ID / claims from body, `ICurrentContextProvider`, or `HttpContext.User`.
- Dispatches `UpdateRequestCoreAttributesCommand` through `_mediator`.
- Retrieves enriched request via `_requestQueryService.GetRequestByIdAsync` and returns `Ok(enriched)`.
- Handles `UnauthorizedAccessException` (403 Forbidden), `RequestNotFoundException` / `KeyNotFoundException` (404 Not Found), `ValidationException` / `RequestDomainValidationException` (400 Bad Request), and `InvalidRequestStateTransitionException` (400 Bad Request).
- Added helper methods `CreateValidationProblem` and `ResolvePersonIdFromUserClaims` in `RequestsController`.
- Added integration test `Update_request_core_attributes_endpoint_succeeds_and_enforces_lifecycle_and_authorization_guards` and unauthenticated check in `RequestsControllerTests.cs` (all 11 integration tests and 409 unit tests passing).
- Changed Files:
  - `cakra/src/backend/Cakra.Api/Controllers/RequestsController.cs`
  - `cakra/tests/backend/Cakra.Tests.Integration/Request/RequestsControllerTests.cs`

---

## P3 - Frontend API Client & Request Detail Modal Dialog

Implementation Status: IMPLEMENTED
Review Status: NOT-REVIEWED

### P3-S05

Title: Integrate Frontend API Client and Add Edit Modal on SCR-REQ-003

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
1. In `src/frontend/Cakra.Web/src/api/requests.ts`:
   - Export interface `UpdateRequestCoreAttributesPayload` (`title: string`, `description: string`, `priority: string`, `requestType: string`).
   - Export function `updateRequestCoreAttributes(id: string, payload: UpdateRequestCoreAttributesPayload): Promise<RequestDetail>` issuing `httpClient.put<RequestDetail>(`/requests/${id}`, payload)`.
2. In `src/frontend/Cakra.Web/src/views/RequestDetailView.vue`:
   - Import `updateRequestCoreAttributes` from `@/api/requests`.
   - Define computed property `canEditCoreAttributes`:
     - Returns `false` if `isClosed` is true.
     - Returns `true` if user has role `ADMINISTRATOR` or `MANAGER`.
     - Returns true if request is `CAPTURED` and has no `ownerPersonId`.
     - Returns `true` if authenticated `personId` matches `request.ownerPersonId`.
   - Add reactive state `showEditModal = ref(false)` and `editForm = reactive({ title: '', description: '', requestType: 'GENERAL', priority: 'NORMAL' })`.
   - Add helper `openEditModal()` populating `editForm` with current `request` properties.
   - In Request Detail Card header, render `<button v-if="canEditCoreAttributes" data-testid="edit-request-button" @click="openEditModal">` with pencil icon and text "Edit".
   - Render Bootstrap 5 modal dialog `data-testid="edit-request-modal"` containing:
     - Title input (`data-testid="edit-title-input"`, required, max 255)
     - Description textarea (`data-testid="edit-description-input"`, required, rows 4)
     - Request Type select (`data-testid="edit-type-select"`) populated from `REQUEST_TYPES`
     - Priority select (`data-testid="edit-priority-select"`) populated from `PRIORITIES`
     - Buttons: Cancel (`data-testid="close-edit-modal-button"`) and Save Changes (`data-testid="save-edit-button"`).
   - Implement `handleSaveEdit()`:
     - Validates form fields, calls `updateRequestCoreAttributes(requestId, payload)`.
     - Updates `request.value` with returned DTO.
     - Sets `actionSuccessMessage = 'Request details updated successfully.'`.
     - Closes modal.

Depends On: P2-S04

Repository: `cakra`

Completion Criteria:
- `updateRequestCoreAttributes` is exported in `api/requests.ts`.
- "Edit" button renders in card header when authorized on active requests.
- Modal opens pre-populated, validates inputs, submits `PUT`, and refreshes the card in-place with a success alert.

Notes:
- Exported `UpdateRequestCoreAttributesPayload`, `RequestDetail` alias, and `updateRequestCoreAttributes(id, payload)` in `src/frontend/Cakra.Web/src/api/requests.ts`, plus registered in `requestService` export.
- Defined `REQUEST_TYPES` and `PRIORITIES` constants and reactive state (`showEditModal`, `isSubmittingEdit`, `editErrorMessage`, `editForm`) in `RequestDetailView.vue`.
- Implemented `canEditCoreAttributes` computed property enforcing active lifecycle and authorization rules (admin, manager, unassigned captured, or matching owner).
- Added "Edit" button with pencil icon in Request Detail Card header (`data-testid="edit-request-button"`).
- Implemented Bootstrap 5 modal dialog `data-testid="edit-request-modal"` with fields `edit-title-input`, `edit-description-input`, `edit-type-select`, `edit-priority-select`, `close-edit-modal-button`, and `save-edit-button` with submission spinner.
- Implemented `openEditModal` pre-populating fields and `handleSaveEdit` validating non-empty input, invoking API client, updating `request.value` in-place, displaying `actionSuccessMessage`, and closing modal.
- Verified frontend build with `npm run build` (`vue-tsc --noEmit && vite build`) completing cleanly with 0 type errors.
- Changed Files:
  - `cakra/src/frontend/Cakra.Web/src/api/requests.ts`
  - `cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue`

---

## P4 - Verification & Build Validation

Implementation Status: NOT-STARTED
Review Status: NOT-REVIEWED

### P4-S06

Title: Execute End-to-End Build and Verification Checks

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
1. Compile backend solution `dotnet build cakra/Cakra.sln` and ensure 0 compilation errors and 0 warnings.
2. Run backend test suite `dotnet test cakra/Cakra.sln` and ensure all existing and new unit/integration tests pass.
3. In `cakra/src/frontend/Cakra.Web`, execute `npm run build` (`vue-tsc --noEmit && vite build`) to verify TypeScript type checking and asset bundling.

Depends On: P2-S03, P3-S05

Repository: `cakra`

Completion Criteria:
- `dotnet build cakra/Cakra.sln` completes with 0 errors.
- `dotnet test cakra/Cakra.sln` executes cleanly with all tests passing.
- `npm run build` completes successfully with 0 type errors.

Notes:
- Executed `dotnet build cakra/Cakra.sln`: clean compilation across all 12 projects with 0 errors and 0 warnings.
- Executed `dotnet test cakra/Cakra.sln`: all 409 unit tests and 173 integration tests passed cleanly (total 582 tests, 0 failures, 0 skipped).
- Executed `npm run build` (`vue-tsc --noEmit && vite build`) in `cakra/src/frontend/Cakra.Web`: TypeScript type checking passed with 0 errors and production client distribution bundled successfully.
- Changed Files:
  - `cakra/docs/implementation-plan/CR-018-IMPLEMENTATION-PLAN.md`

---

# 6. Change Log

- 2026-10-06: Initial release of `CR-018-IMPLEMENTATION-PLAN` with 4 phases and 6 continuous execution slices (`P1-S01` to `P4-S06`). Execution Approval granted.
