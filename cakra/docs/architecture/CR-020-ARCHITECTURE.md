---
Title: Optional Target Deadline for Request Aggregate and Screens Architecture (CR-020)
Code: CR-020
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-06
---

# 1. Overview

This architecture defines the technical realization for Change Request `CR-020`: Optional target deadline for operational requests across domain models, database persistence, REST endpoints, audit logging, and frontend screens.

It consumes and realizes the approved feasibility decisions from [CR-020-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-020-FEASIBILITY-ASSESSMENT.md), establishing:

1. Database schema migration `0016_add_request_deadline.sql` adding column `[Deadline] DATETIME2 NULL` to `[request].[Requests]`.
2. Property `Deadline` (`DateTime?`, UTC) on the `Request` aggregate root with date-only normalization and active lifecycle state gating.
3. Automated audit logging on `RequestAssignment` whenever the deadline is set, modified, or cleared, capturing timeline entries with descriptive notes.
4. Updates to `RequestRepository`, MediatR commands, domain events, query read models, and REST endpoints.
5. Frontend UI integration across request creation forms (`CreateRequestModal.vue`, `CreateRequestView.vue`), the Request Detail information card, the Edit Request modal dialog, and dynamic "Overdue" badge calculation on `SCR-REQ-003`.

---

# 2. Architectural Basis

## Business Context

- ISSUE: [CR-020-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-020-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- UI LAYOUT: [15-scr-req-003.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/15-scr-req-003.md)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-020-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-020-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

This architecture consumes and realizes the approved decisions:
- `GAP-001`: Database migration `0016_add_request_deadline.sql`.
- `GAP-002` & `GAP-008`: Aggregate property `Deadline`, lifecycle invariant enforcement, and automated `RequestAssignment` timeline audit entries upon deadline mutation.
- `GAP-003`, `GAP-004`, `GAP-005`: Persistence queries, MediatR commands, domain events, DTOs, and REST API controller mappings.
- `GAP-006` & `GAP-007`: Frontend inputs in creation flows, Request Detail card display, Edit modal controls, and dynamic overdue badge evaluation.
- Closed decisions `OQ-001` through `OQ-006`: Date-only UTC semantics, active lifecycle gating, nullability/clearing support, red overdue badge when open and past target date, and frontend filtering/sorting.

---

# 3. Scope

## Included

1. **Database Schema Migration (`Cakra.Api/Migrations/Scripts/0016_add_request_deadline.sql`)**:
   - Idempotent script adding `[Deadline] DATETIME2 NULL` and nonclustered index `[IX_Requests_Deadline]`.
2. **Domain Aggregate Mutation (`Cakra.Modules.Request.Domain.Request`)**:
   - Public property `DateTime? Deadline { get; private set; }`.
   - Normalization helper ensuring stored values are UTC date-only (midnight UTC `00:00:00Z`).
   - `Record(...)` parameter `DateTime? deadline = null`.
   - `UpdateCoreAttributes(...)` parameter `DateTime? deadline = null`.
   - Conditional creation and appending of `RequestAssignment` when deadline value changes.
   - Domain events `RequestRecorded` and `RequestCoreAttributesUpdated` extended with `DateTime? Deadline`.
3. **Persistence Layer (`Cakra.Modules.Request.Persistence.RequestRepository`)**:
   - Update `SELECT`, `INSERT INTO [request].[Requests]`, and `UPDATE [request].[Requests]` queries to include `Deadline`.
   - Update aggregate hydration method `HydrateFromRow` to populate `Deadline`.
4. **Application Commands & Handlers (`Cakra.Modules.Request.Services`)**:
   - Update `RecordRequestCommand` with `DateTime? Deadline`.
   - Update `UpdateRequestCoreAttributesCommand` with `DateTime? Deadline`.
   - Update `RequestService.RecordRequestAsync` and `RequestService.UpdateRequestCoreAttributesAsync`.
5. **Read Models & API Contracts (`Cakra.Modules.Request.Models` & `Cakra.Api`)**:
   - `RequestDto` and `RequestDetailDto` extended with `DateTime? Deadline`.
   - `RequestsController` request body models and actions updated to accept and return `Deadline`.
6. **Frontend API Client (`src/frontend/Cakra.Web/src/api/requests.ts`)**:
   - Add `deadline?: string | null` to request DTO interfaces and payload types.
7. **Frontend Screens & Components (`src/frontend/Cakra.Web`)**:
   - `CreateRequestModal.vue` & `CreateRequestView.vue`: Optional date input `form.deadline`.
   - `RequestDetailView.vue`:
     - Render formatted Deadline in Request Information card.
     - Compute and display a red `Overdue` badge when open (`Status` not in `COMPLETED`, `CANCELLED`) and `Deadline < Today`.
     - Add date input with clear button in the Edit Request Details modal dialog.
     - State history timeline automatically surfaces the `RequestAssignment` audit entry for deadline modifications.

## Excluded

- Dedicated separate endpoints for modifying deadline alone (mutations are processed via `UpdateCoreAttributes` command).
- Backend query filter parameters for overdue or date ranges (frontend filtering and sorting is sufficient).
- Modifying other modules (`Organization`, `Customer`, `Product`, `WorkPackage`, `Post`).
- Negative validation preventing past dates on entry (target dates are informational).

---

# 4. Technical Decisions

## TD-001: Database Schema & Migration `0016_add_request_deadline.sql`

A new DbUp migration script `0016_add_request_deadline.sql` will be added:

```sql
IF OBJECT_ID(N'[request].[Requests]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[request].[Requests]', N'Deadline') IS NULL
    BEGIN
        ALTER TABLE [request].[Requests]
            ADD [Deadline] DATETIME2 NULL;
    END;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes 
        WHERE name = N'IX_Requests_Deadline' 
          AND object_id = OBJECT_ID(N'[request].[Requests]')
    )
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_Requests_Deadline] 
            ON [request].[Requests] ([Deadline])
            WHERE [Deadline] IS NOT NULL;
    END;
END;
```

## TD-002: Domain Normalization and Lifecycle Invariants

In `Request.cs`:
- A helper method normalizes any provided `DateTime?`:
  ```csharp
  private static DateTime? NormalizeDeadline(DateTime? deadline)
  {
      if (!deadline.HasValue) return null;
      var d = deadline.Value;
      return new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc);
  }
  ```
- `UpdateCoreAttributes(...)` signature:
  ```csharp
  public void UpdateCoreAttributes(
      string title,
      string description,
      string priority,
      string requestType,
      Guid actorPersonId,
      DateTime? deadline = null,
      DateTime? utcNow = null)
  ```
- Lifecycle invariant: If `Status.IsClosed()` (i.e. `Status == RequestStatus.Completed || Status == RequestStatus.Cancelled`), throws `InvalidRequestStateTransitionException`.

## TD-003: Automated Timeline Audit Trail on Deadline Change

Inside `Request.UpdateCoreAttributes(...)`, changes to the normalized deadline trigger an audit record:

```csharp
var normalizedNewDeadline = NormalizeDeadline(deadline);
var deadlineChanged = normalizedNewDeadline != Deadline;

if (deadlineChanged)
{
    string note = (Deadline, normalizedNewDeadline) switch
    {
        (null, { } newDate) => $"Deadline set to {newDate:yyyy-MM-dd}",
        ({ } oldDate, { } newDate) => $"Deadline changed from {oldDate:yyyy-MM-dd} to {newDate:yyyy-MM-dd}",
        ({ }, null) => "Deadline cleared",
        _ => "Deadline updated"
    };

    var assignment = RequestAssignment.Create(
        requestId: Id,
        previousOwnerPersonId: OwnerPersonId,
        assignedOwnerPersonId: OwnerPersonId,
        actorPersonId: actorPersonId,
        previousStatus: Status,
        newStatus: Status,
        assignedAtUtc: now,
        notes: note);

    _assignments.Add(assignment);
}

Deadline = normalizedNewDeadline;
```

This seamlessly utilizes the existing `_assignments` collection, guaranteeing that the change is persisted to `[request].[RequestAssignments]` and rendered automatically on the Request Detail timeline without schema alterations to assignments.

## TD-004: REST API & DTO Contracts

1. **`RequestDto` and `RequestDetailDto`**:
   Add property:
   ```csharp
   public DateTime? Deadline { get; init; }
   ```
2. **`RecordRequestCommand`**:
   ```csharp
   public sealed record RecordRequestCommand(
       string Title,
       string? Description,
       string RequestType,
       Guid ActorPersonId,
       Guid? CustomerId = null,
       Guid? ProductId = null,
       Guid? WorkPackageId = null,
       string? Priority = null,
       int? Complexity = null,
       DateTime? Deadline = null) : IRequest<RequestDto>;
   ```
3. **`UpdateRequestCoreAttributesCommand`**:
   ```csharp
   public sealed record UpdateRequestCoreAttributesCommand(
       Guid RequestId,
       string Title,
       string Description,
       string Priority,
       string RequestType,
       Guid ActorPersonId,
       DateTime? Deadline = null) : IRequest<RequestDto>;
   ```
4. **`RequestsController`**:
   - `RecordRequestBody` includes `public DateTime? Deadline { get; set; }`.
   - `UpdateRequestCoreAttributesBody` includes `public DateTime? Deadline { get; set; }`.
   - Endpoints map `body.Deadline` to the respective commands.

## TD-005: Frontend Date Handling & Overdue Presentation

1. **Date Format & Transmission**:
   - Date pickers bind to ISO string `YYYY-MM-DD` (e.g. `form.deadline = '2026-10-15'`).
   - If empty, transmitted as `null`.
2. **Overdue Computation (`RequestDetailView.vue`)**:
   ```typescript
   const isOverdue = computed(() => {
     if (!request.value?.deadline) return false
     const status = request.value.status
     if (status === 'COMPLETED' || status === 'CANCELLED') return false
     const deadlineDate = new Date(request.value.deadline)
     const today = new Date()
     today.setHours(0, 0, 0, 0)
     deadlineDate.setHours(0, 0, 0, 0)
     return deadlineDate < today
   })
   ```
3. **Visual Badging**:
   - In Request Information card:
     ```html
     <div class="d-flex align-items-center gap-2">
       <span>{{ formatDeadline(request.deadline) }}</span>
       <span v-if="isOverdue" class="badge bg-danger" data-testid="request-overdue-badge">
         <i class="bi bi-exclamation-triangle-fill me-1"></i>Overdue
       </span>
     </div>
     ```
4. **Edit Modal**:
   - Contains `<input type="date" id="editDeadlineInput" v-model="editForm.deadline" class="form-control" data-testid="edit-deadline-input">`.
   - Includes a "Clear Deadline" button or icon allowing users to reset `editForm.deadline = null`.

---

# 5. Component Responsibilities

| Component | Responsibility |
|---|---|
| `0016_add_request_deadline.sql` | Adds nullable column `Deadline` and nonclustered index to `[request].[Requests]`. |
| `Request.cs` | Aggregate root owning `Deadline`, UTC normalization, lifecycle gating, and automated `RequestAssignment` logging. |
| `RequestRepository.cs` | Dapper persistence mapping for `[Deadline]` in queries and entity hydration. |
| `RequestCommands.cs` | Defines `RecordRequestCommand` and `UpdateRequestCoreAttributesCommand` with optional `Deadline`. |
| `RequestService.cs` | Handles commands, coordinates repository calls, and persists state changes. |
| `RequestsController.cs` | Exposes REST endpoints mapping JSON payloads with `deadline` to MediatR commands. |
| `RequestDto.cs` | DTO exposing `Deadline` to client consumers. |
| `api/requests.ts` | Frontend HTTP client typing and functions for creating/updating requests with deadline. |
| `CreateRequestModal.vue` / `CreateRequestView.vue` | UI forms providing optional deadline date input during request creation. |
| `RequestDetailView.vue` | Renders deadline, overdue warning badge, edit modal controls, and state history timeline. |

---

# 6. Integration Design

| Source | Target | Purpose |
|---|---|---|
| `CreateRequestModal.vue` / `CreateRequestView.vue` | `RequestsController.RecordRequest` | Submits new request payload containing optional `deadline`. |
| `RequestDetailView.vue` (Edit Modal) | `RequestsController.UpdateRequestCoreAttributes` | Submits updated core attributes including modified or cleared `deadline`. |
| `RequestsController` | `IMediator` | Dispatches `RecordRequestCommand` or `UpdateRequestCoreAttributesCommand`. |
| `RequestService` | `Request` aggregate root | Invokes `Request.Record(...)` or `Request.UpdateCoreAttributes(...)`. |
| `Request` aggregate root | `RequestAssignment` | Appends new audit record to `_assignments` when `deadline` is modified. |
| `RequestService` | `IRequestRepository` | Persists aggregate updates to SQL Server database via Dapper. |
| `RequestDetailView.vue` | `RequestDto` | Renders deadline value, evaluates overdue status, and shows audit note in timeline. |

---

# 7. Data Ownership

| Data Element | Owner | Storage Location |
|---|---|---|
| `Request.Deadline` | `Cakra.Modules.Request` | Column `[request].[Requests].[Deadline]` (`DATETIME2 NULL`) |
| Deadline audit note | `Cakra.Modules.Request` | Column `[request].[RequestAssignments].[Notes]` (`NVARCHAR(MAX) NULL`) |

---

# 8. Database Design

## Modified Tables

| Table | Change |
|---|---|
| `[request].[Requests]` | Add column `[Deadline] DATETIME2 NULL`, add filtered nonclustered index `[IX_Requests_Deadline]`. |

## Relationships

No new cross-table foreign key constraints are introduced. `Deadline` is a direct attribute on `[request].[Requests]`. Audit entries reside in existing table `[request].[RequestAssignments]`.

## Migration Considerations

- Migration script is strictly idempotent using `OBJECT_ID`, `COL_LENGTH`, and `sys.indexes` guards.
- Column is nullable (`NULL`), so all existing historical records remain valid without backfill requirements.

---

# 9. Cross-Cutting Concerns

1. **Timezone Normalization**:
   All deadline dates are normalized to midnight UTC (`00:00:00Z`). Clients parse dates in local timezone components to ensure date-only consistency without off-by-one errors.
2. **Audit Integrity**:
   Every mutation to `Deadline` on an existing request is captured in `RequestAssignment` with status preserved and a descriptive human-readable note.
3. **Modularity**:
   Zero foreign keys or direct module dependencies are introduced outside `Cakra.Modules.Request`.

---

# 10. Implementation Constraints

- Must use Dapper with explicit column mapping in `RequestRepository.cs`.
- Must adhere to SQL Server database migration standards via DbUp.
- Must preserve existing unit tests and integration tests in `Cakra.Modules.Request` and `Cakra.Api`.
- Frontend date pickers must support both entry and clearing of the deadline.

---

# 11. Acceptance Conditions

1. Database migration `0016_add_request_deadline.sql` executes cleanly and idempotently, adding `[Deadline] DATETIME2 NULL`.
2. `Request.Record(...)` successfully stores the optional deadline normalized to UTC midnight.
3. `Request.UpdateCoreAttributes(...)` allows modifying or clearing (`null`) the deadline when the request is active, and rejects edits with `InvalidRequestStateTransitionException` when closed.
4. An audit `RequestAssignment` is appended and saved whenever the deadline changes, displaying the formatted note in the state history timeline.
5. `POST /api/v1/requests` and `PUT /api/v1/requests/{id}/core-attributes` accept and return the deadline in `RequestDto`.
6. `CreateRequestModal.vue` and `CreateRequestView.vue` allow capturing a target deadline upon creation.
7. `RequestDetailView.vue` displays the deadline, calculates and shows the red "Overdue" badge when appropriate, and allows editing or clearing the deadline via the Edit Request Details modal.
8. All backend and frontend test suites pass with zero regressions.
