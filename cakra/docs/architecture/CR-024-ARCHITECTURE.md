---
Title: Update Customer and Product Association for Work Package Aggregate and Screen Architecture (CR-024)
Code: CR-024
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-07
---

# 1. Overview

This architecture defines the technical realization for Change Request `CR-024`: Updating and clearing Customer and Product associations on an existing Work Package aggregate root (`WorkPackage.cs`), application services, REST API endpoints, and screen `SCR-WP-001` (`WorkPackageView.vue`).

It consumes and realizes the approved feasibility decisions from [CR-024-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-024-FEASIBILITY-ASSESSMENT.md), establishing:

1. Aggregate mutation method `UpdateContext(Guid? customerId, Guid? productId, DateTime? updatedAtUtc = null)` in `WorkPackage.cs`, with state invariant gating (`DRAFT` and `ACTIVE` mutable; `CLOSED` strictly immutable).
2. Domain event `WorkPackageContextChanged` emitted when `CustomerId` or `ProductId` associations change.
3. Strict DDD aggregate boundary preservation: changes to Work Package context never cascade to or modify constituent Requests.
4. Independent clearing (setting to `null` / unassigned) for Customer, Product, or both.
5. Cross-module active entity validation in `WorkPackageService.cs` via `ICustomerQueryService` and `IProductQueryService` for non-null IDs.
6. MediatR command `UpdateWorkPackageContextCommand(Guid WorkPackageId, Guid? CustomerId, Guid? ProductId) : IRequest<WorkPackageDto>`.
7. Dedicated REST API endpoint `PUT /api/v1/work-packages/{id}/context` in `WorkPackagesController.cs`.
8. Frontend client helper `updateWorkPackageContext` in `workpackages.ts` and interactive "Customer & Product" selection controls in the Detail Drawer of `WorkPackageView.vue`.
9. Zero database migration requirement: the schema and repository already support nullable `[CustomerId]` and `[ProductId]` columns.

---

# 2. Architectural Basis

## Business Context

- ISSUE: [CR-024-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-024-ISSUE.md)
- DOMAIN SPECIFICATION: [work-package-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/work-package-domain.md)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- UI SPECIFICATION: [Work Package Screen (`SCR-WP-001`)](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-024-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-024-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

This architecture consumes and realizes the approved decisions:
- `GAP-001`: Aggregate mutation method `UpdateContext(...)` and invariant locking in `WorkPackage.cs`.
- `GAP-002`: Domain event `WorkPackageContextChanged`.
- `GAP-003`: `UpdateWorkPackageContextCommand` and active entity validation in `WorkPackageService.cs`.
- `GAP-004`: REST API endpoint `PUT /api/v1/work-packages/{id}/context` in `WorkPackagesController.cs`.
- `GAP-005` & `GAP-006`: Frontend API helper and interactive Detail Drawer section in `WorkPackageView.vue`.
- Closed decisions `OQ-001` through `OQ-006`: No cascading to constituent Requests, mutable in `DRAFT` and `ACTIVE`, immutable in `CLOSED`, independently clearable/nullable, unified PUT endpoint, and unified domain event.

---

# 3. Scope

## Included

1. **Domain Aggregate Model (`Cakra.Modules.WorkPackage.Domain.WorkPackage`)**:
   - Method `public void UpdateContext(Guid? customerId, Guid? productId, DateTime? updatedAtUtc = null)`.
   - Invariant guard: throws `WorkPackageDomainException` if `Status == WorkPackageStatus.Closed`.
   - Normalization: treats `Guid.Empty` as `null`.
   - Emits `WorkPackageContextChanged` domain event when values change.
   - Updates `UpdatedAt` timestamp.
2. **Domain Event (`Cakra.Modules.WorkPackage.Domain.Events.WorkPackageContextChanged`)**:
   - Record `WorkPackageContextChanged(Guid WorkPackageId, Guid? PreviousCustomerId, Guid? NewCustomerId, Guid? PreviousProductId, Guid? NewProductId, DateTime OccurredAtUtc) : IDomainEvent`.
3. **Application Commands & Handlers (`Cakra.Modules.WorkPackage.Services`)**:
   - Command `UpdateWorkPackageContextCommand(Guid WorkPackageId, Guid? CustomerId, Guid? ProductId) : IRequest<WorkPackageDto>`.
   - FluentValidation validator `UpdateWorkPackageContextCommandValidator` ensuring `WorkPackageId != Guid.Empty`.
   - Method `UpdateContextAsync(Guid workPackageId, Guid? customerId, Guid? productId, CancellationToken cancellationToken)` on `IWorkPackageService` and `WorkPackageService`.
   - Active entity validation via `ICustomerQueryService.IsCustomerActiveAsync` (for non-null customerId) and `IProductQueryService.IsProductActiveAsync` (for non-null productId).
   - Domain event dispatching via `IDomainEventDispatcher`.
4. **REST API Controller (`Cakra.Api.Controllers.WorkPackagesController`)**:
   - Endpoint `PUT /api/v1/work-packages/{id}/context`.
   - Request body class `UpdateWorkPackageContextBody` (`Guid? CustomerId`, `Guid? ProductId`).
   - Normalization of empty GUIDs to `null`.
   - Status codes: `200 OK` (with `WorkPackageDto`), `400 Bad Request` (domain exception / validation failure), `404 Not Found` (work package or referenced active entity not found).
5. **Frontend API Helper (`src/frontend/Cakra.Web/src/api/workpackages.ts`)**:
   - Interface `UpdateWorkPackageContextPayload { customerId?: string | null; productId?: string | null }`.
   - Exported function `updateWorkPackageContext(id: string, payload: UpdateWorkPackageContextPayload): Promise<WorkPackageDto>`.
6. **Frontend Screen & Detail Drawer (`src/frontend/Cakra.Web/src/views/WorkPackageView.vue`)**:
   - Reactive state `contextForm` (`customerId: string`, `productId: string`).
   - Syncing `contextForm` in `syncDetailForms(wp)`.
   - Dedicated "Customer & Product" editing section in Detail Drawer:
     - Customer select dropdown with `-- None / Unassigned --` option and active customers.
     - Product select dropdown with `-- None / Unassigned --` option and active products.
     - "Save Context" button disabled when package is closed, when an action is in flight, or when no change is detected.
   - Immediate reactive update of `selectedWorkPackage` and summary card labels upon save.

## Excluded

- Cascading updates to constituent Requests (Request domain attributes remain independent).
- Database schema changes (columns `[CustomerId]` and `[ProductId]` already exist in `[workpackage].[WorkPackages]`).
- Modifying other bounded contexts (`Customer`, `Product`, `Organization`, `Request`, `Post`).

---

# 4. Technical Decisions

## TD-001: Zero Database Schema Changes

Inspection of database migration scripts (`0007_workpackage_tables.sql`) and `WorkPackageRepository.cs` confirms that:
- Column `[CustomerId] UNIQUEIDENTIFIER NULL` and `[ProductId] UNIQUEIDENTIFIER NULL` already exist in table `[workpackage].[WorkPackages]`.
- `WorkPackageRepository.AddAsync` already inserts both columns.
- `WorkPackageRepository.UpdateAsync` already executes:
  ```sql
  UPDATE [workpackage].[WorkPackages]
  SET
      [Name] = @Name,
      [Objective] = @Objective,
      [Status] = @Status,
      [OwnerPersonId] = @OwnerPersonId,
      [CustomerId] = @CustomerId,
      [ProductId] = @ProductId,
      [Deadline] = @Deadline,
      [ClosedReason] = @ClosedReason,
      [ClosedAt] = @ClosedAt,
      [UpdatedAt] = @UpdatedAt
  WHERE [Id] = @Id;
  ```
Therefore, no new SQL migration script is needed.

## TD-002: Aggregate Mutation & State Invariants

In [`WorkPackage.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackage.cs):

```csharp
/// <summary>
/// Updates the Customer and Product context associations of the Work Package (Architecture CR-024).
/// Allowed in DRAFT and ACTIVE states; throws WorkPackageDomainException when CLOSED.
/// </summary>
public void UpdateContext(Guid? customerId, Guid? productId, DateTime? updatedAtUtc = null)
{
    if (Status == WorkPackageStatus.Closed)
    {
        throw new WorkPackageDomainException(
            $"Cannot update context for closed Work Package '{Id}'.");
    }

    var normalizedCustomerId = customerId == Guid.Empty ? null : customerId;
    var normalizedProductId = productId == Guid.Empty ? null : productId;

    if (normalizedCustomerId == CustomerId && normalizedProductId == ProductId)
    {
        return;
    }

    var previousCustomerId = CustomerId;
    var previousProductId = ProductId;

    CustomerId = normalizedCustomerId;
    ProductId = normalizedProductId;
    var timestamp = updatedAtUtc ?? DateTime.UtcNow;
    UpdatedAt = timestamp;

    AddDomainEvent(new WorkPackageContextChanged(
        Id,
        previousCustomerId,
        CustomerId,
        previousProductId,
        ProductId,
        timestamp));
}
```

## TD-003: Unified Domain Event

In `Cakra.Modules.WorkPackage.Domain.Events`:

```csharp
using Cakra.Core;

namespace Cakra.Modules.WorkPackage.Domain.Events;

/// <summary>
/// Domain event emitted when the Customer or Product context association of a Work Package changes (CR-024).
/// </summary>
public sealed record WorkPackageContextChanged(
    Guid WorkPackageId,
    Guid? PreviousCustomerId,
    Guid? NewCustomerId,
    Guid? PreviousProductId,
    Guid? NewProductId,
    DateTime OccurredAtUtc) : IDomainEvent;
```

## TD-004: Application Service & MediatR Command Pipeline

In `Cakra.Modules.WorkPackage.Services`:

```csharp
public sealed record UpdateWorkPackageContextCommand(
    Guid WorkPackageId,
    Guid? CustomerId,
    Guid? ProductId) : IRequest<WorkPackageDto>;
```

In `WorkPackageService.cs`:
1. Verify Work Package exists: `var workPackage = await GetRequiredWorkPackageAsync(workPackageId, cancellationToken);`.
2. Validate Customer if non-null:
   ```csharp
   if (customerId.HasValue && customerId.Value != Guid.Empty)
   {
       await ValidateCustomerAsync(customerId.Value, cancellationToken);
   }
   ```
3. Validate Product if non-null:
   ```csharp
   if (productId.HasValue && productId.Value != Guid.Empty)
   {
       await ValidateProductAsync(productId.Value, cancellationToken);
   }
   ```
4. Invoke domain mutation: `workPackage.UpdateContext(customerId, productId, UtcNow);`.
5. Save changes: `await _workPackageRepository.UpdateAsync(workPackage, cancellationToken);`.
6. Dispatch domain events: `await DispatchDomainEventsAsync(workPackage, cancellationToken);`.
7. Return enriched DTO: `return WorkPackageDto.FromDomain(workPackage);`.

## TD-005: REST API Controller Endpoint

In [`WorkPackagesController.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/WorkPackagesController.cs):

```csharp
/// <summary>
/// Updates or clears the Customer and Product context associations of an existing work package (CR-024).
/// </summary>
[HttpPut("{id:guid}/context")]
[ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public async Task<IActionResult> UpdateContext(
    Guid id,
    [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] UpdateWorkPackageContextBody? request = null,
    CancellationToken cancellationToken = default)
{
    var command = new UpdateWorkPackageContextCommand(
        WorkPackageId: id,
        CustomerId: FirstNonEmptyGuid(request?.CustomerId),
        ProductId: FirstNonEmptyGuid(request?.ProductId));

    try
    {
        var updated = await _mediator.Send(command, cancellationToken);
        var enriched = await _workPackageQueryService.GetWorkPackageByIdAsync(updated.Id, cancellationToken) ?? updated;

        _logger.LogInformation(
            "Updated context for work package '{WorkPackageId}' (Customer: '{CustomerId}', Product: '{ProductId}').",
            enriched.Id,
            enriched.CustomerId,
            enriched.ProductId);

        return Ok(enriched);
    }
    catch (Exception ex) when (ex is InvalidOperationException or WorkPackageDomainException)
    {
        return CreateBadRequestProblem(ex.Message);
    }
}
```

Request payload class:
```csharp
public sealed class UpdateWorkPackageContextBody
{
    public Guid? CustomerId { get; set; }
    public Guid? ProductId { get; set; }
}
```

## TD-006: Frontend Client Helper & Detail Drawer UI

In [`src/frontend/Cakra.Web/src/api/workpackages.ts`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/workpackages.ts):

```typescript
export interface UpdateWorkPackageContextPayload {
  customerId?: string | null
  productId?: string | null
}

export async function updateWorkPackageContext(
  id: string,
  payload: UpdateWorkPackageContextPayload,
): Promise<WorkPackageDto> {
  const response = await httpClient.put<WorkPackageDto>(`/work-packages/${id}/context`, payload)
  return response.data
}
```

In [`src/frontend/Cakra.Web/src/views/WorkPackageView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue):
- Reactive form state:
  ```typescript
  const contextForm = reactive({
    customerId: '',
    productId: '',
  })
  ```
- Synchronization in `syncDetailForms(wp)`:
  ```typescript
  contextForm.customerId = wp.customerId ?? ''
  contextForm.productId = wp.productId ?? ''
  ```
- Change detection computed:
  ```typescript
  const isContextDirty = computed(() => {
    if (!selectedWorkPackage.value) return false
    const currentCust = selectedWorkPackage.value.customerId ?? ''
    const currentProd = selectedWorkPackage.value.productId ?? ''
    return contextForm.customerId !== currentCust || contextForm.productId !== currentProd
  })
  ```
- Submission handler `handleUpdateContext()`:
  - Invokes `updateWorkPackageContext(wpId, { customerId: contextForm.customerId || null, productId: contextForm.productId || null })`.
  - Enriches `selectedWorkPackage.value` and updates the item in `workPackages.value`.
  - Emits toast / status notification.

---

# 5. Component Responsibilities

| Component | Responsibility |
|---|---|
| `WorkPackage` Aggregate Root | Encapsulates Customer and Product properties, invariant enforcement (`CLOSED` rejection), and domain event creation. |
| `WorkPackageContextChanged` | Immutable domain event capturing previous and new IDs with UTC occurrence timestamp. |
| `WorkPackageRepository` | Executes parameterized Dapper SQL updates to persist modified `CustomerId`, `ProductId`, and `UpdatedAt`. |
| `WorkPackageService` | Orchestrates query service validations (`ICustomerQueryService`, `IProductQueryService`), aggregate mutation, persistence, and event dispatching. |
| `WorkPackagesController` | Handles HTTP PUT requests, normalizes payload GUIDs, maps domain exceptions to RFC 7807 ProblemDetails, and returns enriched DTOs. |
| `workpackages.ts` | Frontend HTTP client wrapper invoking `PUT /api/v1/work-packages/{id}/context`. |
| `WorkPackageView.vue` | Renders interactive Customer and Product selection dropdowns in the Detail Drawer and reflects changes across views. |

---

# 6. Integration Design

| Source | Target | Purpose |
|---|---|---|
| `WorkPackagesController` | `IMediator` | Dispatches `UpdateWorkPackageContextCommand` to the application service pipeline. |
| `WorkPackageService` | `ICustomerQueryService` | Validates that a non-null `CustomerId` exists and is active before updating aggregate. |
| `WorkPackageService` | `IProductQueryService` | Validates that a non-null `ProductId` exists and is active before updating aggregate. |
| `WorkPackageService` | `IWorkPackageRepository` | Reads aggregate and persists changes to `[workpackage].[WorkPackages]`. |
| `WorkPackageService` | `IDomainEventDispatcher` | Dispatches `WorkPackageContextChanged` event to registered in-process handlers. |
| `WorkPackageView.vue` | `updateWorkPackageContext` | Sends asynchronous PUT request from UI detail drawer to backend API. |

---

# 7. Data Ownership

| Data | Owner |
|---|---|
| Work Package Customer Association (`CustomerId`) | `WorkPackage` Aggregate (`workpackage.WorkPackages`) |
| Work Package Product Association (`ProductId`) | `WorkPackage` Aggregate (`workpackage.WorkPackages`) |
| Customer Master Record & Active State | `Customer` Module (`customer.Customers`) |
| Product Master Record & Active State | `Product` Module (`product.Products`) |
| Request Customer & Product Associations | `Request` Aggregate (`request.Requests` — strictly independent) |

---

# 8. Database Design

## New Tables
None.

## Modified Tables
None. Columns `[CustomerId] UNIQUEIDENTIFIER NULL` and `[ProductId] UNIQUEIDENTIFIER NULL` already exist in table `[workpackage].[WorkPackages]`.

## Relationships
- Logical reference: `[workpackage].[WorkPackages].[CustomerId]` references `[customer].[Customers].[Id]` without a physical cross-schema foreign key constraint.
- Logical reference: `[workpackage].[WorkPackages].[ProductId]` references `[product].[Products].[Id]` without a physical cross-schema foreign key constraint.

## Migration Considerations
Zero migrations required.

---

# 9. Cross-Cutting Concerns

1. **Event Dispatching & Audit**:
   - `WorkPackageContextChanged` is recorded on the aggregate and dispatched after successful repository update.
   - Serilog structured logging records the updated Work Package ID, Customer ID, Product ID, and acting person ID.
2. **Cross-Schema Isolation**:
   - Maintains Cakra architectural rule of zero cross-schema foreign keys. References are validated through query service contracts.
3. **Optimistic UI & Error Handling**:
   - The UI disables controls during in-flight requests and catches HTTP 400/404 errors, displaying structured ProblemDetails error messages.

---

# 10. Implementation Constraints

1. **Strict DDD Boundary**: Updating a Work Package's Customer or Product must **never** cascade to or modify existing Requests in the package.
2. **Persistence Integrity**: All database updates must use Dapper parameterized queries (`@CustomerId`, `@ProductId`).
3. **Lifecycle Locking**: Modifying context on a Work Package with status `CLOSED` must be rejected with `WorkPackageDomainException`.
4. **Idempotency & Equality Check**: If the provided Customer and Product IDs match existing values, mutation and event emission are safely skipped.
5. **No Migration Files**: Do not generate unnecessary SQL migration scripts since the columns already exist.

---

# 11. Acceptance Conditions

1. `WorkPackage.cs` contains `UpdateContext(...)` which updates `CustomerId`, `ProductId`, and `UpdatedAt`, emits `WorkPackageContextChanged`, and throws `WorkPackageDomainException` when called on a closed package.
2. `WorkPackageService.cs` validates that non-null Customer and Product IDs represent active entities via their respective query services before mutating the aggregate.
3. `PUT /api/v1/work-packages/{id}/context` is exposed and returns `200 OK` with enriched `WorkPackageDto` when provided with valid IDs or nulls.
4. Calling `PUT /api/v1/work-packages/{id}/context` on a closed package returns `400 Bad Request`.
5. Calling `PUT /api/v1/work-packages/{id}/context` with an inactive or nonexistent Customer or Product returns `400 Bad Request` or `404 Not Found`.
6. Constituent Requests in the Work Package retain their original Customer and Product attributes untouched.
7. `WorkPackageView.vue` Detail Drawer renders Customer and Product selection dropdowns with a `-- None / Unassigned --` option and a working Save button.
8. Saving context updates in `WorkPackageView.vue` immediately updates the drawer summary card and list table without requiring a page reload.
9. All existing unit and integration tests continue to pass, and new tests verify the context update behavior and invariant enforcement.
