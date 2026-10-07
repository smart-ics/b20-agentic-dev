# ISSUE

## Metadata

ID: CR-026
Type: CHANGE-REQUEST
Status: OPEN
Title: Executive Dark Theme, Modern Typography (Inter & JetBrains Mono), and Clean Color System for Cakra.Web

## Source

Reported By: User
Reported Date: 2026-10-07

## Description

The user requested adopting the executive visual design system, typography, dark theme, and clean accent color palette from the reference infographic (`EXECUTIVE-INFOGRAPHIC-BOD-REV-6.html`) into the Cakra operational frontend (`cakra/src/frontend/Cakra.Web`).

Currently, Cakra.Web operates on a light-themed interface with Bootstrap 5 and legacy custom CSS variables. The user desires an executive-grade aesthetic characterized by a deep dark background canvas, refined dark card surfaces, subtle glassmorphic borders, saturated status accents, and high-contrast modern typography suitable for operational cockpits and management dashboards.

## Desired Outcome

1. **Executive Dark Theme by Default**:
   - Establish a deep dark theme as the default visual appearance across the entire application (canvas: `slate-950`, card surfaces: `slate-900`, elevated panels: `slate-950/60`, borders: `slate-800`).
   - Provide a persistent theme toggle (Dark / Light mode) accessible to the user, storing user preference in local storage and preserving light mode accessibility.

2. **Clean Accent & Semantic Color Palette**:
   - Port the clean, high-contrast accent system from the reference infographic:
     - Cyan (`cyan-400` / `cyan-500` tints) for primary highlights, executive indicators, and active states.
     - Indigo (`indigo-400` / `indigo-500` tints) for secondary telemetry, badges, and progress tracks.
     - Emerald (`emerald-400` / `emerald-500` tints) for nominal flow, completed milestones, and health indicators.
     - Purple, Amber, and Rose for specialized operational categories, warnings, and blockers.

3. **Modern Executive Typography**:
   - Integrate **Inter** as the primary application font family for crisp hierarchy, optical sizing, and tight tracking across headings and labels.
   - Integrate **JetBrains Mono** for all numbers, operational telemetry, metrics, code, and status pill badges.

4. **App Shell & Component Modernization**:
   - Modernize the application layout shell (`App.vue`), updating the Topbar with a theme switch button in the right header and giving the Sidebar a deep slate gradient with cyan navigation accents.
   - Update reusable base components (`BaseCard`, `BaseBadge`, `PageHeader`, `StatusStrip`, `SlideOverDrawer`) to natively reflect the new dark theme, borders, and typography.
   - Overhaul existing view templates to adopt the executive styling tokens and classes.

5. **Tooling & Styling Evolution**:
   - Introduce Tailwind CSS into the Cakra.Web Vite pipeline to enable direct use of modern utility classes, establishing a path to phase out Bootstrap 5 over time.

## Current Situation

1. Cakra.Web is configured with Bootstrap 5.3 and custom tokens defined in `src/assets/main.css` (`#364f6b`, `#3fc1c9`, `#fc5185`, `#f8fafc`).
2. The application currently renders solely in a light theme (`--cakra-bg-canvas: #f8fafc; --cakra-bg-surface: #ffffff;`). There is no dark theme support or theme toggle mechanism.
3. The font stack relies on system sans (`-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto...`) without specialized tabular or high-contrast monospace fonts for telemetry.
4. Cards and panels use standard Bootstrap borders and white backgrounds rather than the deep slate surfaces and subtle border glow effects seen in the executive infographic.

## Evidence

- Reference Style File: [EXECUTIVE-INFOGRAPHIC-BOD-REV-6.html](file:///D:/Project.Aktif/b21-myhosweb-system/EXECUTIVE-INFOGRAPHIC-BOD-REV-6.html)
- Frontend Codebase: [Cakra.Web](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web)
- Specification Artifact: [cakra-dark-theme-specification.md](file:///C:/Users/drury/.gemini/antigravity/brain/f92d85dd-8ef2-4c63-8b67-ae44de1f604b/cakra-dark-theme-specification.md)
- Intake Alignment Interview: Conducted on 2026-10-07 via `/grill-me`, confirming:
  - Dark theme as default with a theme toggle preserving light mode.
  - Styling architecture: Introduce Tailwind CSS into Vite and plan gradual phase-out of Bootstrap 5.
  - Typography: Inter (sans) + JetBrains Mono (monospace).
  - Theme toggle placement: Topbar right-hand side next to the user status badge.
  - Rollout scope: All-in-one overhaul across App Shell, Base Components, and Views.

## Notes

- Intake interview confirmed:
  - Visual Fidelity: Colors, borders, pill badges, and typography must mirror the BOD executive infographic.
  - Compatibility: During the transition, Bootstrap styling should remain consistent via `data-bs-theme="dark"` alongside Tailwind's `dark` class.
- Downstream workflow routing:
  - Next Stage: Discovery & Feasibility Assessment
  - Owning Role: `ica-analyst`
