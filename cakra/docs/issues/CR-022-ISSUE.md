# ISSUE

## Metadata

ID: CR-022
Type: CHANGE-REQUEST
Status: OPEN
Title: UI Foundation Modernization and Component System Architecture

## Source

Reported By: User
Reported Date: 2026-10-07

## Description

Approved modern user interface designs and mockups (such as the Work in Progress operational awareness board in `wip_operational_board.html`) consistently degrade into heavy, clunky, 1990s/early-2000s boxed admin panels when implemented in the Cakra web application (`src/frontend/Cakra.Web`).

A forensic investigation into the UI foundation layer confirmed that the degradation is enforced by foundation-level constraints rather than screen implementation flaws:

1. **Typographic Scale Collapse**: Global styling forces a 13px root font size, cramped 1.35 line height, and crushed headings (`h1` is 14.95px, only 1.95px above body text). This destroys visual hierarchy, forcing developers to box elements into nested cards, badges, and borders to create visual distinction.
2. **Compounded Box & Border Chrome**: Global `.card` styling mandates solid 1px `#dbe3eb` borders, 6px radii, and gray `#f5f7f9` headers. When cards and sections are combined, the interface compounds into multiple layers of heavy rectangular boxes.
3. **Monolithic Navy Color Cast**: Dark steel navy (`--cakra-navy: #364f6b`) is applied universally across text, headings, borders, and tabs instead of neutral slate tones, giving the UI a dated, heavy cast.
4. **Complete Absence of Base Component Primitives**: The frontend codebase possesses zero reusable base components (no `BaseCard`, `BaseBadge`, `PageHeader`, `StatusStrip`, or `Avatar`). All existing components are domain-specific CRUD modals or feed widgets, forcing screens to copy-paste raw Bootstrap markup and inject over 60 ad-hoc inline styles.
5. **Low Design System Maturity**: The application sits at Level 1.5 maturity (between Bootstrap CRUD and a themed admin template), lacking design token abstractions and container flexibility required to support modern product UI standards.

## Desired Outcome

Modernize the Cakra UI foundation across all proposed phases so that approved UI refinements and modern product designs can be reproduced faithfully in the actual application without degrading into legacy visual styles:

### Phase 0: Design Token & Global CSS Modernization
1. Establish a modern base typography scale:
   - Increase root font size to a readable 14px (or 15px) standard.
   - Relax body line height to 1.5 to provide adequate breathing room.
   - Restore true heading hierarchy (`h1` ~22–24px, `h2` ~18–20px, `h3` ~15–16px).
2. Neutralize surfaces and ambient colors:
   - Replace the muddy canvas (`#f5f5f5`) with a clean slate canvas (`#f8fafc`).
   - Introduce neutral slate text tokens (`#0f172a` primary, `#475569` secondary).
   - Reserve brand colors (`#364f6b` navy, `#3fc1c9` cyan) for intentional accents and primary actions.
3. Modernize the border and shadow system:
   - Soften border color from heavy `#dbe3eb` to subtle slate `#e2e8f0`.
   - Expand border-radius scale to support modern radii (6px, 8px, 12px, 16px).
   - Replace muddy navy shadows with diffuse natural ambient elevation tokens.
4. Decouple global `.card` enforcements to allow borderless, flat, and transparent container styling.

### Phase 1: Base Component Primitive Layer
1. Implement encapsulated, type-safe Vue 3 base component primitives in `src/components/base/`:
   - `BaseCard.vue`: Supports bordered, flat, ghost, and elevated variants with customizable padding.
   - `BaseBadge.vue`: Standardizes status pills, subtle tints, and animated live pulse dots.
   - `PageHeader.vue`: Standardizes screen titles, breadcrumb badges, and action strips.
   - `StatusStrip.vue`: Renders compact single-line operational KPI ribbons with vertical dividers.
   - `BaseAvatar.vue`: Generates user initials and presence status beacons (working, paused, offline).
   - `SlideOverDrawer.vue`: Provides slide-over inspection drawers for rich context without cluttering screens.

### Phase 2: Shell & Layout Modernization
1. Update `App.vue` layout container to support flexible max-widths based on route metadata (`narrow` 1024px for focused feeds like WIP and Feed; `wide` 1440px for data-heavy analytics and tables).
2. Harmonize topbar and sidebar visual integration with modern elevation and clean contrast.

### Phase 3: Screen Modernization Pilot (WIP Verification)
1. Re-implement `WorkInProgressView.vue` (`SCR-REQ-006`) strictly using the new foundation primitives.
2. Verify 100% faithful reproduction of the approved operational awareness board mockup (`wip_operational_board.html`) without visual degradation.
3. Preserve all existing automated test selectors (`data-testid`, `data-screen-id="SCR-REQ-006"`).

## Current Situation

1. The frontend application relies on precompiled Bootstrap 5 (`bootstrap.min.css`) combined with a single monolithic 1,238-line stylesheet (`src/assets/main.css`).
2. Global styles force 13px font size, 1.35 line height, and 14.95px `h1` headings across the entire application.
3. Global `.card` rules enforce 1px solid `#dbe3eb` borders, 6px radii, and `#f5f7f9` gray headers.
4. Canvas background is forced to `#f5f5f5` and body text is forced to `#364f6b`.
5. Zero base component primitives exist in `src/frontend/Cakra.Web/src/components/`.
6. Screens directly write raw Bootstrap markup, resulting in visual inconsistency and over 60 ad-hoc inline styles.
7. Attempts to modernize screens (such as `WorkInProgressView.vue`) consistently degrade into boxed, 1990s-style admin panels.

## Evidence

- UI Foundation Investigation Report: [ui_foundation_investigation_report.md](file:///C:/Users/drury/.gemini/antigravity/brain/79c9747e-e2b7-4a51-92a2-294539e6d6c5/ui_foundation_investigation_report.md)
- Approved Modern WIP Mockup: [wip_operational_board.html](file:///C:/Users/drury/.gemini/antigravity/brain/321a8c71-5ea0-4c4d-aa42-f910ab295492/wip_operational_board.html)
- Current WIP View: [WorkInProgressView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkInProgressView.vue#L152-L499)
- Global Stylesheet: [main.css](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/assets/main.css#L7-L142)
- Application Shell Layout: [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue#L344-L358)
- Component Directory Inventory: [src/components/](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components)

## Notes

- Intake findings are derived from the UI Foundation Investigation completed on 2026-10-07.
- Downstream workflow routing:
  - Stage: Discovery & Feasibility Assessment
  - Owning Role: `ica-analyst`
