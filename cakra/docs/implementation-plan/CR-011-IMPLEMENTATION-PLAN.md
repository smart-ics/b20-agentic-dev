---
Title: Constrained Content Container Layout and Redesigned Application Shell Implementation Plan
Code: CR-011
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-05
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement the constrained application shell content layout ($1440\text{px}$ max-width container, centered horizontally on wide viewports, full-bleed workspace chrome) and reorganize the Operational Feed (`FeedView.vue` / `SCR-FEED-001`) into a high-density, scanning-optimized two-column grid on desktop screens, in accordance with [CR-011-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-011-ARCHITECTURE.md) and [CR-011-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-011-FEASIBILITY-ASSESSMENT.md).

**Planning Mode:** FEATURE-PLANNING

**Referenced artifacts:**
- ISSUE: [CR-011-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-011-ISSUE.md)
- FEASIBILITY-ASSESSMENT: [CR-011-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-011-FEASIBILITY-ASSESSMENT.md) (Planning Readiness: `READY-FOR-PLANNING`)
- ARCHITECTURE: [CR-011-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-011-ARCHITECTURE.md)
- UI Layout Spec: [10-scr-feed-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/10-scr-feed-001.md)

**Architecture Applicability:** ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers introducing global CSS layout tokens, establishing the universal `.cakra-shell-container` wrapper class, refactoring `App.vue` to contain page views within $1440\text{px}$, reorganizing `FeedView.vue` into a scanning-optimized two-column layout with a sticky contextual filter/metrics panel, and verifying build integrity.

Scope breakdown:
- **Design Tokens & Layout Classes (`main.css`)**:
  - CSS custom properties in `:root`: `--cakra-content-max-width: 1440px;` and `--cakra-container-padding: clamp(0.75rem, 1.5vw, 1.5rem);`.
  - Container class `.cakra-shell-container` with `max-width: var(--cakra-content-max-width)`, `margin-inline: auto`, and responsive horizontal padding.
  - Sticky contextual panel and grid utility styling for the operational feed (`.op-feed-sticky-panel`, etc.).
- **Application Shell Refactoring (`App.vue`)**:
  - Update `<main class="cakra-page-content">` to host `<div class="cakra-shell-container py-2"><router-view /></div>`.
  - Maintain full-bleed left sidebar navigation (`cakra-sidebar`), sticky topbar (`cakra-topbar`), and full-width footer (`cakra-app-footer`).
- **Operational Feed Reorganization (`FeedView.vue`)**:
  - Reorganize screen `SCR-FEED-001` into a two-column responsive grid on large screens ($\ge 1200\text{px}$ `xl` breakpoint):
    - Left Column (`col-12 col-xl-8 col-xxl-8`): Screen title header, alerts, feed item cards (`.op-feed-card`), excerpts, comments, and pagination.
    - Right Column (`col-12 col-xl-4 col-xxl-4`): Sticky side panel containing search/filter controls, active exception summary card, and operational stream statistics.
  - Responsive stacking below $1200\text{px}$ (`xl` breakpoint) where the side panel stacks neatly below the feed stream.
  - Full preservation of test IDs (`data-testid`), reactive filters, modal affordances, and navigation.
- **Verification & Build**:
  - Execute `npm run build` in `cakra/src/frontend/Cakra.Web` for clean TypeScript compilation and asset bundling.

---

# 3. Dependencies

**External Dependencies:** None.

**Slice Dependencies:** Listed in `Depends On` field for each slice below.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---------|---------|---------|---------|
| P1 - Design Tokens & Container Styles | IMPLEMENTED | GO | 1/1 |
| P2 - Application Shell Layout Refactoring | IMPLEMENTED | GO | 1/1 |
| P3 - Operational Feed Two-Column Reorganization | IMPLEMENTED | GO | 1/1 |
| P4 - Verification & Build | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Design Tokens & Container Styles

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P1-S01

**Title:** Standardize Layout Design Tokens and `.cakra-shell-container` in `main.css`

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Define layout custom properties in `:root` and implement the `.cakra-shell-container` wrapper class and `.op-feed-sticky-panel` styling in `main.css`.

**Depends On:** None

**Repository:** Cakra.Web

**Completion Criteria:**
- In [main.css](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/assets/main.css), `:root` declares `--cakra-content-max-width: 1440px;` and `--cakra-container-padding: clamp(0.75rem, 1.5vw, 1.5rem);`.
- Class `.cakra-shell-container` is defined with `width: 100%; max-width: var(--cakra-content-max-width); margin-left: auto; margin-right: auto; padding-left: var(--cakra-container-padding); padding-right: var(--cakra-container-padding);`.
- Class `.op-feed-sticky-panel` is styled with sticky positioning, viewport-aware max height, vertical scrolling (`overflow-y: auto`), and high-density gap spacing.

**Implementation Notes:**
- Added `--cakra-content-max-width: 1440px;` and `--cakra-container-padding: clamp(0.75rem, 1.5vw, 1.5rem);` to `:root` in `main.css`.
- Added `.cakra-shell-container` with auto margins and horizontal padding token.
- Added `.op-feed-sticky-panel` with sticky positioning and viewport-aware max height.

**Changed Files:**
- `cakra/src/frontend/Cakra.Web/src/assets/main.css`

---

## P2 - Application Shell Layout Refactoring

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P2-S02

**Title:** Update Application Shell Content Wrapper in `App.vue`

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Refactor [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue) to wrap `<router-view />` inside `.cakra-shell-container` while preserving full-bleed workspace chrome.

**Depends On:** P1-S01

**Repository:** Cakra.Web

**Completion Criteria:**
- In `App.vue`, `<main class="cakra-page-content">` replaces `<div class="container-fluid px-2 px-md-3 py-2">` with `<div class="cakra-shell-container py-2">`.
- Full-height left sidebar (`cakra-sidebar`), topbar (`cakra-topbar`), and footer (`cakra-app-footer`) remain full-bleed spanning the entire workspace width.
- On viewports $> 1440\text{px}$, page content is horizontally centered with consistent margins.
- Unauthenticated layout (Login screen) remains unaffected.

**Implementation Notes:**
- Refactored `App.vue` main page content wrapper from `<div class="container-fluid px-2 px-md-3 py-2">` to `<div class="cakra-shell-container py-2">`.
- Preserved workspace chrome (full-bleed sidebar, sticky topbar, full-bleed footer) and unauthenticated login layout.
- Verified TypeScript compilation and bundling with `npm run build`.

**Changed Files:**
- `cakra/src/frontend/Cakra.Web/src/App.vue`

---

## P3 - Operational Feed Two-Column Reorganization

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P3-S03

**Title:** Reorganize `FeedView.vue` into Two-Column Grid with Sticky Contextual Sidebar

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Restructure [FeedView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue) into a high-density two-column layout on large screens ($\ge 1200\text{px}$) with feed stream on the left and sticky filter/metrics sidebar on the right.

**Depends On:** P1-S01, P2-S02

**Repository:** Cakra.Web

**Completion Criteria:**
- `FeedView.vue` uses a responsive grid row with left column (`col-12 col-xl-8 col-xxl-8`) for the operational feed stream and right column (`col-12 col-xl-4 col-xxl-4`) for the sticky contextual sidebar.
- Left column renders: Screen title header (with `SCR-FEED-001` badge and event counter), create request trigger button, refresh button, error/success alert banners, feed item cards (`.op-feed-card`), and pagination.
- Right column renders: Sticky panel (`.op-feed-sticky-panel`) with search input, Customer filter dropdown, Product filter dropdown, Exceptions-only switch, Apply/Reset filter buttons, and a stream metrics / active exception summary card.
- On viewports $< 1200\text{px}$ (`lg`, `md`, `sm`), the sidebar stacks cleanly below the feed stream without clipping or horizontal overflow.
- All existing `data-testid` attributes are preserved (`create-request-btn`, `refresh-feed-btn`, `feed-filter-bar`, `feed-filter-customer`, `feed-filter-product`, `feed-filter-exception`, `apply-feed-filters-btn`, `feed-loading-state`, `feed-empty-state`, `feed-item-card-*`).

**Implementation Notes:**
- Restructured `FeedView.vue` template into a Bootstrap 5 responsive grid (`row g-3`) containing a 67% width left column (`col-12 col-xl-8 col-xxl-8`) and 33% width right column (`col-12 col-xl-4 col-xxl-4`).
- Relocated search, customer, product, and exception filter controls into a dedicated filter card within `.op-feed-sticky-panel` in the right column, and added an operational stream metrics card tracking event count, page exceptions, filter state, and pagination.
- Left column encapsulates the screen header, action buttons, alert banners, feed cards, empty/loading states, and pagination controls.
- Preserved all `data-testid` attributes, reactive bindings, modal integrations (`CreateRequestModal`, `PostDetailModal`), and pagination logic.
- Aliased `.op-feed-card` in `main.css` and verified frontend type-checking and bundling via `npm run build`.

**Changed Files:**
- `cakra/src/frontend/Cakra.Web/src/views/FeedView.vue`
- `cakra/src/frontend/Cakra.Web/src/assets/main.css`

---

## P4 - Verification & Build

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

### P4-S04

**Title:** Frontend Build and Type Verification

**Implementation Status:** IMPLEMENTED  
**Review Status:** GO

**Objective:** Verify that `Cakra.Web` compiles with zero TypeScript or template errors and produces valid distribution assets.

**Depends On:** P3-S03

**Repository:** Cakra.Web

**Completion Criteria:**
- Run `npm run build` in `cakra/src/frontend/Cakra.Web`.
- Build exits with code 0 with no TypeScript errors or bundling failures.
- Visual inspection confirms centered $1440\text{px}$ layout and two-column feed responsiveness.

**Implementation Notes:**
- Executed `npm run build` (`vue-tsc --noEmit && vite build`) in `cakra/src/frontend/Cakra.Web`.
- Verified 0 TypeScript / template errors, 150 modules transformed, and production distribution bundle generated successfully in `dist/`.
- Confirmed full design system styling and layout integration across `.cakra-shell-container`, `App.vue`, and two-column `FeedView.vue`.

**Changed Files:**
- `cakra/docs/implementation-plan/CR-011-IMPLEMENTATION-PLAN.md`

---

# 6. Change Log

- **2026-10-05 (v1.0):** Initial implementation plan created by Architect and granted `Execution Approval: APPROVED`.
