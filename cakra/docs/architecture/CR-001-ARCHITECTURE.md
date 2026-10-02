---
Title: Revocation of Direct Operational Post Creation & Request-Driven Feed Generation Architecture
Code: CR-001
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-02
---

# 1. Overview

This architecture defines the technical realization for Change Request `CR-001`: Revoking Direct Creation of Operational Posts and ensuring operational posts in the Operational Feed originate solely from Request creation (and associated request lifecycle events).

It decommissions `FEAT-FCOL-003`, `SC-FCOL-003`, `UC-FCOL-003`, `UJ-FCOL-003`, and screen `SCR-POST-002: Create Post`, removing all direct user post-authoring capabilities and transitioning operational post creation to a purely event-driven model driven by `RequestRecorded`.

# 2. Architectural Basis

## Business Context

- DOMAIN: [post-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/post-domain.md), [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- FEATURE: [FEAT-FCOL-003-create-operational-post.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-FCOL-003-create-operational-post.md) (REVOKED), [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md), [FEAT-AWR-001-observe-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-AWR-001-observe-operational-feed.md)
- ISSUE: [CR-001-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-001-ISSUE.md)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-001-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-001-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

This architecture consumes and technically realizes the approved decisions:
- `GAP-001`: Remove `POST /api/v1/posts` and `CreateOperationalPostCommand`; transition to event-driven system posts.
- `GAP-002`: Remove "New Operational Post" button and inline form from `FeedView.vue`; decommission `SCR-POST-002`.
- `GAP-003`: Automated system post generation triggered by `RequestRecorded` domain event.
- `GAP-004`: Preserve existing database records and schema support for historical `HUMAN_AUTHORED` posts.
- `GAP-005`: Mark `FEAT-FCOL-003`, `SC-FCOL-003`, `UC-FCOL-003`, `UJ-FCOL-003`, and `SCR-POST-002` as revoked/decommissioned.
- `OQ-001`: Endpoint removal (returns HTTP 404/405) rather than 410/403 stub.
- `OQ-002`: System post payload attribute mapping for `RequestRecorded`.
- `OQ-003`: Historical `HUMAN_AUTHORED` data retention in database and projections.

# 3. Scope

## Included
- Complete removal of `POST /api/v1/posts` endpoint from `PostsController.cs` in `Cakra.Api`.
- Removal of `CreateOperationalPostCommand`, `CreateOperationalPostCommandValidator`, and `CreateOperationalPostAsync` handler methods in `Cakra.Modules.Post`.
- Removal of the "New Operational Post" toggle button, inline creation form, and associated form state/validation from `FeedView.vue` in `Cakra.Web`.
- Decommissioning of screen definition `SCR-POST-002: Create Post`.
- Implementation of `RequestRecordedPostHandler` (subscribing to `RequestRecorded` notification via MediatR) in `Cakra.Modules.Post` to invoke `IPostService.RecordSystemPostAsync`.
- Projection of the generated system post into `[post].[FeedItems]` via `FeedProjectionHandler`.
- Preservation of read-model queries, comments (`FEAT-FCOL-001`), and reactions (`FEAT-FCOL-002`) across both new system posts and historical human-authored posts.
- Test suite updates to verify removal of the endpoint and automatic feed generation on request recording.

## Excluded
- Any changes to comments (`FEAT-FCOL-001`) or reactions (`FEAT-FCOL-002`) functionality.
- Alteration, migration, or deletion of existing `HUMAN_AUTHORED` rows in `[post].[Posts]` or `[post].[FeedItems]`.
- Changes to request lifecycle exception post generation (`RequestEscalated`, `RequestRejected`, `RequestStalled`).

# 4. Technical Decisions

## TD-001: Event-Driven System Post Creation (No Replacement API)
Direct manual post creation is revoked. Operational posts exist exclusively as derived read and collaboration artifacts produced by domain events. No replacement command or API endpoint for direct post creation will be introduced.

## TD-002: Complete Endpoint Removal (HTTP 404 / 405)
`POST /api/v1/posts` is completely removed from `PostsController.cs`. Cakra is an internal system with no external third-party API contracts requiring deprecation grace periods. Removing the route ensures code clarity and eliminates dead code paths.

## TD-003: Cross-Module Event Handling via MediatR Notification
`Cakra.Modules.Post` introduces `RequestRecordedPostHandler : INotificationHandler<RequestRecorded>`. When `RequestService.RecordRequestAsync` completes and publishes `RequestRecorded`, MediatR in-process notification dispatches to `RequestRecordedPostHandler` within the same execution scope.

## TD-004: Standard System Post Payload Mapping
The system post created in response to `RequestRecorded` is mapped as follows:
- **Title**: `$"Request: {notification.Title}"`
- **Content**: `notification.Description`
- **AuthorPersonId**: `notification.ActorPersonId` (the user who recorded the request)
- **Source**: `PostSource.SystemGenerated` (`SYSTEM_GENERATED`)
- **SourceEventType**: `"RequestRecorded"`
- **PostReferences**:
  - `RequestId`: `notification.RequestId`
  - `CustomerId`: `notification.CustomerId`
  - `ProductId`: `notification.ProductId`
  - `WorkPackageId`: `notification.WorkPackageId`

## TD-005: Schema Retention & Backwards Compatibility
Database check constraints on `[post].[Posts]` (`Source IN ('HUMAN_AUTHORED', 'SYSTEM_GENERATED')`) and the `PostSource` enum values remain unchanged. Existing `HUMAN_AUTHORED` records remain fully readable and interactive in feeds and thread modals.

## TD-006: Frontend View Simplification
`FeedView.vue` is refactored to remove the "New Operational Post" button, inline form component, and related submission logic. Screen `SCR-POST-002` is retired.

# 5. Component Responsibilities

| Component | Responsibility |
|---|---|
| `PostsController` (`Cakra.Api`) | Exposes query endpoints (`GET /api/v1/posts/{id}`), comments (`POST /api/v1/posts/{id}/comments`), and reactions (`POST /api/v1/posts/{id}/reactions`). Endpoint `POST /api/v1/posts` is decommissioned and removed. |
| `RequestRecordedPostHandler` (`Cakra.Modules.Post`) | In-process MediatR notification handler subscribing to `RequestRecorded`. Maps event parameters and invokes `IPostService.RecordSystemPostAsync`. |
| `PostService` (`Cakra.Modules.Post`) | Authoritative service for recording system posts (`RecordSystemPostAsync`), managing comments, reactions, visibility, and archiving. Method `CreateOperationalPostAsync` is decommissioned and removed. |
| `FeedProjectionHandler` (`Cakra.Modules.Post`) | In-process handler projecting `PostCreated` events into denormalized `[post].[FeedItems]` table. |
| `FeedView.vue` (`Cakra.Web`) | Displays feed items, search/filtering, comment threads, and reaction controls. Inline post authoring form is removed. |

# 6. Integration Design

| Source | Target | Purpose |
|---|---|---|
| `RequestService` (`Cakra.Modules.Request`) | `IMediator` / `IPublisher` | Publishes `RequestRecorded` notification upon successful request persistence. |
| `IMediator` | `RequestRecordedPostHandler` (`Cakra.Modules.Post`) | Delivers `RequestRecorded` notification to the Post module subscriber. |
| `RequestRecordedPostHandler` | `IPostService` (`Cakra.Modules.Post`) | Invokes `RecordSystemPostAsync` with mapped title, content, author, and reference IDs. |
| `PostService` | `[post].[Posts]` & `[post].[PostReferences]` | Persists the new `SYSTEM_GENERATED` Post aggregate. |
| `PostService` | `FeedProjectionHandler` (`Cakra.Modules.Post`) | Dispatches `PostCreated` domain event to project new feed item into `[post].[FeedItems]`. |
| Frontend `FeedView.vue` | `FeedQueryService` API (`GET /api/v1/feed`) | Fetches operational feed items for display. |

# 7. Data Ownership

| Data Entity | Owning Module | Notes |
|---|---|---|
| `[request].[Requests]` | `Cakra.Modules.Request` | Authoritative request entity. |
| `[post].[Posts]` | `Cakra.Modules.Post` | Authoritative post entity. New rows are strictly `SYSTEM_GENERATED`. Existing `HUMAN_AUTHORED` rows retained. |
| `[post].[PostReferences]` | `Cakra.Modules.Post` | Links system posts to `RequestId`, `CustomerId`, `ProductId`, `WorkPackageId`. |
| `[post].[FeedItems]` | `Cakra.Modules.Post` | Materialized read model for operational feed. |

# 8. Database Design

## New Tables
None.

## Modified Tables
None. The existing schema and constraints already support `Source = 'SYSTEM_GENERATED'` and nullable reference keys in `[post].[PostReferences]`.

## Relationships
Existing relationship from `[post].[PostReferences]` to `RequestId`, `CustomerId`, `ProductId`, and `WorkPackageId` is utilized by the new event-driven post creation.

## Migration Considerations
No data migration or DDL alteration required. Backwards compatibility for existing `HUMAN_AUTHORED` rows is preserved by retaining check constraints and enum values.

# 9. Cross-Cutting Concerns

- **Transaction Scope & Consistency**: `RequestRecordedPostHandler` executes within the in-process MediatR notification pipeline. Failures in post generation are logged without interrupting request recording, or handled idempotently based on `RequestId`.
- **Auditability**: The author of the system post is explicitly set to `notification.ActorPersonId` to preserve traceability to the person who recorded the request.
- **Observability**: Structured logs are emitted when a system post is generated from `RequestRecorded`.

# 10. Implementation Constraints

- **No Human Authoring Entry Points**: No API endpoint, CLI command, or UI control may allow direct creation of operational posts.
- **Historical Data Integrity**: Do not remove `HUMAN_AUTHORED` from `PostSource` enum or database check constraints.
- **Modularity**: Cross-module communication between `Request` and `Post` modules must remain decoupled via MediatR notifications (`RequestRecorded`), with no direct service-to-service compile-time coupling from `Request` to `Post`.

# 11. Acceptance Conditions

- **AC-001**: Endpoint `POST /api/v1/posts` is removed from `PostsController`; calling it returns HTTP 404 Not Found or HTTP 405 Method Not Allowed.
- **AC-002**: `CreateOperationalPostCommand` and `CreateOperationalPostCommandValidator` are deleted from `Cakra.Modules.Post`.
- **AC-003**: `FeedView.vue` does not contain the "New Operational Post" button or inline authoring form; `SCR-POST-002` is retired.
- **AC-004**: Recording a customer request (`POST /api/v1/requests` / `RecordRequestCommand`) automatically creates a `SYSTEM_GENERATED` post in `[post].[Posts]` and a corresponding entry in `[post].[FeedItems]`.
- **AC-005**: The generated post contains Title (`"Request: {Title}"`), Content (`Description`), Author (`ActorPersonId`), and references to `RequestId`, `CustomerId`, and `ProductId`.
- **AC-006**: Historical `HUMAN_AUTHORED` posts in the database remain fully readable in feed queries and thread views.
- **AC-007**: Adding comments and reactions to system-generated posts and historical human-authored posts works without error.
- **AC-008**: All existing and updated unit/integration tests in `Cakra.Api.Tests` and `Cakra.Modules.Post.Tests` pass.
