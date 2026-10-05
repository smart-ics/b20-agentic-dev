---
Title: Architecture Specification for Constrained Content Container Layout and Redesigned Application Shell
Code: CR-011
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-05
---

# 1. Overview

This architecture artifact defines the technical realization for redesigning the Cakra application shell layout and reorganizing the Operational Feed screen (`SCR-FEED-001`), as requested in [CR-011-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-011-ISSUE.md) and assessed in [CR-011-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-011-FEASIBILITY-ASSESSMENT.md).

The target state eliminates unbounded viewport expansion ("stretched dashboard" effect) on wide, 4K, and ultrawide displays by enforcing a centered $1440\text{px}$ maximum content container (`.cakra-shell-container`) while maintaining full-height sidebar navigation and full-bleed workspace chrome. Additionally, the Operational Feed ([FeedView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue)) is restructured into a high-density, scanning-optimized two-column grid on desktop screens ($\ge 1200\text{px}$).

---

# 2. Architectural Basis

## Business Context

- ISSUE: [CR-011-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-011-ISSUE.md)
- UI Layout Spec: [10-scr-feed-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/10-scr-feed-001.md)
- Master Architecture: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

## Analysis Input

- FEASIBILITY-ASSESSMENT: [CR-011-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-011-FEASIBILITY-ASSESSMENT.md)
  - Approved Decisions: `GAP-001` through `GAP-005`, `OQ-001` through `OQ-003`.
  - Planning Gate: `READY-FOR-PLANNING` granted by `ica-architect`.

```text
CR-011-ISSUE + CR-011-FEASIBILITY-ASSESSMENT
                     ↓
           CR-011-ARCHITECTURE
```

---

# 3. Scope

## Included

1. **Design System & Layout Tokens ([`main.css`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/assets/main.css))**:
   - Introduction of `:root` CSS custom properties:
     - `--cakra-content-max-width: 1440px;`
     - `--cakra-container-padding: clamp(0.75rem, 1.5vw, 1.5rem);`
   - Introduction of `.cakra-shell-container` wrapper class with horizontal centering (`margin-inline: auto`) and responsive padding.
   - Introduction of layout classes for two-column feed streaming (`.op-feed-stream-col`, `.op-feed-sidebar-col`, `.op-feed-sticky-panel`).
2. **Application Shell Refactoring ([`App.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue))**:
   - Replacement of `<div class="container-fluid px-2 px-md-3 py-2">` inside `<main class="cakra-page-content">` with `<div class="cakra-shell-container py-2">`.
   - Preservation of full-height fixed left sidebar navigation (`cakra-sidebar`), sticky topbar (`cakra-topbar`), and full-width footer (`cakra-app-footer`).
3. **Operational Feed Restructuring ([`FeedView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue) / `SCR-FEED-001`)**:
   - Reorganization of the screen into a Bootstrap 5 responsive grid row:
     - **Main Stream Column (`col-12 col-xl-8 col-xxl-8`)**: Screen header, alerts, feed item cards (`.op-feed-card`), excerpts, comments, and pagination.
     - **Sticky Contextual Sidebar (`col-12 col-xl-4 col-xxl-4`)**: Search input, active filter selects (Customer, Product), Exception toggle switch, filter apply/reset buttons, and quick stream status / exception summary card.
   - Responsive stacking below $1200\text{px}$ (`xl` breakpoint) where the sidebar stacks below the feed stream.

## Excluded

1. Backend APIs, database migrations, or entity modifications.
2. Router path configuration or authentication/authorization rules.
3. Modifying internal card semantics or interaction APIs in `FeedView.vue`.

---

# 4. Technical Decisions

## TD-001: Standardized CSS Custom Properties for Content Containment

To avoid hardcoded magic numbers and enable uniform theme configuration, layout boundaries are declared in `:root` in [main.css](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/assets/main.css):

```css
:root {
  /* Layout Max Width & Padding Tokens */
  --cakra-content-max-width: 1440px;
  --cakra-container-padding: clamp(0.75rem, 1.5vw, 1.5rem);
}

.cakra-shell-container {
  width: 100%;
  max-width: var(--cakra-content-max-width);
  margin-left: auto;
  margin-right: auto;
  padding-left: var(--cakra-container-padding);
  padding-right: var(--cakra-container-padding);
}
```

## TD-002: Layered Workspace Chrome vs. Contained Content Area

The application shell adopts a SaaS-grade layered architecture (matching Linear, GitHub, Jira):
1. **Outer Chrome Layer (Full Workspace Bleed)**:
   - Left Sidebar (`.cakra-sidebar`): Fixed 100vh height, width $204\text{px}$ (or $50\text{px}$ collapsed).
   - Topbar (`.cakra-topbar`): Sticky top, $100\%$ workspace width right of sidebar.
   - Footer (`.cakra-app-footer`): $100\%$ workspace width right of sidebar.
2. **Inner Content Area (Contained & Centered)**:
   - `.cakra-page-content`: Stretches 100% width with background `--cakra-bg-canvas`.
   - `.cakra-shell-container`: Center-aligned with `max-width: 1440px` and responsive horizontal padding, preventing wide monitors from stretching cards or text lines across multiple feet.

## TD-003: Operational Feed Two-Column Grid Architecture

Within `FeedView.vue`, the layout is split into two responsive columns using standard Bootstrap 5 grid:

```html
<div class="row g-3">
  <!-- Left Column: Feed Stream (~67% / 8 cols on xl+) -->
  <div class="col-12 col-xl-8">
    <!-- Screen Header, Alerts, Feed Items, Pagination -->
  </div>

  <!-- Right Column: Sticky Contextual Panel (~33% / 4 cols on xl+) -->
  <div class="col-12 col-xl-4">
    <div class="op-feed-sticky-panel">
      <!-- Filter Card & Summary Metrics Card -->
    </div>
  </div>
</div>
```

## TD-004: Sticky Contextual Panel Ergonomics & Overflow Safety

To ensure that the right-hand panel remains visible while scrolling down a long list of feed items without clipping on lower-height monitors:

```css
.op-feed-sticky-panel {
  position: sticky;
  top: calc(var(--topbar-height) + 0.75rem);
  max-height: calc(100vh - var(--topbar-height) - 1.5rem);
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
```

---

# 5. Component Responsibilities

| Component / File | Responsibility |
|------------------|----------------|
| [main.css](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/assets/main.css) | Defines `:root` layout tokens (`--cakra-content-max-width`, `--cakra-container-padding`), `.cakra-shell-container` wrapper class, and sticky feed sidebar styling rules. |
| [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue) | Authenticated shell root: wraps `<router-view />` inside `.cakra-shell-container py-2` while preserving full-bleed sidebar, topbar, and footer. |
| [FeedView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue) | Screen `SCR-FEED-001`: implements two-column grid (`col-12 col-xl-8` feed stream and `col-12 col-xl-4` sticky filter & exception summary panel) with full filter reactivity, exception toggles, modal triggers, and pagination. |

---

# 6. Integration Design

```text
[Browser Viewport]
  │
  ├── [Cakra Left Sidebar (.cakra-sidebar)] (Fixed 100vh)
  │
  └── [Cakra Main Workspace (.cakra-main-container)]
        │
        ├── [Sticky Topbar (.cakra-topbar)] (Full Bleed)
        │
        ├── [Page Content Canvas (.cakra-page-content)]
        │     │
        │     └── [Centered Container (.cakra-shell-container, max-width: 1440px)]
        │           │
        │           └── [FeedView / SCR-FEED-001]
        │                 ├── Left Col (col-12 col-xl-8): Stream & Feed Cards
        │                 └── Right Col (col-12 col-xl-4): Sticky Filter & Metrics Panel
        │
        └── [App Footer (.cakra-app-footer)] (Full Bleed)
```

---

# 7. Data Ownership

| Data Element | Owner | Lifecycle & Scope |
|--------------|-------|-------------------|
| Layout Constraints & Tokens | CSS Engine (`main.css`) | Static design system tokens in `:root`. |
| Sidebar Collapse / Mobile State | `App.vue` (`isCollapsed`, `isMobileOpen`) | Ephemeral client-side session state. |
| Feed Filters & Exception Toggle | `FeedView.vue` (`filters`, `activeCustomers`, `activeProducts`) | Reactive view state; bound to query parameters and API requests. |

---

# 8. Database Design

*Not applicable. No database tables, columns, or relationships are modified for this frontend shell and layout change.*

---

# 9. Cross-Cutting Concerns

1. **Information Density & Eye Ergonomics**:
   - Text line lengths in cards are constrained to natural reading widths ($\approx 70\text{–}90$ characters per line on desktop) rather than stretching across multiple screens on ultrawide monitors.
2. **Zero Breaking Changes to Test Affordances**:
   - All `data-testid` attributes (`create-request-btn`, `refresh-feed-btn`, `feed-filter-bar`, `feed-filter-customer`, `feed-filter-product`, `feed-filter-exception`, `apply-feed-filters-btn`, `feed-error-alert`, `request-created-success-alert`, `feed-item-card-*`) are preserved and functional.
3. **Responsive Degradation**:
   - On screens $< 1200\text{px}$, the sticky sidebar gracefully transitions into a stacked flow below the screen header and above the feed stream, ensuring seamless mobile/tablet usability.

---

# 10. Implementation Constraints

1. **Framework**: Vue 3 with Composition API (`<script setup lang="ts">`).
2. **CSS Framework**: Bootstrap 5 grid and utility classes combined with custom tokens in `main.css`.
3. **No External Heavy UI Libraries**: All container and column behaviors must use native CSS custom properties and standard Bootstrap flexbox grid.
4. **Build Integrity**: TypeScript type checks (`npm run build` or `vue-tsc`) must pass without errors.

---

# 11. Acceptance Conditions

1. **Container Constraint**:
   - On viewports $> 1440\text{px}$, the content area does not exceed $1440\text{px}$ in width and remains horizontally centered within the main workspace.
2. **App Chrome Full Bleed**:
   - Left sidebar navigation remains full height ($100\text{vh}$); topbar and footer span $100\%$ width right of the sidebar.
3. **Feed Layout Split**:
   - On viewports $\ge 1200\text{px}$, `FeedView.vue` renders a two-column layout with feed stream in the left column and sticky filter/metrics panel in the right column.
4. **Responsive Stacking**:
   - On viewports $< 1200\text{px}$, the right panel stacks below the feed header without overflow or clipped controls.
5. **Functional Integrity**:
   - Filtering by Customer, Product, Exception-only, and search queries continues to work seamlessly.
   - Post creation modal, post detail modal, and navigation links remain completely functional.
