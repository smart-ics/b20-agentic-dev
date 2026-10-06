---
Title: Implementation Plan for Removal of SCR-REQ-001 Screen Request (CR-017)
Code: CR-017
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-06
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-017`: Retirement and removal of `SCR-REQ-001` (Request List screen) in `Cakra.Web`.

Decommission `RequestListView.vue`, reconfigure route `/requests` in `src/frontend/Cakra.Web/src/router/index.ts` to redirect directly to `/feed`, refactor return navigation in `CreateRequestView.vue` and `RequestDetailView.vue` to use dynamic history return (`router.back()` with fallback to `/feed`), remove obsolete "All Requests" buttons from `MyRequestsView.vue` and `RequestSearchView.vue`, synchronize all system documentation and UI specifications, and verify build integrity across the application.

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- ISSUE: [CR-017-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-017-ISSUE.md)
- FEASIBILITY-ASSESSMENT: [CR-017-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-017-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)
- ARCHITECTURE: [CR-017-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-017-ARCHITECTURE.md) (authoritative target architecture)

Architecture Applicability: ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers all frontend routing changes, component deletions, return navigation updates, documentation alignment, and build verification required to realize `CR-017`:

1. **Frontend Routing & Component Deletion (`Cakra.Web`)**:
   - Update `src/frontend/Cakra.Web/src/router/index.ts`: remove `RequestListView` import and redirect `/requests` -> `/feed`.
   - Delete `src/frontend/Cakra.Web/src/views/RequestListView.vue`.
   - Update `App.vue` `currentScreenTitle` helper.
2. **In-View Return Navigation & Button Cleanup (`Cakra.Web`)**:
   - Update `CreateRequestView.vue`: convert cancel and header back button to dynamic history return (`router.back()` with fallback to `/feed`) with label "Back".
   - Update `RequestDetailView.vue`: convert back button to dynamic history return with label "Back".
   - Remove obsolete "All Requests" buttons from `MyRequestsView.vue` and `RequestSearchView.vue`.
3. **Documentation & Specification Synchronization (`cakra/docs/`)**:
   - Mark `13-scr-req-001.md` as RETIRED / SUPERSEDED BY SCR-FEED-001.
   - Synchronize `screen-inventory.md`, `navigation-map.md`, `request-navigation.md`, `00-screen-inventory.md`, and `01-screen-to-use-case-matrix.md`.
4. **Build & Regression Verification**:
   - Verify frontend TypeScript compilation and packaging via `npm run build`.
   - Verify backend build integrity via `dotnet build cakra/Cakra.sln`.

---

# 3. Dependencies

- Node.js & npm (Vue 3 / Vite / TypeScript in `src/frontend/Cakra.Web`)
- .NET 8 SDK for backend verification
- Vue Router in `src/frontend/Cakra.Web/src/router/index.ts`
- Documentation repository in `cakra/docs/`

For slice dependencies:
- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires referenced slice to have implementation status `IMPLEMENTED`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Router Redirection & Component Retirement | IMPLEMENTED | GO | 1/1 |
| P2 - In-View Return Navigation & Action Cleanup | IMPLEMENTED | GO | 1/1 |
| P3 - Documentation & Specification Synchronization | IMPLEMENTED | GO | 1/1 |
| P4 - Build Verification & Integrity Check | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Router Redirection & Component Retirement

Implementation Status: IMPLEMENTED
Review Status: GO

### P1-S01

Title: Reconfigure /requests Route and Retire RequestListView.vue

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Decommission `RequestListView.vue` and configure route `/requests` to redirect to `/feed`.
1. In `src/frontend/Cakra.Web/src/router/index.ts`:
   - Remove `import RequestListView from '@/views/RequestListView.vue'`.
   - Replace the `/requests` route entry with:
     ```typescript
     {
       path: '/requests',
       name: 'requests-redirect',
       redirect: '/feed',
     },
     ```
2. Delete `src/frontend/Cakra.Web/src/views/RequestListView.vue`.
3. In `src/frontend/Cakra.Web/src/App.vue`:
   - Clean up `currentScreenTitle` logic so redundant `/requests` prefix does not conflict with child paths.

Depends On: None

Repository: `cakra`

Completion Criteria:
- `RequestListView.vue` is deleted from the filesystem.
- `src/frontend/Cakra.Web/src/router/index.ts` cleanly redirects `/requests` to `/feed`.
- No lingering references to `RequestListView` remain in `router/index.ts`.

Implementation Notes:
- Removed `import RequestListView from '@/views/RequestListView.vue'` and replaced `/requests` route record with `{ path: '/requests', name: 'requests-redirect', redirect: '/feed' }` in `src/frontend/Cakra.Web/src/router/index.ts`.
- Deleted `src/frontend/Cakra.Web/src/views/RequestListView.vue` from the filesystem.
- Removed redundant `if (path.startsWith('/requests')) return 'Operational Requests'` check from `currentScreenTitle` computed property in `src/frontend/Cakra.Web/src/App.vue`.
- Verified frontend build via `npm run build` (`vue-tsc --noEmit && vite build`), completing with 0 errors.

Changed Files:
- `src/frontend/Cakra.Web/src/router/index.ts`
- `src/frontend/Cakra.Web/src/views/RequestListView.vue` (deleted)
- `src/frontend/Cakra.Web/src/App.vue`

---

## P2 - In-View Return Navigation & Action Cleanup

Implementation Status: IMPLEMENTED
Review Status: GO

### P2-S02

Title: Refactor In-View Return Navigation and Remove Obsolete Buttons

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Update dependent views to replace static `/requests` targets with dynamic history return and remove obsolete links.
1. In `src/frontend/Cakra.Web/src/views/CreateRequestView.vue`:
   - Replace static `router.push('/requests')` in `handleCancel` with dynamic history check:
     `if (window.history.length > 1) { router.back() } else { await router.push('/feed') }`.
   - Convert the top "Back to Requests" `<router-link to="/requests">` to a button executing `handleCancel` or dynamic back, labeled "Back".
2. In `src/frontend/Cakra.Web/src/views/RequestDetailView.vue`:
   - Replace static `router.push('/requests')` in `handleBack` with dynamic history check:
     `if (window.history.length > 1) { router.back() } else { await router.push('/feed') }`.
   - Convert the top "Back to Requests" `<router-link to="/requests">` to a button executing `handleBack`, labeled "Back".
3. In `src/frontend/Cakra.Web/src/views/MyRequestsView.vue`:
   - Remove the obsolete "All Requests" `<router-link to="/requests" data-testid="all-requests-link">`.
4. In `src/frontend/Cakra.Web/src/views/RequestSearchView.vue`:
   - Remove the obsolete "All Requests" `<router-link to="/requests" data-testid="all-requests-link">`.

Depends On: P1-S01

Repository: `cakra`

Completion Criteria:
- "Back to Requests" buttons in `CreateRequestView.vue` and `RequestDetailView.vue` are replaced with dynamic "Back" buttons returning to caller or `/feed`.
- "All Requests" buttons are completely removed from `MyRequestsView.vue` and `RequestSearchView.vue`.
- No view templates contain dead `<router-link to="/requests">` elements.

Implementation Notes:
- In `src/frontend/Cakra.Web/src/views/CreateRequestView.vue`:
  - Added `handleBack()` with dynamic history return check (`window.history.length > 1 ? router.back() : router.push('/feed')`).
  - Refactored `handleCancel()` to delegate to `handleBack()`.
  - Replaced `<router-link to="/requests">` with `<button @click="handleBack" data-testid="back-button">` labeled "Back".
- In `src/frontend/Cakra.Web/src/views/RequestDetailView.vue`:
  - Added `handleBack()` with dynamic history return check (`window.history.length > 1 ? router.back() : router.push('/feed')`), replacing static `navigateBackToList()`.
  - Replaced `<router-link to="/requests">` with `<button @click="handleBack" data-testid="back-button">` labeled "Back".
- In `src/frontend/Cakra.Web/src/views/MyRequestsView.vue`:
  - Removed obsolete "All Requests" `<router-link to="/requests" data-testid="all-requests-link">`.
- In `src/frontend/Cakra.Web/src/views/RequestSearchView.vue`:
  - Removed obsolete "All Requests" `<router-link to="/requests" data-testid="all-requests-link">`.
- Verified frontend compilation and packaging via `npm run build`, completing with 0 errors.

Changed Files:
- `src/frontend/Cakra.Web/src/views/CreateRequestView.vue`
- `src/frontend/Cakra.Web/src/views/RequestDetailView.vue`
- `src/frontend/Cakra.Web/src/views/MyRequestsView.vue`
- `src/frontend/Cakra.Web/src/views/RequestSearchView.vue`

---

## P3 - Documentation & Specification Synchronization

Implementation Status: IMPLEMENTED
Review Status: GO

### P3-S03

Title: Synchronize System Navigation Specifications and UI Layout Specs

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Update system documentation to reflect the retirement of `SCR-REQ-001` while preserving screen ID stability.
1. In `cakra/docs/ui-layout/13-scr-req-001.md`:
   - Add permanent retirement header noting it has been superseded by `SCR-FEED-001` (Operational Feed).
2. In `cakra/docs/navigation/screen-inventory.md`:
   - Update summary table and definition to mark `SCR-REQ-001` as retired/superseded by `SCR-FEED-001`.
   - Retain `SCR-REQ-002..005` IDs without renumbering.
3. In `cakra/docs/navigation/navigation-map.md`:
   - Update Section 2 (Hierarchy Tree) and Section 3 (Requests Area) to remove `SCR-REQ-001` as an active screen, noting requests are visualized in `SCR-FEED-001`.
4. In `cakra/docs/navigation/request-navigation.md`:
   - Update entry/return points in `UJ-REQ-001`, `UJ-REQ-002`, and `UJ-REQ-008` to reference `SCR-FEED-001` and dynamic return navigation.
5. In `cakra/docs/ui-layout/00-screen-inventory.md` and `01-screen-to-use-case-matrix.md`:
   - Mark `SCR-REQ-001` as retired/superseded.

Depends On: None

Repository: `cakra`

Completion Criteria:
- All referenced documentation files explicitly state that `SCR-REQ-001` is retired and superseded by `SCR-FEED-001`.
- Screen IDs `SCR-REQ-002..005` remain stable and unrenumbered.

Implementation Notes:
- In `cakra/docs/ui-layout/13-scr-req-001.md`:
  - Added permanent retirement header noting `SCR-REQ-001` has been retired and superseded by `SCR-FEED-001` (Operational Feed).
- In `cakra/docs/navigation/screen-inventory.md`:
  - Updated summary table to mark `SCR-REQ-001` as retired / superseded by `SCR-FEED-001`.
  - Updated `SCR-REQ-001` definition with retirement notice and status.
  - Updated entry/exit points in `SCR-REQ-002`, `SCR-REQ-003`, and `SCR-REQ-004` to reference `SCR-FEED-001` and dynamic back navigation without renumbering `SCR-REQ-002..005`.
- In `cakra/docs/navigation/navigation-map.md`:
  - Updated Section 2 (Hierarchy Tree) to remove `SCR-REQ-001` and indicate operational requests are visualized in `SCR-FEED-001`.
  - Updated Section 3 (Requests Area) to mark `SCR-REQ-001` as retired/superseded, updating entry/exit points for `SCR-REQ-002` and `SCR-REQ-003`.
  - Updated Section 4 (Navigation Graph & Movement Descriptions) and Section 5 (Traceability Matrix) to align with retirement of `SCR-REQ-001`.
- In `cakra/docs/navigation/request-navigation.md`:
  - Updated Section 1 (Hierarchy) to show retirement note for `SCR-REQ-001` and dynamic return navigation.
  - Updated entry and return points in `UJ-REQ-001`, `UJ-REQ-002`, and `UJ-REQ-008` (and journeys 003-007) to reference `SCR-FEED-001` and dynamic return navigation.
- In `cakra/docs/ui-layout/00-screen-inventory.md` and `01-screen-to-use-case-matrix.md`:
  - Marked `SCR-REQ-001` as retired / superseded by `SCR-FEED-001`.

Changed Files:
- `cakra/docs/ui-layout/13-scr-req-001.md`
- `cakra/docs/navigation/screen-inventory.md`
- `cakra/docs/navigation/navigation-map.md`
- `cakra/docs/navigation/request-navigation.md`
- `cakra/docs/ui-layout/00-screen-inventory.md`
- `cakra/docs/ui-layout/01-screen-to-use-case-matrix.md`

---

## P4 - Build Verification & Integrity Check

Implementation Status: IMPLEMENTED
Review Status: GO

### P4-S04

Title: Execute Frontend and Backend Build Verification

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Verify that all source changes compile cleanly and no build or typing regressions were introduced.
1. In `cakra/src/frontend/Cakra.Web`:
   - Run `npm run build` (or `vue-tsc --noEmit && vite build`).
   - Confirm zero compilation or bundling errors.
2. In `cakra`:
   - Run `dotnet build cakra/Cakra.sln` to confirm backend solution build succeeds cleanly.

Depends On: P2-S02, P3-S03

Repository: `cakra`

Completion Criteria:
- `npm run build` exits with code 0.
- `dotnet build cakra/Cakra.sln` exits with code 0.

Implementation Notes:
- Ran `npm run build` (`vue-tsc --noEmit && vite build`) in `cakra/src/frontend/Cakra.Web`: completed successfully with 0 compilation and bundling errors.
- Ran `dotnet build cakra/Cakra.sln`: all 12 backend projects built successfully with 0 errors and 0 warnings.

Changed Files:
- None (verification slice)

---

# 6. Change Log

- 2026-10-06: Initial release of CR-017 Implementation Plan with Execution Approval APPROVED.
