---
Title: Feasibility Assessment for Create New Request via Operational Feed (CR-003)
Code: CR-003
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-03
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Feature being assessed: Ability to create a new Request directly from / via the Operational Feed interface (`SCR-FEED-001`), per change request in [CR-003-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-003-ISSUE.md).

Referenced artifacts:

- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md), [post-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/post-domain.md)
- FEATURE: [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md), [FEAT-AWR-001-observe-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-AWR-001-observe-operational-feed.md)
- ISSUE: [CR-003-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-003-ISSUE.md)
- UI LAYOUT & NAVIGATION: [10-scr-feed-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/10-scr-feed-001.md), [feed-navigation.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/feed-navigation.md), [14-scr-req-002.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/14-scr-req-002.md)

## Objective

Assess the feasibility, existing implementation baseline, gap closure decisions, architectural impacts, and planning readiness to:

1. Enable operational actors to initiate, compose, and record a new Request directly from within the Operational Feed workspace (`SCR-FEED-001`).
2. Integrate a request creation modal dialog component (`CreateRequestModal.vue`) into `FeedView.vue`.
3. Ensure that upon request creation, the new request is persisted via `POST /api/v1/requests`, the in-process `RequestRecordedPostHandler` generates an operational post, and `FeedView.vue` refreshes immediately so the newly generated post appears at the top of the feed stream without leaving the Operational Feed context.
4. Update UI layout and navigation specifications to maintain end-to-end traceability.

---

# 2. Current State

## Existing Behavior

1. **Backend Application Layer (`Cakra.Modules.Request` & `Cakra.Modules.Post`)**:
   - `RequestService.cs` implements `RecordRequestAsync` / `RecordRequestCommand`, persisting the Request aggregate to `[request].[Requests]` with status `CAPTURED` and raising the `RequestRecorded` domain event.
   - `RequestRecordedPostHandler.cs` (in `Cakra.Modules.Post.Services`) subscribes to `RequestRecorded` via MediatR and automatically calls `_postService.RecordSystemPostAsync` with title, description, customer ID, product ID, and request ID.
   - `FeedProjectionHandler.cs` receives `PostCreated` and inserts a projected feed item into `[post].[FeedItems]`.
   - `FeedController.cs` exposes `GET /api/v1/feed`, querying `[post].[FeedItems]` ordered reverse-chronologically by `CreatedAt DESC`.
   - **Baseline Finding**: The backend API (`POST /api/v1/requests`), domain event notification, and automatic operational feed post projection pipeline are already fully functional.

2. **Frontend UI Layer (`Cakra.Web`)**:
   - `FeedView.vue` (`SCR-FEED-001`) serves as the primary operational landing workspace. It provides filter controls (Customer, Product, Exception-only), pagination, and displays feed item cards.
   - The header of `FeedView.vue` currently contains only a "Refresh" button (`data-testid="refresh-feed-btn"`). It provides no affordance, button, or mechanism to initiate or submit a new Request.
   - `CreateRequestView.vue` (`SCR-REQ-002`) exists on route `/requests/new`. It contains form fields for Title, Description, Customer dropdown (`GET /api/v1/customers/active`), Product dropdown (`GET /api/v1/products/active`), RequestType, and Priority. On submission, it executes `POST /api/v1/requests` and redirects to `/requests/${id}`.
   - Modal components like `CreateCustomerModal.vue` and `PostDetailModal.vue` demonstrate existing, proven Bootstrap 5 modal patterns in `Cakra.Web`.

3. **Documentation & Traceability**:
   - `10-scr-feed-001.md` lists available actions as Filter Feed, React to Feed Item, and Comment on Feed Item, but lacks any action to create or record a Request.
   - `feed-navigation.md` documents navigation to `SCR-REQ-003` from post reference links, but contains no transition or trigger for request creation from the feed.
   - `FEAT-REQ-001-record-customer-request.md` specifies form access via `SCR-REQ-001` or Global Quick Action, but does not explicitly document the Operational Feed as an intake entry point.
   - `CR-001` revoked direct creation of operational posts (`SC-FCOL-003`), mandating that posts originate from Request creation. However, operational actors working in the feed currently have no direct way to initiate requests from that view.

## Existing Constraints

1. Operational Feed entries must originate from Request creation (per `CR-001` directive); direct post authoring remains revoked.
2. A newly created Request must have non-empty Title, non-empty Description, valid initial status `CAPTURED`, and optional customer/product context (Request Domain Rules 1, 4, 5, 6, 7).
3. Lookups for active Customers (`GET /api/v1/customers/active`) and active Products (`GET /api/v1/products/active`) are required to populate context dropdowns.
4. Users creating a request via the Operational Feed should not be forcefully navigated away from their feed stream unless they explicitly choose to drill into request details.

---

# 3. Gap Analysis

Identify gaps between the requested FEATURE and the current system.

| ID | Severity | Gap |
|---|---|---|
| GAP-001 | CRITICAL | `FeedView.vue` (`SCR-FEED-001`) lacks a "Create Request" action button and an in-context creation component (modal or composer) to record new requests directly from the feed. |
| GAP-002 | CRITICAL | Reusable modal dialog component `CreateRequestModal.vue` does not exist to support modal-based request creation from `FeedView.vue` without duplicating logic from `CreateRequestView.vue`. |
| GAP-003 | MAJOR | Operational Feed UI layout (`10-scr-feed-001.md`), feed navigation (`feed-navigation.md`), and feature specifications (`FEAT-REQ-001`, `FEAT-AWR-001`) do not document the Request Creation capability from `SCR-FEED-001`. |
| GAP-004 | MINOR | Frontend component and integration tests asserting the ability to open the creation modal from `FeedView.vue`, submit a request, and automatically refresh the feed are missing. |

---

# 4. Open Questions

Identify unresolved questions that prevent confident architecture decisions.

| ID | Question | Impact |
|---|---|---|
| OQ-001 | Should request creation from the Operational Feed be realized via a modal dialog (`CreateRequestModal.vue`), an inline feed composer banner, or a navigation redirect to `CreateRequestView.vue`? | UI layout, component architecture, and user workflow continuity. |
| OQ-002 | After a request is successfully recorded from the feed modal, should the UI stay on `SCR-FEED-001` (refreshing the feed to show the new post) or navigate directly to `SCR-REQ-003: Request Detail`? | User experience, feed observation flow, and navigation state. |
| OQ-003 | How should lookups (active customers and products) be loaded for the modal: fetched on initial feed load, or lazily loaded when the modal opens? | Network overhead and modal initialization performance. |

---

# 5. Assumptions

Document assumptions made during the assessment.

| ID | Assumption |
|---|---|
| ASM-001 | The existing backend endpoint `POST /api/v1/requests` and in-process event handler `RequestRecordedPostHandler` function correctly and require no backend domain or contract changes. |
| ASM-002 | Existing standalone route `/requests/new` (`CreateRequestView.vue`) will remain accessible for direct URL navigation, while the Operational Feed becomes the primary operational intake affordance. |
| ASM-003 | All authenticated actors with operational roles (Implementator, Management, Request Owner) are authorized to create requests via the feed modal, matching `FEAT-REQ-001` permissions. |

---

# 6. Risks

Document identified risks.

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| RISK-001 | Feed projection timing race: if `FeedView.vue` re-fetches the feed immediately upon modal submission before `FeedProjectionHandler` completes inserting the new feed item, the new post might not appear at the top. | Medium | Backend MediatR notifications for `RequestRecorded` and `PostCreated` are handled synchronously in-process before `POST /api/v1/requests` returns HTTP 201 Created. Frontend will reload feed upon receiving 201 response. |
| RISK-002 | Form validation or API error display within the modal might cause loss of entered form data if modal closes on error. | Low | Modal remains open on submission failure, displaying specific problem-details validation messages while preserving field values. |

---

# 7. Recommendations

Recommend possible approaches for addressing major gaps.

## Option A: Dedicated Modal Dialog Component (`CreateRequestModal.vue`) — RECOMMENDED

Add a primary `+ Create Request` button in the `FeedView.vue` header. Clicking it opens a `CreateRequestModal.vue` dialog containing fields for Title, Description, Customer, Product, Request Type, and Priority. Upon submission:
1. Submits to `POST /api/v1/requests`.
2. Emits `created` event to `FeedView.vue`.
3. `FeedView.vue` closes modal, refreshes the feed stream (`loadFeed()`), and displays a success alert containing the new Request ID with an optional link to `RequestDetailView.vue`.

### Advantages

- Keeps the operational actor immersed in the Operational Feed without screen switching or loss of feed scroll position.
- Leaves the feed stream clean and uncluttered when not composing a request.
- Consistent with established patterns in the codebase (`CreateCustomerModal.vue` and `PostDetailModal.vue`).
- Provides immediate visual feedback as the newly generated system post appears at the top of the feed stream.

### Disadvantages

- Requires building a new modal component `CreateRequestModal.vue` (though form fields mirror `CreateRequestView.vue`).

## Option B: Inline Feed Composer Banner

Embed an expandable "Create Request" composer card at the top of the feed stream (similar to a social network post creator).

### Advantages

- Immediate visibility on page load with zero modal clicks.

### Disadvantages

- Takes up significant vertical screen space above the feed.
- Clutters the operational awareness dashboard when the user simply wants to monitor events.
- Handling six input fields (title, description, customer, product, type, priority) inline is awkward on smaller viewports.

## Option C: Navigation Button to `CreateRequestView.vue`

Add a `+ Create Request` button in `FeedView.vue` that executes `router.push('/requests/new')`.

### Advantages

- Requires minimal code changes (only a button in `FeedView.vue`).

### Disadvantages

- Disrupts the user's operational feed context by redirecting to a full-page form.
- Violates the direct spirit of the requirement "Create New Request shall be done via Operational Feed".

---

# 8. Gap Closure

Record resolutions for gaps and open questions.

## GAP-001

### Status

CLOSED

### Decision

Add a primary action button `+ Create Request` (`data-testid="create-request-btn"`, class `btn btn-primary btn-sm`) to the header of `FeedView.vue` (`SCR-FEED-001`) that opens the request creation dialog. Accepted by User.

### Rationale

User confirmed and accepted adding the action button for Create Request on the Operational Feed. Empowers operational actors to initiate request creation directly from the Operational Feed workspace.

### Impact

Operational actors have immediate, direct access to request creation from the default landing page.

### Architecture Impact

Updates `FeedView.vue` template and component state.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## GAP-002

### Status

CLOSED

### Decision

Implement `CreateRequestModal.vue` under `Cakra.Web/src/components/` and mount it within `FeedView.vue`. The modal captures Title, Description, Customer, Product, Request Type, and Priority, validates required inputs, calls `POST /api/v1/requests`, and emits `saved` with the created request data.

### Rationale

Option A provides the cleanest UX, keeps the feed unencumbered, and aligns with established architectural patterns (`CreateCustomerModal.vue`).

### Impact

Provides a self-contained, validated request creation dialog within the feed view.

### Architecture Impact

Adds `CreateRequestModal.vue` component and integrates it into `FeedView.vue`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## GAP-003

### Status

CLOSED

### Decision

Update UI layout specification `10-scr-feed-001.md`, feed navigation `feed-navigation.md`, and feature documents (`FEAT-REQ-001`, `FEAT-AWR-001`) to formally include the Create Request action and modal affordance on `SCR-FEED-001`.

### Rationale

Maintains complete traceability across knowledge artifacts, feature coverage, and screen inventories.

### Impact

Aligns formal documentation with user capabilities and system reality.

### Architecture Impact

Documentation updates only.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## GAP-004

### Status

CLOSED

### Decision

Add frontend unit and integration tests verifying that clicking `create-request-btn` opens `CreateRequestModal`, submitting valid payload triggers `POST /api/v1/requests`, and `FeedView` invokes `loadFeed()` upon success.

### Rationale

Guarantees regression protection for the primary operational entry point.

### Impact

High test coverage and confidence in feed-based request intake.

### Architecture Impact

Adds test specs in `Cakra.Web/tests` or equivalent frontend test suite.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## OQ-001

### Status

CLOSED

### Decision

Adopt Option A (Dedicated Modal Dialog Component `CreateRequestModal.vue`).

### Rationale

Ensures smooth workflow continuity without navigating away from the feed or consuming persistent screen real estate.

### Impact

Clean UI, consistent with `PostDetailModal.vue` and `CreateCustomerModal.vue`.

### Architecture Impact

Component structure in `Cakra.Web`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## OQ-002

### Status

CLOSED

### Decision

Stay on `SCR-FEED-001` after successful request creation. Close the modal, immediately trigger `loadFeed()` to render the new operational post at the top, and display a temporary success banner with a link to `RequestDetailView.vue` (`SCR-REQ-003`).

### Rationale

Respects the user's intent to work within the Operational Feed while providing an optional drill-down link if detailed inspection is needed.

### Impact

Seamless user experience with instant visual confirmation of the new post in the feed stream.

### Architecture Impact

FeedView event handling and alert banner rendering.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## OQ-003

### Status

CLOSED

### Decision

Lazily fetch active customers (`GET /api/v1/customers/active`) and active products (`GET /api/v1/products/active`) when `CreateRequestModal.vue` is opened, caching results while the modal remains in memory or pre-fetching alongside feed filters.

### Rationale

Minimizes unnecessary API calls on initial feed load while ensuring dropdowns have up-to-date master data when the user decides to create a request.

### Impact

Optimized network traffic and responsive feed loading.

### Architecture Impact

Modal lifecycle hook and HTTP client invocation in `CreateRequestModal.vue`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

# 9. Architecture Applicability

Record the Architecture Applicability decision.

## Decision

ARCHITECTURE-REQUIRED

## Rationale

Introducing request creation into `SCR-FEED-001: Operational Feed` requires adding the `CreateRequestModal.vue` component, modifying `FeedView.vue` component layout and event handling, updating navigation specifications (`feed-navigation.md`), and synchronizing feature specifications (`FEAT-AWR-001`, `FEAT-REQ-001`). Formal technical realization and implementation slice planning by `ica-architect` are required to maintain architectural consistency.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved
- [x] All required decisions recorded
- [x] All blocking open questions resolved
- [x] Architecture can be finalized or updated

## Status

READY-FOR-PLANNING

## Notes

All feasibility gaps, open questions, and closure decisions have been reviewed and accepted. Gate `READY-FOR-PLANNING` granted by `ica-architect`. Technical realization will be defined in target architecture artifact `CR-003-ARCHITECTURE.md`.

---

# 11. References

Referenced artifacts:

- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- DOMAIN: [post-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/post-domain.md)
- FEATURE: [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md)
- FEATURE: [FEAT-AWR-001-observe-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-AWR-001-observe-operational-feed.md)
- ISSUE: [CR-003-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-003-ISSUE.md)
- UI LAYOUT: [10-scr-feed-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/10-scr-feed-001.md)
- NAVIGATION: [feed-navigation.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/feed-navigation.md)

Referenced codebase locations:

- [FeedView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue)
- [CreateRequestView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/CreateRequestView.vue)
- [CreateCustomerModal.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/CreateCustomerModal.vue)
- [RequestsController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/RequestsController.cs)
- [RequestRecordedPostHandler.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Post/Services/RequestRecordedPostHandler.cs)
- [FeedProjectionHandler.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Post/Services/FeedProjectionHandler.cs)
