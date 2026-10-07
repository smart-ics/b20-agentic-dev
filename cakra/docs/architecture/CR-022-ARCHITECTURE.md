---
Title: UI Foundation Modernization and Component System Architecture (CR-022)
Code: CR-022
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-07
---

# 1. Overview

This architecture defines the technical realization for Change Request `CR-022`: UI Foundation Modernization and Component System Architecture for the Cakra web application (`src/frontend/Cakra.Web`).

It consumes and realizes the approved feasibility decisions from [CR-022-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-022-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`), establishing:

1. **Design Token & Global CSS Modernization** in `src/assets/main.css`: Replacing the "13px Gravitational Field", compressed headings, and muddy navy cast with a modern 14px root typography scale, neutral slate palette (`#0f172a`, `#475569`, `#f8fafc`, `#e2e8f0`), expanded border-radii (6px–16px), natural diffuse elevation shadows, and decoupled `.card` rules.
2. **Base Component Primitive Layer** in `src/components/base/`: Six encapsulated, type-safe Vue 3 primitives (`BaseCard.vue`, `BaseBadge.vue`, `PageHeader.vue`, `StatusStrip.vue`, `BaseAvatar.vue`, and `SlideOverDrawer.vue`) eliminating copy-paste raw Bootstrap markup and ad-hoc inline styles.
3. **Shell & Layout Modernization** in `src/App.vue`: Dynamic route-aware container widths (`narrow` 1024px for focused operational feeds vs `wide` 1440px for dense analytics tables) and harmonized topbar elevation.
4. **Screen Modernization Pilot** in `src/views/WorkInProgressView.vue` (`SCR-REQ-006`): Proving 100% faithful reproduction of the approved operational awareness board prototype (`wip_operational_board.html`) using the new foundation primitives without visual degradation, while preserving all automated test selectors.

---

# 2. Architectural Basis

## Business Context

- ISSUE: [CR-022-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-022-ISSUE.md)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md) (Section 19.4 Frontend Stack)
- APPROVED PROTOTYPE: [wip_operational_board.html](file:///C:/Users/drury/.gemini/antigravity/brain/321a8c71-5ea0-4c4d-aa42-f910ab295492/wip_operational_board.html)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-022-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-022-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)
- UI INVESTIGATION REPORT: [ui_foundation_investigation_report.md](file:///C:/Users/drury/.gemini/antigravity/brain/79c9747e-e2b7-4a51-92a2-294539e6d6c5/ui_foundation_investigation_report.md)

This architecture consumes and realizes the approved decisions:
- `GAP-001` & `GAP-006`: 14px base font, 1.5 line-height, restored heading scale, neutral slate surfaces and text tokens.
- `GAP-002`: Softened border `#e2e8f0`, expanded radius tokens (6px, 8px, 12px, 16px), natural diffuse shadows, decoupled `.card-header` background.
- `GAP-003` & `GAP-005`: Six native Vue 3 base component primitives in `src/components/base/`.
- `GAP-004`: Flexible shell container max-width (`narrow` 1024px vs `wide` 1440px) via `route.meta.containerWidth` in `App.vue`.
- Closed decisions `OQ-001` through `OQ-004`: 14px compact engineering standard, backwards-compatible token aliases, native Vue 3 implementation with zero external library bloat, and route metadata conventions.

---

# 3. Scope

## Included

1. **Design Token & Global CSS Modernization (`src/frontend/Cakra.Web/src/assets/main.css`)**:
   - Modernize `:root` tokens: typography, slate neutrals, ambient canvas, borders, radii, and natural elevation shadows.
   - Update `html, body`: set `font-size: 14px`, `line-height: 1.5`, background to `#f8fafc`, text color to `#0f172a`.
   - Restore true heading scale: `h1` at 1.5rem (21px), `h2` at 1.25rem (17.5px), `h3` at 1.1rem (15.4px), `h4` at 1.0rem (14px).
   - Modernize form control and table base variables without breaking dense data displays.
   - Decouple `.card` and `.card-header` rules to allow transparent headers and borderless card variants.
   - Retain backwards-compatible CSS variable aliases for legacy screens.
2. **Base Component Primitive Layer (`src/frontend/Cakra.Web/src/components/base/`)**:
   - `BaseCard.vue`: Variant-aware container (`bordered`, `flat`, `ghost`, `elevated`), padding sizes (`none`, `sm`, `md`, `lg`), border-radii (`md`, `lg`, `xl`).
   - `BaseBadge.vue`: Semantic status capsule (`neutral`, `success`, `warning`, `danger`, `brand`), style (`subtle`, `solid`, `outline`), optional animated live pulsing beacon.
   - `PageHeader.vue`: Standardized screen header with eyebrow/crumb, primary heading, subtitle, live telemetry badge, action and stat slots.
   - `StatusStrip.vue`: High-density single-line operational KPI ribbon with vertical dividers and value emphasis.
   - `BaseAvatar.vue`: User initials avatar with online, busy, paused, and offline presence beacon indicators.
   - `SlideOverDrawer.vue`: Fixed slide-over contextual flyout panel with backdrop blur, smooth slide transition, ESC dismissal, and action footer.
3. **Application Shell & Layout Modernization (`src/frontend/Cakra.Web/src/App.vue`)**:
   - Dynamic max-width handling via `route.meta.containerWidth`: `'narrow'` (1024px for focused awareness feeds like WIP and Feed) vs `'wide'` (1440px for data-heavy analytics and tables).
   - Modernized topbar styling, subtle border `#e2e8f0`, and clean elevation.
4. **Screen Modernization Pilot (`src/frontend/Cakra.Web/src/views/WorkInProgressView.vue`)**:
   - Re-implementation of `SCR-REQ-006` strictly using `<PageHeader>`, `<StatusStrip>`, `<BaseCard variant="ghost">`, `<BaseAvatar>`, and `<BaseBadge>`.
   - 100% faithful reproduction of `wip_operational_board.html` without boxification or visual degradation.
   - Strict preservation of all automated test selectors (`data-testid`, `data-screen-id="SCR-REQ-006"`).

## Excluded

- Backend API or database changes (no SQL migrations, no C# code changes; pure frontend architecture).
- Wholesale rewrites of other existing screens during this CR (screens will adopt base primitives progressively in future CRs).
- External third-party UI framework dependencies (no PrimeVue, no Vuetify, no Tailwind runtime compiler; native Vue 3 + CSS tokens).

---

# 4. Technical Decisions

## TD-001: Modernized Design Token Architecture in `main.css`

The global CSS architecture is updated in `src/assets/main.css` to establish a modern, scalable design token foundation while preserving backwards compatibility:

```css
:root {
  /* Modern Neutral Slate Palette */
  --cakra-slate-50: #f8fafc;
  --cakra-slate-100: #f1f5f9;
  --cakra-slate-200: #e2e8f0;
  --cakra-slate-300: #cbd5e1;
  --cakra-slate-400: #94a3b8;
  --cakra-slate-500: #64748b;
  --cakra-slate-600: #475569;
  --cakra-slate-700: #334155;
  --cakra-slate-800: #1e293b;
  --cakra-slate-900: #0f172a;

  /* Surfaces & Ambient Neutrals */
  --cakra-bg-canvas: #f8fafc;
  --cakra-bg-surface: #ffffff;
  --cakra-bg-subtle: #f1f5f9;
  --cakra-border: #e2e8f0;
  --cakra-border-subtle: #f1f5f9;
  --cakra-border-strong: #cbd5e1;
  --cakra-text-main: #0f172a;
  --cakra-text-muted: #475569;
  --cakra-text-subtle: #94a3b8;

  /* Semantic Brand Accents (Intentional Actions & Telemetry) */
  --cakra-primary: #364f6b;
  --cakra-primary-rgb: 54, 79, 107;
  --cakra-secondary: #3fc1c9;
  --cakra-secondary-rgb: 63, 193, 201;
  --cakra-accent: #fc5185;
  --cakra-accent-rgb: 252, 81, 133;
  --cakra-emerald: #10b981;
  --cakra-amber: #f59e0b;
  --cakra-rose: #f43f5e;

  /* Modern Diffuse Elevation Shadows */
  --cakra-shadow-xs: 0 1px 2px 0 rgb(0 0 0 / 0.04);
  --cakra-shadow-sm: 0 1px 3px 0 rgb(0 0 0 / 0.08), 0 1px 2px -1px rgb(0 0 0 / 0.04);
  --cakra-shadow-md: 0 4px 6px -1px rgb(0 0 0 / 0.08), 0 2px 4px -2px rgb(0 0 0 / 0.04);
  --cakra-shadow-lg: 0 10px 15px -3px rgb(0 0 0 / 0.08), 0 4px 6px -4px rgb(0 0 0 / 0.04);
  --cakra-shadow-xl: 0 20px 25px -5px rgb(0 0 0 / 0.1), 0 8px 10px -6px rgb(0 0 0 / 0.05);

  /* Modern Radius Scale */
  --cakra-radius-xs: 4px;
  --cakra-radius-sm: 6px;
  --cakra-radius-md: 8px;
  --cakra-radius-lg: 12px;
  --cakra-radius-xl: 16px;
  --cakra-radius-full: 9999px;

  /* Bootstrap 5 Theme Overrides */
  --bs-body-font-size: 0.875rem; /* 14px base */
  --bs-body-line-height: 1.5;
  --bs-body-bg: var(--cakra-bg-canvas);
  --bs-body-color: var(--cakra-text-main);
  --bs-border-color: var(--cakra-border);
  --bs-border-radius: var(--cakra-radius-sm);
}

/* Root Typography Baseline */
html, body {
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Inter", "Helvetica Neue", Arial, sans-serif;
  font-size: 14px;
  line-height: 1.5;
  color: var(--cakra-text-main);
  background-color: var(--cakra-bg-canvas);
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
}

/* Restored Heading Hierarchy */
h1, .h1 { font-size: 1.5rem; font-weight: 700; line-height: 1.3; margin-bottom: 0.35rem; color: var(--cakra-text-main); }
h2, .h2 { font-size: 1.25rem; font-weight: 600; line-height: 1.35; margin-bottom: 0.25rem; color: var(--cakra-text-main); }
h3, .h3 { font-size: 1.1rem; font-weight: 600; line-height: 1.4; margin-bottom: 0.2rem; color: var(--cakra-text-main); }
h4, .h4 { font-size: 1.0rem; font-weight: 600; line-height: 1.4; margin-bottom: 0.2rem; color: var(--cakra-text-main); }
h5, .h5 { font-size: 0.875rem; font-weight: 600; line-height: 1.4; margin-bottom: 0.15rem; color: var(--cakra-text-main); }
h6, .h6 { font-size: 0.75rem; font-weight: 600; line-height: 1.4; margin-bottom: 0.15rem; color: var(--cakra-text-muted); }
```

## TD-002: Base Component Primitive Layer API Contracts

Six reusable primitives are implemented in `src/components/base/`:

### 1. `BaseCard.vue`
```typescript
interface BaseCardProps {
  variant?: 'bordered' | 'flat' | 'ghost' | 'elevated' // default: 'bordered'
  padding?: 'none' | 'sm' | 'md' | 'lg'                // default: 'md'
  rounded?: 'sm' | 'md' | 'lg' | 'xl'                  // default: 'lg'
  hoverEffect?: boolean                                 // default: false
}
```
- **Styling**:
  - `bordered`: `background: #ffffff; border: 1px solid var(--cakra-border); box-shadow: var(--cakra-shadow-xs);`
  - `flat`: `background: var(--cakra-bg-subtle); border: none; box-shadow: none;`
  - `ghost`: `background: transparent; border: none; box-shadow: none;`
  - `elevated`: `background: #ffffff; border: 1px solid var(--cakra-border-subtle); box-shadow: var(--cakra-shadow-md);`

### 2. `BaseBadge.vue`
```typescript
interface BaseBadgeProps {
  variant?: 'neutral' | 'success' | 'warning' | 'danger' | 'brand' | 'info' // default: 'neutral'
  styleType?: 'subtle' | 'solid' | 'outline'                                // default: 'subtle'
  size?: 'sm' | 'md'                                                        // default: 'md'
  pulse?: boolean                                                           // default: false (animated live dot)
}
```
- **Styling**: Subtle styles use 10% opacity colored backgrounds with matching text, eliminating dark harsh badges. Pulse renders a smooth green pulsing beacon (`animation: live-pulse 2s infinite`).

### 3. `PageHeader.vue`
```typescript
interface PageHeaderProps {
  title: string
  subtitle?: string
  screenId?: string
  live?: boolean
}
```
- Slots: `#actions` (for buttons like Refresh, Create), `#stats` (for compact status strips).
- Renders clean top heading, live telemetry badge, screen ID tag, and responsive action placement.

### 4. `StatusStrip.vue`
```typescript
interface StatusStripItem {
  label: string
  value: string | number
  color?: 'primary' | 'success' | 'warning' | 'danger' | 'secondary'
}

interface StatusStripProps {
  items: StatusStripItem[]
}
```
- Renders an inline awareness strip (`3 working · 3 tasks active · 2 paused`) encapsulated in a pill container with subtle vertical dividers (`|`).

### 5. `BaseAvatar.vue`
```typescript
interface BaseAvatarProps {
  name: string
  size?: 'sm' | 'md' | 'lg'                        // sm: 28px, md: 36px, lg: 44px
  status?: 'online' | 'busy' | 'paused' | 'offline' // optional presence beacon
}
```
- Computes clean initials (e.g., "Admin Cakra" -> "AC"), displays smooth gradients or clean neutrals, and attaches positioned presence status beacons.

### 6. `SlideOverDrawer.vue`
```typescript
interface SlideOverDrawerProps {
  modelValue: boolean // v-model visibility
  title: string
  subtitle?: string
  width?: 'sm' | 'md' | 'lg' // sm: 380px, md: 440px, lg: 540px
}
```
- Emits: `'update:modelValue'`
- Slots: `#header`, `#default`, `#footer`
- Features: Fixed viewport backdrop with blur, smooth slide-in transition from right, ESC key listener, and body scroll lock.

## TD-003: Shell Layout Container Width Support (`App.vue`)

In `src/frontend/Cakra.Web/src/App.vue`:

```typescript
// Inspect route meta to determine container width
const containerClass = computed(() => {
  const width = route.meta.containerWidth
  if (width === 'narrow') return 'cakra-shell-narrow'
  return 'cakra-shell-wide'
})
```

In `src/assets/main.css`:
```css
.cakra-shell-container {
  width: 100%;
  margin-left: auto;
  margin-right: auto;
  padding-left: var(--cakra-container-padding);
  padding-right: var(--cakra-container-padding);
}

.cakra-shell-container.cakra-shell-narrow {
  max-width: 1024px; /* Focused editorial & operational feed (Slack/Linear) */
}

.cakra-shell-container.cakra-shell-wide {
  max-width: 1440px; /* Data-heavy tables, analytics, work packages */
}
```

In `src/frontend/Cakra.Web/src/router/index.ts`:
```typescript
{
  path: '/operations/wip',
  name: 'work-in-progress',
  component: () => import('@/views/WorkInProgressView.vue'),
  meta: {
    requiresAuth: true,
    screenId: 'SCR-REQ-006',
    containerWidth: 'narrow', // Enables focused 1024px layout
  },
}
```

## TD-004: Backwards Compatibility Strategy for Legacy Classes

To guarantee that existing views (`CustomerManagementView`, `RequestDetailView`, `FeedView`, `WorkPackageView`) do not experience breaking regressions:
1. Preserve all existing CSS variable names in `:root`:
   - `--cakra-navy`, `--cakra-cyan`, `--cakra-canvas`, `--cakra-pink` are retained as CSS custom properties.
   - Legacy aliases (`--cakra-ice`, `--cakra-cream`, `--cakra-terracotta`, `--cakra-sage`, etc.) are retained.
2. Standard utility classes (`.text-cakra-primary`, `.bg-cakra-navy`, `.fs-11`, `.fs-12`) are preserved.
3. The root font-size change from 13px to 14px naturally increases legibility across all screens while remaining within standard compact tolerances.

## TD-005: Pilot Implementation Architecture for `WorkInProgressView.vue`

`WorkInProgressView.vue` is refactored to consume the new foundation:
1. **Screen Header**: Uses `<PageHeader title="Work in Progress" screen-id="SCR-REQ-006" live>` with `#actions` containing `<button @click="loadWipOverview">` and `#stats` containing `<StatusStrip :items="kpiItems" />`.
2. **Feed Item**: Each person is wrapped in `<BaseCard variant="bordered" rounded="xl" padding="lg">`.
3. **Presence Header**: Displays `<BaseAvatar :name="person.personName" :status="person.inProgressTask ? 'online' : 'paused'" />` followed by person name, email, and task count badge.
4. **Current Activity Row**: Borderless interactive row (`BaseCard variant="flat" rounded="md" padding="md"`) with prominent bold title, customer code badge, and high-visibility elapsed time counter (`Xh Ym`).
5. **Paused Work Section**: Subtle secondary section with quiet zero-state (`No paused work`) or clean horizontal rows with forward chevron navigation.
6. **Contextual Inspection**: Clicking any task opens `<SlideOverDrawer>` showing full operational timing, customer details, and direct link to Request Detail (`/requests/{id}`).

## TD-006: Test Selector & Quality Boundary Preservation

All automated test identifiers and screen metadata attributes are preserved strictly:
- `data-screen-id="SCR-REQ-006"`
- `data-testid="wip-screen"`
- `data-testid="wip-kpis"`
- `data-testid="kpi-active-people"`
- `data-testid="kpi-in-progress-tasks"`
- `data-testid="kpi-paused-tasks"`
- `data-testid="refresh-wip-button"`
- `data-testid="person-card"`
- `data-testid="in-progress-card"`
- `data-testid="in-progress-time-badge"`
- `data-testid="paused-task-card"`
- `data-testid="paused-time-badge"`
- `data-testid="task-title-link"`
- `data-testid="task-id-link"`

---

# 5. Component Responsibilities

| Component | Directory | Responsibility |
|---|---|---|
| `main.css` | `src/assets/` | Global design token baseline, CSS Custom Properties (`:root`), typography scale, neutral slate palette, elevation shadows, and decoupled `.card` rules. |
| `BaseCard.vue` | `src/components/base/` | Encapsulated card container supporting `bordered`, `flat`, `ghost`, and `elevated` variants with configurable padding and rounded corners. |
| `BaseBadge.vue` | `src/components/base/` | Semantic status capsule component supporting subtle/solid/outline styles and animated pulsing live dots. |
| `PageHeader.vue` | `src/components/base/` | Standardized page header component managing title, subtitle, screen ID badge, telemetry beacon, and action/stat slots. |
| `StatusStrip.vue` | `src/components/base/` | Compact single-row operational KPI ribbon component with vertical dividers. |
| `BaseAvatar.vue` | `src/components/base/` | User initials avatar generator with integrated presence beacons (`online`, `busy`, `paused`, `offline`). |
| `SlideOverDrawer.vue` | `src/components/base/` | Contextual slide-over drawer with backdrop blur, smooth slide-in animation, and keyboard ESC dismissal. |
| `App.vue` | `src/` | Shell layout container supporting route-driven container widths (`narrow` 1024px vs `wide` 1440px) and modernized header elevation. |
| `router/index.ts` | `src/router/` | Route metadata registration with `containerWidth` specifications. |
| `WorkInProgressView.vue` | `src/views/` | Pilot screen component (`SCR-REQ-006`) utilizing base primitives to faithfully reproduce `wip_operational_board.html`. |

---

# 6. Integration Design

| Source | Target | Purpose |
|---|---|---|
| `main.css` | `html, body, .card` | Applies foundational tokens, typography, surfaces, and shadows globally. |
| `BaseCard.vue` | `main.css` tokens | Consumes `--cakra-border`, `--cakra-bg-surface`, `--cakra-shadow-*`, and `--cakra-radius-*`. |
| `BaseBadge.vue` | `main.css` tokens | Consumes semantic colors (`emerald`, `amber`, `rose`, `primary`) and opacity variables. |
| `PageHeader.vue` | `BaseBadge.vue` | Embeds live telemetry badge and screen ID tag in page headers. |
| `App.vue` | `vue-router` (`route.meta`) | Reads `route.meta.containerWidth` to dynamically toggle `cakra-shell-narrow` vs `cakra-shell-wide`. |
| `WorkInProgressView.vue` | `src/components/base/` | Composes `PageHeader`, `StatusStrip`, `BaseCard`, `BaseAvatar`, `BaseBadge`, and `SlideOverDrawer`. |
| `WorkInProgressView.vue` | `api/requests.ts` | Consumes `getWorkInProgressOverview()` API client method. |

---

# 7. Data Ownership

| Artifact / Asset | Owner | Description |
|---|---|---|
| Global Design Tokens | `main.css` | Authoritative source of styling tokens, typography, surfaces, and elevation. |
| Base UI Primitives | `src/components/base/` | Authoritative source of reusable component contracts and layout variants. |
| Route Metadata | `router/index.ts` | Authoritative definition of screen IDs, authentication rules, and container widths. |
| WIP View Projection | `WorkInProgressView.vue` | Presentation layer for operational active and paused task tracking. |

---

# 8. Database Design

## New Tables
None. Pure frontend architecture.

## Modified Tables
None.

## Relationships
None.

## Migration Considerations
No database migrations required.

---

# 9. Cross-Cutting Concerns

- **Accessibility (a11y)**:
  - Contrast ratios: Text `#0f172a` on canvas `#f8fafc` exceeds WCAG AAA (16.2:1). Secondary text `#475569` on `#f8fafc` exceeds WCAG AA (7.4:1).
  - Keyboard navigation: `SlideOverDrawer` listens for ESC key press; interactive card rows support keyboard focus and Enter key navigation.
- **Performance & Bundle Size**:
  - Zero external npm dependencies introduced; bundle size remains virtually unchanged.
  - Base components are lightweight SFCs with minimal template overhead.
- **Test Compatibility**:
  - All existing automated test hooks (`data-testid`, `data-screen-id`) remain exactly intact.

---

# 10. Implementation Constraints

- Must use Vue 3 Composition API with `<script setup lang="ts">`.
- Strict prohibition on installing bulky external UI libraries (PrimeVue, Vuetify, Element Plus) or CSS preprocessors (Sass) not already in `package.json`.
- CSS custom properties must be used for tokenization in `main.css`.
- Must pass `npm run build` (`vue-tsc --noEmit && vite build`) and backend tests (`dotnet test cakra\Cakra.sln`) with zero errors.

---

# 11. Acceptance Conditions

1. `src/assets/main.css` is updated with modernized design tokens (14px root typography, 1.5 line height, restored heading scale, slate neutral palette, 6px–16px radii, natural diffuse shadows).
2. All 6 base component primitives (`BaseCard`, `BaseBadge`, `PageHeader`, `StatusStrip`, `BaseAvatar`, `SlideOverDrawer`) are implemented in `src/components/base/` with complete TypeScript prop interfaces.
3. `src/App.vue` supports `route.meta.containerWidth` (`narrow` 1024px and `wide` 1440px).
4. `router/index.ts` specifies `containerWidth: 'narrow'` on `/operations/wip`.
5. `WorkInProgressView.vue` (`SCR-REQ-006`) is re-implemented using the base primitives and faithfully matches the layout, density, and elegance of `wip_operational_board.html` without boxification or visual degradation.
6. All existing automated test attributes (`data-testid`, `data-screen-id`) remain functional and verified.
7. `npm run build` exits with code 0.
