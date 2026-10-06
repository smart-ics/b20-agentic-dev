---
Title: Feasibility Assessment for Editing Request Core Attributes in SCR-REQ-003 (CR-018)
Code: CR-018
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-06
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Change being assessed: Implementation of the capability to edit request core attributes (Title, Description, Priority, and Request Type) directly within `SCR-REQ-003: Request Detail`, as formally captured in [CR-018-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-018-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-018-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-018-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md), [post-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/post-domain.md)
- UI LAYOUT: [15-scr-req-003.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/15-scr-req-003.md)
- FEATURES: [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md), [FEAT-AWR-001-observe-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-AWR-001-observe-operational-feed.md)

## Objective

Assess the feasibility, domain boundaries, UI/UX interaction pattern, cross-module synchronization, and planning readiness to:

1. Enable authorized operational actors to edit core request attributes (Title, Description, Priority, and Request Type) from `SCR-REQ-003: Request Detail`.
2. Restrict editing to active lifecycle states (`CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`) while locking closed states (`COMPLETED`, `CANCELLED`, `REJECTED`).
3. Enforce authorization allowing Request Owner, Manager, Administrator (and authenticated staff if unassigned in `CAPTURED`) to update attributes.
4. Expose a REST endpoint `PUT /api/v1/requests/{id}` backed by MediatR command and service handlers.
5. Provide aggregate domain logic on `Request.cs` to mutate attributes and emit `RequestCoreAttributesUpdated`.
6. Ensure consistency with the Operational Feed by synchronizing the original root operational post's title and content via an in-process MediatR notification handler in `Cakra.Modules.Post`.
7. Confirm that database persistence already supports all four fields without requiring schema migrations.

---

# 2. Current State

## Existing Behavior

1. **Frontend Presentation (`Cakra.Web`, `SCR-REQ-003`)**:
   - `RequestDetailView.vue` renders Title and Description within the `Request Detail Card` (`data-testid="request-detail-card"`) in read-only mode.
   - Interactive controls exist for lifecycle actions (Start, Pause, Cancel, Complete), complexity editing, ownership assignment, sub-tasks, and comment discussions, but there is no mechanism to edit the request's title, description, type, or priority.
   - Screen specification `15-scr-req-003.md` specifies "Edit Core Attributes" under Available Actions (Actor: Request Owner, Outcome: Updates authoritative record), but it is currently unfulfilled in the implementation.

2. **Backend API Endpoints (`Cakra.Api`, `RequestsController`)**:
   - `RequestsController.cs` exposes commands for `RecordRequest`, `AssignRequestOwner`, `StartWork`, `PauseWork`, `UpdateRequestComplexity`, `CancelRequest`, `ReviewRequestCompletion`, `ReassignRequestOwnership`, and sub-task operations.
   - No `PUT /api/v1/requests/{id}` endpoint or core attribute update action currently exists.

3. **Domain Layer (`Cakra.Modules.Request`)**:
   - The `Request` aggregate root contains `Title`, `Description`, `RequestType`, and `Priority` properties with private setters.
   - The entity provides mutating domain methods for state transitions, assignments, complexity, and sub-tasks, but no method exists to update `Title`, `Description`, `RequestType`, or `Priority` post-creation.

4. **Persistence Layer (`Cakra.Modules.Request.Persistence`)**:
   - `RequestRepository.cs` contains an `UpdateAsync` method executing an SQL `UPDATE` statement on `[request].[Requests]` that already includes `[Title] = @Title`, `[Description] = @Description`, `[RequestType] = @RequestType`, `[Priority] = @Priority`, and `[UpdatedAt] = @UpdatedAt`.
   - Therefore, the database schema and repository update query already fully support updating these fields without requiring any schema migrations.

5. **Operational Feed Integration (`Cakra.Modules.Post`)**:
   - Upon request recording, `RequestRecordedPostHandler` subscribes to `RequestRecorded` and creates a system post with `title: $"Request: {notification.Title}"`, `content: notification.Description`, and `requestId: notification.RequestId`.
   - No event or handler currently exists to synchronize this root post when request attributes change.

## Existing Constraints

1. **Lifecycle Locking**: Closed requests (`COMPLETED`, `CANCELLED`, `REJECTED`) represent terminal historical records and must be immutable against attribute updates.
2. **Actor Authorization**: Unauthorized actors must be rejected with HTTP 403 Forbidden.
3. **Data Integrity & Validation**: Title is mandatory (max 255 characters, trimmed). Description is mandatory (trimmed). Priority must belong to `['LOW', 'NORMAL', 'HIGH', 'URGENT']`. Request Type must belong to `['GENERAL', 'BUG', 'FEATURE', 'SUPPORT', 'CHANGE_REQUEST', 'INCIDENT']`.
4. **Context Linkage Separation**: Customer, Product, and Work Package linkages are separate relational concerns and must not be altered through this core attributes edit action.
5. **Feed & Audit Cleanliness**: Editing core attributes must update the authoritative record and synchronize the root post, but must not generate extraneous chat/feed spam or redundant assignment logs.

---

# 3. Gap Analysis

| ID | Severity | Gap |
|---|---|---|
| GAP-001 | CRITICAL | Missing backend API endpoint `PUT /api/v1/requests/{id}` to process core attribute updates. |
| GAP-002 | CRITICAL | `Request` aggregate root has no domain method to update core attributes or emit `RequestCoreAttributesUpdated`. |
| GAP-003 | MAJOR | `RequestDetailView.vue` lacks an "Edit" button and "Edit Request Details" modal dialog. |
| GAP-004 | MAJOR | `Cakra.Modules.Post` has no notification handler to synchronize the root operational post when a request is edited. |
| GAP-005 | MINOR | Frontend API client `src/frontend/Cakra.Web/src/api/requests.ts` lacks a typed client method for `updateRequestCoreAttributes`. |

---

# 4. Open Questions

| ID | Question | Impact | Status |
|---|---|---|---|
| OQ-001 | Under which lifecycle states is editing permitted? | Validation & UI button visibility. | CLOSED |
| OQ-002 | Who is authorized to edit request core attributes? | Authorization checks in backend & frontend. | CLOSED |
| OQ-003 | What UI presentation pattern should be used on SCR-REQ-003? | Modal vs inline edit experience. | CLOSED |
| OQ-004 | What fields are included in the edit operation? | Input form fields & command payload. | CLOSED |
| OQ-005 | Should Context Linkages (Customer, Product, Work Package) be included in this modal? | Form complexity & architectural boundaries. | CLOSED |
| OQ-006 | What REST endpoint design should be used? | API contract & conventions. | CLOSED |
| OQ-007 | Should editing generate a feed post or assignment log? | Activity feed signal-to-noise ratio. | CLOSED |
| OQ-008 | Should the root operational post in the feed be synchronized? | Operational feed consistency. | CLOSED |

---

# 5. Assumptions

| ID | Assumption |
|---|---|
| ASM-001 | The database table `[request].[Requests]` and `RequestRepository.UpdateAsync` already map `Title`, `Description`, `Priority`, and `RequestType`, so no database migration is required. |
| ASM-002 | Using an in-process MediatR domain event (`RequestCoreAttributesUpdated`) cleanly preserves the decoupling between `Cakra.Modules.Request` and `Cakra.Modules.Post`. |
| ASM-003 | Each request has an associated root post where `sourceEventType == "RequestRecorded"` created during request recording. |
| ASM-004 | The user interface in `RequestDetailView.vue` can update its local reactive state immediately from the `RequestDto` returned by `PUT /api/v1/requests/{id}`. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| RISK-001 | A user attempts to edit a closed request, violating historical audit integrity. | Corrupted terminal historical state. | Enforce lifecycle validation in the aggregate root throwing `InvalidRequestStateTransitionException`; hide edit trigger on closed states. |
| RISK-002 | Direct coupling introduced between Request and Post modules. | Monolith modularity violation. | Decouple via MediatR in-process notification handler `RequestCoreAttributesUpdatedPostHandler`. |
| RISK-003 | Validation bypass on empty or whitespace strings. | Blank title or description saved to database. | Enforce trimming and required validation on both frontend form and backend domain entity. |

---

# 7. Recommendations

## Option A: MediatR Event-Driven Synchronization with Modal Dialog (Recommended)

1. **Backend**:
   - Implement `PUT /api/v1/requests/{id}` on `RequestsController` accepting `UpdateRequestCoreAttributesBody`.
   - Dispatch `UpdateRequestCoreAttributesCommand` handled by `RequestService`.
   - Add `UpdateCoreAttributes(...)` on aggregate `Request`, mutating properties, updating `UpdatedAt`, and emitting `RequestCoreAttributesUpdated`.
   - Implement `RequestCoreAttributesUpdatedPostHandler` in `Cakra.Modules.Post` to synchronize the root post title and content.
2. **Frontend**:
   - Add typed API function `updateRequestCoreAttributes(id, payload)` in `api/requests.ts`.
   - Add an "Edit" button with a pencil icon in the `Request Detail Card` header next to the title.
   - Render an "Edit Request Details" modal dialog with Title (text, max 255), Description (textarea), Request Type (select), and Priority (select).
   - On submission, close modal, update reactive `request` in-place, and show a success alert banner.

### Advantages
- Clean separation of concerns adhering to modular monolith architecture.
- Reuses existing repository SQL without schema migrations.
- Direct alignment with user alignment decisions from `/grill-me`.
- Non-intrusive modal UI avoiding layout disruption on `SCR-REQ-003`.

### Disadvantages
- Requires adding one domain event handler in `Cakra.Modules.Post`.

## Option B: In-Place Inline Card Editing

- Replace card title and description with form inputs directly on the card.

### Advantages
- Avoids modal backdrop.

### Disadvantages
- Clutters the authoritative detail card and causes layout shifts during editing.
- Dismissed during collaborative alignment in favor of Modal Dialog.

---

# 8. Gap Closure

## GAP-001 & GAP-002 (Backend API & Domain Logic)
### Decision
Implement `PUT /api/v1/requests/{id}` handling `UpdateRequestCoreAttributesCommand(Guid RequestId, string Title, string Description, string Priority, string RequestType, Guid ActorPersonId)`. Add domain method `Request.UpdateCoreAttributes(string title, string description, string priority, string requestType, Guid actorPersonId, DateTime? utcNow = null)` that validates active state, updates fields and `UpdatedAt`, and raises `RequestCoreAttributesUpdated`.
### Rationale
Provides a robust, RESTful, and domain-validated mechanism to update authoritative request attributes.
### Impact
`Cakra.Api` (`RequestsController.cs`), `Cakra.Modules.Request` (`Domain/Request.cs`, `Services/RequestCommands.cs`, `Services/RequestService.cs`, `Domain/Events/RequestCoreAttributesUpdated.cs`).
### Architecture Impact
New endpoint contract and aggregate method.
### Resolved By
User & Analyst
### Resolved Date
2026-10-06

---

## GAP-003 & GAP-005 (Frontend Modal & API Client)
### Decision
Add `updateRequestCoreAttributes` to `src/frontend/Cakra.Web/src/api/requests.ts`. In `RequestDetailView.vue`, add an Edit button (`data-testid="edit-request-button"`) in the card header next to the title (visible when request is active and actor is authorized) that opens an "Edit Request Details" modal (`data-testid="edit-request-modal"`).
### Rationale
Delivers the user-aligned UX pattern while keeping the presentation clean and accessible.
### Impact
`src/frontend/Cakra.Web/src/api/requests.ts`, `src/frontend/Cakra.Web/src/views/RequestDetailView.vue`.
### Architecture Impact
Screen component enhancement for `SCR-REQ-003`.
### Resolved By
User & Analyst
### Resolved Date
2026-10-06

---

## GAP-004 (Operational Feed Synchronization)
### Decision
Create `RequestCoreAttributesUpdatedPostHandler` implementing `INotificationHandler<RequestCoreAttributesUpdated>` in `Cakra.Modules.Post`. When handled, locate the initial root operational post linked to the request (`sourceEventType == "RequestRecorded"` and matching `RequestId`), and update its title to `$"Request: {notification.Title}"` and content to `notification.Description`.
### Rationale
Ensures the Operational Feed and feed streams remain synchronized with the authoritative request record without introducing feed spam.
### Impact
`Cakra.Modules.Post` (`Services/RequestCoreAttributesUpdatedPostHandler.cs`, `PostModule.cs`).
### Architecture Impact
Cross-module eventual consistency via MediatR in-process domain event.
### Resolved By
User & Analyst
### Resolved Date
2026-10-06

---

## OQ-001 through OQ-008 (Closed Alignment Decisions)
### Decision
All open questions are closed as agreed in `/grill-me`:
- OQ-001: Allowed in any active state (`CAPTURED`, `ASSIGNED`, `IN_PROGRESS`, `PAUSED`); prohibited once closed (`COMPLETED`, `CANCELLED`, `REJECTED`).
- OQ-002: Authorized for Request Owner, Manager, Administrator, and authenticated staff if unassigned in `CAPTURED`.
- OQ-003: Modal Dialog opened via Edit button in the card header.
- OQ-004: Fields: Title, Description, Priority, Request Type.
- OQ-005: Context Linkages remain separate.
- OQ-006: `PUT /api/v1/requests/{id}`.
- OQ-007: No feed announcement or assignment log created for edits.
- OQ-008: Synchronize root operational post title and content.
### Rationale
Full alignment achieved through user interview.
### Impact
Eliminates ambiguity for architecture and implementation planning.
### Architecture Impact
Authoritative guidance for target architecture updates.
### Resolved By
User & Analyst
### Resolved Date
2026-10-06

---

# 9. Architecture Applicability

## Decision
ARCHITECTURE-REQUIRED

## Rationale
Architecture definition is required because this change introduces:
1. A new backend REST endpoint contract (`PUT /api/v1/requests/{id}`).
2. A new domain event (`RequestCoreAttributesUpdated`) in `Cakra.Modules.Request`.
3. Cross-module integration in `Cakra.Modules.Post` with an event handler updating post records.
4. Role-based and lifecycle authorization rules spanning frontend presentation and backend command processing.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved
- [x] All required decisions recorded
- [x] All blocking open questions resolved
- [x] Architecture can be finalized or updated (Gate granted by Architect)

## Status

READY-FOR-PLANNING

## Notes

All feasibility analysis and gap closure decisions are completed and approved. The Architect has evaluated the artifact and granted the READY-FOR-PLANNING gate. Target architecture definition may proceed.

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-018-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-018-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md), [post-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/post-domain.md)
- UI LAYOUT: [15-scr-req-003.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/15-scr-req-003.md)

Referenced codebase locations:

- [RequestsController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/RequestsController.cs)
- [Request.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs)
- [RequestService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs)
- [RequestRepository.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Persistence/RequestRepository.cs)
- [RequestRecordedPostHandler.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Post/Services/RequestRecordedPostHandler.cs)
- [RequestDetailView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue)
- [requests.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/requests.ts)
