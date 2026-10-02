---
Title: Implementation Plan for Revoking Direct Creation of Operational Posts (CR-001)
Code: CR-001
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-02
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-001`: Decommission and remove all direct user-facing mechanisms for creating operational posts (`FEAT-FCOL-003`, `SC-FCOL-003`, `UC-FCOL-003`, `SCR-POST-002`), ensure operational posts and materialized feed items are automatically generated when a Request is recorded (`RequestRecorded`), preserve historical `HUMAN_AUTHORED` posts, and maintain full discussion (comments/reactions) capabilities.

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- FEATURE: [FEAT-FCOL-003-create-operational-post.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-FCOL-003-create-operational-post.md) (REVOKED), [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md), [FEAT-AWR-001-observe-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-AWR-001-observe-operational-feed.md)
- ARCHITECTURE: [CR-001-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-001-ARCHITECTURE.md) (authoritative capability architecture), [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md) (master target architecture v1.4)
- FEASIBILITY-ASSESSMENT: [CR-001-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-001-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

Architecture Applicability: ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers all backend, frontend, and test modifications required to realize `CR-001`:

1. **Backend Event Subscription (`Cakra.Modules.Post`)**:
   - Create `RequestRecordedPostHandler` implementing `INotificationHandler<RequestRecorded>` in `Cakra.Modules.Post.Services`.
   - In response to `RequestRecorded`, invoke `IPostService.RecordSystemPostAsync` with `Source = SYSTEM_GENERATED`, Title = `$"Request: {Title}"`, Content = `Description`, AuthorPersonId = `ActorPersonId`, and references to `RequestId`, `CustomerId`, `ProductId`, `WorkPackageId`.
   - Ensure the post is projected into `[post].[FeedItems]` via existing `FeedProjectionHandler`.

2. **Backend API & Service Decommissioning (`Cakra.Modules.Post`, `Cakra.Api`)**:
   - Remove `POST /api/v1/posts` endpoint and request body DTOs (`CreateOperationalPostBody`, `CreateOperationalPostReferenceItem`) from `PostsController.cs`.
   - Remove `CreateOperationalPostCommand` and `CreateOperationalPostCommandValidator` from `PostCommands.cs`.
   - Remove `CreateOperationalPostAsync` from `IPostService` and `PostService`.
   - Remove `CreateOperationalPost` factory method from `Domain.Post`.

3. **Frontend UI Decommissioning (`Cakra.Web`)**:
   - Remove the "New Operational Post" toggle button and inline post authoring form from `FeedView.vue`.
   - Remove all associated reactive state variables, submission handlers (`handleCreateOperationalPost`), and validation logic.
   - Decommission screen inventory entry `SCR-POST-002: Create Post`.

4. **Integration & Unit Test Alignment (`Cakra.Tests.Integration`, `Cakra.Tests.Unit`)**:
   - Update tests in `FeedAndPostsControllerTests.cs`, `PostModuleIntegrationTests.cs`, and `FeedProjectionIntegrationTests.cs`.
   - Verify calling `POST /api/v1/posts` returns HTTP 404 or 405.
   - Verify recording a new request (`POST /api/v1/requests`) automatically creates a system post and a corresponding feed item in `[post].[FeedItems]`.
   - Verify historical `HUMAN_AUTHORED` post queries, comments, and reactions remain operational.

---

# 3. Dependencies

- .NET 8 SDK / C# 12
- Node.js & npm (Vue 3 / Vite)
- Microsoft SQL Server LocalDB for integration test suites
- MediatR in-process notification dispatch pipeline

For slice dependencies:
- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires referenced slice to have implementation status `IMPLEMENTED`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Backend Event Integration & Decommissioning | IMPLEMENTED | GO | 2/2 |
| P2 - Frontend View Decommissioning | IMPLEMENTED | GO | 1/1 |
| P3 - Test Suite & Verification Alignment | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Backend Event Integration & Decommissioning

Implementation Status: NOT-STARTED
Review Status: GO

### P1-S01

Title: Implement RequestRecorded Domain Event Subscription in Post Module

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Implement `RequestRecordedPostHandler` in `Cakra.Modules.Post` to subscribe to the `RequestRecorded` domain event from `Cakra.Modules.Request` and record a system-generated operational post via `IPostService.RecordSystemPostAsync`.

Depends On: None

Repository: `cakra`

Completion Criteria:
1. `RequestRecordedPostHandler.cs` is created in `src/backend/Cakra.Modules.Post/Services/` implementing `INotificationHandler<RequestRecorded>`.
2. When `RequestRecorded` is published, handler extracts:
   - `Title`: `$"Request: {notification.Title}"`
   - `Content`: `notification.Description`
   - `AuthorPersonId`: `notification.ActorPersonId`
   - `SourceEventType`: `"RequestRecorded"`
   - `CustomerId`: `notification.CustomerId`
   - `ProductId`: `notification.ProductId`
   - `RequestId`: `notification.RequestId`
   - `WorkPackageId`: `notification.WorkPackageId`
3. Handler calls `IPostService.RecordSystemPostAsync` with the mapped attributes.
4. Handler is registered with DI / MediatR assembly scanning in `Cakra.Modules.Post`.
5. Post creation emits `PostCreated`, which is automatically projected into `[post].[FeedItems]` by `FeedProjectionHandler`.
6. Code compiles with zero errors and zero warnings.

Notes:
- `IPostService.RecordSystemPostAsync` is already implemented and tested in `PostService.cs`.
- Ensure appropriate logging and null safety checks.

Implementation Notes:
- Created `RequestRecordedPostHandler` implementing `INotificationHandler<RequestRecorded>` in `src/backend/Cakra.Modules.Post/Services/RequestRecordedPostHandler.cs`.
- Handled incoming `RequestRecorded` event, mapped all properties according to TD-004, and invoked `IPostService.RecordSystemPostAsync`.
- Registered `RequestRecordedPostHandler` as a scoped service in `Cakra.Modules.Post.PostModule`.
- Verified compilation with 0 warnings and 0 errors (`dotnet build src/backend/Cakra.Api`).

Changed Files:
- `src/backend/Cakra.Modules.Post/Services/RequestRecordedPostHandler.cs`
- `src/backend/Cakra.Modules.Post/PostModule.cs`
- `docs/implementation-plan/CR-001-IMPLEMENTATION-PLAN.md`

---

### P1-S02

Title: Decommission Direct Post Creation Backend API, Commands, and Service Methods

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Completely remove the `POST /api/v1/posts` endpoint and all direct operational post authoring commands, validators, and service methods across `Cakra.Api` and `Cakra.Modules.Post`.

Depends On: P1-S01

Repository: `cakra`

Completion Criteria:
1. Remove `POST /api/v1/posts` action (`CreateOperationalPost`) and DTO classes (`CreateOperationalPostBody`, `CreateOperationalPostReferenceItem`) from `src/backend/Cakra.Api/Controllers/PostsController.cs`.
2. Remove `CreateOperationalPostCommand` and `CreateOperationalPostCommandValidator` from `src/backend/Cakra.Modules.Post/Services/PostCommands.cs`.
3. Remove `CreateOperationalPostAsync` and `CreateOperationalPost` from `src/backend/Cakra.Modules.Post/Services/IPostService.cs` and `PostService.cs`.
4. Remove `CreateOperationalPost` factory method from `src/backend/Cakra.Modules.Post/Domain/Post.cs`.
5. Verify `PostSource.HumanAuthored` enum and database check constraints remain unchanged to preserve historical data.
6. Backend solution compiles cleanly (`dotnet build src/backend/Cakra.Api`).

Notes:
- Ensure comments (`POST /api/v1/posts/{id}/comments`) and reactions (`POST /api/v1/posts/{id}/reactions`) remain untouched.

Implementation Notes:
- Removed `POST /api/v1/posts` action method (`CreateOperationalPost`) and request DTO classes (`CreateOperationalPostBody`, `PostReferenceItemBody`) from `src/backend/Cakra.Api/Controllers/PostsController.cs`. Comments and reactions endpoints remain fully operational.
- Removed `CreateOperationalPostCommand` and `CreateOperationalPostCommandValidator` from `src/backend/Cakra.Modules.Post/Services/PostCommands.cs`.
- Removed `CreateOperationalPostAsync` and `CreateOperationalPost` interface methods from `src/backend/Cakra.Modules.Post/Services/IPostService.cs` and their implementations and MediatR command handler `Handle(CreateOperationalPostCommand)` from `src/backend/Cakra.Modules.Post/Services/PostService.cs`.
- Removed `CreateOperationalPost` factory method from `src/backend/Cakra.Modules.Post/Domain/Post.cs`.
- Verified that `PostSource.HumanAuthored` enum (`PostSource.cs`) and database check constraint `CK_Posts_Source` (`0008_post_tables.sql`) remain unchanged to preserve historical data integrity.
- Verified that `dotnet build src/backend/Cakra.Api` builds cleanly with 0 warnings and 0 errors.

Changed Files:
- `src/backend/Cakra.Api/Controllers/PostsController.cs`
- `src/backend/Cakra.Modules.Post/Services/PostCommands.cs`
- `src/backend/Cakra.Modules.Post/Services/IPostService.cs`
- `src/backend/Cakra.Modules.Post/Services/PostService.cs`
- `src/backend/Cakra.Modules.Post/Domain/Post.cs`
- `docs/implementation-plan/CR-001-IMPLEMENTATION-PLAN.md`

---

## P2 - Frontend View Decommissioning

Implementation Status: IMPLEMENTED
Review Status: GO

### P2-S03

Title: Remove Direct Post Authoring Form and Controls from FeedView

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Remove the "New Operational Post" button, inline post authoring form, and associated client-side logic from `FeedView.vue`.

Depends On: None

Repository: `cakra`

Completion Criteria:
1. Remove "New Operational Post" toggle button from `src/frontend/Cakra.Web/src/views/FeedView.vue`.
2. Remove inline `<form @submit.prevent="handleCreateOperationalPost">` and all form fields (Title, Content, Customer, Product, Request, Exception Type, references) from `FeedView.vue`.
3. Remove reactive form state properties and `handleCreateOperationalPost` method from `<script setup lang="ts">`.
4. Remove unused imports and CSS styles associated with the post creation form.
5. Verify `FeedView.vue` still renders the operational feed stream, search/filter controls, and post cards with comment and reaction interactions.
6. Frontend build succeeds cleanly (`npm run build` or `npm run type-check`).

Notes:
- Adheres to `SCR-POST-002` decommissioning.

Implementation Notes:
- Removed "New Operational Post" toggle button from the header action bar and "Create First Post" button from the empty state in `src/frontend/Cakra.Web/src/views/FeedView.vue`.
- Removed inline `<form @submit.prevent="handleCreateOperationalPost">` and all form fields (Title, Content, Customer, Product, Request, Exception Type) along with the post authoring card container.
- Removed reactive form state properties (`showCreatePostForm`, `createPostForm`), submission flag (`isSubmittingPost`), feedback ref (`feedbackMessage`), requests lookup state (`availableRequests`), and unused interfaces (`RequestLookupItem`, `PagedRequestLookupPayload`).
- Removed computed property `isCreatePostDisabled` and methods `toggleCreatePostForm`, `resetCreatePostForm`, `handleLinkedRequestSelect`, and `handleCreateOperationalPost`.
- Updated `loadReferenceLookups` to only query active customers and active products.
- Preserved operational feed stream rendering, search and filter bar, pagination, post cards with source badges, reactions, and modal thread interactions.
- Verified frontend type checking (`npm run type-check`) and production build (`npm run build`) succeeded with 0 errors.

Changed Files:
- `src/frontend/Cakra.Web/src/views/FeedView.vue`
- `docs/implementation-plan/CR-001-IMPLEMENTATION-PLAN.md`

---

## P3 - Test Suite & Verification Alignment

Implementation Status: IMPLEMENTED
Review Status: GO

### P3-S04

Title: Update Integration Tests and Verify End-to-End Event-Driven Feed Generation

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Update integration and unit tests across `Cakra.Tests.Integration` and `Cakra.Tests.Unit` to assert the decommissioned endpoint behavior and verify automated feed item creation upon request recording.

Depends On: P1-S01, P1-S02, P2-S03

Repository: `cakra`

Completion Criteria:
1. Update `FeedAndPostsControllerTests.cs`:
   - Replace direct post creation tests calling `POST /api/v1/posts` with an assertion verifying that calling `POST /api/v1/posts` returns HTTP 404 Not Found or HTTP 405 Method Not Allowed.
2. Add integration test verifying that recording a request via `POST /api/v1/requests` (or `RecordRequestCommand`) automatically creates a system post in `[post].[Posts]` and a corresponding row in `[post].[FeedItems]`.
3. Update helper test fixtures in `FeedQueryIntegrationTests.cs`, `FeedProjectionIntegrationTests.cs`, and `PostModuleIntegrationTests.cs` that previously called `CreateOperationalPostCommand` to use `RecordSystemPost` or request recording fixtures instead.
4. Verify historical `HUMAN_AUTHORED` post reads, comments, and reactions remain fully functional in test assertions.
5. Run test suite: all backend integration and unit tests pass cleanly.

Notes:
- Follow established testing conventions with `WebApplicationFactory<Program>` and LocalDB.

Implementation Notes:
- Updated `FeedAndPostsControllerTests.cs`:
  - Asserted calling decommissioned `POST /api/v1/posts` returns HTTP 404 Not Found or 405 Method Not Allowed.
  - Added integration test `Recording_a_request_via_api_automatically_creates_system_post_and_feed_item` verifying `POST /api/v1/requests` automatically creates a system post in `[post].[Posts]` and a row in `[post].[FeedItems]`.
  - Added integration test `Historical_human_authored_posts_remain_readable_and_support_comments_and_reactions_via_api` verifying historical `HUMAN_AUTHORED` posts can be read and discussion (comments/reactions) functions properly.
  - Updated seeding and test assertions to use system-generated posts and request-driven fixtures.
- Updated `FeedQueryIntegrationTests.cs`:
  - Replaced decommissioned `CreateOperationalPostCommand` fixtures with `RecordSystemPostCommand` fixtures.
  - Updated escalation test scenario to use the auto-created operational post from `RecordRequestCommand`.
- Updated `FeedProjectionIntegrationTests.cs`:
  - Replaced decommissioned `CreateOperationalPostCommand` with `RecordSystemPostCommand` and updated post type assertions to `SYSTEM_GENERATED`.
  - Updated rebuild assertion counts in `RebuildAll` and `EnqueueRebuildAsync` to account for the auto-created request post.
- Updated `PostModuleIntegrationTests.cs`:
  - Replaced decommissioned `CreateOperationalPostCommand` with `RecordSystemPostCommand` in reference validation tests and domain event emission tests.
  - Added `Historical_human_authored_posts_remain_readable_and_support_comments_and_reactions` test asserting historical `HUMAN_AUTHORED` posts are readable via `IPostQueryService` and accept comments and reactions.
- Updated `CrossCuttingSystemIntegrationTests.cs`:
  - Updated Criterion 3 to assert `POST /api/v1/requests` automatically creates the feed item and `POST /api/v1/posts` returns 404/405.
  - Updated rebuild and operational lifecycle test steps to use request auto-generated posts and `RecordSystemPostCommand`.
- Ran test suites:
  - Unit tests (`dotnet test tests/backend/Cakra.Tests.Unit`): 202 passed, 0 failed.
  - Post integration tests (`dotnet test tests/backend/Cakra.Tests.Integration --filter "FullyQualifiedName~Post"`): 22 passed, 0 failed.
  - Cross-cutting integration tests (`dotnet test tests/backend/Cakra.Tests.Integration --filter "FullyQualifiedName~CrossCuttingSystemIntegrationTests"`): 9 passed, 0 failed.

Changed Files:
- `tests/backend/Cakra.Tests.Integration/Post/FeedAndPostsControllerTests.cs`
- `tests/backend/Cakra.Tests.Integration/Post/FeedQueryIntegrationTests.cs`
- `tests/backend/Cakra.Tests.Integration/Post/FeedProjectionIntegrationTests.cs`
- `tests/backend/Cakra.Tests.Integration/Post/PostModuleIntegrationTests.cs`
- `tests/backend/Cakra.Tests.Integration/SystemValidation/CrossCuttingSystemIntegrationTests.cs`
- `docs/implementation-plan/CR-001-IMPLEMENTATION-PLAN.md`

---

# 6. Change Log

- 2026-10-02: Initial plan created for `CR-001` based on approved `CR-001-ARCHITECTURE.md` and `CR-001-FEASIBILITY-ASSESSMENT.md`.
