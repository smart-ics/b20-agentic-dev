---
Title: Removal of SCR-REQ-001 Screen Request Architecture
Code: CR-017
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-06
---

# 1. Overview

This architecture defines the technical realization for Change Request `CR-017`: Retirement and removal of `SCR-REQ-001` (Request List screen).

It consumes and realizes the approved feasibility decisions from [CR-017-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-017-FEASIBILITY-ASSESSMENT.md), streamlining the presentation layer in `Cakra.Web` by decommissioning `RequestListView.vue`, establishing an explicit `/requests` -> `/feed` router redirect, transitioning return navigation in `CreateRequestView.vue` and `RequestDetailView.vue` to dynamic history navigation, eliminating obsolete "All Requests" buttons in `MyRequestsView.vue` and `RequestSearchView.vue`, preserving all backend API endpoints, and synchronizing system navigation specifications.

---

# 2. Architectural Basis

## Business Context

- ISSUE: [CR-017-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-017-ISSUE.md)
- NAVIGATION: [navigation-map.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/navigation-map.md), [request-navigation.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/request-navigation.md), [collaboration-navigation.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/collaboration-navigation.md), [screen-inventory.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/screen-inventory.md)
- UI LAYOUT: [13-scr-req-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/13-scr-req-001.md), [00-screen-inventory.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/00-screen-inventory.md), [01-screen-to-use-case-matrix.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/01-screen-to-use-case-matrix.md)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-017-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-017-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

This architecture consumes and realizes the approved decisions:
- `GAP-001` & `GAP-002`: Delete `RequestListView.vue`, redirect route `/requests` to `/feed`.
- `GAP-003`: Transition return navigation in `CreateRequestView.vue` and `RequestDetailView.vue` to dynamic history navigation (`router.back()`) with fallback to `/feed`.
- `GAP-004`: Remove obsolete "All Requests" buttons from `MyRequestsView.vue` and `RequestSearchView.vue`.
- `GAP-005`: Mark `13-scr-req-001.md` as RETIRED / SUPERSEDED BY SCR-FEED-001; update navigation specifications while preserving `SCR-REQ-002..005` IDs without renumbering.
- Backend query endpoints (`GET /api/v1/requests`) remain untouched and active.

---

# 3. Scope

## Included

1. **Router Configuration (`src/frontend/Cakra.Web/src/router/index.ts`)**:
   - Remove `RequestListView` import.
   - Replace the `/requests` component route entry with `{ path: '/requests', redirect: '/feed' }`.
2. **Component Retirement**:
   - Delete `src/frontend/Cakra.Web/src/views/RequestListView.vue`.
3. **In-View Return Navigation Refactoring**:
   - `CreateRequestView.vue`:
     - Refactor `handleCancel` and header back button to execute dynamic return navigation (`router.back()` with fallback to `/feed`).
     - Update button label from "Back to Requests" to "Back".
   - `RequestDetailView.vue`:
     - Refactor `handleBack` and header back button to execute dynamic return navigation (`router.back()` with fallback to `/feed`).
     - Update button label from "Back to Requests" to "Back".
4. **Obsolete Navigation Button Cleanup**:
   - `MyRequestsView.vue`: Remove the obsolete "All Requests" `<router-link to="/requests">` element.
   - `RequestSearchView.vue`: Remove the obsolete "All Requests" `<router-link to="/requests">` element.
5. **Topbar Screen Title Adjustment (`App.vue`)**:
   - Clean up or adjust `/requests` title handling in `currentScreenTitle` computed property.
6. **Documentation & Specification Synchronization**:
   - Mark `13-scr-req-001.md` as RETIRED / SUPERSEDED BY SCR-FEED-001.
   - Update `screen-inventory.md`, `navigation-map.md`, `request-navigation.md`, `00-screen-inventory.md`, and `01-screen-to-use-case-matrix.md`.

## Excluded

- Modifying any backend API endpoints, services, or controllers (`RequestsController.cs`, `IRequestQueryService.cs`).
- Modifying backend integration tests.
- Modifying other request routes (`/requests/create`, `/requests/my`, `/requests/search`, `/requests/:id`).
- Renumbering screen identifiers (`SCR-REQ-002..005`).

---

# 4. Technical Decisions

## TD-001: Explicit URL Redirection for `/requests`

In `src/frontend/Cakra.Web/src/router/index.ts`, route `/requests` will be explicitly mapped to redirect to `/feed`:

```typescript
{
  path: '/requests',
  name: 'requests-redirect',
  redirect: '/feed',
},
```

**Rationale:** Users bookmarking `/requests` or following historical links are seamlessly routed to the Operational Feed where all requests are now visualized, preventing broken links or 404 errors.

## TD-002: Complete Deletion of `RequestListView.vue`

`src/frontend/Cakra.Web/src/views/RequestListView.vue` is deleted from the source tree.

**Rationale:** Keeping obsolete views creates dead code, maintenance drag, and confusion for ongoing development.

## TD-003: Dynamic History Return Pattern with Fallback

In both `CreateRequestView.vue` and `RequestDetailView.vue`, static navigation to `/requests` is replaced with a resilient dynamic history pattern:

```typescript
function handleBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.push('/feed')
  }
}
```

The header navigation link is converted into a button triggering `handleBack()`:

```html
<button
  type="button"
  class="btn btn-outline-secondary btn-sm py-0 px-2"
  style="font-size: 12px; height: 26px; line-height: 24px"
  data-testid="back-button"
  @click="handleBack"
>
  <i class="bi bi-arrow-left me-1" aria-hidden="true"></i>
  Back
</button>
```

**Rationale:** Users arrive at Request Detail or Create Request from various origin screens (Operational Feed, My Requests, Work Packages, Customer Portfolio, Programmer Workload). Dynamic return restores them directly to their workflow origin.

## TD-004: Removal of Obsolete "All Requests" Buttons

The "All Requests" button (`data-testid="all-requests-link"`) is removed from both `MyRequestsView.vue` and `RequestSearchView.vue`.

**Rationale:** With `SCR-REQ-001` removed, this affordance is dead. The Operational Feed is already prominently accessible in the main sidebar.

## TD-005: Screen Identifier Preservation & Document Deprecation

1. `SCR-REQ-002` (Create Request), `SCR-REQ-003` (Request Detail), `SCR-REQ-004` (My Requests), and `SCR-REQ-005` (Request Search) preserve their existing IDs without renumbering.
2. `13-scr-req-001.md` is updated with a permanent deprecation header: `> [!IMPORTANT]\n> **RETIRED / SUPERSEDED**: This screen (SCR-REQ-001) has been retired. Operational requests are visualized and tracked within SCR-FEED-001 (Operational Feed).`

---

# 5. Component Responsibilities

| Component | Responsibility |
|---|---|
| `router/index.ts` | Redirects `/requests` to `/feed`; unregisters `RequestListView`. |
| `App.vue` | Evaluates topbar title for active routes, removing redundant `/requests` prefix handling. |
| `CreateRequestView.vue` | Records new requests; provides dynamic return (`router.back()` / `/feed`) on cancel or back. |
| `RequestDetailView.vue` | Displays request lifecycle & execution details; provides dynamic return (`router.back()` / `/feed`) on back. |
| `MyRequestsView.vue` | Displays personal assigned requests; removes obsolete "All Requests" link. |
| `RequestSearchView.vue` | Provides advanced request search & history; removes obsolete "All Requests" link. |
| `RequestsController.cs` | Unchanged: Serves `GET /api/v1/requests` for Work Packages and Request Search. |

---

# 6. Integration Design

| Source | Target | Purpose |
|---|---|---|
| User / Browser URL `/requests` | Vue Router (`router/index.ts`) | Evaluates route and immediately redirects to `/feed`. |
| `CreateRequestView.vue` | Browser History / Vue Router | Returns user to previous origin screen (e.g. `/feed`), or navigates to `/feed` if accessed directly. |
| `RequestDetailView.vue` | Browser History / Vue Router | Returns user to previous origin screen (e.g. `/feed`, `/requests/my`, `/work-packages`), or navigates to `/feed` if accessed directly. |

---

# 7. Data Ownership

No data ownership changes. All data ownership rules defined in `CAKRA-ARCHITECTURE.md` remain intact.

---

# 8. Database Design

No database tables, columns, constraints, or migrations are modified.

---

# 9. Cross-Cutting Concerns

- **Navigation Compatibility:** Direct URL access to `/requests` continues to succeed cleanly without 404 or white-screen errors.
- **Build Verification:** Running `npm run build` in `src/frontend/Cakra.Web` must complete with zero TypeScript errors and zero unresolved import warnings.
- **Traceability:** Documentation records the retirement of `SCR-REQ-001` so audit history and requirement coverage remains clear.

---

# 10. Implementation Constraints

1. Strict TypeScript compilation must pass without errors (`npm run build`).
2. Do not renumber existing screen IDs.
3. Backend APIs must remain completely unmodified.
4. Button styling in `CreateRequestView.vue` and `RequestDetailView.vue` must maintain Bootstrap 5 visual consistency.

---

# 11. Acceptance Conditions

1. `RequestListView.vue` is removed from `src/frontend/Cakra.Web/src/views/`.
2. Accessing `/requests` in the browser redirects immediately to `/feed`.
3. In `CreateRequestView.vue`:
   - "Back" button and Cancel handler return dynamically to caller or `/feed`.
   - "Back to Requests" label is replaced by "Back".
4. In `RequestDetailView.vue`:
   - "Back" button and back handler return dynamically to caller or `/feed`.
   - "Back to Requests" label is replaced by "Back".
5. In `MyRequestsView.vue` and `RequestSearchView.vue`:
   - The "All Requests" button is completely removed.
6. Frontend build (`npm run build`) succeeds cleanly with no compilation or bundling errors.
7. System documentation (`13-scr-req-001.md`, `navigation-map.md`, `request-navigation.md`, `screen-inventory.md`) reflects `SCR-REQ-001` retirement.
