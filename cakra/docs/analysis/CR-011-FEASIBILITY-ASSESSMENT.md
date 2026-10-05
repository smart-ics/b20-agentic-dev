---
Title: Feasibility Assessment for Constrained Content Container Layout and Redesigned Application Shell
Code: CR-011
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-05
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Feature being assessed: Constrained Content Container Layout and Redesigned Application Shell, per request in [CR-011-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-011-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-011-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-011-ISSUE.md)
- ARCHITECTURE (Reference): [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- UI Layout Spec (Reference): [10-scr-feed-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/10-scr-feed-001.md)

## Objective

Assess the feasibility, baseline code state, layout gaps, risks, architectural impacts, and planning readiness to:

1. Redesign the Cakra application shell ([App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue)) to use a centered, constrained maximum content width of 1440px (`--cakra-content-max-width: 1440px`) instead of an unbounded 100% fluid container.
2. Maintain full-height left navigation sidebar (`cakra-sidebar`), sticky topbar (`cakra-topbar`), and app footer (`cakra-app-footer`) spanning the full workspace width, while keeping the inner main view content centered on screens $> 1440\text{px}$.
3. Standardize layout tokens and styling classes in [main.css](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/assets/main.css), including `--cakra-content-max-width` and `.cakra-shell-container`.
4. Reorganize the Operational Feed ([FeedView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue) / `SCR-FEED-001`) into a two-column layout on large screens ($\ge 1200\text{px}$) with a main feed stream column (~68–70% width) and a sticky contextual sidebar (~30–32% width) containing quick filters, active exception summaries, and metrics, reducing horizontal eye travel.
5. Ensure responsive stacking behavior on screens $< 1200\text{px}$ (tablets and mobile devices) and fluid horizontal padding adapting between 16px and 32px.

---

# 2. Current State

Summarize relevant findings from current artifacts and codebase. This section contains facts only.

## Existing Behavior

1. **Application Shell ([`App.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue))**:
   - The authenticated shell renders a fixed left sidebar (`.cakra-sidebar`) with width 204px (or 50px collapsed) and a main container (`.cakra-main-container`) with `margin-left: var(--sidebar-width)`.
   - The main content area renders `<main class="cakra-page-content"><div class="container-fluid px-2 px-md-3 py-2"><router-view /></div></main>`.
   - On large displays (1440p, 4K, 21:9 ultrawide, 32:9 super ultrawide), `.container-fluid` expands to 100% of the remaining workspace width, causing layout stretch.
2. **Global Stylesheet ([`main.css`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/assets/main.css))**:
   - Defines theme tokens in `:root` (palette colors, typography, sidebar width `--sidebar-width: 204px`, topbar height `--topbar-height: 42px`).
   - `.cakra-page-content` has `flex: 1; background-color: var(--cakra-bg-canvas);`.
   - There are no CSS tokens or container utility classes for max-width content containment or centered alignment.
3. **Operational Feed Screen ([`FeedView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue))**:
   - `FeedView.vue` renders `.op-screen-header`, alerts, `.op-toolbar` (filters), and feed item cards (`.op-feed-card`) in a single vertical stack stretching across the full width of `.container-fluid`.
   - On wide viewports, feed item text lines, title headers, and action rows span up to 2000px–3000px horizontally, resulting in excessive horizontal eye travel.

## Existing Constraints

1. **Workspace Navigation Integrity**: Left sidebar navigation (`cakra-sidebar`) must remain full height (100vh) and sticky topbar (`cakra-topbar`) must remain sticky at `top: 0`.
2. **Responsive Compatibility**: Mobile drawer behavior (`isMobileOpen`), collapsible desktop sidebar (`isCollapsed`), and touch responsiveness must continue functioning seamlessly across all screen sizes.
3. **No Visual Clipping**: Dense data tables in other views (e.g., Customer Management, Person Management, Product Catalog) must remain legible and utilize responsive overflow scrolling (`table-responsive`) if table columns exceed the 1440px container.

---

# 3. Gap Analysis

Identify gaps between the requested change and the current system.

| ID | Severity | Gap |
|------|------|------|
| GAP-001 | CRITICAL | Missing CSS tokens (`--cakra-content-max-width: 1440px`, `--cakra-container-padding`) and `.cakra-shell-container` utility class in `main.css`. |
| GAP-002 | CRITICAL | `App.vue` main content area uses unconstrained `.container-fluid` rather than `.cakra-shell-container`, causing viewport stretching on wide screens. |
| GAP-003 | MAJOR | `FeedView.vue` uses a single full-width column layout for toolbar and feed items instead of a scanning-optimized two-column structure (stream + sticky sidebar). |
| GAP-004 | MAJOR | `FeedView.vue` lacks responsive breakpoint layout rules to stack the contextual side panel cleanly below the stream on viewports $< 1200\text{px}$. |
| GAP-005 | MINOR | `FeedView.vue` lacks quick metrics / exception summary card in the right column to consolidate operational status without scrolling. |

---

# 4. Open Questions

Identify unresolved questions and their impact on design and architecture.

| ID | Question | Impact |
|------|------|------|
| OQ-001 | Should the 1440px content constraint apply globally across all application views in `App.vue` or be opt-in per view? | Determines whether `App.vue` wraps `<router-view />` inside `.cakra-shell-container` universally or delegates container sizing to each view. |
| OQ-002 | What contextual information should be presented in the FeedView right-hand sidebar? | Determines the component structure and data inputs for the side panel. |
| OQ-003 | What is the exact breakpoint for switching FeedView between two columns and a stacked layout? | Establishes the CSS grid / flex media query threshold (e.g., $1200\text{px}$ `xl` breakpoint). |

---

# 5. Assumptions

Document assumptions made during the assessment.

| ID | Assumption |
|------|------|
| ASM-001 | Sidebar width (204px expanded / 50px collapsed) and full-height layout remain unchanged. |
| ASM-002 | Topbar and footer remain full-bleed spanning the entire workspace width, providing a clean frame around the centered 1440px content container. |
| ASM-003 | All existing views (Customer Management, User Management, Request Detail, Work Packages, Analytics) render comfortably within 1440px without breaking layout. |

---

# 6. Risks

Document identified risks, impacts, and mitigations.

| ID | Risk | Impact | Mitigation |
|------|------|------|------|
| RISK-001 | Data-dense tables in views (e.g. Work Packages, Customer Management) might have reduced horizontal space on 1440px compared to full-width 4K monitors. | Moderate | Ensure all data tables are wrapped in standard Bootstrap `.table-responsive` wrappers to allow clean horizontal scroll when columns exceed container width. |
| RISK-002 | Sticky right sidebar in `FeedView` might overflow vertically on low-height laptop displays (e.g. 768px height). | Low | Style sticky sidebar with `position: sticky; top: calc(var(--topbar-height) + 0.75rem); max-height: calc(100vh - var(--topbar-height) - 1.5rem); overflow-y: auto;`. |

---

# 7. Recommendations

Recommend possible approaches for addressing major gaps.

## Option A (Recommended)

1. Introduce design tokens in `main.css`:
   - `--cakra-content-max-width: 1440px;`
   - `--cakra-container-padding: clamp(0.75rem, 1.5vw, 1.5rem);`
   - `.cakra-shell-container { width: 100%; max-width: var(--cakra-content-max-width); margin-inline: auto; padding-inline: var(--cakra-container-padding); }`
2. Update `App.vue` to wrap `<router-view />` within `.cakra-shell-container` inside `<main class="cakra-page-content">`.
3. Refactor `FeedView.vue` to use a two-column responsive grid:
   - Left / Main Column (`col-12 col-xl-8 col-xxl-8`): Feed item stream, alerts, search/filter active tags, and pagination.
   - Right / Side Column (`col-12 col-xl-4 col-xxl-4`): Sticky side panel containing filter controls, active exception summary card, and operational stream metrics.
4. On screens $< 1200\text{px}$ (`lg`, `md`, `sm`), the side panel automatically stacks below the main feed stream.

### Advantages

- Universal visual consistency across all views.
- Optimal readability and reduced horizontal eye travel on wide screens.
- Zero breaking changes to existing routing or backend APIs.
- Clean separation between global shell framing and view-level content layout.

### Disadvantages

- Slightly less horizontal space on ultra-wide screens for views with very wide multi-column tables (mitigated by responsive scroll).

## Option B

Apply max-width constraints only within `FeedView.vue` and keep `App.vue` fluid.

### Advantages

- Other views remain 100% full-width on ultra-wide monitors.

### Disadvantages

- Inconsistent layout widths across pages when navigating between Feed, Request Detail, and Management screens.
- Violates single-source-of-truth shell layout principle.

---

# 8. Gap Closure

Record resolutions for all gaps and open questions.

## GAP-001

### Decision

Add `--cakra-content-max-width: 1440px`, `--cakra-container-padding: clamp(0.75rem, 1.5vw, 1.5rem)`, and `.cakra-shell-container` utility styles in [main.css](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/assets/main.css).

### Rationale

Establishes reusable design tokens and a standardized container class for content containment and horizontal centering across the application.

### Impact

Enables centralized control of max-width constraints and padding.

### Architecture Impact

None on backend; defines standard frontend design system layout tokens in `main.css`.

### Resolved By

ica-analyst / User Alignment

### Resolved Date

2026-10-05

---

## GAP-002

### Decision

Update [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue) `<main class="cakra-page-content">` to wrap `<router-view />` in `<div class="cakra-shell-container py-2">`.

### Rationale

Applies the 1440px constraint uniformly across all authenticated application screens while keeping topbar, sidebar, and footer full width.

### Impact

All views render centered within 1440px on viewports $> 1440\text{px}$.

### Architecture Impact

Modifies authenticated application shell template structure in `App.vue`.

### Resolved By

ica-analyst / User Alignment

### Resolved Date

2026-10-05

---

## GAP-003 & GAP-004 & GAP-005

### Decision

Refactor [FeedView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue) template and styles into a responsive two-column grid (`col-12 col-xl-8 col-xxl-8` for feed stream, `col-12 col-xl-4 col-xxl-4` for sticky sidebar with filters and metrics). On viewports $< 1200\text{px}$, the sidebar stacks below the feed stream.

### Rationale

Minimizes horizontal eye travel distance on large screens while preserving all filter, search, exception highlighting, and pagination functionality.

### Impact

Enhanced readability, faster scanning, and responsive adaptability on tablets and mobile devices.

### Architecture Impact

Updates `SCR-FEED-001` UI layout implementation in `FeedView.vue`.

### Resolved By

ica-analyst / User Alignment

### Resolved Date

2026-10-05

---

## OQ-001

### Decision

Apply the 1440px `.cakra-shell-container` globally in `App.vue` wrapping all route views.

### Rationale

Ensures consistent visual hierarchy, alignment, and reading margins across all pages in the application.

### Impact

Eliminates page-to-page width jumping.

### Architecture Impact

Standardizes shell layout container.

### Resolved By

ica-analyst / User Alignment

### Resolved Date

2026-10-05

---

## OQ-002

### Decision

Place filter controls (Customer dropdown, Product dropdown, Exceptions toggle, Search input, Apply/Reset buttons), stream summary metrics, and active exception alerts in the sticky right sidebar of `FeedView.vue`.

### Rationale

Consolidates operational controls in a fixed, easily accessible panel adjacent to the scrolling feed items.

### Impact

Users can adjust filters and monitor exception status without scrolling back to the top of long feed pages.

### Architecture Impact

Updates `FeedView.vue` component hierarchy and styling.

### Resolved By

ica-analyst / User Alignment

### Resolved Date

2026-10-05

---

## OQ-003

### Decision

Use the $1200\text{px}$ (`xl`) Bootstrap breakpoint for two-column activation. On viewports $< 1200\text{px}$, stack side panel below the stream.

### Rationale

Provides adequate column width for both the feed stream ($\approx 800\text{px}$) and side panel ($\approx 380\text{px}$) on desktops, while avoiding cramped side-by-side columns on tablet viewports.

### Impact

Optimized layout density across desktop and mobile devices.

### Architecture Impact

Media queries and responsive Bootstrap grid classes in `FeedView.vue`.

### Resolved By

ica-analyst / User Alignment

### Resolved Date

2026-10-05

---

# 9. Architecture Applicability

Record the Architecture Applicability decision.

## Decision

ARCHITECTURE-REQUIRED

## Rationale

The change introduces cross-cutting application shell modifications ([App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue)), global design tokens and layout utility classes in [main.css](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/assets/main.css), and structural component redesign of `SCR-FEED-001` in [FeedView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue). Architecture specification is required to formally document the UI layout specification and technical implementation slices before implementation planning begins.

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

The Architect has verified that all critical gaps, open questions, and assumptions have been fully resolved with rigorous decisions recorded. READY-FOR-PLANNING is granted for CR-011 target architecture creation and implementation planning.

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-011-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-011-ISSUE.md)
- UI Layout Spec: [10-scr-feed-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/10-scr-feed-001.md)
- Architecture: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

Referenced codebase locations:

- Application Shell: [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue)
- Global Styles: [main.css](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/assets/main.css)
- Operational Feed View: [FeedView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue)
