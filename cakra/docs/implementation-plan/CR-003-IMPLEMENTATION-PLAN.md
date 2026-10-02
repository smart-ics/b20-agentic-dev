---
Title: Implementation Plan for Create New Request via Operational Feed (CR-003)
Code: CR-003
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-03
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-003`: Create New Request via Operational Feed. Enable operational actors to initiate, compose, and record new Requests directly from within the Operational Feed interface (`SCR-FEED-001`) via a dedicated modal dialog component (`CreateRequestModal.vue`), automatically synchronize the feed stream upon submission so the newly generated post appears at the top of the feed, and maintain complete end-to-end traceability across documentation and test suites.

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- FEATURE: [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md), [FEAT-AWR-001-observe-operational-feed.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-AWR-001-observe-operational-feed.md)
- ARCHITECTURE: [CR-003-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-003-ARCHITECTURE.md) (authoritative capability architecture), [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- FEASIBILITY-ASSESSMENT: [CR-003-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-003-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

Architecture Applicability: ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers all frontend UI components, view integration, stream synchronization, documentation, and test suite updates required to realize `CR-003`:

1. **Frontend Component (`Cakra.Web`)**:
   - Implement `CreateRequestModal.vue` (`SCR-FEED-001` modal affordance) under `Cakra.Web/src/components/`.
   - Provide form fields: Title, Description, Customer dropdown, Product dropdown, Request Type, Priority.
   - Implement client-side validation, error handling, lazy lookup loading, and submission to `POST /api/v1/requests` via `httpClient`.
   - Emit `@close` and `@saved` events.

2. **Operational Feed View Integration (`Cakra.Web`)**:
   - Update `FeedView.vue` (`SCR-FEED-001`) to render a primary `+ Create Request` action button (`data-testid="create-request-btn"`) in the header next to `Refresh`.
   - Mount and wire `CreateRequestModal.vue` to reactive visibility state.
   - Handle `@saved`: close modal, call `loadFeed()` immediately to display the newly generated operational post at index 0, and render a dismissible success banner with a link to `SCR-REQ-003: Request Detail`.

3. **Documentation Alignment & Verification (`cakra/docs`, `Cakra.Web`)**:
   - Update UI layout documentation `10-scr-feed-001.md` with Create Request action and modal details.
   - Update navigation maps `feed-navigation.md` and `screen-inventory.md`.
   - Update feature specifications `FEAT-REQ-001` and `FEAT-AWR-001` to document request creation from the Operational Feed.
   - Verify frontend build and type checking (`npm run build`, `npm run type-check`) and execute backend regression tests (`dotnet test`).

---

# 3. Dependencies

- Node.js & npm (Vue 3 / Vite / TypeScript)
- Bootstrap 5 & Bootstrap Icons
- Existing backend API endpoint `POST /api/v1/requests` (`RequestsController.cs`)
- Existing backend post handler `RequestRecordedPostHandler.cs` and `FeedProjectionHandler.cs`
- .NET 8 SDK / C# 12 for backend regression tests

For slice dependencies:
- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires referenced slice to have implementation status `IMPLEMENTED`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Frontend Modal Component Implementation | IMPLEMENTED | GO | 1/1 |
| P2 - Feed Integration & Stream Synchronization | IMPLEMENTED | GO | 1/1 |
| P3 - Verification & Documentation Traceability Alignment | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Frontend Modal Component Implementation

Implementation Status: IMPLEMENTED
Review Status: GO

### P1-S01

Title: Implement CreateRequestModal.vue Component

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Create `CreateRequestModal.vue` under `Cakra.Web/src/components/` with fields for Title, Description, Customer dropdown (`GET /api/v1/customers/active`), Product dropdown (`GET /api/v1/products/active`), RequestType, and Priority. Enforce client-side required field validation (Title and Description non-blank). Post to `POST /api/v1/requests` via `httpClient`. Handle ProblemDetails error responses, and emit `@close` and `@saved(createdRequest)`. Lazily load lookups on show.

Depends On: None

Repository: `cakra`

Completion Criteria:
- `CreateRequestModal.vue` created under `Cakra.Web/src/components/`.
- Props `show: boolean` and emits `close`, `saved` defined with strict TypeScript types.
- Form inputs: Title (text, required), Description (textarea, required), Customer (select), Product (select), Request Type (select, defaults to `GENERAL`), Priority (select, defaults to `NORMAL`).
- Client-side validation prevents submission when Title or Description is empty/whitespace.
- Dispatches `POST /api/v1/requests` with typed `RecordRequestBody` on valid submission.
- Emits `saved(response.data)` and resets form fields on success.
- Catches errors and displays RFC 7807 problem details in an alert box without discarding inputs.
- TypeScript compilation and type check pass cleanly (`npm run type-check`).

Notes:
- Created `CreateRequestModal.vue` under `Cakra.Web/src/components/CreateRequestModal.vue` following Cakra Design System and modal standards.
- Implemented form fields: Title (text, required), Description (textarea, required), Customer select (optional), Product select (optional), Request Type select (default GENERAL), Priority select (default NORMAL).
- Client-side validation ensures Title and Description are non-blank with inline error states and alert notice.
- Lazy loading implemented for active customers (`GET /api/v1/customers/active`) and active products (`GET /api/v1/products/active`) on modal visibility (`props.show === true`).
- Implemented RFC 7807 problem details error handling without discarding form state.
- Dispatches `POST /api/v1/requests` via `httpClient` and emits `@saved` and `@close` on success.
- Validated with `npm run type-check` (vue-tsc) and `npm run build` with zero errors.

---

## P2 - Feed Integration & Stream Synchronization

Implementation Status: IMPLEMENTED
Review Status: GO

### P2-S02

Title: Integrate Create Request Modal & Action Button into Operational Feed (FeedView.vue)

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Update `FeedView.vue` (`SCR-FEED-001`) to add a primary action button `+ Create Request` (`data-testid="create-request-btn"`, class `btn btn-primary btn-sm`) in the header next to `Refresh`. Mount `CreateRequestModal.vue`, bind its visibility to reactive state, and handle `@saved` by closing modal, immediately calling `loadFeed()` to render the newly created request's system post at index 0 of the stream, and rendering a dismissible success banner with Request ID and a link to `SCR-REQ-003: Request Detail`.

Depends On: P1-S01

Repository: `cakra`

Completion Criteria:
- Header of `FeedView.vue` renders `+ Create Request` button with `data-testid="create-request-btn"`.
- Clicking `+ Create Request` sets `showCreateRequestModal = true`.
- `<CreateRequestModal :show="showCreateRequestModal" @close="showCreateRequestModal = false" @saved="handleRequestSaved" />` is mounted and operational.
- Upon `@saved`, modal is closed, `loadFeed()` is invoked immediately, and dismissible success alert banner (`data-testid="request-created-success-alert"`) appears with link to `/requests/${id}`.
- `npm run build` and `npm run type-check` complete with zero errors.

Notes:
- Updated `FeedView.vue` header to add primary `+ Create Request` button (`data-testid="create-request-btn"`, class `btn btn-primary btn-sm`) next to `Refresh`.
- Imported `CreateRequestModal` and `type CreatedRequestResponse` from `@/components/CreateRequestModal.vue`.
- Added reactive state `showCreateRequestModal = ref<boolean>(false)` and `createdRequestAlert = ref<{ id: string; title: string } | null>(null)`.
- Mounted `<CreateRequestModal :show="showCreateRequestModal" @close="showCreateRequestModal = false" @saved="handleRequestSaved" />`.
- Implemented `handleRequestSaved(createdRequest: CreatedRequestResponse)`: closes modal, triggers immediate `loadFeed()` to display the newly projected system post at index 0 of the operational feed (TD-003), and populates `createdRequestAlert`.
- Rendered Bootstrap 5 dismissible success alert banner (`data-testid="request-created-success-alert"`) directly above filter toolbar with link to `SCR-REQ-003: Request Detail` (`/requests/${createdRequestAlert.id}`) and close dismissal handler (TD-004).
- Fixed module export in `CreateRequestModal.vue` script setup to support standard SFC compiler requirements.
- Verified cleanly: `npm run type-check` (vue-tsc) passed with zero errors, and `npm run build` (vite build) generated production assets successfully.

---

## P3 - Verification & Documentation Traceability Alignment

Implementation Status: IMPLEMENTED
Review Status: GO

### P3-S03

Title: Verify Request Creation Workflow via Tests & Align Documentation Artifacts

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Verify the end-to-end request creation from the feed via frontend build validation and backend test suite execution. Update UI layout specification `10-scr-feed-001.md`, feed navigation `feed-navigation.md`, screen inventory `screen-inventory.md`, and feature documents (`FEAT-REQ-001`, `FEAT-AWR-001`) to reflect the new capability and maintain 100% traceability across all knowledge artifacts.

Depends On: P2-S02

Repository: `cakra`

Completion Criteria:
- Full frontend build and type check pass cleanly (`npm run build`).
- Full backend test suite passes with zero failures (`dotnet test`).
- `10-scr-feed-001.md` updated with "Create Request" action and modal sketch/details.
- `feed-navigation.md` updated with modal transition and direct navigation link.
- `screen-inventory.md` updated with `CreateRequestModal` affordance.
- `FEAT-REQ-001` and `FEAT-AWR-001` updated to specify request creation entry point on `SCR-FEED-001`.

Notes:
- Executed frontend type check and build (`npm run type-check` and `npm run build` in `Cakra.Web`): verified clean compilation with zero TypeScript errors and production bundle generated successfully.
- Executed backend test suite (`dotnet test Cakra.sln`): all 440 tests across unit and integration projects (Customer.Tests, Product.Tests, Request.Tests, Post.Tests, Api.IntegrationTests, Tests.Unit, Tests.Integration) passed cleanly with 0 failures.
- Updated `docs/ui-layout/10-scr-feed-001.md`: added "Create Request Modal (Affordance)" information section, "Create Request" action under Available Actions, success banner alert link under Navigation Destinations, and modal overlay layout sketch.
- Updated `docs/navigation/feed-navigation.md`: added `CreateRequestModal` overlay affordance and success banner link to navigation hierarchy, and documented the journey movement path for request creation via the operational feed.
- Updated `docs/navigation/screen-inventory.md`: added `CreateRequestModal` to the inventory summary table, updated `SCR-FEED-001` exit destinations to include `CreateRequestModal` and success banner link, and added full screen definition for `CreateRequestModal`.
- Updated `docs/features/FEAT-REQ-001-record-customer-request.md`: added `SCR-FEED-001` to screen traceability, updated capability description to include the feed modal entry point, updated business rules, and added acceptance criteria for request creation via `SCR-FEED-001`.
- Updated `docs/features/FEAT-AWR-001-observe-operational-feed.md`: added `CreateRequestModal` affordance to screen traceability, updated capability and business rules to reflect the header request creation mechanism and deterministic stream synchronization, and added corresponding acceptance criteria.

---

# 6. Change Log

- 2026-10-03: Initial release of CR-003 implementation plan covering Create Request modal component, FeedView integration, stream refresh, verification, and documentation updates. Execution Approval granted by Architect.
- 2026-10-03: User confirmed and approved execution of CR-003 implementation plan. Ready for autonomous execution by ica-developer.
