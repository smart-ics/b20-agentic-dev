---
Title: Work Package Re-Open Capability Architecture (CR-035)
Code: CR-035
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-16
---

# 1. Overview

This architecture defines the technical realization for Change Request `CR-035`: Controlled capability to re-open a previously closed Work Package back into `DRAFT` state when constituent operational requests remain unfinished, enforcing integrity invariants, audit trails, and UI affordances, as formally captured in [CR-035-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-035-ISSUE.md).

It consumes and realizes the approved feasibility decisions from [CR-035-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-035-FEASIBILITY-ASSESSMENT.md), establishing:

1. Aggregate lifecycle state transition from `CLOSED` to `DRAFT` (`CLOSED` $\rightarrow$ `DRAFT`) on [`WorkPackage.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackage.cs) with aggregate mutation method `Reopen(reason, newDeadline, reopenedAtUtc)`.
2. Domain event `WorkPackageReopened` in `Cakra.Modules.WorkPackage.Domain.Events` recording the re-open event, reason, previous closure metadata (`PreviousClosedReason`, `PreviousClosedAt`), and timestamp.
3. Application service orchestration in [`WorkPackageService.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Services/WorkPackageService.cs) and MediatR command `ReopenWorkPackageCommand`, enforcing three strict validation gates:
   - **Authorization Gate**: Caller must be Work Package Owner, Management, or Administrator.
   - **Eligibility Gate**: Package must contain at least one active constituent request with live status not in `['COMPLETED', 'CANCELLED', 'REJECTED']` via `IRequestQueryService`. Packages with zero requests or all completed requests remain permanently closed.
   - **Integrity Gate (Business Rule 9)**: No constituent unfinished request may currently belong to another active or draft Work Package via `IsRequestInActiveWorkPackageAsync`.
4. Dedicated REST API endpoint `POST /api/v1/work-packages/{id}/reopen` in [`WorkPackagesController.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/WorkPackagesController.cs).
5. Frontend API client method `reopenWorkPackage` and `ReopenWorkPackagePayload` in [`workpackages.ts`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/workpackages.ts).
6. Frontend UI affordances on `SCR-WP-001` ([`WorkPackageView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue)):
   - "Re-Open Work Package" button in the Detail Panel action strip (disabled with explanatory tooltip when ineligible).
   - Interactive Re-Open Modal dialog displaying remaining unfinished requests, expired-deadline warning alert, optional updated deadline picker, and mandatory justification reason textarea.
7. Zero database migrations: all required columns and constraints already exist on `[workpackage].[WorkPackages]`.

---

# 2. Architectural Basis

## Business Context

- ISSUE: [CR-035-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-035-ISSUE.md)
- DOMAIN SPECIFICATION: [work-package-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/work-package-domain.md) (v1.2)
- DOMAIN SPECIFICATION: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- PRIOR ARCHITECTURE: [CR-025-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-025-ARCHITECTURE.md)
- UI SPECIFICATION: [Work Package Screen (`SCR-WP-001`)](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-035-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-035-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

This architecture consumes and realizes the approved decisions:
- `GAP-001`: Aggregate mutation method `Reopen(...)` and `CLOSED` $\rightarrow$ `DRAFT` state transition.
- `GAP-002`: Domain event `WorkPackageReopened` capturing prior closure history.
- `GAP-003`: `ReopenWorkPackageCommand`, validator, and 3-gate validation pipeline in `WorkPackageService.cs`.
- `GAP-004`: REST API endpoint `POST /api/v1/work-packages/{id}/reopen`.
- `GAP-005`: Typed API client method in `workpackages.ts`.
- `GAP-006`: Frontend UI controls, eligibility tooltips, and modal dialog on `SCR-WP-001`.
- `GAP-007`: Domain specification updated to v1.2 with Rules 16–20.
- Closed decisions `OQ-001` through `OQ-006`: Destination state strictly `DRAFT`, strict unfinished requests requirement, blocking conflict resolution, aggregate closure field clearing, role authorization, and in-modal deadline override.

---

# 3. Scope

## Included

1. **Domain Aggregate Model (`Cakra.Modules.WorkPackage.Domain`)**:
   - Method `public void Reopen(string reason, DateTime? newDeadline = null, DateTime? reopenedAtUtc = null)` on `WorkPackage.cs`.
   - Update `WorkPackageStatus.cs` documentation and state transition validation.
   - Domain event `WorkPackageReopened` in `Cakra.Modules.WorkPackage.Domain.Events`.
2. **Application Service & MediatR Pipeline (`Cakra.Modules.WorkPackage.Services`)**:
   - Command `ReopenWorkPackageCommand` and FluentValidation validator `ReopenWorkPackageCommandValidator` in `WorkPackageCommands.cs`.
   - Method `ReopenWorkPackageAsync(...)` in `IWorkPackageService` and `WorkPackageService.cs`.
   - Live request status resolution via `IRequestQueryService`.
   - Cross-package active membership conflict check via `IWorkPackageRepository.IsRequestInActiveWorkPackageAsync`.
   - Actor role authorization verification (Owner, Management, Administrator).
3. **API Layer (`Cakra.Api`)**:
   - Endpoint `POST /api/v1/work-packages/{id}/reopen` in `WorkPackagesController.cs`.
   - Request DTO `ReopenWorkPackageRequest`.
4. **Frontend API Client (`Cakra.Web`)**:
   - Interface `ReopenWorkPackagePayload` and function `reopenWorkPackage` in `src/api/workpackages.ts`.
5. **Frontend Screen (`Cakra.Web`)**:
   - Screen `SCR-WP-001` ([`WorkPackageView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue)):
     - Computed properties `canReopen`, `hasUnfinishedRequests`, `reopenDisabledTooltip`.
     - Action button "Re-Open Work Package" in the detail panel action strip.
     - Interactive Re-Open Modal dialog with unfinished request summary, expired-deadline warning alert, optional new deadline picker, and mandatory reason textarea.

## Excluded

- Modifying the database schema (zero migrations required; all columns already exist).
- Allowing re-opening directly to `ACTIVE` (strictly rejected; must transition to `DRAFT`).
- Allowing re-opening for packages where all requests are completed or cancelled (closure is permanent).
- Modifying operational telemetry calculations (`DRAFT` packages are naturally ignored by the Cockpit).

---

# 4. Technical Decisions

## TD-001: Aggregate Lifecycle Mutation & Invariant Enforcement

To enforce domain integrity, the `WorkPackage` aggregate root is extended with the `Reopen` mutation method:

```csharp
namespace Cakra.Modules.WorkPackage.Domain;

public sealed partial class WorkPackage
{
    /// <summary>
    /// Transitions the Work Package state from CLOSED back to DRAFT (Architecture CR-035 §4 TD-001).
    /// </summary>
    /// <param name="reason">Mandatory reason describing why the package was re-opened.</param>
    /// <param name="newDeadline">Optional updated target deadline date.</param>
    /// <param name="reopenedAtUtc">Optional re-open timestamp in UTC.</param>
    public void Reopen(string reason, DateTime? newDeadline = null, DateTime? reopenedAtUtc = null)
    {
        if (Status != WorkPackageStatus.Closed)
        {
            throw new InvalidWorkPackageStateTransitionException(
                Id,
                Status,
                WorkPackageStatus.Draft,
                $"Only CLOSED Work Packages can be re-opened. Current status is '{Status}'.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Re-open reason cannot be null or empty.", nameof(reason));
        }

        var prevClosedReason = ClosedReason;
        var prevClosedAt = ClosedAt;

        Status = WorkPackageStatus.Draft;
        ClosedReason = null;
        ClosedAt = null;

        if (newDeadline.HasValue)
        {
            Deadline = NormalizeDeadline(newDeadline);
        }

        var timestamp = reopenedAtUtc ?? DateTime.UtcNow;
        UpdatedAt = timestamp;

        AddDomainEvent(new WorkPackageReopened(Id, reason.Trim(), prevClosedReason, prevClosedAt, timestamp));
    }
}
```

Key invariant rules:
1. State must currently be `WorkPackageStatus.Closed`; attempting to call `Reopen()` on a `DRAFT` or `ACTIVE` package throws `InvalidWorkPackageStateTransitionException`.
2. `reason` is strictly required; empty or whitespace throws `ArgumentException`.
3. `ClosedReason` and `ClosedAt` are cleared (`null`) on the active aggregate, while their prior values are passed to the domain event.
4. If `newDeadline` is provided, it is normalized to UTC midnight; if null, the existing `Deadline` remains intact.

---

## TD-002: Domain Event `WorkPackageReopened`

A dedicated domain event is introduced in `Cakra.Modules.WorkPackage.Domain.Events`:

```csharp
namespace Cakra.Modules.WorkPackage.Domain.Events;

/// <summary>
/// Domain event published when a closed Work Package is re-opened into DRAFT state (Architecture CR-035 §4 TD-002).
/// </summary>
public sealed record WorkPackageReopened(
    Guid WorkPackageId,
    string Reason,
    string? PreviousClosedReason,
    DateTime? PreviousClosedAt,
    DateTime ReopenedAtUtc) : IDomainEvent;
```

This event ensures downstream subscribers and audit handlers capture both the reactivation justification and the historical closure context without polluting the current aggregate state.

---

## TD-003: Multi-Gate Application Service Orchestration & Business Rule 9 Validation

In `Cakra.Modules.WorkPackage.Services`:

### 1. Command & Validator Definition (`WorkPackageCommands.cs`)

```csharp
/// <summary>
/// Command to transition a <see cref="Domain.WorkPackage"/> from <c>CLOSED</c> back to <c>DRAFT</c> state
/// when unfinished operational requests remain (Architecture CR-035 §4 TD-003).
/// </summary>
public sealed record ReopenWorkPackageCommand(
    Guid WorkPackageId,
    string Reason,
    DateTime? NewDeadline = null) : IRequest<WorkPackageDto>;

public sealed class ReopenWorkPackageCommandValidator : AbstractValidator<ReopenWorkPackageCommand>
{
    public ReopenWorkPackageCommandValidator()
    {
        RuleFor(x => x.WorkPackageId)
            .NotEmpty().WithMessage("Work package ID is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Re-open reason is required.")
            .MaximumLength(1000).WithMessage("Re-open reason must not exceed 1000 characters.");
    }
}
```

### 2. Service Implementation (`WorkPackageService.cs`)

`ReopenWorkPackageAsync` executes a 3-gate validation pipeline:

```csharp
public async Task<WorkPackageDto> ReopenWorkPackageAsync(
    Guid workPackageId,
    string reason,
    DateTime? newDeadline = null,
    CancellationToken cancellationToken = default)
{
    if (workPackageId == Guid.Empty)
    {
        throw new ArgumentException("WorkPackageId cannot be empty.", nameof(workPackageId));
    }

    var workPackage = await GetRequiredWorkPackageAsync(workPackageId, cancellationToken);

    if (workPackage.Status != WorkPackageStatus.Closed)
    {
        throw new InvalidWorkPackageStateTransitionException(
            workPackage.Id,
            workPackage.Status,
            WorkPackageStatus.Draft,
            $"Work Package '{workPackage.Id}' is in status '{workPackage.Status}' and cannot be re-opened. Only CLOSED packages can be re-opened.");
    }

    // Gate 1: Authorization Gate
    var actorPersonId = ResolveActorPersonId(workPackage.OwnerPersonId);
    var isOwner = workPackage.OwnerPersonId == actorPersonId;
    var isLeadership = _currentContextProvider?.CurrentUserRoles?.Any(r => r is "Administrator" or "Management") ?? false;

    if (!isOwner && !isLeadership)
    {
        throw new UnauthorizedAccessException(
            $"Actor '{actorPersonId}' is not authorized to re-open Work Package '{workPackage.Id}'. Re-opening is restricted to the Work Package Owner, Management, or Administrator.");
    }

    // Gate 2: Eligibility Gate (Unfinished Requests Check)
    var activeMemberships = workPackage.ActiveRequests.ToList();
    if (activeMemberships.Count == 0)
    {
        throw new WorkPackageDomainException(
            $"Work Package '{workPackage.Id}' has no constituent requests and cannot be re-opened. Closure is permanent.");
    }

    var unfinishedRequests = new List<RequestDto>();
    foreach (var membership in activeMemberships)
    {
        var requestDto = _requestQueryService is not null
            ? await _requestQueryService.GetRequestByIdAsync(membership.RequestId, cancellationToken)
            : null;

        var status = requestDto?.Status?.ToUpperInvariant() ?? string.Empty;
        var isFinished = status is "COMPLETED" or "CANCELLED" or "REJECTED";

        if (!isFinished && requestDto is not null)
        {
            unfinishedRequests.Add(requestDto);
        }
    }

    if (unfinishedRequests.Count == 0)
    {
        throw new WorkPackageDomainException(
            $"Work Package '{workPackage.Id}' contains no unfinished requests. All constituent requests are completed or cancelled. Closure is permanent.");
    }

    // Gate 3: Integrity Gate (Business Rule 9 Check)
    var conflictingRequests = new List<string>();
    foreach (var req in unfinishedRequests)
    {
        var isClaimed = await _workPackageRepository.IsRequestInActiveWorkPackageAsync(
            req.Id,
            workPackage.Id,
            cancellationToken);

        if (isClaimed)
        {
            conflictingRequests.Add($"'{req.Title}' ({req.Id})");
        }
    }

    if (conflictingRequests.Count > 0)
    {
        throw new InvalidOperationException(
            $"Cannot re-open Work Package '{workPackage.Id}': The following unfinished request(s) are currently assigned to another active or draft Work Package: {string.Join(", ", conflictingRequests)}. Resolve the conflicts before re-opening.");
    }

    // Mutation & Persistence
    var now = UtcNow;
    workPackage.Reopen(reason, newDeadline, now);

    await _workPackageRepository.UpdateAsync(workPackage, cancellationToken);
    await DispatchDomainEventsAsync(workPackage, cancellationToken);

    _logger.LogInformation(
        "WorkPackage {WorkPackageId} re-opened to DRAFT with reason '{Reason}' by ActorPersonId {ActorPersonId}",
        workPackage.Id,
        reason,
        actorPersonId);

    return WorkPackageDto.FromDomain(workPackage);
}
```

---

## TD-004: REST API Endpoint & Request Model

In `WorkPackagesController.cs`:

```csharp
/// <summary>
/// Re-opens a closed Work Package back to DRAFT state when unfinished operational requests remain
/// (Architecture CR-035 §4 TD-004).
/// </summary>
[HttpPost("{id:guid}/reopen")]
[ProducesResponseType(typeof(WorkPackageDto), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<ActionResult<WorkPackageDto>> ReopenWorkPackage(
    Guid id,
    [FromBody] ReopenWorkPackageRequest request,
    CancellationToken cancellationToken)
{
    if (request is null || string.IsNullOrWhiteSpace(request.Reason))
    {
        return BadRequest(new { message = "Re-open reason is required." });
    }

    try
    {
        var result = await _sender.Send(
            new ReopenWorkPackageCommand(id, request.Reason, request.Deadline),
            cancellationToken);
        return Ok(result);
    }
    catch (UnauthorizedAccessException ex)
    {
        return Unauthorized(new { message = ex.Message });
    }
    catch (InvalidWorkPackageStateTransitionException ex)
    {
        return BadRequest(new { message = ex.Message });
    }
    catch (WorkPackageDomainException ex)
    {
        return BadRequest(new { message = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return BadRequest(new { message = ex.Message });
    }
}

public sealed record ReopenWorkPackageRequest(string Reason, DateTime? Deadline = null);
```

---

## TD-005: Frontend Client API Module (`workpackages.ts`)

In `src/frontend/Cakra.Web/src/api/workpackages.ts`:

```typescript
export interface ReopenWorkPackagePayload {
  reason: string
  deadline?: string | null
}

/**
 * Re-opens a CLOSED work package back into DRAFT state (Architecture CR-035 §4 TD-005).
 */
export async function reopenWorkPackage(
  id: string,
  payload: ReopenWorkPackagePayload,
): Promise<WorkPackageDto> {
  const response = await httpClient.post<WorkPackageDto>(`/work-packages/${id}/reopen`, payload)
  return response.data
}
```

---

## TD-006: Frontend UI Interaction Design (`WorkPackageView.vue`)

In `src/frontend/Cakra.Web/src/views/WorkPackageView.vue`:

### 1. Computed Authorization & Eligibility

```typescript
const isSelectedClosed = computed(() => selectedStatus.value === 'CLOSED')

const canReopen = computed(() => {
  if (!selectedWorkPackage.value || !isSelectedClosed.value) return false
  const roles = authStore.roles || []
  const isLeadership = roles.includes('Administrator') || roles.includes('Management')
  const isOwner = selectedWorkPackage.value.ownerPersonId === authStore.currentPersonId
  return isLeadership || isOwner
})

const hasUnfinishedRequests = computed(() => {
  if (!activeScopeItems.value || activeScopeItems.value.length === 0) return false
  const finishedStatuses = ['COMPLETED', 'CANCELLED', 'REJECTED']
  return activeScopeItems.value.some(
    (item) => !finishedStatuses.includes((item.status || item.requestStatus || '').toUpperCase()),
  )
})

const isReopenEligible = computed(() => hasUnfinishedRequests.value)

const reopenDisabledTooltip = computed(() => {
  if (!isSelectedClosed.value) return ''
  if (!hasUnfinishedRequests.value) {
    return 'Cannot re-open: all requests in this package are completed or cancelled.'
  }
  return ''
})
```

### 2. Action Strip Button Presentation

In the detail panel's lifecycle action strip:

```html
<div v-if="isSelectedClosed && canReopen" class="flex items-center gap-2">
  <button
    type="button"
    class="btn btn-warning text-dark flex items-center gap-1.5"
    :disabled="!isReopenEligible || isSubmitting"
    :title="reopenDisabledTooltip"
    @click="openReopenModal"
    data-testid="wp-reopen-button"
  >
    <i class="bi bi-arrow-counterclockwise"></i>
    <span>Re-Open Work Package</span>
  </button>
  <span v-if="!isReopenEligible" class="text-xs text-slate-500 italic">
    Permanently sealed (all requests completed)
  </span>
</div>
```

### 3. Re-Open Modal Dialog

The modal contains:
- **Header**: "Re-Open Work Package to DRAFT"
- **Context Card**: Displays package name, objective, and the number of unfinished requests remaining in scope.
- **Expired Deadline Warning**: If `selectedWorkPackage.deadline && new Date(selectedWorkPackage.deadline) < new Date()`, displays an alert banner:
  > ⚠️ **Target Deadline Expired:** The previous deadline (`{formatDate(deadline)}`) has passed. Setting a revised deadline below is recommended to avoid an immediate deadline breach upon reactivation.
- **Deadline Input**: Optional date picker pre-filled with the existing deadline, allowing immediate adjustment or clearing.
- **Justification Textarea**: Mandatory justification reason field with minimum length validation.
- **Action Buttons**: "Cancel" and "Confirm Re-Open" (with loading spinner and error alert).

---

# 5. Component Responsibilities

| Component | Responsibility |
| :--- | :--- |
| **`WorkPackage` Aggregate Root** | Enforces lifecycle transition `CLOSED` $\rightarrow$ `DRAFT`, clears `ClosedReason`/`ClosedAt`, normalizes optional `Deadline`, emits `WorkPackageReopened`. |
| **`WorkPackageStatus`** | Defines canonical status enum and transition helpers. |
| **`WorkPackageReopened`** | Domain event recording re-open justification and historical closure metadata. |
| **`WorkPackageService`** | Orchestrates 3 validation gates (Authorization, Eligibility, BR-9 Conflict), executes aggregate mutation, persists changes, dispatches events. |
| **`ReopenWorkPackageCommand` & Validator** | FluentValidation contract enforcing required fields. |
| **`WorkPackagesController`** | Exposes `POST /api/v1/work-packages/{id}/reopen` and maps domain exceptions to standard HTTP error codes. |
| **`workpackages.ts`** | Frontend TypeScript API client for the re-open endpoint. |
| **`WorkPackageView.vue` (`SCR-WP-001`)** | Evaluates eligibility, renders the Re-Open button with tooltip, and presents the interactive Re-Open modal dialog. |

---

# 6. Integration Design

| Source Component | Target Component | Purpose |
| :--- | :--- | :--- |
| `WorkPackageView.vue` | `workpackages.ts` | Dispatches `reopenWorkPackage(id, { reason, deadline })` upon modal confirmation. |
| `workpackages.ts` | `WorkPackagesController.cs` | Issues HTTP `POST /api/v1/work-packages/{id}/reopen`. |
| `WorkPackagesController.cs` | `WorkPackageService.cs` (MediatR) | Sends `ReopenWorkPackageCommand`. |
| `WorkPackageService.cs` | `IRequestQueryService` | Synchronously queries live request statuses to evaluate the Eligibility Gate. |
| `WorkPackageService.cs` | `IWorkPackageRepository` | Queries `IsRequestInActiveWorkPackageAsync` to enforce Business Rule 9 integrity. |
| `WorkPackageService.cs` | `WorkPackage` Aggregate | Calls `workPackage.Reopen(reason, newDeadline, now)`. |
| `WorkPackageService.cs` | `IWorkPackageRepository` | Updates the aggregate record in `[workpackage].[WorkPackages]`. |
| `WorkPackageService.cs` | `IDomainEventDispatcher` | Dispatches `WorkPackageReopened` event to registered handlers and loggers. |

---

# 7. Data Ownership

| Data Concept | Authoritative Owner |
| :--- | :--- |
| **Work Package Lifecycle Status & Closure Metadata** | `Cakra.Modules.WorkPackage.Domain.WorkPackage` |
| **Work Package Scope Memberships** | `[workpackage].[WorkPackageRequests]` |
| **Request Operational Lifecycle Status** | `Cakra.Modules.Request.Domain.Request` |
| **Re-Open Audit Record & Domain Event** | `WorkPackageReopened` event stream and audit logs |

---

# 8. Database Design

## New Tables

*None.* No new tables are required.

## Modified Tables

*None.* The existing schema of `[workpackage].[WorkPackages]` already contains:
- `[Status] VARCHAR(20) NOT NULL` (updated to `'DRAFT'`)
- `[ClosedReason] NVARCHAR(MAX) NULL` (updated to `NULL`)
- `[ClosedAt] DATETIME2 NULL` (updated to `NULL`)
- `[Deadline] DATETIME2 NULL` (optionally updated to `@Deadline`)
- `[UpdatedAt] DATETIME2 NOT NULL` (updated to UTC now)

## Relationships

Existing foreign key relationships between `[workpackage].[WorkPackageRequests]` and `[workpackage].[WorkPackages]` remain unchanged.

## Migration Considerations

Zero database schema migrations (`.sql` scripts) are required. All queries utilize existing columns and indices.

---

# 9. Cross-Cutting Concerns

1. **Security & Authorization**:
   - Only the Work Package Owner, Management, or Administrator can invoke `POST /api/v1/work-packages/{id}/reopen`.
   - Unauthorized attempts return HTTP 401/403.
2. **Auditability & Traceability**:
   - Every re-open action publishes `WorkPackageReopened`, logging the actor, reason, previous closure reason, and timestamps to the system audit logger.
3. **Data Integrity (Business Rule 9)**:
   - Synchronous transactional validation ensures a request is never simultaneously active in more than one Work Package.
4. **Operations Cockpit Isolation**:
   - Re-opening into `DRAFT` isolates the package from active operations telemetry (`GetOperationsCockpitQueryHandler`), preventing premature workload or capacity skew until deliberate reactivation.

---

# 10. Implementation Constraints

1. **Strict Target State**: Must transition `CLOSED` $\rightarrow$ `DRAFT`. Under no circumstances may a package transition directly from `CLOSED` to `ACTIVE`.
2. **Synchronous Request Status Resolution**: Must resolve live request statuses via `IRequestQueryService` to eliminate stale read-model race conditions.
3. **Zero Migration Rule**: Do not create or run any SQL migration scripts.
4. **Dapper & SQL Consistency**: Repository updates must use explicit parameterized SQL consistent with existing Dapper patterns in `WorkPackageRepository.cs`.
5. **Selector Preservation**: Preserve all existing `data-testid` and `data-screen-id` attributes on `SCR-WP-001`.

---

# 11. Acceptance Conditions

Implementation is architecturally complete when:

1. `WorkPackage.cs` permits `CLOSED` $\rightarrow$ `DRAFT` transition via `Reopen(reason, newDeadline, reopenedAtUtc)`, correctly clearing `ClosedReason` and `ClosedAt`.
2. Calling `Reopen()` on a package with status other than `CLOSED` throws `InvalidWorkPackageStateTransitionException`.
3. `WorkPackageReopened` domain event is emitted and logged.
4. `WorkPackageService.ReopenWorkPackageAsync` enforces:
   - Authorization: Non-owners without leadership roles are rejected.
   - Eligibility: Packages with 0 requests or only completed/cancelled requests are rejected.
   - Business Rule 9: Packages with constituent requests claimed by other active/draft packages are rejected with explicit conflict details.
5. Endpoint `POST /api/v1/work-packages/{id}/reopen` accepts valid payloads and returns HTTP 200 with updated `WorkPackageDto`.
6. `workpackages.ts` exposes `reopenWorkPackage` typed function.
7. `WorkPackageView.vue` displays "Re-Open Work Package" when viewing a closed package, shows disabled tooltip when ineligible, and provides an interactive modal with expired-deadline warning and reason capture.
8. Full suite of unit and integration tests passes across domain, service, and API layers.
