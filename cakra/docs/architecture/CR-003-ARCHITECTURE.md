---
Title: Create New Request via Operational Feed Architecture
Code: CR-003
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-03
---

# 1. Overview

This architecture defines the technical realization for Change Request `CR-003`: Create New Request via Operational Feed.

It realizes [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md), [FEAT-AWR-001-observe-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-AWR-001-observe-operational-feed.md), and the approved gap-closure decisions from [CR-003-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-003-FEASIBILITY-ASSESSMENT.md). It establishes a direct request creation mechanism within the Operational Feed (`SCR-FEED-001`) via a dedicated modal dialog component (`CreateRequestModal.vue`), wires submission to the existing backend `POST /api/v1/requests` endpoint, and ensures instantaneous feed stream synchronization and user feedback.

# 2. Architectural Basis

## Business Context

- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md), [post-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/post-domain.md)
- FEATURE: [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md), [FEAT-AWR-001-observe-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-AWR-001-observe-operational-feed.md)
- ISSUE: [CR-003-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-003-ISSUE.md)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-003-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-003-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

This architecture consumes and realizes the approved decisions:
- `GAP-001`: Add primary action button `+ Create Request` (`data-testid="create-request-btn"`) in the header of `FeedView.vue` (`SCR-FEED-001`).
- `GAP-002`: Implement `CreateRequestModal.vue` under `Cakra.Web/src/components/` and mount it within `FeedView.vue`.
- `GAP-003`: Update UI layout specification `10-scr-feed-001.md`, feed navigation `feed-navigation.md`, and feature documents (`FEAT-REQ-001`, `FEAT-AWR-001`).
- `GAP-004`: Add frontend unit and integration tests verifying modal opening, form submission, and automated feed reload.
- `OQ-001`: Adopt Option A (Dedicated Modal Dialog Component `CreateRequestModal.vue`).
- `OQ-002`: Stay on `SCR-FEED-001` after creation, close modal, reload feed (`loadFeed()`), and display success banner with Request Detail link.
- `OQ-003`: Lazily load active customers and active products when the modal opens.

# 3. Scope

## Included

- Implementation of `CreateRequestModal.vue` component in `Cakra.Web/src/components/` supporting:
  - Input fields: Title, Description, Customer dropdown, Product dropdown, Request Type dropdown, Priority dropdown.
  - Client-side validation (required non-blank Title and Description).
  - ProblemDetails error rendering from API responses.
  - HTTP `POST /api/v1/requests` submission via `httpClient`.
  - Lazy loading of lookups: `GET /api/v1/customers/active` and `GET /api/v1/products/active`.
- Integration of `CreateRequestModal.vue` into `FeedView.vue` (`SCR-FEED-001`):
  - Primary button `+ Create Request` in the header toolbar (`data-testid="create-request-btn"`).
  - State management (`showCreateRequestModal = ref(false)`).
  - Handlers for `@close` and `@saved`.
  - Automatic feed stream refresh (`loadFeed()`) upon receiving `@saved`.
  - Dismissible success banner displaying created Request ID and link to `SCR-REQ-003: Request Detail`.
- Updates to documentation artifacts:
  - `cakra/docs/ui-layout/10-scr-feed-001.md`
  - `cakra/docs/navigation/feed-navigation.md`
  - `cakra/docs/navigation/screen-inventory.md`
  - `cakra/docs/features/FEAT-REQ-001-record-customer-request.md`
  - `cakra/docs/features/FEAT-AWR-001-observe-operational-feed.md`
- Frontend test coverage for `CreateRequestModal.vue` and `FeedView.vue` creation workflow.

## Excluded

- Backend API modifications: `RequestsController.cs` (`POST /api/v1/requests`) already exists, validates commands, persists the Request aggregate, and emits `RequestRecorded`.
- Domain event or post handler modifications: `RequestRecordedPostHandler.cs` already catches `RequestRecorded`, calls `IPostService.RecordSystemPostAsync`, and `FeedProjectionHandler.cs` already populates `[post].[FeedItems]`.
- Database schema changes: tables `[request].[Requests]` and `[post].[FeedItems]` require no DDL changes.
- Removal of standalone screen `SCR-REQ-002: Create Request` (`CreateRequestView.vue`): remains available for direct routes and fallback.

# 4. Technical Decisions

## TD-001: Component-Level Modal Encapsulation (`CreateRequestModal.vue`)
`CreateRequestModal.vue` encapsulates its own form state (`title`, `description`, `customerId`, `productId`, `requestType`, `priority`), validation errors, loading indicators, and lookup fetching. It communicates with the host view strictly via props (`show: boolean`) and events:
- `@close`: informs parent to hide modal.
- `@saved(createdRequest: CreatedRequestResponse)`: notifies parent of successful creation, passing back the created request details.

## TD-002: Reusing Backend Request Creation Pipeline
The modal dispatches the exact same payload schema consumed by `POST /api/v1/requests` (`RecordRequestBody`):
```json
{
  "title": "string",
  "description": "string",
  "customerId": "guid | null",
  "productId": "guid | null",
  "workPackageId": null,
  "requestType": "string",
  "priority": "string"
}
```
This reuses existing FluentValidation (`RecordRequestCommandValidator`), aggregate invariant checks, and in-process MediatR domain event publication.

## TD-003: Deterministic Feed Stream Synchronization
Because MediatR publishes `RequestRecorded` synchronously in-process before `RequestsController.RecordRequest` returns HTTP `201 Created`, the system post in `[post].[FeedItems]` is guaranteed to be persisted when the frontend receives the success response. Therefore, immediately triggering `loadFeed()` in `FeedView.vue` deterministically displays the new post at index 0 of the feed stream without artificial timeouts or polling delays.

## TD-004: Post-Submission User Feedback on Operational Feed
Upon receiving `@saved`, `FeedView.vue`:
1. Sets `showCreateRequestModal.value = false`.
2. Sets `createdRequestAlert.value = { id: request.id, title: request.title }`.
3. Calls `loadFeed()`.
4. Renders a Bootstrap 5 success banner directly above the filter toolbar:
   ```html
   <div class="alert alert-success alert-dismissible fade show" role="alert" data-testid="request-created-success-alert">
     <i class="bi bi-check-circle-fill me-2"></i>
     Request <strong>{{ createdRequestAlert.id }}</strong> recorded successfully. Post published to feed.
     <router-link :to="`/requests/${createdRequestAlert.id}`" class="alert-link ms-2">View Request Detail &rarr;</router-link>
     <button type="button" class="btn-close" @click="createdRequestAlert = null"></button>
   </div>
   ```

## TD-005: Lazy Lookup Strategy
Lookups (`GET /api/v1/customers/active` and `GET /api/v1/products/active`) are loaded inside `CreateRequestModal.vue` on watch of `props.show === true`. If already populated, redundant network calls are bypassed unless explicitly refreshed.

# 5. Component Responsibilities

| Component | Responsibility |
|---|---|
| `FeedView.vue` (`Cakra.Web`) | Renders feed header with `+ Create Request` button (`data-testid="create-request-btn"`), filter bar, and feed cards; hosts `CreateRequestModal.vue`; refreshes feed stream upon request creation; renders success banner. |
| `CreateRequestModal.vue` (`Cakra.Web`) | Renders Bootstrap 5 modal dialog capturing request inputs; performs client-side validation; lazily loads active customer/product lookups; executes `POST /api/v1/requests`; handles error states; emits `@close` and `@saved`. |
| `RequestsController` (`Cakra.Api`) | Exposes `POST /api/v1/requests`, binds `RecordRequestBody`, executes `RecordRequestCommand` via MediatR, returns HTTP `201 Created` with `CreatedRequestResponse`. |
| `RequestService` (`Cakra.Modules.Request`) | Executes `RecordRequestCommand`, instantiates and persists `Request` aggregate, raises `RequestRecorded` domain event. |
| `RequestRecordedPostHandler` (`Cakra.Modules.Post`) | In-process MediatR notification handler subscribing to `RequestRecorded`, calls `_postService.RecordSystemPostAsync` to generate the operational system post. |
| `FeedProjectionHandler` (`Cakra.Modules.Post`) | In-process handler subscribing to `PostCreated`, projects item into `[post].[FeedItems]`. |
| `FeedController` (`Cakra.Api`) | Exposes `GET /api/v1/feed` returning paginated feed items ordered by `CreatedAt DESC`. |

# 6. Integration Design

| Source | Target | Purpose |
|---|---|---|
| `FeedView.vue` | `CreateRequestModal.vue` | Controls dialog visibility (`:show="showCreateRequestModal"`), receives `@close` and `@saved`. |
| `CreateRequestModal.vue` | `httpClient` (`POST /api/v1/requests`) | Sends request creation payload to backend API. |
| `CreateRequestModal.vue` | `httpClient` (`GET /api/v1/customers/active`) | Retrieves list of active customers for dropdown selection. |
| `CreateRequestModal.vue` | `httpClient` (`GET /api/v1/products/active`) | Retrieves list of active products for dropdown selection. |
| `FeedView.vue` | `httpClient` (`GET /api/v1/feed`) | Refreshes feed items immediately upon receiving `@saved`. |
| `FeedView.vue` | Vue Router (`/requests/:id`) | Provides direct navigation link to `SCR-REQ-003: Request Detail` from the post-creation success alert. |

# 7. Data Ownership

| Data | Owner |
|---|---|
| Request Record (`[request].[Requests]`) | Request Domain (`Cakra.Modules.Request`) |
| Operational Post Record (`[post].[Posts]`) | Post Domain (`Cakra.Modules.Post`) |
| Materialized Feed Items (`[post].[FeedItems]`) | Post Domain (`Cakra.Modules.Post`) |
| Modal Component State (`show`, `form`, `validationErrors`) | Frontend Presentation (`Cakra.Web`) |

# 8. Database Design

## New Tables
None required.

## Modified Tables
None required (`[request].[Requests]`, `[post].[Posts]`, and `[post].[FeedItems]` already support all required fields).

## Relationships
Existing referential association between `Post` / `FeedItem` and `Request` (`RequestId`) is maintained.

## Migration Considerations
No database migrations or data backfills required.

# 9. Cross-Cutting Concerns

- **Security & Authorization**: The request creation endpoint requires an authenticated session (`[Authorize]`). `httpClient` automatically attaches the bearer token from the session store.
- **Error Handling**: API errors (HTTP 400 validation, 401, 500) are caught and displayed inside the modal dialog using standard ProblemDetails alert rendering, preserving entered form data.
- **Accessibility & UX**: Modal supports Escape key closure, focus trapping, clear validation error messages, and loading spinners during network requests.
- **Observability**: Modal operations emit standard frontend console/debug logs during submission and error states.

# 10. Implementation Constraints

- Vue 3 `<script setup lang="ts">` with strict TypeScript typing.
- Bootstrap 5 CSS classes and icons (`bi-*`) consistent with the Cakra Design System.
- Zero changes to backend domain entities or database schema.
- Existing standalone view `CreateRequestView.vue` must remain operational.

# 11. Acceptance Conditions

1. `FeedView.vue` displays a primary `+ Create Request` button (`data-testid="create-request-btn"`) in the header next to `Refresh`.
2. Clicking `+ Create Request` opens `CreateRequestModal.vue` (`data-testid="create-request-modal"`).
3. The modal provides input fields for Title, Description, Customer, Product, Request Type, and Priority.
4. Active Customer and Active Product dropdowns are correctly populated via lazy API calls.
5. Form validation prevents submission when Title or Description is blank.
6. Submitting valid data sends `POST /api/v1/requests`, creates the Request, automatically closes the modal, and triggers `loadFeed()`.
7. The newly created request's system-generated post appears at the top of the feed stream.
8. A success alert banner appears on `FeedView.vue` showing the new Request ID with a link to `RequestDetailView.vue`.
9. Component and workflow tests in `Cakra.Web` verify the entire modal creation and feed reload interaction.
10. Documentation artifacts (`10-scr-feed-001.md`, `feed-navigation.md`, `screen-inventory.md`, `FEAT-REQ-001`, `FEAT-AWR-001`) are updated to reflect the new capability.
