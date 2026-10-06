---
Title: Edit Request Core Attributes Architecture (CR-018)
Code: CR-018
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-06
---

# 1. Overview

This architecture defines the technical realization for Change Request `CR-018`: Capability to edit request core attributes (Title, Description, Priority, and Request Type) directly within `SCR-REQ-003: Request Detail`.

It consumes and realizes the approved feasibility decisions from [CR-018-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-018-FEASIBILITY-ASSESSMENT.md), establishing:
1. A new backend REST endpoint `PUT /api/v1/requests/{id}` backed by MediatR command handling and domain validation.
2. An aggregate mutation method on `Request.cs` that enforces active lifecycle gating and emits in-process domain event `RequestCoreAttributesUpdated`.
3. An in-process MediatR notification handler in `Cakra.Modules.Post` to synchronize the associated root operational post in the Operational Feed.
4. A modal editing dialog triggered from the Request Detail card header on `SCR-REQ-003` (`RequestDetailView.vue`), complete with typed client integration in `api/requests.ts`.

---

# 2. Architectural Basis

## Business Context

- ISSUE: [CR-018-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-018-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md), [post-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/post-domain.md)
- UI LAYOUT: [15-scr-req-003.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/15-scr-req-003.md)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-018-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-018-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

This architecture consumes and realizes the approved decisions:
- `GAP-001` & `GAP-002`: Backend API endpoint `PUT /api/v1/requests/{id}` and aggregate mutation method `Request.UpdateCoreAttributes(...)`.
- `GAP-003` & `GAP-005`: Header "Edit" button and "Edit Request Details" modal dialog on `SCR-REQ-003` (`RequestDetailView.vue`) with typed client `updateRequestCoreAttributes`.
- `GAP-004`: Root post title and content synchronization via `RequestCoreAttributesUpdatedPostHandler` in `Cakra.Modules.Post`.
- Closed questions `OQ-001` through `OQ-008`: Active states only (`CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`), role-based authorization (Owner, Manager, Admin, and staff in unassigned CAPTURED), exclusion of context linkages, and avoidance of feed/audit noise.

---

# 3. Scope

## Included

1. **Domain Aggregate Mutation (`Cakra.Modules.Request.Domain.Request`)**:
   - Domain method `UpdateCoreAttributes(...)` with active lifecycle validation, parameter normalization, and monotonic `UpdatedAt` updating.
   - Domain event `RequestCoreAttributesUpdated`.
2. **Application Command & Handler (`Cakra.Modules.Request.Services`)**:
   - `UpdateRequestCoreAttributesCommand` and FluentValidation rule set.
   - `IRequestService.UpdateRequestCoreAttributesAsync` implementation.
   - Dispatching domain events through existing `PersistStateChangesAndDispatchAsync`.
3. **Backend API Endpoint (`Cakra.Api.Controllers.RequestsController`)**:
   - `PUT /api/v1/requests/{id}` action accepting `UpdateRequestCoreAttributesBody`.
   - Authorization validation enforcing role and ownership rules.
   - Returning enriched `RequestDto`.
4. **Cross-Module Feed Synchronization (`Cakra.Modules.Post`)**:
   - Notification handler `RequestCoreAttributesUpdatedPostHandler` implementing `INotificationHandler<RequestCoreAttributesUpdated>`.
   - Locating and updating the root system post's title (`$"Request: {notification.Title}"`) and content (`notification.Description`).
   - DI registration in `PostModule.cs`.
5. **Frontend API Client (`src/frontend/Cakra.Web/src/api/requests.ts`)**:
   - TypeScript interface `UpdateRequestCoreAttributesPayload`.
   - Function `updateRequestCoreAttributes(id, payload)` issuing `httpClient.put`.
6. **Frontend Presentation (`src/frontend/Cakra.Web/src/views/RequestDetailView.vue`)**:
   - Computed authorization check `canEditCoreAttributes`.
   - "Edit" trigger button in the Request Detail Card header next to the title.
   - "Edit Request Details" Bootstrap 5 modal dialog with Title input, Description textarea, Type select, and Priority select.
   - Reactive submission handling, in-place state update, and success alert banner.

## Excluded

- Modifying Customer, Product, or Work Package linkages (Context Linkages remain distinct).
- Modifying complexity rating or sub-task management (existing controls are preserved).
- Database migrations or table alterations (existing columns in `[request].[Requests]` are sufficient).
- Emitting new feed announcement posts or creating extraneous assignment audit entries.

---

# 4. Technical Decisions

## TD-001: REST API Contract `PUT /api/v1/requests/{id}`

`RequestsController` will expose an HTTP `PUT` action:

```csharp
[HttpPut("{id:guid}")]
[ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public async Task<IActionResult> UpdateRequestCoreAttributes(
    [FromRoute] Guid id,
    [FromBody] UpdateRequestCoreAttributesBody body,
    CancellationToken cancellationToken)
```

**Request Payload:**

```csharp
public sealed class UpdateRequestCoreAttributesBody
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? RequestType { get; set; }
    public string? Priority { get; set; }
}
```

**Authorization Logic:**
The controller resolves the calling actor's identity via `User` claims. An actor is authorized if:
1. Actor possesses the `ADMINISTRATOR` or `MANAGER` role; OR
2. The request is in `CAPTURED` state and has no `OwnerPersonId` assigned (allowing any authenticated operational user to refine captured details); OR
3. The actor's `PersonId` matches the request's `OwnerPersonId`.

If unauthorized, the endpoint returns `403 Forbidden` with RFC 7807 Problem Details.

## TD-002: Aggregate Domain Mutation & Lifecycle State Machine Gating

In `Cakra.Modules.Request.Domain.Request`:

```csharp
public void UpdateCoreAttributes(
    string title,
    string description,
    string priority,
    string requestType,
    Guid actorPersonId,
    DateTime? utcNow = null)
{
    if (Status.IsClosed())
    {
        throw new InvalidRequestStateTransitionException(
            $"Cannot edit core attributes of closed request '{Id}' in status '{Status}'.",
            Status,
            Status);
    }

    if (string.IsNullOrWhiteSpace(title))
        throw new RequestDomainValidationException("Title cannot be empty.", nameof(title));
    if (string.IsNullOrWhiteSpace(description))
        throw new RequestDomainValidationException("Description cannot be empty.", nameof(description));

    var trimmedTitle = title.Trim();
    if (trimmedTitle.Length > 255)
        throw new RequestDomainValidationException("Title must not exceed 255 characters.", nameof(title));

    var normalizedType = string.IsNullOrWhiteSpace(requestType) ? "GENERAL" : requestType.Trim().ToUpperInvariant();
    var normalizedPriority = string.IsNullOrWhiteSpace(priority) ? "NORMAL" : priority.Trim().ToUpperInvariant();

    var now = utcNow ?? DateTime.UtcNow;

    Title = trimmedTitle;
    Description = description.Trim();
    Priority = normalizedPriority;
    RequestType = normalizedType;
    UpdatedAt = now;

    _domainEvents.Add(new RequestCoreAttributesUpdated(
        requestId: Id,
        title: Title,
        description: Description,
        priority: Priority,
        requestType: RequestType,
        actorPersonId: actorPersonId,
        occurredAtUtc: now));
}
```

**Rationale:** Enforces aggregate invariants directly in the domain model. Any attempt to modify a closed request (`COMPLETED`, `CANCELLED`, `REJECTED`) is rejected at domain level.

## TD-003: Domain Event Definition

A new domain event record is created in `Cakra.Modules.Request.Domain.Events`:

```csharp
namespace Cakra.Modules.Request.Domain.Events;

public sealed record RequestCoreAttributesUpdated(
    Guid RequestId,
    string Title,
    string Description,
    string Priority,
    string RequestType,
    Guid ActorPersonId,
    DateTime OccurredAtUtc) : IDomainEvent;
```

## TD-004: MediatR Command and Service Layer

1. **Command:**
   ```csharp
   public sealed record UpdateRequestCoreAttributesCommand(
       Guid RequestId,
       string Title,
       string Description,
       string Priority,
       string RequestType,
       Guid? ActorPersonId) : IRequest<RequestDto>;
   ```
2. **Validator:** FluentValidation checks:
   - `RequestId` is not empty.
   - `Title` is not empty and maximum length 255.
   - `Description` is not empty.
   - `Priority` must be one of `LOW`, `NORMAL`, `HIGH`, `URGENT`.
   - `RequestType` must be one of `GENERAL`, `BUG`, `FEATURE`, `SUPPORT`, `CHANGE_REQUEST`, `INCIDENT`.
3. **Execution in `RequestService`:**
   - Retrieves the request aggregate via `GetRequiredRequestAsync`.
   - Validates authorization against the resolved actor.
   - Calls `request.UpdateCoreAttributes(...)`.
   - Calls `PersistStateChangesAndDispatchAsync(request, snapshotAssignmentIds, hadResolution, cancellationToken)`.
   - Returns `RequestDto.FromDomain(request)`.

## TD-005: Cross-Module Root Operational Post Synchronization

In `Cakra.Modules.Post.Services`:

```csharp
public sealed class RequestCoreAttributesUpdatedPostHandler : INotificationHandler<RequestCoreAttributesUpdated>
{
    private readonly IPostRepository _postRepository;
    private readonly ILogger<RequestCoreAttributesUpdatedPostHandler> _logger;

    public RequestCoreAttributesUpdatedPostHandler(
        IPostRepository postRepository,
        ILogger<RequestCoreAttributesUpdatedPostHandler>? logger = null)
    {
        _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
        _logger = logger ?? NullLogger<RequestCoreAttributesUpdatedPostHandler>.Instance;
    }

    public async Task Handle(RequestCoreAttributesUpdated notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        // Find root operational post for this request where SourceEventType is "RequestRecorded"
        var posts = await _postRepository.GetByRequestIdAsync(notification.RequestId, cancellationToken);
        var rootPost = posts.FirstOrDefault(p => p.SourceEventType == "RequestRecorded");

        if (rootPost is null)
        {
            _logger.LogWarning("No root operational post found for RequestId {RequestId} to synchronize.", notification.RequestId);
            return;
        }

        rootPost.UpdateContent(
            title: $"Request: {notification.Title}",
            content: notification.Description,
            updatedAtUtc: notification.OccurredAtUtc);

        await _postRepository.UpdateAsync(rootPost, cancellationToken);

        _logger.LogInformation("Successfully synchronized root operational post {PostId} for Request {RequestId}.", rootPost.Id, notification.RequestId);
    }
}
```

*Note:* If `Domain.Post` needs an explicit content update method, `rootPost.UpdateCoreContent(title, content, updatedAtUtc)` is added to the `Post` aggregate.

## TD-006: Frontend Modal UX Pattern on SCR-REQ-003

1. **Edit Button Trigger:**
   Placed in the `card-header` of `Request Detail Card`:
   ```html
   <div class="card-header py-1 px-2 bg-body-tertiary d-flex align-items-center justify-content-between">
     <h2 class="h6 mb-0 fw-bold" data-testid="request-detail-title">{{ request.title }}</h2>
     <button
       v-if="canEditCoreAttributes"
       type="button"
       class="btn btn-outline-secondary btn-sm py-0 px-2"
       style="font-size: 11.5px; height: 24px; line-height: 22px"
       data-testid="edit-request-button"
       @click="openEditModal"
     >
       <i class="bi bi-pencil me-1" aria-hidden="true"></i>Edit
     </button>
   </div>
   ```

2. **Modal Dialog (`edit-request-modal`):**
   - Backdrop-dismissible modal with header `"Edit Request Details"`.
   - Title input (`v-model="editForm.title"`, `maxlength="255"`, required).
   - Description textarea (`v-model="editForm.description"`, rows `4`, required).
   - Request Type dropdown populated from `REQUEST_TYPES`.
   - Priority dropdown populated from `PRIORITIES`.
   - Action buttons: "Cancel" and "Save Changes".

3. **Submission & Reactive Flow:**
   - Trims inputs and verifies validity before dispatching `updateRequestCoreAttributes`.
   - On response: assigns returned `RequestDto` to `request.value`.
   - Sets `actionSuccessMessage = 'Request details updated successfully.'`.
   - Closes modal.

---

# 5. Component Responsibilities

| Component | Responsibility |
|---|---|
| `Cakra.Api.Controllers.RequestsController` | Exposes `PUT /api/v1/requests/{id}`, verifies HTTP authentication/authorization claims, and translates DTOs. |
| `Cakra.Modules.Request.Domain.Request` | Authoritative aggregate root enforcing core attribute invariants, lifecycle gating, and emitting `RequestCoreAttributesUpdated`. |
| `Cakra.Modules.Request.Domain.Events.RequestCoreAttributesUpdated` | Domain event carrying updated title, description, priority, type, actor, and timestamp. |
| `Cakra.Modules.Request.Services.RequestService` | Application service handling `UpdateRequestCoreAttributesCommand`, orchestrating repository persistence and MediatR dispatch. |
| `Cakra.Modules.Post.Services.RequestCoreAttributesUpdatedPostHandler` | MediatR event subscriber synchronizing the associated root operational post in the Post module. |
| `src/frontend/Cakra.Web/src/api/requests.ts` | Frontend HTTP client function for updating core attributes via Axios. |
| `src/frontend/Cakra.Web/src/views/RequestDetailView.vue` | UI component presenting the edit button, modal dialog form, client validation, and updating local reactive state. |

---

# 6. Integration Design

| Source | Target | Purpose |
|---|---|---|
| `RequestDetailView.vue` | `api/requests.ts` (`updateRequestCoreAttributes`) | Dispatches user input from the modal. |
| `api/requests.ts` | `RequestsController` (`PUT /api/v1/requests/{id}`) | Transmits HTTP REST request with JSON payload. |
| `RequestsController` | `IMediator` (`UpdateRequestCoreAttributesCommand`) | Forwards command into the MediatR pipeline. |
| `RequestService` | `Request` aggregate root | Invokes `request.UpdateCoreAttributes(...)`. |
| `RequestService` | `IRequestRepository.UpdateAsync` | Persists updated record to `[request].[Requests]`. |
| `RequestService` | `IPublisher.Publish` (`RequestCoreAttributesUpdated`) | Broadcasts in-process domain event. |
| `IPublisher` | `RequestCoreAttributesUpdatedPostHandler` | Delivers domain event to the Post module subscriber. |
| `RequestCoreAttributesUpdatedPostHandler` | `IPostRepository` | Updates root post's title and content. |

---

# 7. Data Ownership

| Data | Owner |
|---|---|
| Request Title, Description, Priority, RequestType, UpdatedAt | `Cakra.Modules.Request` |
| Operational Feed Post Title, Content, UpdatedAt | `Cakra.Modules.Post` |

Strict bounded context ownership is preserved: `Cakra.Modules.Request` owns the authoritative request record; `Cakra.Modules.Post` owns feed posts and updates its own data in response to domain events.

---

# 8. Database Design

## New Tables
None.

## Modified Tables
None. The existing table `[request].[Requests]` already contains columns:
- `[Title]` (nvarchar(255))
- `[Description]` (nvarchar(max))
- `[RequestType]` (nvarchar(50))
- `[Priority]` (nvarchar(20))
- `[UpdatedAt]` (datetime2)

All columns are already included in `RequestRepository.UpdateAsync`.

## Relationships
No changes to relational schema.

## Migration Considerations
Zero database migrations required.

---

# 9. Cross-Cutting Concerns

## Security & Authorization
- Only authenticated users with appropriate roles (`ADMINISTRATOR`, `MANAGER`), assigned ownership, or authenticated staff during unassigned `CAPTURED` state can execute the update.
- Checked in both frontend presentation (`canEditCoreAttributes`) and enforced authoritatively in backend API / service layer.

## Validation & Data Integrity
- Title: Required, trimmed, max 255 characters.
- Description: Required, trimmed, non-empty.
- Priority: Restrict to allowed domain set (`LOW`, `NORMAL`, `HIGH`, `URGENT`).
- RequestType: Restrict to allowed domain set (`GENERAL`, `BUG`, `FEATURE`, `SUPPORT`, `CHANGE_REQUEST`, `INCIDENT`).
- Status: Disallow editing when request is closed (`COMPLETED`, `CANCELLED`, `REJECTED`).

## Concurrency
- Uses monotonic UTC timestamp updates on `UpdatedAt`.
- Returning the updated `RequestDto` immediately syncs the frontend view.

---

# 10. Implementation Constraints

1. **No Direct Module Coupling:** `Cakra.Modules.Request` must never directly reference `Cakra.Modules.Post` classes or repositories. Integration must flow solely through the MediatR domain event `RequestCoreAttributesUpdated`.
2. **Framework Alignment:** Use existing FluentValidation and MediatR pipeline patterns established in `Cakra.Modules.Request`.
3. **Repository Conventions:** Utilize existing `IRequestRepository.UpdateAsync` method without altering repository interfaces.
4. **Bootstrap 5 UI Conformity:** Modal dialog on `RequestDetailView.vue` must adhere to existing Bootstrap 5 structure, modal classes, and standard dismiss handlers used by `showPauseModal` and `showCancelModal`.

---

# 11. Acceptance Conditions

1. **API Operation:** `PUT /api/v1/requests/{id}` successfully updates Title, Description, Priority, and RequestType when invoked with valid data and authorized credentials.
2. **Lifecycle Gate:** Attempting to update a request in `COMPLETED`, `CANCELLED`, or `REJECTED` state returns HTTP 400 with `InvalidRequestStateTransitionException`.
3. **Authorization Gate:** An unauthorized actor receives HTTP 403 Forbidden.
4. **Post Synchronization:** The root operational post in `[post].[Posts]` associated with the request has its title and content updated to match the new request attributes.
5. **UI Interaction:**
   - The "Edit" button appears in the Request Detail card header for active requests when authorized, and is hidden for closed requests.
   - Clicking "Edit" opens the "Edit Request Details" modal pre-populated with current values.
   - Submitting updates the UI immediately and displays a success alert.
6. **Persistence:** The `[request].[Requests]` database table reflects updated `Title`, `Description`, `Priority`, `RequestType`, and `UpdatedAt` values.
