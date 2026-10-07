---
Title: Implementation Plan for UI Foundation Modernization and Component System Architecture (CR-022)
Code: CR-022
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-07
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-022`: UI Foundation Modernization and Component System Architecture for the Cakra web application (`src/frontend/Cakra.Web`).

Deliver end-to-end realization across:
1. **Design Token & Global CSS Modernization** in `src/assets/main.css`: Establishing a 14px root typography scale, relaxed 1.5 line-height, restored heading scale, neutral slate palette (`#0f172a`, `#475569`, `#f8fafc`, `#e2e8f0`), expanded border-radii (6px–16px), natural diffuse elevation shadows, and decoupled `.card` rules.
2. **Base Component Primitive Layer** in `src/components/base/`: Six encapsulated Vue 3 primitives (`BaseCard.vue`, `BaseBadge.vue`, `PageHeader.vue`, `StatusStrip.vue`, `BaseAvatar.vue`, and `SlideOverDrawer.vue`).
3. **Application Shell & Layout Modernization** in `src/App.vue`: Route-driven container widths (`narrow` 1024px for focused operational feeds vs `wide` 1440px for dense tables) and harmonized topbar elevation.
4. **Screen Modernization Pilot** in `src/views/WorkInProgressView.vue` (`SCR-REQ-006`): Proving 100% faithful reproduction of the approved operational awareness board mockup (`wip_operational_board.html`) using the new foundation primitives without visual degradation, while preserving all automated test selectors.
5. **Full-Stack Verification**: Complete frontend production build (`npm run build`) and full backend test suite run (`dotnet test cakra\Cakra.sln`).

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- ISSUE: [CR-022-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-022-ISSUE.md)
- FEASIBILITY-ASSESSMENT: [CR-022-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-022-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)
- ARCHITECTURE: [CR-022-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-022-ARCHITECTURE.md) (authoritative target architecture)

Architecture Applicability: ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers design token modernizations, global stylesheet refactoring, base component primitives development, application shell layout adaptations, pilot screen re-implementation, and full verification:

1. **Design Token & Global CSS Modernization (`src/frontend/Cakra.Web/src/assets/main.css`)**:
   - Modernize `:root` CSS custom properties: typography scale, neutral slate palette, ambient canvas, borders, radii, and natural elevation shadows.
   - Update `html, body` and heading scales (`h1` through `h6`).
   - Decouple `.card` and `.card-header` rules to allow transparent headers and borderless/flat cards.
   - Retain backwards-compatible CSS variable aliases for legacy screens.
2. **Base Component Primitive Layer (`src/frontend/Cakra.Web/src/components/base/`)**:
   - Create `BaseCard.vue` supporting variant, padding, and rounded options.
   - Create `BaseBadge.vue` supporting semantic variants, styles, and animated pulsing dots.
   - Create `PageHeader.vue` supporting title, subtitle, screen ID, telemetry beacon, and slots.
   - Create `StatusStrip.vue` supporting inline awareness metric items with vertical dividers.
   - Create `BaseAvatar.vue` supporting user initials and online/busy/paused/offline presence beacons.
   - Create `SlideOverDrawer.vue` supporting flyout contextual inspection with backdrop blur and ESC dismissal.
3. **Application Shell & Layout Modernization (`src/frontend/Cakra.Web/src/App.vue`)**:
   - Inspect `route.meta.containerWidth` to apply `cakra-shell-narrow` (1024px) or `cakra-shell-wide` (1440px).
   - Update `src/frontend/Cakra.Web/src/router/index.ts` to set `containerWidth: 'narrow'` on `/operations/wip`.
   - Modernize topbar styling and elevation.
4. **Screen Modernization Pilot (`src/frontend/Cakra.Web/src/views/WorkInProgressView.vue`)**:
   - Re-implement `SCR-REQ-006` using the base primitives to faithfully match `wip_operational_board.html`.
   - Preserve all existing automated test selectors (`data-testid`, `data-screen-id="SCR-REQ-006"`).
5. **Full-Stack Regression & Build Verification**:
   - Execute frontend typecheck and production build (`npm run build`).
   - Execute backend unit and integration test suite (`dotnet test cakra\Cakra.sln`).

---

# 3. Dependencies

- Node.js & npm (Vue 3 / Vite / TypeScript in `src/frontend/Cakra.Web`)
- .NET 8 SDK & ASP.NET Core (`Cakra.sln`) for regression validation

For slice dependencies:
- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires the referenced slice to have implementation status `IMPLEMENTED`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Design Token & Global CSS Modernization | IMPLEMENTED | GO | 2/2 |
| P2 - Base Component Primitive Layer | IMPLEMENTED | GO | 3/3 |
| P3 - Shell & Layout Modernization | IMPLEMENTED | GO | 1/1 |
| P4 - Pilot Screen & Verification | IMPLEMENTED | GO | 2/2 |

---

# 5. Phases

## P1 - Design Token & Global CSS Modernization

Implementation Status: IMPLEMENTED
Review Status: GO

### P1-S01

Title: Modernize Typography, Surfaces, and Palette Design Tokens in main.css

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Update `src/frontend/Cakra.Web/src/assets/main.css` to establish the modernized design token baseline in accordance with TD-001 in `CR-022-ARCHITECTURE.md`:
1. Update `:root` variables to define the neutral slate palette (`--cakra-slate-50` through `--cakra-slate-900`).
2. Update `--cakra-text-main` to `#0f172a` and `--cakra-text-muted` to `#475569`.
3. Update `--cakra-bg-canvas` to clean slate `#f8fafc` and `--cakra-bg-surface` to `#ffffff`.
4. Update `html, body` styling: set base font size to `14px` (`--bs-body-font-size: 0.875rem`), line-height to `1.5`, background to `var(--cakra-bg-canvas)`, and text color to `var(--cakra-text-main)`.
5. Restore heading hierarchy:
   - `h1`: `1.5rem` (21px), `font-weight: 700`, `line-height: 1.3`
   - `h2`: `1.25rem` (17.5px), `font-weight: 600`, `line-height: 1.35`
   - `h3`: `1.1rem` (15.4px), `font-weight: 600`, `line-height: 1.4`
   - `h4`: `1.0rem` (14px), `font-weight: 600`, `line-height: 1.4`
   - `h5`: `0.875rem`, `font-weight: 600`, `line-height: 1.4`
   - `h6`: `0.75rem`, `font-weight: 600`, `line-height: 1.4`
6. Preserve all existing CSS variable names and semantic aliases (`--cakra-navy`, `--cakra-cyan`, `--cakra-ice`, `--cakra-cream`, etc.) to guarantee backwards compatibility with legacy views.

Depends On: None

Repository: cakra

Completion Criteria:
- `src/frontend/Cakra.Web/src/assets/main.css` contains modernized slate palette tokens and 14px root typography scale.
- Headings render with distinct hierarchy over body text.
- `npm run build` in `src/frontend/Cakra.Web` succeeds without errors.

Notes:
- Consumes TD-001 and TD-004 in `CR-022-ARCHITECTURE.md`.
- Implementation: Modernized `:root` design tokens with slate neutral palette (`--cakra-slate-50` through `--cakra-slate-900`), updated `--cakra-text-main` (`#0f172a`), `--cakra-text-muted` (`#475569`), `--cakra-bg-canvas` (`#f8fafc`), and `--cakra-bg-surface` (`#ffffff`). Updated `html, body` styling to 14px root typography scale, 1.5 line-height, and restored heading scale `h1` through `h6`. Preserved all legacy palette tokens and semantic aliases. Verified successful production build with `npm run build`.

Changed Files:
- `src/frontend/Cakra.Web/src/assets/main.css`

---

### P1-S02

Title: Modernize Borders, Radii, Shadows, and Decouple Card Chrome in main.css

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Update `src/frontend/Cakra.Web/src/assets/main.css` to modernize surface chrome, borders, radii, and shadows in accordance with TD-001 in `CR-022-ARCHITECTURE.md`:
1. Soften global border color `--cakra-border` from heavy `#dbe3eb` to subtle slate `#e2e8f0` (`--bs-border-color: var(--cakra-border)`).
2. Expand border-radius tokens:
   - `--cakra-radius-xs: 4px;`
   - `--cakra-radius-sm: 6px;`
   - `--cakra-radius-md: 8px;`
   - `--cakra-radius-lg: 12px;`
   - `--cakra-radius-xl: 16px;`
   - `--cakra-radius-full: 9999px;`
3. Modernize elevation shadow tokens to natural, diffuse black-alpha shadows:
   - `--cakra-shadow-xs: 0 1px 2px 0 rgb(0 0 0 / 0.04);`
   - `--cakra-shadow-sm: 0 1px 3px 0 rgb(0 0 0 / 0.08), 0 1px 2px -1px rgb(0 0 0 / 0.04);`
   - `--cakra-shadow-md: 0 4px 6px -1px rgb(0 0 0 / 0.08), 0 2px 4px -2px rgb(0 0 0 / 0.04);`
   - `--cakra-shadow-lg: 0 10px 15px -3px rgb(0 0 0 / 0.08), 0 4px 6px -4px rgb(0 0 0 / 0.04);`
4. Decouple `.card` styling: update default `.card` to use subtle border `#e2e8f0` and `--cakra-radius-lg` (12px); remove forced gray header background fill from `.card-header` (default to transparent with subtle bottom border), enabling borderless and flat card variants.
5. Add utility classes for modern radii (`.rounded-lg`, `.rounded-xl`), soft borders (`.border-subtle`), and shadows (`.shadow-subtle`).

Depends On: P1-S01

Repository: cakra

Completion Criteria:
- `main.css` contains updated border, radius, shadow tokens, and decoupled `.card` rules.
- Card chrome is softened without gray header boxiness.
- `npm run build` succeeds without errors.

Notes:
- Consumes TD-001 and TD-004 in `CR-022-ARCHITECTURE.md`.
- Implementation: Softened global border token `--cakra-border` from `#dbe3eb` to `#e2e8f0`, updated `--bs-border-color: var(--cakra-border)` and `--bs-border-radius: var(--cakra-radius-sm)`. Expanded radius tokens (`--cakra-radius-xs` 4px to `--cakra-radius-full` 9999px). Modernized elevation shadows to natural diffuse black-alpha shadows (`--cakra-shadow-xs` through `--cakra-shadow-xl`). Decoupled `.card` styling to use `var(--cakra-border)` and `var(--cakra-radius-lg)`, set `.card-header` and `.card-footer` background to transparent, and added card variants (`.card-flat`, `.card-borderless`, `.card-ghost`, `.card-elevated`). Added utility classes for radii (`.rounded-*`), borders (`.border-*`), and diffuse shadows (`.shadow-*`). Verified successful frontend production build via `npm run build`.

Changed Files:
- `src/frontend/Cakra.Web/src/assets/main.css`

---

## P2 - Base Component Primitive Layer

Implementation Status: IMPLEMENTED
Review Status: GO

### P2-S03

Title: Implement BaseCard.vue and BaseBadge.vue Primitives

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Create the `BaseCard.vue` and `BaseBadge.vue` primitive components in `src/frontend/Cakra.Web/src/components/base/` in accordance with TD-002 in `CR-022-ARCHITECTURE.md`:
1. Create `BaseCard.vue`:
   - Props:
     - `variant`: `'bordered' | 'flat' | 'ghost' | 'elevated'` (default: `'bordered'`)
     - `padding`: `'none' | 'sm' | 'md' | 'lg'` (default: `'md'`)
     - `rounded`: `'sm' | 'md' | 'lg' | 'xl'` (default: `'lg'`)
     - `hoverEffect`: `boolean` (default: `false`)
   - Template: Render container `<div :class="computedClasses">` with default slot.
   - Styles: Scoped styles consuming `--cakra-border`, `--cakra-bg-surface`, `--cakra-bg-subtle`, `--cakra-shadow-*`, and `--cakra-radius-*`.
2. Create `BaseBadge.vue`:
   - Props:
     - `variant`: `'neutral' | 'success' | 'warning' | 'danger' | 'brand' | 'info'` (default: `'neutral'`)
     - `styleType`: `'subtle' | 'solid' | 'outline'` (default: `'subtle'`)
     - `size`: `'sm' | 'md'` (default: `'md'`)
     - `pulse`: `boolean` (default: `false`)
   - Template: Render capsule `<span :class="computedClasses">` with optional `<span class="badge-pulse-dot">` and slot.
   - Styles: Scoped subtle tint styles (10% background opacity) and animated pulsing dot keyframes (`animation: live-pulse 2s infinite`).

Depends On: P1-S02

Repository: cakra

Completion Criteria:
- Files `src/frontend/Cakra.Web/src/components/base/BaseCard.vue` and `src/frontend/Cakra.Web/src/components/base/BaseBadge.vue` are created with full TypeScript prop interfaces.
- Components compile cleanly with `npm run build`.

Notes:
- Consumes TD-002 in `CR-022-ARCHITECTURE.md`.
- Implementation: Created `BaseCard.vue` and `BaseBadge.vue` primitives in `src/frontend/Cakra.Web/src/components/base/` with strict TypeScript prop contracts. `BaseCard.vue` supports `variant` (`bordered`, `flat`, `ghost`, `elevated`), `padding` (`none`, `sm`, `md`, `lg`), `rounded` (`sm`, `md`, `lg`, `xl`), and `hoverEffect` consuming design tokens `--cakra-border`, `--cakra-bg-surface`, `--cakra-bg-subtle`, `--cakra-shadow-*`, and `--cakra-radius-*`. `BaseBadge.vue` supports `variant` (`neutral`, `success`, `warning`, `danger`, `brand`, `info`), `styleType` (`subtle`, `solid`, `outline`), `size` (`sm`, `md`), and `pulse` with animated dot keyframes (`live-pulse 2s infinite`). Verified successful production build via `npm run build` (`vue-tsc --noEmit && vite build`).

Changed Files:
- `src/frontend/Cakra.Web/src/components/base/BaseCard.vue`
- `src/frontend/Cakra.Web/src/components/base/BaseBadge.vue`

---

### P2-S04

Title: Implement PageHeader.vue and StatusStrip.vue Primitives

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Create the `PageHeader.vue` and `StatusStrip.vue` primitive components in `src/frontend/Cakra.Web/src/components/base/` in accordance with TD-002 in `CR-022-ARCHITECTURE.md`:
1. Create `PageHeader.vue`:
   - Props:
     - `title`: `string` (required)
     - `subtitle`: `string` (optional)
     - `screenId`: `string` (optional)
     - `live`: `boolean` (optional, default: `false`)
   - Slots: `#actions` (right-aligned action controls), `#stats` (compact telemetry/status strip).
   - Template: Renders title row with optional live telemetry badge (`BaseBadge variant="success" pulse`), screen ID tag (`BaseBadge variant="neutral"`), subtitle, and responsive flex container for slots.
2. Create `StatusStrip.vue`:
   - Props:
     - `items`: `Array<{ label: string, value: string | number, color?: 'primary' | 'success' | 'warning' | 'danger' | 'secondary' }>`
   - Template: Renders a compact horizontal status pill container with subtle vertical dividers (`|`) separating metric items.
   - Styles: Scoped styles with subtle background, hairline border `#e2e8f0`, and high-contrast bold values.

Depends On: P2-S03

Repository: cakra

Completion Criteria:
- Files `src/frontend/Cakra.Web/src/components/base/PageHeader.vue` and `src/frontend/Cakra.Web/src/components/base/StatusStrip.vue` are created with full TypeScript prop interfaces.
- Components compile cleanly with `npm run build`.

Notes:
- Consumes TD-002 in `CR-022-ARCHITECTURE.md`.
- Implementation: Created `PageHeader.vue` and `StatusStrip.vue` primitive components in `src/frontend/Cakra.Web/src/components/base/` with strict TypeScript prop contracts. `PageHeader.vue` implements required `title`, optional `subtitle`, `screenId`, and `live` props, embedding `BaseBadge` for the live pulsing badge (`LIVE`) and monospace screen ID tag, with responsive flex alignment and dedicated slots for `#stats` and `#actions`. `StatusStrip.vue` implements `items` prop (`Array<{ label, value, color?, dot?, testId?, dataTestId? }>`) rendering a compact horizontal status pill container with subtle vertical dividers (`|`), high-contrast bold tabular values, semantic color mappings (`primary`, `success`, `warning`, `danger`, `secondary`), hairline border `var(--cakra-border)`, and subtle shadow `var(--cakra-shadow-xs)`. Verified cleanly with `npm run build` (`vue-tsc --noEmit && vite build`).

Changed Files:
- `src/frontend/Cakra.Web/src/components/base/PageHeader.vue`
- `src/frontend/Cakra.Web/src/components/base/StatusStrip.vue`

---

### P2-S05

Title: Implement BaseAvatar.vue and SlideOverDrawer.vue Primitives

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Create the `BaseAvatar.vue` and `SlideOverDrawer.vue` primitive components in `src/frontend/Cakra.Web/src/components/base/` in accordance with TD-002 in `CR-022-ARCHITECTURE.md`:
1. Create `BaseAvatar.vue`:
   - Props:
     - `name`: `string` (required)
     - `size`: `'sm' | 'md' | 'lg'` (default: `'md'`)
     - `status`: `'online' | 'busy' | 'paused' | 'offline'` (optional)
   - Logic: Computes user initials from `name` (e.g. "Admin Cakra" -> "AC").
   - Template: Renders avatar circle with initials and positioned presence beacon dot.
   - Styles: Scoped clean background, border, and status dot colors.
2. Create `SlideOverDrawer.vue`:
   - Props:
     - `modelValue`: `boolean` (v-model open state)
     - `title`: `string` (required)
     - `subtitle`: `string` (optional)
     - `width`: `'sm' | 'md' | 'lg'` (default: `'md'`)
   - Emits: `'update:modelValue'`
   - Slots: `#header`, `#default`, `#footer`
   - Features: Viewport backdrop with blur, smooth slide-in CSS transition from right, ESC key dismissal event listener, and body scroll lock.

Depends On: P2-S04

Repository: cakra

Completion Criteria:
- Files `src/frontend/Cakra.Web/src/components/base/BaseAvatar.vue` and `src/frontend/Cakra.Web/src/components/base/SlideOverDrawer.vue` are created with full TypeScript prop interfaces.
- Components compile cleanly with `npm run build`.

Notes:
- Consumes TD-002 in `CR-022-ARCHITECTURE.md`.
- Implementation: Created `BaseAvatar.vue` and `SlideOverDrawer.vue` primitive components in `src/frontend/Cakra.Web/src/components/base/` with strict TypeScript prop contracts. `BaseAvatar.vue` implements required `name`, configurable `size` (`sm`: 28px, `md`: 36px, `lg`: 44px), and optional presence beacon `status` (`online`, `busy`, `paused`, `offline`). Initials computation accurately handles single names, multi-word names, and whitespace. Presence beacon dot is positioned at bottom-right with semantic color mapping (green for online, amber for busy/paused, slate for offline) and surface border isolation. `SlideOverDrawer.vue` implements `modelValue`, `title`, optional `subtitle`, and `width` (`sm`: 380px, `md`: 480px, `lg`: 640px) with responsive clamping, custom slots for `#header`, `#default`, and `#footer` (with slot scope `:close`), viewport backdrop with backdrop blur, `<Transition>` slide animation from right, window ESC key dismissal listener, and body scroll locking. Verified with `npm run build` (`vue-tsc --noEmit && vite build`) and backend tests (`dotnet test`).

Changed Files:
- `src/frontend/Cakra.Web/src/components/base/BaseAvatar.vue`
- `src/frontend/Cakra.Web/src/components/base/SlideOverDrawer.vue`

---

## P3 - Shell & Layout Modernization

Implementation Status: IMPLEMENTED
Review Status: GO

### P3-S06

Title: Shell Container Width Extension and Layout Refresh in App.vue

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Update `src/frontend/Cakra.Web/src/App.vue` and `src/frontend/Cakra.Web/src/router/index.ts` in accordance with TD-003 in `CR-022-ARCHITECTURE.md`:
1. In `src/assets/main.css`:
   - Define `.cakra-shell-container.cakra-shell-narrow { max-width: 1024px; }` for focused operational awareness feeds.
   - Define `.cakra-shell-container.cakra-shell-wide { max-width: 1440px; }` for dense multi-column analytics tables.
2. In `src/frontend/Cakra.Web/src/App.vue`:
   - Compute `containerClass` based on `route.meta.containerWidth` (`'narrow'` -> `cakra-shell-narrow`, default -> `cakra-shell-wide`).
   - Bind `:class="containerClass"` to `.cakra-shell-container`.
   - Soften topbar bottom border to `#e2e8f0` and adjust header elevation.
3. In `src/frontend/Cakra.Web/src/router/index.ts`:
   - Update route `/operations/wip` metadata to include `containerWidth: 'narrow'`.

Depends On: P2-S05

Repository: cakra

Completion Criteria:
- `App.vue` dynamically switches between narrow (1024px) and wide (1440px) shell containers based on route metadata.
- `/operations/wip` specifies `containerWidth: 'narrow'`.
- `npm run build` succeeds without errors.

Notes:
- Consumes TD-003 in `CR-022-ARCHITECTURE.md`.
- Implementation: Defined `.cakra-shell-container.cakra-shell-narrow` (`max-width: 1024px`) and `.cakra-shell-wide` (`max-width: 1440px`) modifier classes in `src/frontend/Cakra.Web/src/assets/main.css`. Softened topbar header styling with diffuse elevation shadow (`box-shadow: var(--cakra-shadow-xs)`) and modern text token (`color: var(--cakra-text-main)`). In `src/frontend/Cakra.Web/src/App.vue`, introduced computed `containerClass` resolving `route.meta.containerWidth` (`'narrow'` -> `cakra-shell-narrow`, default -> `cakra-shell-wide`) and bound it dynamically via `:class="containerClass"` on `.cakra-shell-container`. In `src/frontend/Cakra.Web/src/router/index.ts`, added `containerWidth: 'narrow'` to route `/operations/wip` (`SCR-REQ-006`) and augmented `vue-router` `RouteMeta` interface for full TypeScript type safety. Verified cleanly with frontend production build (`npm run build`) and backend test suite (`dotnet test Cakra.sln` with 612 passing tests).

Changed Files:
- `src/frontend/Cakra.Web/src/assets/main.css`
- `src/frontend/Cakra.Web/src/App.vue`
- `src/frontend/Cakra.Web/src/router/index.ts`

---

## P4 - Pilot Screen & Verification

Implementation Status: IMPLEMENTED
Review Status: GO

### P4-S07

Title: Re-implement WorkInProgressView.vue Using Base Primitives

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Re-implement `src/frontend/Cakra.Web/src/views/WorkInProgressView.vue` (`SCR-REQ-006`) strictly using the newly created foundation primitives in accordance with TD-005 and TD-006 in `CR-022-ARCHITECTURE.md`:
1. Replace raw screen header with `<PageHeader title="Work in Progress" screen-id="SCR-REQ-006" live>`:
   - `#actions` slot: Refresh button with loading spinner.
   - `#stats` slot: `<StatusStrip :items="kpiItems" />` (`working`, `tasks active`, `paused`).
2. Replace person cards with clean continuous feed rows using `<BaseCard variant="bordered" rounded="xl" padding="lg">`:
   - Person header: `<BaseAvatar :name="person.personName" :status="person.inProgressTask ? 'online' : 'paused'" />`, person name, email, and task count badge.
3. Current Activity section:
   - Render in-progress task in an interactive row `<BaseCard variant="flat" rounded="md" padding="md">` with bold title, customer code badge, and prominent elapsed time counter (`Xh Ym`).
   - If idle, render clean quiet placeholder.
4. Paused Work section:
   - Render quiet secondary section with count, subtle zero-state (`No paused work`), or clean horizontal rows with forward chevron links.
5. Contextual Inspection:
   - Clicking a task card opens `<SlideOverDrawer>` showing full operational metadata, customer details, elapsed duration, and link to `/requests/{id}`.
6. Test Selectors Preservation:
   - Retain 100% of existing `data-testid` attributes (`data-testid="wip-screen"`, `data-testid="wip-kpis"`, `data-testid="kpi-active-people"`, `data-testid="kpi-in-progress-tasks"`, `data-testid="kpi-paused-tasks"`, `data-testid="refresh-wip-button"`, `data-testid="person-card"`, `data-testid="in-progress-card"`, `data-testid="in-progress-time-badge"`, `data-testid="paused-task-card"`, `data-testid="paused-time-badge"`, `data-testid="task-title-link"`, `data-testid="task-id-link"`).

Depends On: P3-S06

Repository: cakra

Completion Criteria:
- `WorkInProgressView.vue` faithfully reproduces the layout and elegance of `wip_operational_board.html` without boxiness or visual degradation.
- All `data-testid` and `data-screen-id` selectors remain intact.
- `npm run build` succeeds without errors.

Notes:
- Consumes TD-005 and TD-006 in `CR-022-ARCHITECTURE.md`.
- Implementation: Re-implemented `src/frontend/Cakra.Web/src/views/WorkInProgressView.vue` (`SCR-REQ-006`) strictly using the newly created foundation primitives (`PageHeader.vue`, `StatusStrip.vue`, `BaseCard.vue`, `BaseBadge.vue`, `BaseAvatar.vue`, and `SlideOverDrawer.vue`). Standardized page header with live telemetry and StatusStrip ribbon (`data-testid="wip-kpis"`, `kpi-active-people`, `kpi-in-progress-tasks`, `kpi-paused-tasks`). Formatted continuous person feed using `BaseCard variant="bordered" rounded="xl" padding="lg"` (`data-testid="person-card"`), `BaseAvatar` with presence status beacons, and `BaseBadge` tags. Implemented active task spotlight using `BaseCard variant="flat" rounded="md" padding="md"` (`data-testid="in-progress-card"`), prominent elapsed time badge (`data-testid="in-progress-time-badge"`), and clean idle placeholder (`data-testid="idle-placeholder"`). Implemented paused tasks using `BaseCard variant="flat" rounded="md" padding="sm"` (`data-testid="paused-task-card"`), elapsed pause time badge (`data-testid="paused-time-badge"`), and quiet zero-state (`data-testid="empty-paused-tasks"`). Integrated `SlideOverDrawer` for contextual inspection displaying full task metadata, timing, and router link to `/requests/{id}`. Integrated lightweight filter pills and search bar matching `wip_operational_board.html` prototype. Preserved 100% of automated test selectors. Verified production build with `npm run build` (`vue-tsc --noEmit && vite build`) and backend regression suite (`dotnet test Cakra.sln` passing all 612 tests).

Changed Files:
- `src/frontend/Cakra.Web/src/views/WorkInProgressView.vue`

---

### P4-S08

Title: Full-Stack Regression & Build Verification

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Execute complete automated test suites and production build checks across the entire repository to verify that the UI foundation modernization caused zero regressions:
1. Run frontend typecheck and production build in `src/frontend/Cakra.Web`:
   - Command: `npm run build` (`vue-tsc --noEmit && vite build`).
   - Verify exit code 0.
2. Run backend test suite:
   - Command: `dotnet test cakra\Cakra.sln`.
   - Verify all 612 backend unit and integration tests pass with 0 failures.
3. Verify that all automated test selectors across the application remain intact.

Depends On: P4-S07

Repository: cakra

Completion Criteria:
- `npm run build` exits with code 0.
- `dotnet test cakra\Cakra.sln` passes with 0 failures.

Notes:
- Final verification slice for CR-022.
- Implementation: Executed full automated regression and build checks across the entire repository. Frontend typecheck and production build (`npm run build` executing `vue-tsc --noEmit && vite build`) succeeded with exit code 0, cleanly compiling all Vue components, design tokens, and TypeScript contracts into dist assets. Full backend automated test suite (`dotnet test Cakra.sln`) executed with exit code 0: 435 unit tests passed and 177 integration tests passed, totaling 612 tests passed with 0 failures and 0 skipped. Verified 100% preservation of all existing automated test selectors (`data-screen-id`, `data-testid`) across the modernized views and components. Zero regressions detected.

Changed Files:
- None (verification slice)

---

# 6. Change Log

- 2026-10-07: Initial implementation plan for CR-022 created by Architect across 4 phases (8 slices). Execution Approval: PENDING.
- 2026-10-07: Execution Approval granted by Architect (PENDING -> APPROVED). Plan released for autonomous execution.
- 2026-10-07: All slices (P1-S01 through P4-S08) implemented and reviewed with GO. Plan status marked as COMPLETED.
