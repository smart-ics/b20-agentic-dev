---
Title: Feasibility Assessment for Removing SCR-REQ-001 Screen Request (CR-017)
Code: CR-017
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-06
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Change being assessed: Removal and retirement of `SCR-REQ-001` (Request List screen), per change request documented in [CR-017-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-017-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-017-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-017-ISSUE.md)
- NAVIGATION: [navigation-map.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/navigation-map.md), [request-navigation.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/request-navigation.md), [collaboration-navigation.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/collaboration-navigation.md), [screen-inventory.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/screen-inventory.md)
- UI LAYOUT: [13-scr-req-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/13-scr-req-001.md), [00-screen-inventory.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/00-screen-inventory.md), [01-screen-to-use-case-matrix.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/01-screen-to-use-case-matrix.md)
- FEATURES: [FEAT-REQ-002-assign-request-owner.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-002-assign-request-owner.md), [FEAT-REQ-008-review-request-completion.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-008-review-request-completion.md), [FEAT-COL-003-track-request-progress.md](file:///d:/Project.Aktic/b20-agentic-dev/cakra/docs/features/FEAT-COL-003-track-request-progress.md)

## Objective

Assess the feasibility, architectural implications, navigation flow impact, and planning readiness to:

1. Safely decommission and remove `RequestListView.vue` (`SCR-REQ-001`) from `src/frontend/Cakra.Web/src/views/`.
2. Configure route `/requests` in `src/frontend/Cakra.Web/src/router/index.ts` to redirect directly to `/feed` (`Operational Feed`), preserving URL backwards compatibility.
3. Update return/back navigation across `CreateRequestView.vue` and `RequestDetailView.vue` to dynamically navigate to the originating caller (`router.back()`) with fallback to `/feed`.
4. Clean up obsolete "All Requests" buttons in `MyRequestsView.vue` and `RequestSearchView.vue`.
5. Verify that backend query services and endpoints (`GET /api/v1/requests`) remain untouched and operational for dependent screens (`SCR-WP-001` Work Packages and `SCR-REQ-005` Request Search).
6. Update navigation specifications and layout documentation to reflect the retirement of `SCR-REQ-001` without renumbering other screen IDs.

---

# 2. Current State

## Existing Behavior

1. **Frontend Router & Views (`Cakra.Web`)**:
   - `src/frontend/Cakra.Web/src/router/index.ts` imports `RequestListView` and registers route `/requests` associated with `screenId: 'SCR-REQ-001'`.
   - `RequestListView.vue` renders an operational data table of all requests with status filters, search input, and action links to `/requests/create` and `/requests/:id`.
   - `CreateRequestView.vue` has a "Back to Requests" button linking to `/requests` and `handleCancel()` navigating to `/requests`.
   - `RequestDetailView.vue` has a "Back to Requests" button linking to `/requests` and `handleBack()` navigating to `/requests`.
   - `MyRequestsView.vue` renders a top action button "All Requests" (`data-testid="all-requests-link"`) linking to `/requests`.
   - `RequestSearchView.vue` renders a top action button "All Requests" (`data-testid="all-requests-link"`) linking to `/requests`.
   - `App.vue` includes a topbar title check for `path.startsWith('/requests')` returning `'Operational Requests'`.

2. **Backend API Endpoints (`Cakra.Api`, `Cakra.Modules.Request`)**:
   - `RequestsController.cs` exposes `GET /api/v1/requests` backed by `IRequestQueryService.GetPagedRequestsAsync`.
   - This endpoint is utilized by:
     - `WorkPackageView.vue` (for searching and adding requests into work package scope).
     - `RequestSearchView.vue` (for cross-cutting request search and historical inspection).
     - Backend integration smoke tests in `CrossCuttingSystemIntegrationTests.cs`.

3. **System Documentation & Specifications**:
   - `13-scr-req-001.md` documents the layout specification for `SCR-REQ-001`.
   - `navigation-map.md` and `request-navigation.md` document `SCR-REQ-001` as a secondary/contextual navigation destination.
   - `screen-inventory.md`, `00-screen-inventory.md`, and `01-screen-to-use-case-matrix.md` list `SCR-REQ-001` supporting `UC-REQ-002`, `UC-REQ-008`, and `UC-COL-003`.

## Existing Constraints

1. Backward compatibility: Existing bookmarks and external URLs targeting `/requests` must not result in 404 or broken navigation.
2. Screen identity stability: Existing screen identifiers (`SCR-REQ-002`, `SCR-REQ-003`, `SCR-REQ-004`, `SCR-REQ-005`) must not be renumbered, avoiding systemic breaks across user journeys, test fixtures, and architectural diagrams.
3. Backend service preservation: Backend endpoints must not be removed as multiple active features rely on `GET /api/v1/requests`.

---

# 3. Gap Analysis

| ID | Severity | Gap |
|---|---|---|
| GAP-001 | CRITICAL | `RequestListView.vue` (`SCR-REQ-001`) is redundant since all operational requests are tracked in `SCR-FEED-001`, but it remains in the codebase and active router table. |
| GAP-002 | MAJOR | Route `/requests` resolves to `RequestListView.vue` rather than redirecting to `/feed`. |
| GAP-003 | MAJOR | `CreateRequestView.vue` and `RequestDetailView.vue` statically return to `/requests` rather than returning dynamically to the caller (`router.back()`) with fallback to `/feed`. |
| GAP-004 | MINOR | `MyRequestsView.vue` and `RequestSearchView.vue` include obsolete "All Requests" buttons targeting `/requests`. |
| GAP-005 | MINOR | System navigation and UI layout documentation specify `SCR-REQ-001` as an active screen rather than retired/superseded. |

---

# 4. Open Questions

| ID | Question | Impact | Status |
|---|---|---|---|
| OQ-001 | How should direct navigation to `/requests` behave after `SCR-REQ-001` is removed? | Route table behavior and deep link UX. | CLOSED |
| OQ-002 | How should return/back buttons in `CreateRequestView` and `RequestDetailView` behave? | Return flow navigation continuity. | CLOSED |
| OQ-003 | What should happen to the "All Requests" buttons in `MyRequestsView` and `RequestSearchView`? | In-screen secondary navigation cleanliness. | CLOSED |
| OQ-004 | Does removing `SCR-REQ-001` impact backend API endpoints (`GET /api/v1/requests`)? | Backend service lifecycle and integration tests. | CLOSED |
| OQ-005 | Should remaining Request screen IDs be renumbered? | Documentation, test suite, and traceability stability. | CLOSED |
| OQ-006 | How should UI layout specification `13-scr-req-001.md` be maintained? | Traceability and audit history. | CLOSED |

---

# 5. Assumptions

| ID | Assumption |
|---|---|
| ASM-001 | Operational Feed (`SCR-FEED-001`) provides all necessary visualization for incoming, in-progress, and completed requests, making a dedicated standalone list screen redundant. |
| ASM-002 | Browser history dynamic navigation (`router.back()`) is reliable in `Cakra.Web` with fallback to `/feed` when history length is insufficient or accessed directly. |
| ASM-003 | Backend API `GET /api/v1/requests` must remain fully supported for `WorkPackageView.vue` and `RequestSearchView.vue`. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| RISK-001 | Direct navigation to `/requests` fails or shows blank page if route is deleted without redirection. | Users lose access to request flow. | Redirect `/requests` -> `/feed` explicitly in `router/index.ts`. |
| RISK-002 | Breaking test assertions in integration or smoke tests expecting `SCR-REQ-001` endpoints. | Test failure during CI build. | Keep backend endpoints intact; update test comments/descriptions to note screen retirement where applicable. |
| RISK-003 | Renumbering screens causes widespread broken cross-references in specs. | Massive documentation drift. | Explicitly preserve `SCR-REQ-002..005` IDs without renumbering. |

---

# 7. Recommendations

## Option A: Clean Frontend Retirement with Route Redirection (Recommended)
- Remove `RequestListView.vue`.
- Redirect `/requests` -> `/feed` in `router/index.ts`.
- Update `CreateRequestView` and `RequestDetailView` to `router.back()` with fallback to `/feed`.
- Remove "All Requests" buttons in `MyRequestsView` and `RequestSearchView`.
- Mark `13-scr-req-001.md` as RETIRED / SUPERSEDED BY SCR-FEED-001.
- Retain all backend API endpoints and existing screen IDs.

### Advantages
- Completely eliminates redundant component code.
- Zero risk to backend integrations or other views.
- Seamless user experience for existing bookmarks.
- Full traceability preserved in system documentation.

### Disadvantages
- None identified.

---

# 8. Gap Closure

## GAP-001 & GAP-002 (Screen Removal & Route Redirection)
### Decision
Delete `src/frontend/Cakra.Web/src/views/RequestListView.vue`. In `src/frontend/Cakra.Web/src/router/index.ts`, remove `RequestListView` import and configure `/requests` as a direct redirect to `/feed`.
### Rationale
Operational requests are now visualized and tracked in `SCR-FEED-001`. Redirecting `/requests` ensures existing links smoothly transition users to the Operational Feed.
### Impact
Frontend routing and view catalog.
### Architecture Impact
Retirement of screen `SCR-REQ-001` from the presentation layer; router update.
### Resolved By
User & Analyst
### Resolved Date
2026-10-06

---

## GAP-003 (Dynamic Return Navigation)
### Decision
Update `CreateRequestView.vue` and `RequestDetailView.vue` so that "Back" and "Cancel" buttons call a dynamic return handler that invokes `router.back()` if navigation history exists, falling back to `router.push('/feed')`. Update template labels from "Back to Requests" to "Back".
### Rationale
Users may arrive at Create or Detail from the Operational Feed, My Requests, Work Packages, or Management analytics. Dynamic history return ensures the user seamlessly returns to their true origin point.
### Impact
Frontend views `CreateRequestView.vue` and `RequestDetailView.vue`.
### Architecture Impact
Local presentation interaction pattern update.
### Resolved By
User & Analyst
### Resolved Date
2026-10-06

---

## GAP-004 (Obsolete Action Buttons)
### Decision
Remove the "All Requests" buttons (`data-testid="all-requests-link"`) from `src/frontend/Cakra.Web/src/views/MyRequestsView.vue` and `src/frontend/Cakra.Web/src/views/RequestSearchView.vue`.
### Rationale
The Request List screen is decommissioned, and the Operational Feed is already prominently featured in the global sidebar navigation.
### Impact
Frontend views `MyRequestsView.vue` and `RequestSearchView.vue`.
### Architecture Impact
Minor UI layout cleanup.
### Resolved By
User & Analyst
### Resolved Date
2026-10-06

---

## GAP-005 (Documentation Alignment)
### Decision
Mark `13-scr-req-001.md` as RETIRED / SUPERSEDED BY SCR-FEED-001. Update `screen-inventory.md`, `navigation-map.md`, and `request-navigation.md` to reflect that `SCR-REQ-001` is retired and that requests are tracked via `SCR-FEED-001`. Preserve `SCR-REQ-002..005` IDs without renumbering.
### Rationale
Preserves historical documentation traceability while aligning system specifications with the updated user interface.
### Impact
Working and permanent documentation artifacts in `cakra/docs/`.
### Architecture Impact
Screen catalog and navigation specifications update.
### Resolved By
User & Analyst
### Resolved Date
2026-10-06

---

## OQ-001 through OQ-006 (Closed Decisions)
### Decision
All open questions recorded in Section 4 are resolved and incorporated into the gap closure decisions above.
### Rationale
Full alignment was achieved through the collaborative intake interview.
### Impact
Eliminates all ambiguity for architecture and planning.
### Architecture Impact
Provides unambiguous input for target architecture updates.
### Resolved By
User & Analyst
### Resolved Date
2026-10-06

---

# 9. Architecture Applicability

## Decision
ARCHITECTURE-REQUIRED

## Rationale
Although the change is primarily a frontend screen decommissioning, it involves:
1. Decommissioning an authoritative screen entity (`SCR-REQ-001`) from the system screen inventory.
2. Modifying navigation topologies, route tables, and inter-screen interaction paths.
3. Updating architectural documentation and screen catalogs across the system.

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

- ISSUE: [CR-017-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-017-ISSUE.md)
- NAVIGATION: [navigation-map.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/navigation-map.md), [request-navigation.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/request-navigation.md), [screen-inventory.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/screen-inventory.md)
- UI LAYOUT: [13-scr-req-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/13-scr-req-001.md)

Referenced codebase locations:

- [router/index.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/router/index.ts)
- [RequestListView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestListView.vue)
- [CreateRequestView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/CreateRequestView.vue)
- [RequestDetailView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue)
- [MyRequestsView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/MyRequestsView.vue)
- [RequestSearchView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestSearchView.vue)
