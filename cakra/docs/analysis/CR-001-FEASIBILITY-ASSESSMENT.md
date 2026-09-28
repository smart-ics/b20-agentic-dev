---
Title: Feasibility Assessment for Revoking Direct Creation of Operational Posts (CR-001)
Code: CR-001
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-09-29
Status: NOT-READY
---

# 1. Request Summary

Feature being assessed: Revocation and decommissioning of direct user creation of operational posts (`FEAT-FCOL-003`, `SC-FCOL-003`, `UC-FCOL-003`, `UJ-FCOL-003`, `SCR-POST-002`) and ensuring operational posts in the Operational Feed originate solely from Request creation (and associated request lifecycle events), per Product Owner issue `CR-001`.

Referenced artifacts:

- DOMAIN: [post-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/post-domain.md), [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- FEATURE: [FEAT-FCOL-003-create-operational-post.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-FCOL-003-create-operational-post.md), [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md), [FEAT-AWR-001-observe-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-AWR-001-observe-operational-feed.md)
- ISSUE: [CR-001-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-001-ISSUE.md)
- SCENARIOS: [feed-collaboration-scenarios.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/scenarios/feed-collaboration-scenarios.md) (`SC-FCOL-003`)
- USE CASES: [feed-collaboration-use-cases.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/use-cases/feed-collaboration-use-cases.md) (`UC-FCOL-003`)
- USER JOURNEYS: [feed-collaboration-user-journeys.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/user-journey/feed-collaboration-user-journeys.md) (`UJ-FCOL-003`)
- NAVIGATION / SCREENS: [feed-navigation.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/feed-navigation.md), [screen-inventory.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/screen-inventory.md) (`SCR-POST-002`)

## Objective

Assess the feasibility, dependencies, architectural impacts, and gap-closure decisions required to:
1. Decommission and remove all direct user-facing mechanisms for authoring or creating operational posts.
2. Ensure that operational posts and materialized feed items are automatically produced upon Request creation (and associated request lifecycle events).
3. Preserve historical post data and ensure continuous operation of the Operational Feed and discussion capabilities (comments and reactions).

---

# 2. Current State

## Existing Behavior

1. **Backend API & Application Services**:
   - `PostsController.cs` exposes `POST /api/v1/posts` (`CreateOperationalPost`), accepting `CreateOperationalPostBody` and dispatching `CreateOperationalPostCommand`.
   - `PostService.cs` implements `CreateOperationalPostAsync`, creating an active Post aggregate with `Source = PostSource.HumanAuthored`, persisting to `[post].[Posts]`, and publishing `PostCreated`.
   - `FeedProjectionHandler.cs` handles `PostCreated` by inserting a row into `[post].[FeedItems]` with `PostType = 'HUMAN_AUTHORED'`.
   - `RequestService.cs` executes `RecordRequestAsync` and dispatches `RequestRecorded`, but currently no handler subscribes to `RequestRecorded` to generate a system post or feed item. Currently, only exception events (`RequestEscalated`, `RequestRejected`, `RequestStalled`) trigger fallback system post generation in `FeedProjectionHandler`.

2. **Frontend UI & Navigation**:
   - `FeedView.vue` contains a "New Operational Post" toggle button and an inline post creation form with inputs for Title, Content, Customer, Product, Request, and Exception Type. On submission, it executes an HTTP POST to `/api/v1/posts`.
   - Screen `SCR-POST-002: Create Post` is documented in `screen-inventory.md` and linked from `feed-navigation.md` and `navigation-map.md`.

3. **Persistence & Schema**:
   - Database table `[post].[Posts]` has check constraint `Source IN ('HUMAN_AUTHORED', 'SYSTEM_GENERATED')`.
   - Table `[post].[FeedItems]` maintains denormalized feed items with `PostType`.
   - Existing database entries may contain `HUMAN_AUTHORED` posts.

## Existing Constraints

1. Comments and reactions on existing posts must remain functional (`FEAT-FCOL-001`, `FEAT-FCOL-002`). Only the direct creation of posts is revoked.
2. Historical posts already recorded with `Source = 'HUMAN_AUTHORED'` must remain valid and readable in thread details and feed views without causing database or projection deserialization errors.
3. System posts generated from Requests must maintain referential links to `RequestId`, `CustomerId`, and `ProductId` to preserve contextual navigation to `SCR-REQ-003: Request Detail`.
4. The Feed materialized read model (`post.FeedItems`) must remain synchronously or reliably consistent with Request creation events.

---

# 3. Gap Analysis

| ID | Severity | Gap |
|---|---|---|
| GAP-001 | CRITICAL | Direct Post Creation API (`POST /api/v1/posts`) and command handler `CreateOperationalPostCommand` remain active in backend code. |
| GAP-002 | CRITICAL | UI authoring form and "New Operational Post" action remain active in `FeedView.vue` and referenced in navigation/screen artifacts (`SCR-POST-002`). |
| GAP-003 | CRITICAL | Request creation (`RequestRecorded`) currently does not automatically create an operational system post or feed item; only exception events (`RequestEscalated`, `RequestRejected`, `RequestStalled`) create posts. |
| GAP-004 | MAJOR | Historical data handling: existing human-authored posts in database must be preserved for historical fidelity while disallowing new creations. |
| GAP-005 | MINOR | Documentation artifacts (`feed-collaboration-scenarios.md`, `feed-collaboration-use-cases.md`, `feed-collaboration-user-journeys.md`, `screen-inventory.md`, `navigation-map.md`, `feature-catalog.md`) still advertise `SC-FCOL-003`, `UC-FCOL-003`, `UJ-FCOL-003`, `FEAT-FCOL-003`, and `SCR-POST-002`. |

---

# 4. Open Questions

| ID | Question | Impact |
|---|---|---|
| OQ-001 | How should the backend handle existing `POST /api/v1/posts` requests if invoked by legacy clients or direct API calls: return HTTP 405/410/403 or remove the endpoint entirely? | API contract and backward compatibility. |
| OQ-002 | When `RequestRecorded` triggers a system post, what should be the Post Title, Content, Author attribution, and References? | Feed presentation content and consistency across system posts. |
| OQ-003 | Should existing `HUMAN_AUTHORED` posts in the database be retained in `[post].[Posts]` and `[post].[FeedItems]`, or converted/archived? | Data retention and historical integrity. |

---

# 5. Assumptions

| ID | Assumption |
|---|---|
| ASM-001 | Existing comments and reactions capabilities on existing posts remain active and untouched (`FEAT-FCOL-001`, `FEAT-FCOL-002`). |
| ASM-002 | Existing database records with `Source = 'HUMAN_AUTHORED'` must be preserved for historical audit fidelity. |
| ASM-003 | Removing direct post creation does not prevent future system-generated posts from other operational domain events if later defined. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| RISK-001 | If `RequestRecorded` event handling fails during request creation, request persistence could be rolled back or feed could fall out of sync. | High | Ensure in-process event handling either runs in the same transaction or uses a resilient projection handler pattern with proper error logging and idempotency. |
| RISK-002 | Removing UI elements without deprecating/decommissioning the route/endpoint could leave hidden entry points. | Medium | Cleanly remove both frontend form/buttons and backend endpoints/commands; update navigation definitions. |
| RISK-003 | Existing tests that assert direct post creation via `POST /api/v1/posts` will fail once the feature is revoked. | Medium | Identify all unit and integration tests covering `CreateOperationalPost` and update them to reflect the revoked capability and test the new `RequestRecorded` feed generation. |

---

# 7. Recommendations

## Option A: Complete Endpoint Decommissioning and Domain Event Integration (Recommended)

1. Remove `POST /api/v1/posts` endpoint from `PostsController.cs` and decommission `CreateOperationalPostCommand` and `CreateOperationalPostCommandValidator`.
2. Remove the "New Operational Post" button and inline creation form from `FeedView.vue`.
3. Add a MediatR notification handler subscribing to `RequestRecorded` that invokes `IPostService.RecordSystemPostAsync` with `Source = SYSTEM_GENERATED`, Title = `$"Request: {notification.Title}"`, Content = notification.Description, AuthorPersonId = notification.ActorPersonId, RequestId = notification.RequestId, CustomerId = notification.CustomerId, ProductId = notification.ProductId, and projects to `[post].[FeedItems]`.
4. Retain `HUMAN_AUTHORED` in database check constraints and enum definitions to ensure historical records remain readable.

### Advantages

- Completely eliminates unauthorized post creation pathways.
- Clean code architecture without obsolete endpoint stubs.
- Directly satisfies the Product Owner directive.

### Disadvantages

- Breaking change for any external caller attempting to call `POST /api/v1/posts`.

## Option B: Stub Endpoint with HTTP 410 Gone and Hide UI

1. Retain `POST /api/v1/posts` endpoint but have it immediately return `410 Gone` or `403 Forbidden` with a ProblemDetails payload explaining that direct post creation is revoked.
2. Hide UI button in `FeedView.vue`.

### Advantages

- Explicit error message to automated legacy clients.

### Disadvantages

- Retains dead endpoint code in the active codebase.

---

# 8. Gap Closure

## GAP-001

### Decision
Option A is adopted: Completely decommission and remove the `POST /api/v1/posts` endpoint from `PostsController.cs` and decommission `CreateOperationalPostCommand` and `CreateOperationalPostCommandValidator` in `Cakra.Modules.Post`.

### Rationale
In accordance with PO directive in CR-001, users must have no mechanism to author or submit operational posts directly. Removing the endpoint eliminates unnecessary dead code and prevents accidental invocation.

### Impact
Callers cannot initiate direct operational post creation. Unit and integration tests targeting `CreateOperationalPost` will be updated or replaced.

### Architecture Impact
Decommissions command and endpoint in Post module API boundary; updates API surface.

### Resolved By
ica-analyst (Gap Closure stage)

### Resolved Date
2026-09-29

---

## GAP-002

### Decision
Remove the "New Operational Post" toggle button and inline post authoring form from `FeedView.vue`. Decommission screen definition `SCR-POST-002: Create Post`.

### Rationale
Ensures the UI strictly adheres to business requirements and does not expose direct post authoring controls to any user role.

### Impact
Frontend feed view renders only the feed stream, search/filters, and post cards (with comments and reactions), without the create post form.

### Architecture Impact
Removes `SCR-POST-002` from frontend routes and component views; updates screen inventory and navigation architecture.

### Resolved By
ica-analyst (Gap Closure stage)

### Resolved Date
2026-09-29

---

## GAP-003

### Decision
Implement automated operational post creation on Request recording: subscribe to `RequestRecorded` domain event in the Post module (or via `FeedProjectionHandler` / dedicated handler), invoking `Post.RecordSystemPost(...)` and projecting the resulting system post into `[post].[FeedItems]`.

### Rationale
Satisfies Desired Outcome 2 of CR-001: Operational posts in the Operational Feed are generated solely from Request creation (and associated request lifecycle events).

### Impact
Every new Request recorded via `FEAT-REQ-001` immediately and automatically generates an operational system post with `Source = SYSTEM_GENERATED`, visible in the Operational Feed with direct links back to `SCR-REQ-003: Request Detail`.

### Architecture Impact
Introduces `INotificationHandler<RequestRecorded>` in `Cakra.Modules.Post`, bridging `RequestRecorded` events to `Post.RecordSystemPost` and `[post].[FeedItems]`.

### Resolved By
ica-analyst (Gap Closure stage)

### Resolved Date
2026-09-29

---

## GAP-004

### Decision
Preserve existing database records and schema support for historical `HUMAN_AUTHORED` posts. Do not alter existing data rows or remove `HUMAN_AUTHORED` from `[post].[Posts]` check constraint or enum definitions.

### Rationale
Preserves historical audit fidelity and prevents existing data corruption or query failure when reading historical feeds and discussions.

### Impact
Historical posts remain readable and interactive (comments/reactions), while new posts can only be created as `SYSTEM_GENERATED`.

### Architecture Impact
No disruptive database migration required; check constraints retain backwards compatibility for historical rows.

### Resolved By
ica-analyst (Gap Closure stage)

### Resolved Date
2026-09-29

---

## GAP-005

### Decision
Mark `FEAT-FCOL-003` as REVOKED/DECOMMISSIONED. Update scenario, use case, user journey, and screen catalogs to record the revocation of `SC-FCOL-003`, `UC-FCOL-003`, `UJ-FCOL-003`, and `SCR-POST-002`.

### Rationale
Ensures complete artifact consistency across the Knowledge-Centric SDLC.

### Impact
Documentation reflects the true system state and removes ambiguity for implementers, testers, and reviewers.

### Architecture Impact
Updates architecture traceability matrix to remove `FEAT-FCOL-003` and update feed generation triggers.

### Resolved By
ica-analyst (Gap Closure stage)

### Resolved Date
2026-09-29

---

## OQ-001

### Decision
Remove the `POST /api/v1/posts` endpoint entirely rather than keeping a 410/403 stub.

### Rationale
Cakra is an internal system with no external third-party public API consumers that require deprecation transition periods. A clean removal ensures code clarity and prevents technical debt.

### Impact
Any manual invocation will return HTTP 404/405 Not Found / Method Not Allowed.

### Architecture Impact
Clean removal of unused route in `PostsController.cs`.

### Resolved By
ica-analyst (Gap Closure stage)

### Resolved Date
2026-09-29

---

## OQ-002

### Decision
The system post generated from `RequestRecorded` shall use:
- Title: `$"Request: {notification.Title}"`
- Content: `notification.Description`
- AuthorPersonId: `notification.ActorPersonId` (the Person recording the request)
- Source: `SYSTEM_GENERATED`
- SourceEventType: `"RequestRecorded"`
- RequestId: `notification.RequestId`
- CustomerId: `notification.CustomerId`
- ProductId: `notification.ProductId`
- WorkPackageId: `notification.WorkPackageId`

### Rationale
Ensures the resulting feed card contains the exact summary, context, and reference links necessary for actors observing the feed to understand the demand and navigate to `SCR-REQ-003: Request Detail`.

### Impact
Consistent post structure across all operational requests.

### Architecture Impact
Specifies payload mapping for `Post.RecordSystemPost` in `RequestRecorded` event handling.

### Resolved By
ica-analyst (Gap Closure stage)

### Resolved Date
2026-09-29

---

## OQ-003

### Decision
Retain existing `HUMAN_AUTHORED` posts in the database. Do not delete or force-migrate them.

### Rationale
The Operational Knowledge Lifecycle dictates that historical operational knowledge must be preserved. Deleting historical records would break referential links and lose historical discussion.

### Impact
Full audit fidelity and backwards compatibility preserved.

### Architecture Impact
Read models and query services must continue to deserialize and display historical posts with `Source = 'HUMAN_AUTHORED'`.

### Resolved By
ica-analyst (Gap Closure stage)

### Resolved Date
2026-09-29

---

# 9. Architecture Applicability

## Decision

ARCHITECTURE-REQUIRED

## Rationale

This change requires formal technical target-state definition by the Architect, including:
1. Decommissioning an active API endpoint (`POST /api/v1/posts`) and associated command in `Cakra.Modules.Post`.
2. Introducing cross-module domain event handling between `Cakra.Modules.Request` (`RequestRecorded`) and `Cakra.Modules.Post` (`RecordSystemPost` / `post.FeedItems`).
3. Updating component responsibilities, navigation maps, and frontend layout specifications to decommission `SCR-POST-002`.

*(Note: Architecture Applicability decision is owned by the Architect role; this assessment provides the technical justification for the Architect's formal determination).*

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved (GAP-001 through GAP-005 closed)
- [x] All required decisions recorded (Decisions, Rationales, Impacts, Architecture Impacts, Resolved By/Date documented)
- [x] All blocking open questions resolved (OQ-001 through OQ-003 closed)
- [x] Architecture can be finalized or updated based on the approved decisions

## Status

NOT-READY

## Notes

All feasibility gaps and open questions have been fully analyzed and closed with explicit decisions.
In accordance with Knowledge-Centric SDLC governance:
- The Analyst maintains the readiness checklist and keeps Status as `NOT-READY`.
- The Architect is the sole role authorized to grant the `READY-FOR-PLANNING` gate.
- Upon Architect review, the Architect will evaluate the gate, set Status to `READY-FOR-PLANNING`, decide Architecture Applicability, and proceed with Architecture Update and Planning.

---

# 11. References

Referenced artifacts:

- DOMAIN: [post-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/post-domain.md), [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- FEATURE: [FEAT-FCOL-003-create-operational-post.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-FCOL-003-create-operational-post.md), [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md), [FEAT-AWR-001-observe-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-AWR-001-observe-operational-feed.md)
- ISSUE: [CR-001-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-001-ISSUE.md)
- SCENARIOS: [feed-collaboration-scenarios.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/scenarios/feed-collaboration-scenarios.md)
- USE CASES: [feed-collaboration-use-cases.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/use-cases/feed-collaboration-use-cases.md)
- USER JOURNEYS: [feed-collaboration-user-journeys.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/user-journey/feed-collaboration-user-journeys.md)
- NAVIGATION: [feed-navigation.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/feed-navigation.md), [screen-inventory.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/screen-inventory.md)
- ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

Referenced codebase locations:

- `src/backend/Cakra.Api/Controllers/PostsController.cs`
- `src/backend/Cakra.Modules.Post/Services/PostService.cs`
- `src/backend/Cakra.Modules.Post/Services/PostCommands.cs`
- `src/backend/Cakra.Modules.Post/Services/FeedProjectionHandler.cs`
- `src/backend/Cakra.Modules.Post/Domain/Post.cs`
- `src/backend/Cakra.Modules.Request/Services/RequestService.cs`
- `src/backend/Cakra.Modules.Request/Domain/Events/RequestRecorded.cs`
- `src/frontend/Cakra.Web/src/views/FeedView.vue`
