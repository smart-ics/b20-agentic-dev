---
Title: Implementation Plan for Removal of Request and Request Search from Left Sidebar Menu (CR-004)
Code: CR-004
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-03
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-004`: Removal of the "Request" (`/requests`, `SCR-REQ-001`) and "Request Search" (`/requests/search`, `SCR-REQ-004`) menu items from the left sidebar navigation menu in `Cakra.Web` (`src/frontend/Cakra.Web/src/App.vue`).

Preserve all underlying Vue view components (`RequestListView.vue`, `RequestSearchView.vue`), route definitions in `router/index.ts`, and contextual in-view navigation buttons (e.g. "All Requests" on `MyRequestsView.vue`). Synchronize system navigation architecture documentation (`navigation-map.md`, `request-navigation.md`, `collaboration-navigation.md`) and verify clean build compilation.

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- FEATURE: [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md), [FEAT-COL-002-search-request-history.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-COL-002-search-request-history.md)
- ARCHITECTURE: [CR-004-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-004-ARCHITECTURE.md) (authoritative capability architecture)
- FEASIBILITY-ASSESSMENT: [CR-004-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-004-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

Architecture Applicability: ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers all frontend application shell modifications, documentation alignment, and build verification required to realize `CR-004`:

1. **Frontend Application Shell (`Cakra.Web`)**:
   - Refactor `src/frontend/Cakra.Web/src/App.vue` to remove `<router-link to="/requests">` (`data-testid="nav-requests-link"`) and `<router-link to="/requests/search">` (`data-testid="nav-request-search-link"`).
   - Ensure the "Operations" navigation section contains exclusively: "Operational Feed" (`/feed`), "My Requests" (`/requests/my`), and "Work Packages" (`/work-packages`).
   - Retain responsive sidebar collapse/expand controls and `currentScreenTitle` topbar title mappings.

2. **Navigation Documentation Synchronization (`cakra/docs/navigation/`)**:
   - Update `navigation-map.md` (Navigation Hierarchy Tree and Requests Area destinations).
   - Update `request-navigation.md` (Movement paths and entry points).
   - Update `collaboration-navigation.md` (Search Request History entry points).

3. **Build & Route Integrity Verification (`Cakra.Web`)**:
   - Verify TypeScript compilation and production packaging (`npm run build`).
   - Confirm routes `/requests` and `/requests/search` remain declared and functional via direct URL and secondary links.

---

# 3. Dependencies

- Node.js & npm (Vue 3 / Vite / TypeScript in `src/frontend/Cakra.Web`)
- Frontend application shell `App.vue`
- Vue Router configuration in `src/frontend/Cakra.Web/src/router/index.ts`
- Documentation repository in `cakra/docs/navigation/`

For slice dependencies:
- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires referenced slice to have implementation status `IMPLEMENTED`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Frontend Application Shell Refactoring | IMPLEMENTED | GO | 1/1 |
| P2 - Navigation Documentation Synchronization | IMPLEMENTED | GO | 1/1 |
| P3 - Build Verification & Route Integrity | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Frontend Application Shell Refactoring

Implementation Status: NOT-STARTED
Review Status: GO

### P1-S01

Title: Remove Request and Request Search Items from App.vue Left Sidebar

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Update `src/frontend/Cakra.Web/src/App.vue` to remove the navigation links for "Requests" (`to="/requests"`, `data-testid="nav-requests-link"`) and "Request Search" (`to="/requests/search"`, `data-testid="nav-request-search-link"`). Retain the other items under the "Operations" section: "Operational Feed" (`/feed`), "My Requests" (`/requests/my`), and "Work Packages" (`/work-packages`). Retain `currentScreenTitle` logic and sidebar collapse state.

Depends On: None

Repository: `cakra`

Completion Criteria:
- In `src/frontend/Cakra.Web/src/App.vue`, the `<router-link>` elements for `/requests` and `/requests/search` are removed from the template.
- The "Operations" navigation section renders:
  1. `Operational Feed` (`to="/feed"`)
  2. `My Requests` (`to="/requests/my"`, `data-testid="nav-my-requests-link"`)
  3. `Work Packages` (`to="/work-packages"`, `data-testid="nav-work-packages-link"`)
- The computed `currentScreenTitle` in `App.vue` continues to provide clean titles for all paths.
- Responsive collapse/expand and mobile drawer behavior remain fully operational.

Notes:
Do not modify `src/frontend/Cakra.Web/src/router/index.ts` or delete view components.

Implementation Notes:
- Removed `<router-link to="/requests" data-testid="nav-requests-link">` from `src/frontend/Cakra.Web/src/App.vue`.
- Removed `<router-link to="/requests/search" data-testid="nav-request-search-link">` from `src/frontend/Cakra.Web/src/App.vue`.
- Operations section cleanly contains Operational Feed (`/feed`), My Requests (`/requests/my`), and Work Packages (`/work-packages`).
- Preserved `currentScreenTitle` logic and responsive sidebar collapse/mobile drawer functionality.
- Did not touch `router/index.ts` or view components.

Changed Files:
- `src/frontend/Cakra.Web/src/App.vue`
- `docs/implementation-plan/CR-004-IMPLEMENTATION-PLAN.md`

---

## P2 - Navigation Documentation Synchronization

Implementation Status: NOT-STARTED
Review Status: GO

### P2-S02

Title: Synchronize Navigation Maps and Movement Path Specifications

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Update `cakra/docs/navigation/navigation-map.md`, `cakra/docs/navigation/request-navigation.md`, and `cakra/docs/navigation/collaboration-navigation.md` to reflect that `SCR-REQ-001: Request List` and `SCR-REQ-004: Request Search & History` are contextual navigation destinations rather than primary global sidebar items, aligning system documentation with the updated UI application shell.

Depends On: None

Repository: `cakra`

Completion Criteria:
- `navigation-map.md`: Update Section 2 (Navigation Hierarchy Tree) and Section 3 (`Area: Requests`) to record `SCR-REQ-005: My Assigned Requests` as the primary global sidebar navigation destination for requests, and annotate `SCR-REQ-001` and `SCR-REQ-004` as contextual / secondary destinations.
- `request-navigation.md`: Update Section 2 Journey Movement Paths for `UJ-REQ-001`, `UJ-REQ-002`, and `UJ-REQ-008` to reflect entry via `SCR-REQ-005`, `SCR-FEED-001`, or contextual links.
- `collaboration-navigation.md`: Update `UJ-COL-002: Search Request History` entry point from `Global Navigation (Requests > Search & History)` to contextual/direct navigation.

Notes:
Preserve existing markdown structure and traceability links.

Implementation Notes:
- Updated `cakra/docs/navigation/navigation-map.md` Section 2 (Navigation Hierarchy Tree) and Section 3 (Area: Requests) to classify `SCR-REQ-005: My Assigned Requests` as the primary global sidebar destination, and `SCR-REQ-001` and `SCR-REQ-004` as contextual / secondary navigation destinations.
- Updated `cakra/docs/navigation/request-navigation.md` Section 1 (Hierarchy) and Section 2 (`UJ-REQ-001`, `UJ-REQ-002`, `UJ-REQ-008`) to define primary global sidebar entry points to `SCR-FEED-001` or `SCR-REQ-005` with contextual navigation to `SCR-REQ-001: Request List` via "All Requests" link.
- Updated `cakra/docs/navigation/collaboration-navigation.md` Section 1 (Hierarchy) and Section 2 (`UJ-COL-002`) to update entry point from global navigation to contextual navigation / direct URL (`/requests/search`) or `SCR-REQ-001: Request List`.
- Preserved all markdown structures, diagrams, traceability tables, and cross references.

Changed Files:
- `cakra/docs/navigation/navigation-map.md`
- `cakra/docs/navigation/request-navigation.md`
- `cakra/docs/navigation/collaboration-navigation.md`
- `cakra/docs/implementation-plan/CR-004-IMPLEMENTATION-PLAN.md`

---

## P3 - Build Verification & Route Integrity

Implementation Status: NOT-STARTED
Review Status: GO

### P3-S03

Title: Frontend Build Verification and Route Integrity Validation

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Execute the frontend build process (`npm run build` / `vue-tsc --noEmit`) in `src/frontend/Cakra.Web` to ensure zero compilation or type-checking errors. Validate that routes `/requests` and `/requests/search` remain cleanly defined and accessible via direct URLs and in-view toolbar buttons (e.g. from `MyRequestsView.vue`).

Depends On: P1-S01, P2-S02

Repository: `cakra`

Completion Criteria:
- `npm run build` in `src/frontend/Cakra.Web` exits with code 0 without errors or warnings.
- `router/index.ts` route declarations for `/requests` and `/requests/search` are confirmed intact.
- Secondary navigation pathways from `MyRequestsView.vue` (`data-testid="all-requests-link"`) and `RequestDetailView.vue` (`data-testid="back-to-requests-link"`) are verified intact.

Notes:
Executes after slices P1-S01 and P2-S02 are implemented.

Implementation Notes:
- Executed `npm run build` (`vue-tsc --noEmit && vite build`) in `src/frontend/Cakra.Web`; succeeded cleanly with exit code 0 and 0 errors/warnings (128 modules transformed, production bundle generated in dist/).
- Verified `router/index.ts` maintains `/requests` (`SCR-REQ-001`, `RequestListView`) and `/requests/search` (`SCR-REQ-005`, `RequestSearchView`) intact.
- Verified secondary navigation links to `/requests` in `MyRequestsView.vue` (`data-testid="all-requests-link"`) and `RequestDetailView.vue` (`data-testid="back-to-requests-link"`) remain present and functional.

Changed Files:
- `docs/implementation-plan/CR-004-IMPLEMENTATION-PLAN.md`

---

# 6. Change Log

- 2026-10-03: Initial implementation plan created by `ica-architect` for `CR-004`. Structure finalized and `Execution Approval: APPROVED` granted.
