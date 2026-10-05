# ISSUE

## Metadata

ID: CR-011
Type: CHANGE-REQUEST
Status: OPEN
Title: Constrained Content Container Layout and Redesigned Application Shell

## Source

Reported By: User
Reported Date: 2026-10-05

## Description

The user reported that the current application shell uses a full-width fluid layout stretching across 100% of the viewport width, causing content lines and feed items to become excessively long and difficult to scan on large, ultra-wide, and high-resolution monitors ("stretched dashboard" effect). The requested change is to redesign the application shell to adopt a centered, constrained maximum content width (1400px–1600px ceiling, specifically aligned at 1440px) while maintaining full-height sidebar navigation and full-bleed topbar/footer workspace chrome. Additionally, the Operational Feed (`FeedView.vue`) should be reorganized into a high-density, scanning-optimized layout with a main feed column and sticky contextual sidebar, reducing horizontal eye travel distance and maintaining responsive adaptability across smaller devices.

## Desired Outcome

1. Application shell content area is constrained to a maximum width of 1440px (`--cakra-content-max-width: 1440px`), centered horizontally (`margin-inline: auto`) on viewports wider than 1440px.
2. Full-height sidebar navigation (`cakra-sidebar`), sticky topbar (`cakra-topbar`), and footer (`cakra-app-footer`) continue to span the full workspace width seamlessly.
3. Universal application of the constrained content wrapper across all application views in `App.vue` for visual consistency and predictable scanning margins.
4. Operational Feed (`FeedView.vue` / `SCR-FEED-001`) layout optimized for reading density and reduced horizontal eye travel via a two-column structure (feed stream ~68–70% width alongside a sticky sidebar with quick filters, active exception summaries, and metrics) on large screens (>= 1200px).
5. Responsive behavior preserved across all breakpoints: side panel neatly stacks below the feed stream on screens under 1200px, and horizontal padding adapts smoothly from mobile (16px) to wide desktops (32px).
6. CSS design tokens and standardized shell container utility classes integrated cleanly into the design system.

## Current Situation

1. Currently in `App.vue`, the main content wrapper uses `<main class="cakra-page-content"><div class="container-fluid px-2 px-md-3 py-2">...</div></main>`, which expands to 100% of the viewport width without a max-width ceiling.
2. On wide and ultra-wide displays (e.g., 1440p, 4K, ultrawide monitors), cards, text lines, and forms in `FeedView.vue` and other views span excessively across the full monitor width, resulting in high horizontal eye travel distance and poor reading scanning comfort.
3. `FeedView.vue` renders all feed items in a single wide column stretching across the entire width of the fluid container.

## Evidence

- User request:
  - Redesign application shell to use constrained content width instead of full-width fluid layout.
  - Max width between 1400px–1600px (1440px selected).
  - Center content horizontally on screens wider than max width.
  - Keep sidebar full height; do not allow feed content to span entire monitor width.
  - Reduce horizontal eye travel distance and optimize for readability and information scanning.
  - Maintain responsive behavior for smaller screens.
- Alignment interview (/grill-me) decisions:
  - 1440px max-width container for main page content, with topbar and footer stretching full width across the workspace area.
  - Two-column layout in FeedView: main feed stream alongside sticky sidebar with quick filters and metrics.
  - Stack side panel below feed on viewports < 1200px; side-by-side on >= 1200px.
  - CSS custom property `--cakra-content-max-width: 1440px` and `.cakra-shell-container` wrapper class in `App.vue`.
  - Global containment applied uniformly in `App.vue` wrapping all route views.
- Existing files:
  - Application Shell: [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue)
  - Design Tokens & Layout CSS: [main.css](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/assets/main.css)
  - Operational Feed View: [FeedView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue)

## Notes

This artifact formally captures the intake request for the Application Shell Constrained Content Layout change request (CR-011) in a solution-neutral manner according to Knowledge-Centric SDLC standards. Detailed feasibility assessment, domain and feature updates, architecture design, and implementation planning belong to downstream analysis and architecture stages.
