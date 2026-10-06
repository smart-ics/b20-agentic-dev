# ISSUE

## Metadata

ID: CR-017
Type: CHANGE-REQUEST
Status: OPEN
Title: Remove SCR-REQ-001 Screen Request

## Source

Reported By: User
Reported Date: 2026-10-06

## Description

The user requested the removal of `SCR-REQ-001` (Request List screen), as operational requests are now visualized and tracked directly within the Operational Feed (`SCR-FEED-001`), making the separate dedicated request list view redundant.

## Desired Outcome

1. `SCR-REQ-001` (Request List screen) is removed from the active screen catalog and retired.
2. Direct navigation to `/requests` redirects to `/feed` (`Operational Feed`), preserving backwards compatibility for bookmarks and direct URL entries.
3. Screens previously returning to `SCR-REQ-001` (such as `SCR-REQ-002: Create Request` and `SCR-REQ-003: Request Detail`) utilize dynamic return navigation to their originating screen with fallback to `/feed`.
4. Dependent views (`SCR-REQ-004: My Requests` and `SCR-REQ-005: Request Search`) remove obsolete navigation buttons targeting `SCR-REQ-001`.
5. Backend API endpoints (including `GET /api/v1/requests`) remain active and unaffected to support querying by Work Packages (`SCR-WP-001`) and Request Search (`SCR-REQ-005`).
6. Existing Screen IDs (`SCR-REQ-002`, `SCR-REQ-003`, `SCR-REQ-004`, `SCR-REQ-005`) are preserved without renumbering.
7. System documentation and UI specifications reflect the retirement of `SCR-REQ-001`, preserving historical traceability.

## Current Situation

1. The route `/requests` renders `RequestListView.vue` with metadata screen ID `SCR-REQ-001`.
2. Multiple views (`CreateRequestView.vue`, `RequestDetailView.vue`, `MyRequestsView.vue`, `RequestSearchView.vue`) contain buttons or back navigation targeting `/requests`.
3. Navigation maps, screen inventories, and UI layout documentation specify `SCR-REQ-001` as an active operational screen.

## Evidence

- User directive: "SCR-REQ-001 Screen Request is not used anymore since all request has been visualize in Operational Feed. Please remove SCR-REQ-001"
- Screen component: [RequestListView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestListView.vue)
- Router definition: [router/index.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/router/index.ts#L48-L55)
- UI Layout specification: [13-scr-req-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/13-scr-req-001.md)
- Screen inventory: [screen-inventory.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/screen-inventory.md#L35-L60)

## Notes

- Intake interview confirmed:
  - Route `/requests` redirects to `/feed`.
  - Dynamic `router.back()` navigation with fallback to `/feed` for Create and Detail return buttons.
  - Removal of "All Requests" buttons in `MyRequestsView` and `RequestSearchView`.
  - Preservation of existing screen IDs without renumbering.
  - Pure frontend removal (backend query APIs remain untouched).
  - Marking `13-scr-req-001.md` as RETIRED / SUPERSEDED BY SCR-FEED-001.
- Detailed feasibility assessment, technical impact analysis, and architecture updates belong to downstream SDLC stages.
