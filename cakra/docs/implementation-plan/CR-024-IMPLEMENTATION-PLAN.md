---
Title: Implementation Plan for Updating Customer and Product Association in Work Package Aggregate and Screen (CR-024)
Code: CR-024
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-07
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-024`: Updating and clearing Customer and Product associations on an existing Work Package aggregate root (`WorkPackage.cs`), application services, REST API endpoints, and screen `SCR-WP-001` (`WorkPackageView.vue`).

Deliver end-to-end technical realization across:
1. Aggregate mutation method `UpdateContext(Guid? customerId, Guid? productId, DateTime? updatedAtUtc = null)` in `WorkPackage.cs` with state invariant gating (`DRAFT` and `ACTIVE` mutable, `CLOSED` strictly immutable).
2. Domain event `WorkPackageContextChanged` emitted when `CustomerId` or `ProductId` associations change.
3. Strict DDD aggregate boundary preservation: modifications to Work Package context never cascade to or alter constituent Requests.
4. Independent clearing (setting to `null` / unassigned) for Customer, Product, or both.
5. Cross-module active entity validation in `WorkPackageService.cs` via `ICustomerQueryService` and `IProductQueryService` for non-null IDs.
6. MediatR command `UpdateWorkPackageContextCommand(Guid WorkPackageId, Guid? CustomerId, Guid? ProductId) : IRequest<WorkPackageDto>`.
7. Dedicated REST API endpoint `PUT /api/v1/work-packages/{id}/context` in `WorkPackagesController.cs`.
8. Frontend client helper `updateWorkPackageContext` in `src/frontend/Cakra.Web/src/api/workpackages.ts`.
9. Interactive "Customer & Product" editing section in the Detail Drawer of `src/frontend/Cakra.Web/src/views/WorkPackageView.vue`.
10. Zero database schema migrations (columns `[CustomerId]` and `[ProductId]` already exist in `[workpackage].[WorkPackages]`).

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- ISSUE: [CR-024-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-024-ISSUE.md)
- DOMAIN: [work-package-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/work-package-domain.md)
- FEASIBILITY-ASSESSMENT: [CR-024-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-024-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)
- ARCHITECTURE: [CR-024-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-024-ARCHITECTURE.md) (authoritative target architecture)

Architecture Applicability: ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers all domain modeling, application commands, REST endpoints, frontend API helpers, UI components, and full-stack verifications required to realize `CR-024`:

1. **Domain Aggregate & Event (`Cakra.Modules.WorkPackage`)**:
   - Create domain event `WorkPackageContextChanged.cs` in `Cakra.Modules.WorkPackage.Domain.Events`.
   - Add domain method `UpdateContext(Guid? customerId, Guid? productId, DateTime? updatedAtUtc = null)` to `WorkPackage.cs`.
   - Enforce invariant: throw `WorkPackageDomainException` when called in `CLOSED` state.
   - Record `WorkPackageContextChanged` when values change and update `UpdatedAt`.
   - Add unit test coverage in `Cakra.Tests.Unit`.
2. **Application Layer (`Cakra.Modules.WorkPackage`)**:
   - Define `UpdateWorkPackageContextCommand` and FluentValidation validator in `WorkPackageCommands.cs`.
   - Add `UpdateContextAsync` contract on `IWorkPackageService.cs`.
   - Implement `UpdateContextAsync` and command handler in `WorkPackageService.cs`.
   - Validate non-null `CustomerId` via `_customerQueryService.IsCustomerActiveAsync` (throwing `InvalidOperationException` or `KeyNotFoundException`).
   - Validate non-null `ProductId` via `_productQueryService.IsProductActiveAsync` (throwing `InvalidOperationException` or `KeyNotFoundException`).
   - Persist aggregate via `_workPackageRepository.UpdateAsync` and dispatch domain events.
3. **REST API Controller (`Cakra.Api`)**:
   - Add endpoint `PUT /api/v1/work-packages/{id}/context` to `WorkPackagesController.cs`.
   - Define request body `UpdateWorkPackageContextBody` (`Guid? CustomerId`, `Guid? ProductId`).
   - Return status `200 OK` with enriched `WorkPackageDto`.
   - Add integration test coverage in `Cakra.Tests.Integration`.
4. **Frontend API Client & Screen (`Cakra.Web`)**:
   - Add interface `UpdateWorkPackageContextPayload` and function `updateWorkPackageContext` in `src/frontend/Cakra.Web/src/api/workpackages.ts`.
   - In `WorkPackageView.vue`:
     - Add reactive state `contextForm` (`customerId`, `productId`).
     - Populate `contextForm` in `syncDetailForms`.
     - Add `isContextDirty` computed property.
     - Add dedicated "Customer & Product" section in Detail Drawer with `<select>` dropdowns including `-- None / Unassigned --` option and a "Save Context" button.
     - Add async handler `handleUpdateContext` to invoke API and refresh reactive state.
5. **Full-Stack Verification**:
   - Execute backend test suite (`dotnet test`).
   - Execute frontend production build (`npm run build`).

---

# 3. Dependencies

- .NET 8 SDK & ASP.NET Core (`Cakra.Api`, `Cakra.Modules.WorkPackage`)
- MediatR & FluentValidation
- Dapper object mapping in `Cakra.Modules.WorkPackage.Persistence`
- Node.js & npm (Vue 3 / Vite / TypeScript in `src/frontend/Cakra.Web`)

For slice dependencies:
- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires the referenced slice to have implementation status `IMPLEMENTED`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Domain Aggregate & Event | IMPLEMENTED | GO | 1/1 |
| P2 - Application Layer & REST API | IMPLEMENTED | GO | 2/2 |
| P3 - Frontend API Client & Work Package Screen (SCR-WP-001) | IMPLEMENTED | GO | 2/2 |
| P4 - Full-Stack Verification & Test Validation | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Domain Aggregate & Event

Implementation Status: IMPLEMENTED
Review Status: GO

### P1-S01

Title: Domain Event WorkPackageContextChanged and Aggregate Mutation UpdateContext

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
1. Create `WorkPackageContextChanged.cs` in `cakra/src/backend/Cakra.Modules.WorkPackage/Domain/Events/` implementing `IDomainEvent` with `WorkPackageId`, `PreviousCustomerId`, `NewCustomerId`, `PreviousProductId`, `NewProductId`, and `OccurredAtUtc`.
2. In `cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackage.cs`, add method `UpdateContext(Guid? customerId, Guid? productId, DateTime? updatedAtUtc = null)`:
   - Throws `WorkPackageDomainException` if `Status == WorkPackageStatus.Closed`.
   - Normalizes empty GUIDs to `null`.
   - If both IDs are unchanged, exits early without updating `UpdatedAt` or emitting events.
   - Updates `CustomerId` and `ProductId`, sets `UpdatedAt = updatedAtUtc ?? DateTime.UtcNow`.
   - Emits `WorkPackageContextChanged` domain event.
3. Add unit test coverage in `cakra/tests/backend/Cakra.Tests.Unit/WorkPackage/` validating context updates, clearing to null, event recording, and exception on closed packages.

Depends On: None

Repository: cakra

Completion Criteria:
- File `cakra/src/backend/Cakra.Modules.WorkPackage/Domain/Events/WorkPackageContextChanged.cs` exists.
- Method `WorkPackage.UpdateContext(...)` exists and passes domain unit tests.
- Closed work packages cannot have their context updated.

Notes:
- Realizes TD-002 and TD-003 of `CR-024-ARCHITECTURE.md`.
- Implemented `WorkPackageContextChanged` domain event implementing `IDomainEvent`.
- Added `UpdateContext(Guid? customerId, Guid? productId, DateTime? updatedAtUtc = null)` to `WorkPackage` aggregate with closed state guard, empty Guid normalization, idempotency check, and event recording.
- Added comprehensive unit tests in `WorkPackageContextDomainTests.cs` (10 tests covering draft/active update, clearing, idempotency, empty GUID handling, and closed package exception).
- Changed Files:
  - `cakra/src/backend/Cakra.Modules.WorkPackage/Domain/Events/WorkPackageContextChanged.cs`
  - `cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackage.cs`
  - `cakra/tests/backend/Cakra.Tests.Unit/WorkPackage/WorkPackageContextDomainTests.cs`

---

## P2 - Application Layer & REST API

Implementation Status: IMPLEMENTED
Review Status: GO

### P2-S02

Title: Application Service Command Pipeline and Cross-Module Active Validation

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
1. In `cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageCommands.cs`, define `UpdateWorkPackageContextCommand(Guid WorkPackageId, Guid? CustomerId, Guid? ProductId) : IRequest<WorkPackageDto>` and `UpdateWorkPackageContextCommandValidator`.
2. In `cakra/src/backend/Cakra.Modules.WorkPackage/Services/IWorkPackageService.cs`, add method signature `UpdateContextAsync(Guid workPackageId, Guid? customerId, Guid? productId, CancellationToken cancellationToken = default)` and convenience overload `UpdateContext(...)`.
3. In `cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageService.cs`:
   - Implement `IRequestHandler<UpdateWorkPackageContextCommand, WorkPackageDto>`.
   - Validate Work Package existence via `GetRequiredWorkPackageAsync`.
   - Validate non-null `CustomerId` via `ValidateCustomerAsync(customerId.Value, cancellationToken)`.
   - Validate non-null `ProductId` via `ValidateProductAsync(productId.Value, cancellationToken)`.
   - Mutate aggregate with `workPackage.UpdateContext(customerId, productId, UtcNow)`.
   - Persist updated aggregate via `_workPackageRepository.UpdateAsync(workPackage, cancellationToken)`.
   - Dispatch domain events via `DispatchDomainEventsAsync`.
   - Return `WorkPackageDto.FromDomain(workPackage)`.

Depends On: P1-S01

Repository: cakra

Completion Criteria:
- `UpdateWorkPackageContextCommand` is handled by `WorkPackageService`.
- Active entity checks for Customer and Product are enforced.
- Domain events are dispatched and persistence is executed.

Notes:
- Realizes TD-004 of `CR-024-ARCHITECTURE.md`.
- Added `UpdateWorkPackageContextCommand` and `UpdateWorkPackageContextCommandValidator` in `WorkPackageCommands.cs`.
- Added `UpdateContextAsync` and convenience alias `UpdateContext` in `IWorkPackageService.cs`.
- Implemented `UpdateContextAsync` and MediatR `Handle` method in `WorkPackageService.cs` with Work Package existence validation, active Customer validation, active Product validation, aggregate mutation, persistence, and domain event dispatching.
- Added comprehensive unit tests in `WorkPackageServiceTests.cs` (10 tests covering update, clearing, inactive customer/product exceptions, not found customer/product/work package exceptions, closed package exception, empty ID exception, MediatR command handling, and validator behavior).
- Changed Files:
  - `cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageCommands.cs`
  - `cakra/src/backend/Cakra.Modules.WorkPackage/Services/IWorkPackageService.cs`
  - `cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageService.cs`
  - `cakra/tests/backend/Cakra.Tests.Unit/WorkPackage/WorkPackageServiceTests.cs`

---

### P2-S03

Title: REST API Controller Endpoint PUT /api/v1/work-packages/{id}/context

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
1. In `cakra/src/backend/Cakra.Api/Controllers/WorkPackagesController.cs`:
   - Define `UpdateWorkPackageContextBody` class with `Guid? CustomerId` and `Guid? ProductId`.
   - Expose endpoint `[HttpPut("{id:guid}/context")]` mapping to `UpdateWorkPackageContextCommand`.
   - Catch domain exceptions and return `CreateBadRequestProblem(...)`.
   - Return `200 OK` with enriched `WorkPackageDto`.
2. Add API integration test in `cakra/tests/backend/Cakra.Tests.Integration/` verifying `PUT /api/v1/work-packages/{id}/context` success, clearing to null, and 400 Bad Request on closed packages.

Depends On: P2-S02

Repository: cakra

Completion Criteria:
- HTTP `PUT /api/v1/work-packages/{id}/context` is callable and returns `200 OK` with updated context.
- Returns `400 Bad Request` if Work Package is closed or if Customer/Product is invalid.

Notes:
- Realizes TD-005 of `CR-024-ARCHITECTURE.md`.
- Added `UpdateWorkPackageContextBody` request payload class with `CustomerId` and `ProductId` in `WorkPackagesController.cs`.
- Added `[HttpPut("{id:guid}/context")]` endpoint dispatching `UpdateWorkPackageContextCommand` with normalized non-empty GUIDs.
- Handled domain/validation exceptions (`InvalidOperationException`, `WorkPackageDomainException`, `ArgumentException`) returning RFC 7807 `ProblemDetails` with 400 Bad Request.
- Added comprehensive integration tests in `WorkPackagesControllerTests.cs` (unauthenticated 401 check, context update with Customer and Product, clearing to null, 404 for nonexistent Work Package, 400/404 for invalid Customer/Product, and 400 rejection on closed Work Package).
- Changed Files:
  - `cakra/src/backend/Cakra.Api/Controllers/WorkPackagesController.cs`
  - `cakra/tests/backend/Cakra.Tests.Integration/WorkPackage/WorkPackagesControllerTests.cs`

---

## P3 - Frontend API Client & Work Package Screen (SCR-WP-001)

Implementation Status: IMPLEMENTED
Review Status: GO

### P3-S04

Title: Frontend API Client Helper updateWorkPackageContext

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
1. In `cakra/src/frontend/Cakra.Web/src/api/workpackages.ts`:
   - Add interface `UpdateWorkPackageContextPayload { customerId?: string | null; productId?: string | null }`.
   - Add exported function `updateWorkPackageContext(id: string, payload: UpdateWorkPackageContextPayload): Promise<WorkPackageDto>` executing `httpClient.put<WorkPackageDto>('/work-packages/' + id + '/context', payload)`.
   - Export `updateWorkPackageContext` from default service object.

Depends On: P2-S03

Repository: cakra

Completion Criteria:
- `workpackages.ts` exports `updateWorkPackageContext` with correct types.

Notes:
- Realizes TD-006 of `CR-024-ARCHITECTURE.md`.
- Added interface `UpdateWorkPackageContextPayload` with optional `customerId` and `productId` fields.
- Added and exported function `updateWorkPackageContext(id, payload)` executing `httpClient.put<WorkPackageDto>('/work-packages/' + id + '/context', payload)`.
- Exported `updateWorkPackageContext` in `workPackageService` export object.
- Verified frontend build and typecheck with `npm run build`.
- Changed Files:
  - `cakra/src/frontend/Cakra.Web/src/api/workpackages.ts`

---

### P3-S05

Title: Detail Drawer Customer & Product Editing Controls in WorkPackageView.vue

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
1. In `cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue`:
   - Import `updateWorkPackageContext` from `@/api/workpackages`.
   - Add reactive state `contextForm = reactive({ customerId: '', productId: '' })`.
   - Synchronize `contextForm` inside `syncDetailForms(wp)`.
   - Add computed `isContextDirty` evaluating if `contextForm` differs from `selectedWorkPackage.customerId` or `productId`.
   - Add handler `handleUpdateContext` calling `updateWorkPackageContext`, updating `selectedWorkPackage`, updating item in `workPackages`, and showing success alert.
   - In Detail Drawer template, add a dedicated "Customer & Product" section (when `canModifyPackage` is true) with:
     - Customer select dropdown including `<option value="">-- None / Unassigned --</option>` and active customers.
     - Product select dropdown including `<option value="">-- None / Unassigned --</option>` and active products.
     - "Save Context" button disabled when `!canModifyPackage || isSubmittingAction || !isContextDirty`.
   - Ensure top metadata card immediately updates labels and customer/product codes upon save.

Depends On: P3-S04

Repository: cakra

Completion Criteria:
- Customer and Product dropdowns are rendered in the Detail Drawer for active/draft work packages.
- Saving updates persists the new associations and updates the UI immediately.

Notes:
- Realizes TD-006 of `CR-024-ARCHITECTURE.md`.
- Imported `updateWorkPackageContext` from `@/api/workpackages` in `WorkPackageView.vue`.
- Added reactive state `contextForm` with `customerId` and `productId`.
- Synchronized `contextForm` inside `syncDetailForms(wp)`.
- Added computed property `isContextDirty` checking differences between `contextForm` and `selectedWorkPackage`.
- Added async handler `handleUpdateContext` updating backend context, mutating `selectedWorkPackage.value`, updating the matching package in `workPackages.value`, and reloading package list.
- Added dedicated `detail-context-section` in Detail Drawer template with customer and product select dropdowns and "Save Context" button.
- Verified build with `npm run build` with zero TypeScript or Vite errors.
- Changed Files:
  - `cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue`

---

## P4 - Full-Stack Verification & Test Validation

Implementation Status: IMPLEMENTED
Review Status: GO

### P4-S06

Title: End-to-End Build and Test Verification

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
1. Run backend unit and integration test suites: `dotnet test cakra/tests/backend/Cakra.Tests.Unit/` and `dotnet test cakra/tests/backend/Cakra.Tests.Integration/`.
2. Run frontend typecheck and production build in `cakra/src/frontend/Cakra.Web`: `npm run build`.
3. Verify zero compiler warnings or regressions across backend and frontend.

Depends On: P2-S03, P3-S05

Repository: cakra

Completion Criteria:
- All unit and integration tests pass cleanly.
- Frontend builds cleanly without TypeScript or template errors.

Notes:
- Verifies Acceptance Conditions 1 through 9 in `CR-024-ARCHITECTURE.md`.
- Backend Unit Tests: 472 passed, 0 failed, 0 skipped (`dotnet test cakra/tests/backend/Cakra.Tests.Unit/`).
- Backend Integration Tests: 179 passed, 0 failed, 0 skipped (`dotnet test cakra/tests/backend/Cakra.Tests.Integration/`).
- Frontend Build & Typecheck: Production build completed cleanly with `npm run build` (`vue-tsc --noEmit && vite build`) in `cakra/src/frontend/Cakra.Web` with 0 errors.
- Changed Files: None (verification slice).

---

# 6. Change Log

- 2026-10-07: Initial creation of `CR-024-IMPLEMENTATION-PLAN.md` with continuous slice numbering P1-S01 through P4-S06. Execution Approval granted by Architect.
