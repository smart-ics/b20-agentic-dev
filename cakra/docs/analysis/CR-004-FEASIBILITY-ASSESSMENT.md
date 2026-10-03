---
Title: Feasibility Assessment for Removing Request and Request Search from Left Sidebar Menu (CR-004)
Code: CR-004
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-03
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Change being assessed: Removal of the "Request" (`/requests`, `SCR-REQ-001`) and "Request Search" (`/requests/search`, `SCR-REQ-004`) menu items from the left sidebar navigation menu (`App.vue`), per change request documented in [CR-004-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-004-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-004-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-004-ISSUE.md)
- NAVIGATION: [navigation-map.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/navigation-map.md), [request-navigation.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/request-navigation.md), [collaboration-navigation.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/collaboration-navigation.md)
- UI LAYOUT: [13-scr-req-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/13-scr-req-001.md), [16-scr-req-004.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/16-scr-req-004.md)
- FEATURES: [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md), [FEAT-COL-002-search-request-history.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-COL-002-search-request-history.md)

## Objective

Assess the feasibility, baseline implementation, navigation flow impact, and architectural readiness to:

1. Remove the "Request" (`/requests`) and "Request Search" (`/requests/search`) navigation links from the application shell's left sidebar menu in `Cakra.Web` (`src/frontend/Cakra.Web/src/App.vue`).
2. Retain the remaining operational menu items ("Operational Feed", "My Requests", and "Work Packages") and management menu items in the left sidebar menu with appropriate styling and responsive layout intact.
3. Determine the status and accessibility of the underlying routes and screens (`SCR-REQ-001: Request List` and `SCR-REQ-004: Request Search & History`) to avoid breaking existing deep links, secondary navigation, or user workflows.
4. Identify required updates to navigation specifications (`navigation-map.md`, `request-navigation.md`, `collaboration-navigation.md`) to maintain strict traceability.

---

# 2. Current State

## Existing Behavior

1. **Frontend Application Shell (`App.vue`)**:
   - The left sidebar navigation (`aside.cakra-sidebar nav.sidebar-nav`) groups links under the section title "Operations":
     - `Operational Feed` (`/feed`)
     - `Requests` (`/requests`, `data-testid="nav-requests-link"`)
     - `My Requests` (`/requests/my`, `data-testid="nav-my-requests-link"`)
     - `Request Search` (`/requests/search`, `data-testid="nav-request-search-link"`)
     - `Work Packages` (`/work-packages`, `data-testid="nav-work-packages-link"`)
   - `SCR-REQ-001` (`/requests`) and `SCR-REQ-004` (`/requests/search`) are directly reachable via these top-level sidebar items.
   - The computed property `currentScreenTitle` maps `/requests` to "Operational Requests" and `/requests/search` to "Request Search" for the top header bar.

2. **Frontend Routing & Views (`router/index.ts`, `views/`)**:
   - Route `/requests` renders `RequestListView.vue` (`SCR-REQ-001`).
   - Route `/requests/search` renders `RequestSearchView.vue` (`SCR-REQ-004` / `SCR-REQ-005`).
   - `MyRequestsView.vue` includes secondary navigation buttons: "All Requests" (`to="/requests"`), "Create Request" (`to="/requests/create"`), and "Refresh".
   - `RequestDetailView.vue` includes a "Back" button navigating to `/requests`.
   - `RequestSearchView.vue` includes secondary navigation buttons: "All Requests" (`to="/requests"`) and "My Requests" (`to="/requests/my"`).
   - `FeedView.vue` provides direct item drill-down links to `/requests/${id}` (`SCR-REQ-003`) and inline request creation via `CreateRequestModal.vue` (per `CR-003`).

3. **Navigation Specifications (`cakra/docs/navigation/`)**:
   - `navigation-map.md` lists `SCR-REQ-001` and `SCR-REQ-004` as primary global navigation destinations under the Requests Area.
   - `request-navigation.md` lists `SCR-REQ-001` as an entry point for `UJ-REQ-001`, `UJ-REQ-002`, and `UJ-REQ-008`.
   - `collaboration-navigation.md` lists `Global Navigation (Requests > Search & History)` as an entry point for `UJ-COL-002`.

## Existing Constraints

1. The change is specifically requested for the left sidebar menu ("From left side bar menu").
2. Direct URL navigation, bookmarks, and internal in-view buttons targeting `/requests` and `/requests/search` should not be arbitrarily broken unless specified.
3. The left sidebar layout styling, collapse mechanism (`isCollapsed`), and mobile drawer behavior (`isMobileOpen`) must remain intact when these two links are removed.
4. Downstream documentation must be kept consistent with the updated global navigation tree.

---

# 3. Gap Analysis

Identify gaps between the requested change and the current system.

| ID | Severity | Gap |
|---|---|---|
| GAP-001 | CRITICAL | `App.vue` explicitly includes `router-link` elements for `/requests` (`data-testid="nav-requests-link"`) and `/requests/search` (`data-testid="nav-request-search-link"`) in the left sidebar menu template. |
| GAP-002 | MAJOR | Navigation documentation (`navigation-map.md`, `request-navigation.md`, `collaboration-navigation.md`) designates `SCR-REQ-001` and `SCR-REQ-004` as primary Global Navigation (sidebar) entry points. |
| GAP-003 | MINOR | Secondary views (`MyRequestsView.vue`, `RequestDetailView.vue`) link to `/requests`. Clarification is needed on whether those secondary links and route definitions should remain intact. |

---

# 4. Open Questions

Identify unresolved questions that prevent confident architecture decisions.

| ID | Question | Impact |
|---|---|---|
| OQ-001 | Should the routes `/requests` and `/requests/search` and their corresponding Vue components (`RequestListView.vue`, `RequestSearchView.vue`) remain active in the router and codebase, or are the screens intended to be removed completely? | Codebase scope, component lifecycle, and regression risk. |
| OQ-002 | Should secondary navigation links in other views (e.g., "All Requests" in `MyRequestsView.vue` or "Back to Requests" in `RequestDetailView.vue`) remain unchanged, or should their target routes be adjusted? | User workflow continuity between related screens. |
| OQ-003 | Should `App.vue` retain the topbar title derivation for `/requests` and `/requests/search` in `currentScreenTitle`? | Topbar consistency when navigating to these screens via direct URL or secondary links. |

---

# 5. Assumptions

Document assumptions made during the assessment.

| ID | Assumption |
|---|---|
| ASM-001 | The change request strictly targets the left sidebar menu navigation items in `App.vue` and does not request the deletion or deprecation of the underlying views or backend endpoints. |
| ASM-002 | Routes `/requests` and `/requests/search` will remain registered in `src/frontend/Cakra.Web/src/router/index.ts` so direct URLs, bookmarks, and internal links continue to function seamlessly. |
| ASM-003 | In `App.vue`, the "Operations" section of the sidebar will continue to render `Operational Feed` (`/feed`), `My Requests` (`/requests/my`), and `Work Packages` (`/work-packages`). |

---

# 6. Risks

Document identified risks.

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| RISK-001 | Operators accustomed to clicking "Request" or "Request Search" from the sidebar may not realize how to view full request listings. | Low | Operational Feed provides immediate activity visibility with drill-downs to request details, and `My Requests` (`/requests/my`) remains in the sidebar with an "All Requests" toolbar button. |
| RISK-002 | Automated frontend tests or end-to-end assertions that look for `data-testid="nav-requests-link"` or `data-testid="nav-request-search-link"` in the sidebar will fail once removed. | Low | Verify that frontend tests do not hard-depend on these sidebar selectors, or update test assertions if present. |

---

# 7. Recommendations

## Option A: Sidebar Menu Item Removal Only (Recommended)

Remove `<router-link to="/requests">` and `<router-link to="/requests/search">` from `src/frontend/Cakra.Web/src/App.vue`. Preserve route registrations in `router/index.ts` and keep `RequestListView.vue` and `RequestSearchView.vue` accessible via direct URL and secondary in-view links (e.g. from `MyRequestsView.vue`). Update navigation specifications to reflect that these screens are secondary/contextual rather than primary global sidebar items.

### Advantages

- Exactly satisfies the user request with minimal blast radius.
- Leaves existing deep links, test cases, and secondary screen linkages fully functional.
- Zero risk of regression in backend or data services.
- Clean and streamlined sidebar navigation.

### Disadvantages

- `SCR-REQ-001` and `SCR-REQ-004` become secondary destinations rather than first-click sidebar items.

## Option B: Full Screen and Route Deletion

Remove sidebar links, unregister routes `/requests` and `/requests/search` in `router/index.ts`, and delete `RequestListView.vue` and `RequestSearchView.vue`.

### Advantages

- Slightly reduces bundle size.

### Disadvantages

- Violates principle of minimal disruption: breaks existing user journeys (`UJ-COL-002`, `UJ-REQ-008`), breaks back links in `RequestDetailView.vue`, breaks secondary links in `MyRequestsView.vue`.
- Exceeds the scope of the user request.

---

# 8. Gap Closure

Record resolutions for gaps and open questions.

## GAP-001

### Status

CLOSED

### Decision

Adopt Option A: Remove the "Requests" and "Request Search" `<router-link>` elements from `src/frontend/Cakra.Web/src/App.vue`.

### Rationale

Directly achieves the requested change ("Please remove menu: - Request - Request Search From left side bar menu") cleanly without side effects.

### Impact

The left sidebar menu under "Operations" will contain only: "Operational Feed", "My Requests", and "Work Packages".

### Architecture Impact

Requires updating `App.vue` navigation template in `Cakra.Web`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## GAP-002

### Status

CLOSED

### Decision

Update `navigation-map.md`, `request-navigation.md`, and `collaboration-navigation.md` to indicate that `SCR-REQ-001` and `SCR-REQ-004` are accessed via contextual navigation / secondary links rather than primary Global Sidebar Navigation.

### Rationale

Maintains documentation accuracy and traceability between navigation models and the UI application shell.

### Impact

Navigation hierarchy tree and entry point lists in docs are synchronized with the UI.

### Architecture Impact

Documentation updates in `cakra/docs/navigation/`.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## GAP-003

### Status

CLOSED

### Decision

Preserve existing secondary links (e.g., "All Requests" button in `MyRequestsView.vue` and "Back" button in `RequestDetailView.vue`).

### Rationale

Preserving secondary links allows users who need comprehensive request management to navigate without cluttering the global left sidebar.

### Impact

Seamless continuity for existing operational users.

### Architecture Impact

No modifications required for secondary views.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## OQ-001

### Status

CLOSED

### Decision

Retain routes `/requests` and `/requests/search` and their corresponding Vue components in `router/index.ts`.

### Rationale

The user request strictly requested removal of the menu items from the left sidebar, not the deletion of the screens or API endpoints.

### Impact

Direct URLs and bookmarks remain functional.

### Architecture Impact

No router configuration changes required.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## OQ-002

### Status

CLOSED

### Decision

Keep secondary navigation links in `MyRequestsView.vue` and `RequestDetailView.vue` unchanged.

### Rationale

Prevents dead ends and maintains intuitive navigation paths between related operational screens.

### Impact

Workflow continuity preserved.

### Architecture Impact

None.

### Resolved By

`ica-analyst`

### Resolved Date

2026-10-03

---

## OQ-003

### Status

CLOSED

### Decision

Retain `currentScreenTitle` computed logic in `App.vue` for paths starting with `/requests`.

### Rationale

If an actor accesses `/requests` or `/requests/search` through a direct link or secondary button, the topbar will continue to display the proper contextual title.

### Impact

Consistent topbar behavior across all accessible routes.

### Architecture Impact

None.

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

Removing global navigation entry points alters the system navigation model and UI application shell documented across multiple architecture/navigation artifacts (`navigation-map.md`, `request-navigation.md`, `collaboration-navigation.md`) and the frontend shell (`App.vue`). An architecture update and implementation plan slice breakdown are required to ensure synchronized documentation and verified implementation.

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

All feasibility analysis, gap identification, open questions, and closure decisions have been verified. Gate `READY-FOR-PLANNING` is granted by `ica-architect`. Technical realization will be defined in target architecture artifact `CR-004-ARCHITECTURE.md`.

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-004-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-004-ISSUE.md)
- NAVIGATION: [navigation-map.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/navigation-map.md)
- NAVIGATION: [request-navigation.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/request-navigation.md)
- NAVIGATION: [collaboration-navigation.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/collaboration-navigation.md)
- UI LAYOUT: [13-scr-req-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/13-scr-req-001.md)
- UI LAYOUT: [16-scr-req-004.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/16-scr-req-004.md)
- FEATURES: [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md)
- FEATURES: [FEAT-COL-002-search-request-history.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-COL-002-search-request-history.md)

Referenced codebase locations:

- [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue)
- [router/index.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/router/index.ts)
- [MyRequestsView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/MyRequestsView.vue)
- [RequestDetailView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue)
- [RequestListView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestListView.vue)
- [RequestSearchView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/RequestSearchView.vue)
