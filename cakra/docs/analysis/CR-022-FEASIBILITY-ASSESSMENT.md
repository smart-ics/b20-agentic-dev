---
Title: Feasibility Assessment for UI Foundation Modernization and Component System Architecture (CR-022)
Code: CR-022
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-07
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Change being assessed: UI Foundation Modernization and Component System Architecture across all proposed phases (Phase 0: Design Tokens & Global CSS Modernization, Phase 1: Base Component Primitive Layer, Phase 2: Shell & Layout Modernization, Phase 3: Screen Modernization Pilot), as formally captured in [CR-022-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-022-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-022-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-022-ISSUE.md)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md) (specifically Section 19.4 Frontend Stack)
- UI INVESTIGATION: [ui_foundation_investigation_report.md](file:///C:/Users/drury/.gemini/antigravity/brain/79c9747e-e2b7-4a51-92a2-294539e6d6c5/ui_foundation_investigation_report.md)
- APPROVED PROTOTYPE: [wip_operational_board.html](file:///C:/Users/drury/.gemini/antigravity/brain/321a8c71-5ea0-4c4d-aa42-f910ab295492/wip_operational_board.html)

## Objective

Assess the feasibility, technical boundaries, architectural compatibility, CSS cascade impact, component contract design, regression risks across existing screens, and planning readiness to:

1. Modernize the global design tokens and CSS architecture in `src/assets/main.css` to eliminate the "13px Gravitational Field", neutralize the muddy `#f5f5f5` canvas and heavy `#364f6b` navy color cast, restore true heading hierarchy, soften borders to `#e2e8f0`, expand the radius scale (6px, 8px, 12px, 16px), and decouple global `.card` rules.
2. Design and implement a reusable **Base Component Primitive Layer** in `src/components/base/` (`BaseCard.vue`, `BaseBadge.vue`, `PageHeader.vue`, `StatusStrip.vue`, `BaseAvatar.vue`, and `SlideOverDrawer.vue`) eliminating copy-paste raw Bootstrap markup and ad-hoc inline styling across screens.
3. Modernize the application shell layout in `App.vue` to support flexible content container widths (`narrow` 1024px for focused operational feeds like WIP and Feed; `wide` 1440px for data-heavy analytics tables and work package trees) and harmonize visual elevation.
4. Pilot the modernized UI foundation by re-implementing `WorkInProgressView.vue` (`SCR-REQ-006`) to prove 100% faithful reproduction of the approved operational awareness board mockup (`wip_operational_board.html`) without visual degradation, while maintaining complete automated test selector compatibility (`data-testid`, `data-screen-id`).

---

# 2. Current State

## Existing Behavior

1. **CSS Framework Integration (`src/frontend/Cakra.Web`)**:
   - The frontend application imports precompiled Bootstrap 5 (`bootstrap/dist/css/bootstrap.min.css`) and Bootstrap Icons (`bootstrap-icons/font/bootstrap-icons.css`) directly in `main.ts`.
   - There is no Sass/SCSS build pipeline configured in Vite; all theme customization and overrides are executed at runtime via a single 1,238-line stylesheet: `src/assets/main.css`.
   - Global styling in `main.css` enforces `html, body { font-size: 13px; line-height: 1.35; color: #364f6b; background-color: #f5f5f5; }`.
   - Headings are compressed to micro-scale (`h1` = 1.15rem / 14.95px; `h2` = 1.025rem / 13.32px; `h3` = 0.925rem / 12.02px), completely flattening visual hierarchy.
   - Global `.card` rules enforce solid 1px `#dbe3eb` borders, 6px radii, and gray `#f5f7f9` headers with no variant classes.

2. **Component Architecture (`src/frontend/Cakra.Web/src/components`)**:
   - The codebase contains exactly 11 components: 8 domain-specific administrative CRUD modals (`CreateCustomerModal`, `CustomerModal`, `EditCustomerModal`, `CustomerContactModal`, `PersonModal`, `UserAccountModal`, `CreateRequestModal`, `PostDetailModal`) and 3 operational feed components (`FeedDossierPanel`, `FeedLedgerRow`, `FeedTimelineCard`).
   - There are **zero reusable UI base components** (`BaseCard`, `BaseBadge`, `PageHeader`, `StatusStrip`, `Avatar` do not exist).
   - Every screen directly writes raw HTML elements with Bootstrap classes (`<div class="card border">`, `<table class="table">`, `<span class="badge">`).
   - Because base components with variant props do not exist, views have accumulated over 60 instances of raw inline `style="..."` overrides (e.g. `style="font-size: 11px; height: 26px;"`).

3. **Shell & Layout Container (`src/frontend/Cakra.Web/src/App.vue`)**:
   - `App.vue` wraps `<router-view />` in a fixed structure: `.cakra-sidebar` (fixed 204px width, `#223245` gradient), `.cakra-topbar` (fixed 42px sticky header), and `.cakra-shell-container` with `max-width: 1440px`.
   - All screens are forced into the same 1440px wide container, stretching out focused editorial feeds (like WIP) across wide displays.

4. **Screen Implementation Degradation**:
   - When modern mockups are designed (such as `wip_operational_board.html` using modern 14px typography, slate `#0f172a` text, `#f8fafc` canvas, 12px radii, and borderless rows), implementing them in Cakra forces them to inherit the 13px font-size, 1.35 line-height, `#364f6b` navy cast, and rigid 6px `#dbe3eb` borders, degrading the output into a 1990s-style boxed admin panel.

## Existing Constraints

1. **Vite Tooling Stack**:
   - `package.json` specifies Vue 3.5, Vite 8.3, TypeScript 5.9, and `vue-tsc`.
   - Sass preprocessor is not installed. Introducing a full Sass compile pipeline for Bootstrap would require significant changes to Vite configuration and build times. Runtime CSS Custom Properties (`:root`) and modular CSS are preferred.
2. **Backward Compatibility with Existing Screens**:
   - 13 views (`FeedView`, `RequestDetailView`, `CustomerManagementView`, `WorkPackageView`, `MyRequestsView`, etc.) currently rely on classes in `main.css`.
   - Global token adjustments must preserve layout stability and avoid breaking existing data tables, forms, and modals.
3. **Automated Test Compatibility**:
   - Backend integration tests and frontend verification rely on exact `data-testid` attributes (e.g., `data-testid="wip-screen"`, `data-testid="person-card"`, `data-testid="in-progress-card"`, `data-testid="paused-task-card"`) and `data-screen-id="SCR-REQ-006"`.
   - Foundation modernizations must preserve 100% of these test hooks.

---

# 3. Gap Analysis

| ID | Severity | Gap |
|---|---|---|
| **GAP-001** | CRITICAL | **Typographic Scale Collapse & Line Height Compression**: `main.css` forces 13px base font, 1.35 line height, and 14.95px `h1`. Modern typography hierarchy cannot be expressed, forcing developers to box elements into cards and badges to create visual separation. |
| **GAP-002** | CRITICAL | **Compounded Box & Border Chrome in Global CSS**: Global `.card`, `.card-header`, `.op-screen-header`, and `.op-toolbar` rules mandate solid 1px `#dbe3eb` borders, 6px radii, and gray `#f5f7f9` fills, creating visually heavy nested boxes across all views. |
| **GAP-003** | CRITICAL | **Absence of Base Component Primitives Layer**: Zero reusable UI primitives exist in `src/components/base/`. Every screen writes raw HTML and ad-hoc inline styles, making global design iteration impossible and causing mockups to degrade during implementation. |
| **GAP-004** | MAJOR | **Rigid Layout Shell without Screen-Adaptive Widths**: `App.vue` forces all views into a 1440px wide container, stretching out focused editorial and operational awareness boards like WIP and Feed. |
| **GAP-005** | MAJOR | **Absence of Contextual Flyout / Inspection Drawer**: The application shell lacks a reusable slide-over drawer primitive, forcing screens to either cram dense metadata (GUIDs, timestamps, logs) into tight cards or link away to detail views. |
| **GAP-006** | MINOR | **Monolithic Navy Color Cast**: `--cakra-navy: #364f6b` is applied universally to body text, headings, borders, active tabs, and icons instead of neutral slate tones (`#0f172a`, `#475569`, `#e2e8f0`), creating a dark, muddy visual cast. |

---

# 4. Open Questions

| ID | Question | Impact |
|---|---|---|
| **OQ-001** | What standard base font size should be adopted for the modernized foundation: `14px` (DevOps/Linear compact standard) or `15px` / `16px`? | Impacts overall visual density across all 13 screens and data table cell heights. |
| **OQ-002** | How should global CSS variables be updated so that existing screens (`FeedView`, `RequestDetailView`, `CustomerManagementView`) do not visually break while typography and colors are modernized? | Determines whether changes to `main.css` are backwards-compatible via aliased tokens or require screen-by-screen auditing. |
| **OQ-003** | Should base component primitives be implemented purely as native Vue 3 Single File Components (SFCs) with scoped CSS / Bootstrap utility classes, or should an external component library be introduced? | Affects package dependencies, bundle size, and build configuration. |
| **OQ-004** | How should route-specific container widths (`narrow` 1024px vs `wide` 1440px) be communicated from view components or router metadata to `App.vue`? | Impacts `router/index.ts` route `meta` definitions and `App.vue` shell wrapper classes. |

---

# 5. Assumptions

| ID | Assumption |
|---|---|
| **ASM-001** | Updating base font size from 13px to 14px and line-height from 1.35 to 1.5 will improve readability and contrast across all screens without breaking table or form layouts. |
| **ASM-002** | Native Vue 3 components using the Composition API (`<script setup lang="ts">`) and CSS Custom Properties provide sufficient abstraction for base primitives without needing external third-party UI libraries (such as PrimeVue or Vuetify). |
| **ASM-003** | Preserving all existing `data-testid` and `data-screen-id` attributes on components and views guarantees zero regressions in automated backend and frontend test suites. |
| **ASM-004** | The approved mockup [`wip_operational_board.html`](file:///C:/Users/drury/.gemini/antigravity/brain/321a8c71-5ea0-4c4d-aa42-f910ab295492/wip_operational_board.html) serves as the authoritative visual benchmark for the modernized UI foundation. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| **RISK-001** | Visual regressions or layout shifts on existing screens (e.g. `CustomerManagementView`, `RequestDetailView`) when global tokens in `main.css` are updated. | HIGH | Retain backwards-compatible CSS variable aliases; use subtle enhancements (14px base, slate neutrals); verify all views via production build (`npm run build`). |
| **RISK-002** | Inconsistent adoption where new screens use base primitives while legacy screens continue using raw Bootstrap markup. | MEDIUM | Standardize base primitives in `src/components/base/`; document clear usage patterns; pilot on `WorkInProgressView.vue` before broader adoption. |
| **RISK-003** | Class collision between Bootstrap 5 utility classes and custom base component classes. | LOW | Scope base component styles or prefix custom component utility classes cleanly. |

---

# 7. Recommendations

## Option A: Native Vue 3 Base Component Primitives + Modernized CSS Token Architecture (Recommended)

Modernize `main.css` with a 14px base typography scale, neutral slate palette (`#0f172a`, `#475569`, `#f8fafc`, `#e2e8f0`), expanded border-radii (6px–16px), and natural elevation shadows. Build native Vue 3 base component primitives in `src/components/base/` (`BaseCard`, `BaseBadge`, `PageHeader`, `StatusStrip`, `BaseAvatar`, `SlideOverDrawer`) consuming these tokens. Update `App.vue` to support `meta.containerWidth`.

### Advantages
- Zero new third-party npm runtime dependencies; builds directly on Vue 3 and Vite.
- Seamless backwards compatibility with existing Bootstrap 5 classes and automated test selectors.
- Immediately enables faithful reproduction of `wip_operational_board.html` on `WorkInProgressView.vue`.
- Incremental and non-disruptive: existing screens continue functioning while new primitives are introduced.

### Disadvantages
- Requires creating and maintaining 6 lightweight base component primitives in `src/components/base/`.

## Option B: Full Migration to a Third-Party Component Framework (e.g. PrimeVue / Vuetify)

Install an external component suite, replace Bootstrap completely, and rewrite all screens.

### Advantages
- Provides pre-packaged components with established design tokens.

### Disadvantages
- Highly disruptive: breaks all 13 existing screens and 11 modals.
- Introduces large bundle overhead and steep refactoring risk across the entire application.
- Violates Architecture §19.4 which mandates Bootstrap 5 with Vue 3. Rejected.

---

# 8. Gap Closure

## GAP-001 & GAP-006 (Typographic Scale, Line Height, and Palette Modernization)
### Decision
In `src/assets/main.css`:
1. Increase root font size to `14px` (`--bs-body-font-size: 0.875rem`) and line-height to `1.5`.
2. Restore true heading hierarchy: `h1` at `1.5rem` (21px), `h2` at `1.25rem` (17.5px), `h3` at `1.1rem` (15.4px), `h4` at `1.0rem` (14px).
3. Introduce neutral slate text tokens: `--cakra-text-main: #0f172a` (primary text) and `--cakra-text-muted: #475569` (secondary text).
4. Update canvas background to clean slate `--cakra-bg-canvas: #f8fafc`.
5. Retain `--cakra-primary: #364f6b` and `--cakra-secondary: #3fc1c9` as brand accents for buttons, active badges, and highlights rather than universal text fills.
### Rationale
Eliminates the "13px Gravitational Field", restores visual hierarchy, and removes the heavy blue-gray cast across the entire interface.
### Impact
`src/assets/main.css`.
### Architecture Impact
Updates Section 19.4 typography and color palette baseline in `CAKRA-ARCHITECTURE.md`.
### Resolved By
User & Analyst
### Resolved Date
2026-10-07

---

## GAP-002 (Compounded Box & Border Chrome Modernization)
### Decision
1. Soften global border color `--cakra-border` from heavy `#dbe3eb` to subtle slate `#e2e8f0`.
2. Define modern radius tokens: `--cakra-radius-sm: 6px; --cakra-radius-md: 8px; --cakra-radius-lg: 12px; --cakra-radius-xl: 16px;`.
3. Modernize shadows to diffuse natural elevation tokens: `--cakra-shadow-xs: 0 1px 2px 0 rgb(0 0 0 / 0.04); --cakra-shadow-sm: 0 1px 3px 0 rgb(0 0 0 / 0.08), 0 1px 2px -1px rgb(0 0 0 / 0.04); --cakra-shadow-md: 0 4px 6px -1px rgb(0 0 0 / 0.08), 0 2px 4px -2px rgb(0 0 0 / 0.04);`.
4. Decouple `.card-header` by making its background transparent by default with subtle border separation, allowing flat and borderless card variants.
### Rationale
Removes the 1990s nested-box appearance and provides modern whitespace-driven card presentation.
### Impact
`src/assets/main.css`.
### Architecture Impact
CSS architecture token updates.
### Resolved By
User & Analyst
### Resolved Date
2026-10-07

---

## GAP-003 & GAP-005 (Base Component Primitive Layer & Slide-Over Drawer)
### Decision
Create encapsulated Vue 3 base component primitives in `src/components/base/`:
1. `BaseCard.vue`: Supports `variant` (`'bordered' | 'flat' | 'ghost' | 'elevated'`), `padding` (`'none' | 'sm' | 'md' | 'lg'`), and `rounded` (`'md' | 'lg' | 'xl'`).
2. `BaseBadge.vue`: Supports `variant` (`'neutral' | 'success' | 'warning' | 'danger' | 'brand'`), `style` (`'subtle' | 'solid' | 'outline'`), and optional animated pulsing live dot.
3. `PageHeader.vue`: Standardizes screen title, subtitle, screen ID badge, and live telemetry status.
4. `StatusStrip.vue`: Renders compact single-line operational awareness KPI ribbons with vertical dividers.
5. `BaseAvatar.vue`: Standardizes initials display, avatar sizes (`sm`, `md`, `lg`), and presence status indicators (`online`, `busy`, `paused`, `offline`).
6. `SlideOverDrawer.vue`: Provides slide-over inspection drawers with backdrop blur, keyboard ESC dismissal, and clean action footer.
### Rationale
Gives screens reusable, variant-aware building blocks, eliminating copy-paste raw Bootstrap markup and preventing modern mockups from degrading.
### Impact
New directory `src/frontend/Cakra.Web/src/components/base/`.
### Architecture Impact
New component layer defined under Section 19.4.
### Resolved By
User & Analyst
### Resolved Date
2026-10-07

---

## GAP-004 (Flexible Layout Shell Container)
### Decision
In `src/frontend/Cakra.Web/src/App.vue`:
1. Inspect `route.meta.containerWidth`:
   - If `'narrow'`: apply max-width `1024px` (ideal for continuous operational feeds like WIP and Feed).
   - If `'wide'` (or default): maintain max-width `1440px` (ideal for multi-column data tables, work package trees, and analytics).
2. Soften topbar bottom border to `#e2e8f0` and adjust padding for modern alignment.
### Rationale
Allows focused awareness boards to feel intimate and intentional (Slack/Linear style) without being artificially stretched across ultra-wide monitors.
### Impact
`src/frontend/Cakra.Web/src/App.vue`, `src/frontend/Cakra.Web/src/router/index.ts`.
### Architecture Impact
Shell container responsiveness and route metadata conventions.
### Resolved By
User & Analyst
### Resolved Date
2026-10-07

---

## OQ-001 through OQ-004 (Aligned Operational Decisions)
### Decision
- **OQ-001 (Base Font Size)**: Adopt `14px` (0.875rem) as the base font size. It matches modern DevOps and engineering productivity tools (Linear, GitHub, VS Code) while preserving high data density.
- **OQ-002 (Backwards Compatibility)**: Preserve all existing CSS variable names and semantic aliases in `main.css`, updating their values to the modernized slate palette. Legacy screens will immediately benefit from cleaner contrast without breaking.
- **OQ-003 (Primitive Implementation)**: Implement primitives natively as Vue 3 SFCs using `<script setup lang="ts">` and CSS tokens. Zero third-party runtime UI library overhead.
- **OQ-004 (Container Width Routing)**: Use Vue Router `route.meta.containerWidth` property (`'narrow' | 'wide'`).
### Rationale
Maintains architectural determinism, zero bundle bloat, and predictable implementation.
### Impact
Clear operational specifications for Architecture and Implementation Planning.
### Architecture Impact
Direct input for `CR-022-ARCHITECTURE.md`.
### Resolved By
User & Analyst
### Resolved Date
2026-10-07

---

# 9. Architecture Applicability

## Decision
ARCHITECTURE-REQUIRED

## Rationale
Architecture definition is required because this change introduces:
1. A new architectural layer: **Base Component Primitive Layer** in `src/components/base/`.
2. Modifications to foundational cross-cutting styling contracts in `main.css` (typography scale, design tokens, elevation, border-radius).
3. Modifications to the application shell layout in `App.vue` and route metadata conventions in `router/index.ts`.
4. Re-implementation of `WorkInProgressView.vue` (`SCR-REQ-006`) to pilot and prove the modernized UI foundation.

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

All feasibility analysis, gap identification, and gap closure decisions across Phase 0 through Phase 3 have been completed and approved. The Architect has evaluated the artifact and granted the READY-FOR-PLANNING gate. Target architecture definition may proceed.

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-022-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-022-ISSUE.md)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- UI INVESTIGATION: [ui_foundation_investigation_report.md](file:///C:/Users/drury/.gemini/antigravity/brain/79c9747e-e2b7-4a51-92a2-294539e6d6c5/ui_foundation_investigation_report.md)
- APPROVED PROTOTYPE: [wip_operational_board.html](file:///C:/Users/drury/.gemini/antigravity/brain/321a8c71-5ea0-4c4d-aa42-f910ab295492/wip_operational_board.html)

Referenced codebase locations:

- [main.css](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/assets/main.css)
- [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue)
- [router/index.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/router/index.ts)
- [WorkInProgressView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkInProgressView.vue)
- [components/](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components)
