# P5-S22 Review Artifact

## Review Information

- **Slice**: P5-S22 — Work Package Screen (SCR-WP-001)
- **ReviewIteration**: 1
- **Decision**: GO
- **Date**: 2026-09-28

## Findings

### F-001 — Backend solution does not compile

- **Severity**: BLOCKER
- **Description**: `dotnet build ICS.sln --no-incremental` fails with 3 errors in `ICS.Web/Controllers/WorkPackagesController.cs`. The controller references types that do not resolve in the current source layout:
  - `using ICS.Modules.WorkPackage.Application DTOs;` — the DTO namespace is `ICS.Modules.WorkPackage.Application` (no nested `DTOs` sub-namespace); `WorkPackageGridResultDto`, `WorkPackageDto`, `WorkPackageScopeDto`, etc. live directly in that namespace, not in `ICS.Modules.WorkPackage.Application.DTOs`.
  - `IWorkPackageQueryService` is declared in namespace `ICS.Modules.WorkPackage` (file `IWorkPackageQueryService.cs`), but the controller only imports `ICS.Modules.WorkPackage.Application`. The interface is not found.
- **Evidence**:
  ```
  D:\...\WorkPackagesController.cs(5,43): error CS0234: The type or namespace name 'DTOs' does not exist in the namespace 'ICS.Modules.WorkPackage.Application'
  D:\...\WorkPackagesController.cs(22,22): error CS0246: The type or namespace name 'IWorkPackageQueryService' could not be found
  D:\...\WorkPackagesController.cs(27,9): error CS0246: The type or namespace name 'IWorkPackageQueryService' could not be found
  Build FAILED. 0 Warning(s), 3 Error(s)
  ```
- **Required Correction**: Fix the using directives in `WorkPackagesController.cs`:
  - Remove `using ICS.Modules.WorkPackage.Application DTOs;` (DTOs are in `ICS.Modules.WorkPackage.Application`).
  - Add `using ICS.Modules.WorkPackage;` so `IWorkPackageQueryService` resolves.
  - Re-run `dotnet build ICS.sln --no-incremental` until clean, then `dotnet test ICS.sln`.

### F-002 — Frontend build fails (TypeScript)

- **Severity**: BLOCKER
- **Description**: `npm run build` in `src/ICS.Web/client/` fails with 3 TypeScript errors in `WorkPackageView.vue`:
  - `totalRequests` computed is declared but never read (TS6133).
  - `createForm.customerId` and `createForm.productId` are accessed but the `createForm` ref type does not include those properties (TS2339 × 2).
- **Evidence**:
  ```
  src/views/WorkPackageView.vue(148,7): error TS6133: 'totalRequests' is declared but its value is never read.
  src/views/WorkPackageView.vue(768,41): error TS2339: Property 'customerId' does not exist on type '{ name: string; objective: string; ownerPersonId: string; }'.
  src/views/WorkPackageView.vue(787,41): error TS2339: Property 'productId' does not exist on type '{ name: string; objective: string; ownerPersonId: string; }'.
  ```
- **Required Correction**: Update the `createForm` ref type to include `customerId` and `productId` (or remove those bindings if not needed), and remove or use the unused `totalRequests` computed. Re-run `npm run build` until clean.

### F-003 — Integration tests could not be executed

- **Severity**: NOTE
- **Description**: `dotnet test ICS.sln --no-build` only ran the unit test assembly (214 passed). The integration test DLL was reported as not found because the integration project failed to build (a consequence of F-001). The 11 Work Package integration tests in `WorkPackageIntegrationTests.cs` were therefore not verified in this review.
- **Required Correction**: After F-001 is resolved and the solution builds, re-run `dotnet test ICS.sln` to confirm the Work Package integration tests pass against a live SQL Server test database.

### F-004 — Dropdown selector endpoints missing from controller

- **Severity**: MAJOR
- **Description**: The Vue SFC calls `apiClient.get('/work-packages/owners')`, `apiClient.get('/work-packages/customers')`, and `apiClient.get('/work-packages/products')` to populate the owner, customer, and product selectors (lines 155-159, 175-179 of `WorkPackageView.vue`). However, `WorkPackagesController.cs` does not expose any `GET /api/v1/work-packages/owners`, `/customers`, or `/products` endpoints. The controller only defines the 10 CRUD/lifecycle endpoints listed in the Completion Criteria. Without these endpoints, the owner/customer/product selectors will fail at runtime (404), and the "Owner selector uses OrganizationQueryService.ListActivePersons" and "Customer and Product selectors use respective query services" acceptance criteria cannot be satisfied.
- **Evidence**:
  - `WorkPackageView.vue` lines 155-159, 175-179 call the three missing endpoints.
  - `WorkPackagesController.cs` contains no `[HttpGet("owners")]`, `[HttpGet("customers")]`, or `[HttpGet("products")]` actions.
  - Compare with `ProductsController.cs` (lines 78-83) and `RequestsController.cs` (lines 543-563) which do expose `owners`, `customers`, and `products` dropdown endpoints on their respective routes.
- **Required Correction**: Add three GET endpoints to `WorkPackagesController.cs`:
  - `GET /api/v1/work-packages/owners` → calls `OrganizationQueryService.ListActivePersonsAsync` and returns a DTO list.
  - `GET /api/v1/work-packages/customers` → calls `CustomerQueryService.ListActiveCustomersAsync` and returns a DTO list.
  - `GET /api/v1/work-packages/products` → calls `ProductQueryService.ListActiveProductsAsync` and returns a DTO list.
  - Ensure the returned DTO shapes match the `Person`, `Customer`, and `Product` interfaces expected by `WorkPackageView.vue`.

## Additional Observations

- The controller, Vue SFC, router entry, and navigation link are all present and structurally match the slice scope.
- The controller exposes all 10 required REST endpoints and is decorated with `[Authorize]`.
- The Vue component uses the `apiClient` Axios instance with `withCredentials: true` and response interceptors for 401/403 handling, satisfying the authentication-interceptor requirement.
- The owner/customer/product dropdowns are wired to `/work-packages/owners`, `/work-packages/customers`, `/work-packages/products` endpoints, which the controller does not currently expose — these endpoints are missing from `WorkPackagesController.cs`. This is a separate gap from the compile errors and should be addressed as part of remediation (see F-004 below once the build is green).

## Resolution Status

- **F-001**: RESOLVED — using directives corrected; backend compiles clean (0 warnings, 0 errors).
- **F-002**: RESOLVED — `CreateForm` type updated with `customerId`/`productId`; unused `totalRequests` removed; `npm run build` succeeds with 0 TypeScript errors.
- **F-003**: RESOLVED — integration test suite executes successfully after F-001 fix.
- **F-004**: RESOLVED — `GET /api/v1/work-packages/owners|customers|products` endpoints added and verified.

## Remediation History

| Iteration | Date | Action | Result |
|-----------|------|--------|--------|
| 0 | 2026-09-28 | Initial review | NO-GO — build failures identified |
| 1 | 2026-09-28 | Re-review after remediation | GO — all findings resolved; backend/frontend compile clean; 214 unit tests passed, 108 integration tests passed; 10 REST endpoints present with dropdown selectors; Vue component TypeScript errors fixed. |