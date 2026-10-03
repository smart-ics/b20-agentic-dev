---
Title: Removal of Request and Request Search from Left Sidebar Menu Architecture
Code: CR-004
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-03
---

# 1. Overview

This architecture defines the technical realization for Change Request `CR-004`: Removal of the "Request" (`/requests`, `SCR-REQ-001`) and "Request Search" (`/requests/search`, `SCR-REQ-004`) menu items from the left sidebar navigation menu.

It consumes and realizes the approved feasibility decisions from [CR-004-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-004-FEASIBILITY-ASSESSMENT.md), streamlining the primary application shell navigation in `Cakra.Web` (`src/frontend/Cakra.Web/src/App.vue`) while preserving underlying screen views, routing definitions, secondary in-view navigation pathways, and synchronizing navigation architecture documentation.

---

# 2. Architectural Basis

## Business Context

- ISSUE: [CR-004-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-004-ISSUE.md)
- NAVIGATION: [navigation-map.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/navigation-map.md), [request-navigation.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/request-navigation.md), [collaboration-navigation.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/collaboration-navigation.md)
- UI LAYOUT: [13-scr-req-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/13-scr-req-001.md), [16-scr-req-004.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/16-scr-req-004.md)
- FEATURES: [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md), [FEAT-COL-002-search-request-history.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-COL-002-search-request-history.md)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-004-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-004-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

This architecture consumes and realizes the approved decisions:
- `GAP-001`: Remove `<router-link to="/requests">` and `<router-link to="/requests/search">` from `App.vue`.
- `GAP-002`: Update navigation specifications (`navigation-map.md`, `request-navigation.md`, `collaboration-navigation.md`) to reclassify `SCR-REQ-001` and `SCR-REQ-004` as contextual/secondary destinations rather than primary global sidebar items.
- `GAP-003`: Retain secondary in-app navigation links (`MyRequestsView.vue` "All Requests" button, `RequestDetailView.vue` "Back" button) and keep routes intact.
- `OQ-001`: Preserve routes `/requests` and `/requests/search` and views `RequestListView.vue` and `RequestSearchView.vue` in `router/index.ts`.
- `OQ-002`: Keep contextual navigation affordances active across the system.
- `OQ-003`: Retain `currentScreenTitle` logic in `App.vue` for topbar display continuity.

---

# 3. Scope

## Included

- Refactoring of `src/frontend/Cakra.Web/src/App.vue`:
  - Removal of the "Requests" navigation item (`<router-link to="/requests" data-testid="nav-requests-link">`).
  - Removal of the "Request Search" navigation item (`<router-link to="/requests/search" data-testid="nav-request-search-link">`).
  - Retention of the remaining items under "Operations": "Operational Feed" (`/feed`), "My Requests" (`/requests/my`), and "Work Packages" (`/work-packages`).
- Updates to navigation documentation:
  - `cakra/docs/navigation/navigation-map.md`: Update Navigation Hierarchy Tree and Global Navigation entry points.
  - `cakra/docs/navigation/request-navigation.md`: Update entry points for Request Lifecycle journeys.
  - `cakra/docs/navigation/collaboration-navigation.md`: Update entry point for `UJ-COL-002: Search Request History`.
- Frontend build validation (`npm run build`) ensuring zero TypeScript compilation or bundle regressions.

## Excluded

- Deleting `RequestListView.vue` or `RequestSearchView.vue`.
- Unregistering routes in `src/frontend/Cakra.Web/src/router/index.ts`.
- Altering backend API endpoints (`/api/v1/requests`, `/api/v1/requests/my`, etc.) or database schemas.
- Modifying secondary buttons in `MyRequestsView.vue` or `RequestDetailView.vue`.

---

# 4. Technical Decisions

## TD-001: Primary Sidebar Navigation Streamlining (`App.vue`)

In `src/frontend/Cakra.Web/src/App.vue`, the sidebar navigation section "Operations" will be streamlined from 5 items to 3 items:
1. `Operational Feed` (`to="/feed"`, icon: `bi-activity`)
2. `My Requests` (`to="/requests/my"`, `data-testid="nav-my-requests-link"`, icon: `bi-person-workspace`)
3. `Work Packages` (`to="/work-packages"`, `data-testid="nav-work-packages-link"`, icon: `bi-kanban`)

The `<router-link to="/requests">` and `<router-link to="/requests/search">` elements are removed completely from the sidebar navigation markup.

## TD-002: Full Route and Component Preservation (`router/index.ts`)

The routes:
- `/requests` -> `SCR-REQ-001` (`RequestListView.vue`)
- `/requests/create` -> `SCR-REQ-002` (`CreateRequestView.vue`)
- `/requests/my` -> `SCR-REQ-004` (`MyRequestsView.vue`)
- `/requests/search` -> `SCR-REQ-005` (`RequestSearchView.vue`)
- `/requests/:id` -> `SCR-REQ-003` (`RequestDetailView.vue`)

remain fully registered in `src/frontend/Cakra.Web/src/router/index.ts`. This ensures:
1. Direct URL access and browser bookmarks continue to function.
2. In-view navigation (such as clicking "All Requests" on `MyRequestsView.vue` or "View Request Detail" from `FeedView.vue`) functions without disruption.
3. Automated test suites targeting specific routes or views continue passing.

## TD-003: Contextual Entry Points for Request List and Search

Access to `SCR-REQ-001: Request List` and `SCR-REQ-004: Request Search & History` transitions from global sidebar navigation to contextual secondary navigation:
- `SCR-REQ-001` is reachable via:
  - The "All Requests" button (`data-testid="all-requests-link"`) on `MyRequestsView.vue` (`/requests/my`).
  - The "Back to Requests" button (`data-testid="back-to-requests-link"`) on `RequestDetailView.vue` (`/requests/:id`).
  - The "All Requests" button (`data-testid="all-requests-link"`) on `RequestSearchView.vue` (`/requests/search`).
- `SCR-REQ-004` is reachable via:
  - Direct URL `/requests/search`.
  - Secondary navigation paths as required by operational actors.

## TD-004: Retaining Contextual Topbar Title Computation

In `App.vue`, `currentScreenTitle` will continue to evaluate:
```typescript
if (path.startsWith('/requests/create')) return 'Create Operational Request'
if (path.startsWith('/requests/my')) return 'My Assigned Requests'
if (path.startsWith('/requests/search')) return 'Request Search'
if (path.startsWith('/requests/')) return 'Request Details'
if (path.startsWith('/requests')) return 'Operational Requests'
```
This guarantees that when an actor navigates to `/requests` or `/requests/search` via a secondary link or direct URL, the top header bar continues to display the proper contextual title.

## TD-005: Navigation Architecture Documentation Synchronization

The navigation specification files in `cakra/docs/navigation/` must be synchronized:
1. `navigation-map.md`:
   - Update Section 2 (Navigation Hierarchy Tree) to indicate that `SCR-REQ-001` and `SCR-REQ-004` are contextual destinations rather than primary global navigation destinations.
   - Update Section 3 (Navigation Areas and Destinations) under `Area: Requests` to reflect that the primary global sidebar entry point is `SCR-REQ-005: My Assigned Requests` (`/requests/my`), with `SCR-REQ-001` accessible contextually.
2. `request-navigation.md`:
   - Update Section 2 (Journey Movement Paths) entry points to reflect that global sidebar navigation routes to `SCR-REQ-005: My Assigned Requests` and `SCR-FEED-001: Operational Feed`.
3. `collaboration-navigation.md`:
   - Update `UJ-COL-002: Search Request History` entry point from `Global Navigation (Requests > Search & History)` to `Contextual Navigation / Direct URL`.

---

# 5. Component Responsibilities

| Component | Responsibility |
|---|---|
| `App.vue` (`Cakra.Web`) | Renders application shell, topbar, and left sidebar menu. Operations section renders only "Operational Feed", "My Requests", and "Work Packages". |
| `router/index.ts` (`Cakra.Web`) | Client-side routing table. Preserves route definitions and authentication guards for all operational screens. |
| `navigation-map.md` | Authoritative system navigation map. Defines primary vs contextual navigation destinations. |
| `request-navigation.md` | Request lifecycle navigation specification detailing journey movement paths. |
| `collaboration-navigation.md` | Collaboration navigation specification detailing search and history entry points. |

---

# 6. Integration Design

| Source | Target | Purpose |
|---|---|---|
| `App.vue` Left Sidebar | `vue-router` | Navigates to `/feed`, `/requests/my`, `/work-packages`, `/products`, `/analytics/*`. |
| `MyRequestsView.vue` | `vue-router` | Navigates to `/requests` via "All Requests" toolbar button. |
| `RequestDetailView.vue` | `vue-router` | Navigates back to `/requests` via "Back" button. |

---

# 7. Data Ownership

| Data | Owner |
|---|---|
| Left Sidebar Navigation Links | `src/frontend/Cakra.Web/src/App.vue` |
| Route Definitions & Screen Metadata | `src/frontend/Cakra.Web/src/router/index.ts` |
| Navigation Hierarchy & Movement Paths | `cakra/docs/navigation/` |

---

# 8. Database Design

This change affects only the frontend application shell navigation and documentation artifacts. No database tables, schemas, constraints, or migrations are introduced or altered.

---

# 9. Cross-Cutting Concerns

- **Responsive Sidebar State**: Removing the two menu items preserves all sidebar toggle (`toggleSidebar`), collapse (`isCollapsed`), and mobile drawer (`isMobileOpen`) behavior.
- **Topbar Synchronization**: The computed title in `App.vue` continues to provide correct screen titles regardless of entry method.

---

# 10. Implementation Constraints

- Vue 3 SFC with `<script setup lang="ts">`.
- Bootstrap 5 and Bootstrap Icons (`bi-*`).
- Do NOT delete `RequestListView.vue` or `RequestSearchView.vue`.
- Do NOT alter route paths in `router/index.ts`.
- Must verify with `npm run build` in `src/frontend/Cakra.Web`.

---

# 11. Acceptance Conditions

- [ ] **AC-1**: In `src/frontend/Cakra.Web/src/App.vue`, `<router-link to="/requests">` (`data-testid="nav-requests-link"`) is removed.
- [ ] **AC-2**: In `src/frontend/Cakra.Web/src/App.vue`, `<router-link to="/requests/search">` (`data-testid="nav-request-search-link"`) is removed.
- [ ] **AC-3**: The left sidebar menu under "Operations" cleanly renders only "Operational Feed", "My Requests", and "Work Packages".
- [ ] **AC-4**: Routes `/requests` and `/requests/search` remain accessible via direct URL and secondary in-view buttons.
- [ ] **AC-5**: Navigation specifications (`navigation-map.md`, `request-navigation.md`, `collaboration-navigation.md`) are updated to reflect the updated navigation hierarchy.
- [ ] **AC-6**: Frontend project compiles cleanly via `npm run build` with zero errors.
