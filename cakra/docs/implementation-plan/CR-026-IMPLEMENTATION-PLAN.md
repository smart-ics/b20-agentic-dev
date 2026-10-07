---
Title: Implementation Plan for Executive Dark Theme, Modern Typography, and Clean Color System for Cakra.Web (CR-026)
Code: CR-026
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-07
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-026`: Executive Dark Theme, Modern Typography (Inter & JetBrains Mono), and Clean Color System for Cakra.Web, porting the executive styling system from the reference infographic (`EXECUTIVE-INFOGRAPHIC-BOD-REV-6.html`).

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- ISSUE: [CR-026-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-026-ISSUE.md)
- FEASIBILITY-ASSESSMENT: [CR-026-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-026-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)
- ARCHITECTURE: [CR-026-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-026-ARCHITECTURE.md) (authoritative target architecture)
- REFERENCE FILE: [EXECUTIVE-INFOGRAPHIC-BOD-REV-6.html](file:///D:/Project.Aktif/b21-myhosweb-system/EXECUTIVE-INFOGRAPHIC-BOD-REV-6.html)

Architecture Applicability: ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan delivers the complete visual modernization of `Cakra.Web` across:

1. **Build Toolchain & Infrastructure**:
   - Install `tailwindcss`, `postcss`, and `autoprefixer` in `Cakra.Web`.
   - Configure `tailwind.config.ts` and `postcss.config.js` with `darkMode: 'class'`, font families (`Inter`, `JetBrains Mono`), and executive slate/accent tokens.
   - Configure font imports in `main.css` and early FOUT-prevention script in `index.html`.
2. **State Management**:
   - Implement `useThemeStore` in Pinia (`src/stores/theme.ts`) with `localStorage` persistence, defaulting to Dark Mode, and dual DOM synchronization (`class="dark"` and `data-bs-theme="dark"`).
3. **App Shell & Presentation Primitives**:
   - Modernize `App.vue`: Topbar theme switcher button, deep dark canvas backdrop, and deep slate gradient sidebar with cyan active indicators.
   - Modernize Base Components in `src/components/base/`: `BaseCard.vue`, `BaseBadge.vue`, `PageHeader.vue`, `StatusStrip.vue`, `SlideOverDrawer.vue`.
4. **All-in-One Views Overhaul**:
   - Executive Dashboards: `OperationsCockpitView.vue`, `WorkPackageView.vue`.
   - Feeds & In-Progress: `FeedView.vue`, `WorkInProgressView.vue`.
   - Analytics, Catalogs, Requests & Administration: `CustomerPortfolioView.vue`, `ProgrammerWorkloadView.vue`, `ProgrammerPerformanceView.vue`, `ProductCatalogView.vue`, `RequestDetailView.vue`, `RequestSearchView.vue`, `CreateRequestView.vue`, `MyRequestsView.vue`, `CustomerManagementView.vue`, `PersonManagementView.vue`, `UserManagementView.vue`, `LoginView.vue`.
5. **Full System Verification**:
   - Ensure clean compilation with `vue-tsc --noEmit && vite build`.
   - Verify smooth theme toggling and visual fidelity matching the BOD infographic.

---

# 3. Dependencies

- Node.js & npm in `d:\Project.Aktif\b20-agentic-dev\cakra\src\frontend\Cakra.Web`
- Vite & Vue 3 + TypeScript
- Google Fonts (`Inter`, `JetBrains Mono`)

For slice dependencies:
- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires the referenced slice to have implementation status `IMPLEMENTED`.
- Dependency satisfaction does not require review status `GO`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Build Toolchain, Theme Infrastructure & Styling Pipeline | IMPLEMENTED | GO | 2/2 |
| P2 - App Shell Modernization & Base Components | IMPLEMENTED | GO | 2/2 |
| P3 - Executive Dashboards & Application Views Overhaul | IMPLEMENTED | GO | 3/3 |
| P4 - Full System Verification & Type-Check | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Build Toolchain, Theme Infrastructure & Styling Pipeline

Implementation Status: IMPLEMENTED
Review Status: GO

### P1-S01

Title: Install Tailwind CSS, PostCSS, and Configure Fonts and Tokens

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Equip `Cakra.Web` with Tailwind CSS, configure PostCSS, establish Google Fonts imports for Inter and JetBrains Mono, and add FOUT prevention in `index.html`.

Depends On: None

Repository: `cakra/src/frontend/Cakra.Web`

Completion Criteria:
- `tailwindcss`, `postcss`, and `autoprefixer` installed in `package.json`.
- `tailwind.config.ts` created with `darkMode: 'class'`, content glob patterns, and font/color extensions.
- `postcss.config.js` created.
- `src/assets/main.css` imports Google Fonts and Tailwind directives (`@tailwind base; @tailwind components; @tailwind utilities;`).
- `index.html` updated with early theme detection script to prevent white flash.

Notes:
- Installed `tailwindcss`, `postcss`, and `autoprefixer` as devDependencies.
- Created `tailwind.config.ts` configuring `darkMode: 'class'`, content glob patterns, font families (`Inter`, `JetBrains Mono`), and slate color tokens (`slate-850`, `slate-950`).
- Created `postcss.config.js` configuring `tailwindcss` and `autoprefixer` plugins.
- Added Google Fonts import and Tailwind directives (`@tailwind base; @tailwind components; @tailwind utilities;`) to `src/assets/main.css`, preserving existing custom styles.
- Added inline theme detection script to `<head>` in `index.html` to pre-apply `class="dark"` and `data-bs-theme="dark"` preventing FOUT.
- Verified build compiles cleanly (`vue-tsc --noEmit && vite build` succeeded).
- Changed files:
  - `cakra/src/frontend/Cakra.Web/package.json`
  - `cakra/src/frontend/Cakra.Web/package-lock.json`
  - `cakra/src/frontend/Cakra.Web/tailwind.config.ts`
  - `cakra/src/frontend/Cakra.Web/postcss.config.js`
  - `cakra/src/frontend/Cakra.Web/src/assets/main.css`
  - `cakra/src/frontend/Cakra.Web/index.html`

---

### P1-S02

Title: Implement Pinia Theme Store and Dual DOM Synchronization

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Create reactive theme state management with `localStorage` persistence, defaulting to Dark Mode, and dual DOM synchronization (`class="dark"` and `data-bs-theme="dark"`).

Depends On: P1-S01

Repository: `cakra/src/frontend/Cakra.Web`

Completion Criteria:
- `src/stores/theme.ts` created exporting `useThemeStore`.
- `initTheme()` reads `localStorage['cakra_theme']`, defaults to `true` (dark), and applies classes.
- `toggleTheme()` flips state, updates `localStorage`, and updates DOM attributes.
- `main.ts` calls `initTheme()` on app bootstrap.

Notes:
- Created `src/stores/theme.ts` with `useThemeStore` managing `isDark` state, localStorage persistence under key `cakra_theme` (defaulting to dark mode), and dual DOM synchronization (`class="dark"` and `data-bs-theme="dark"`).
- Updated `src/main.ts` to instantiate Pinia and invoke `themeStore.initTheme()` immediately upon startup.
- Verified build and type checks: `vue-tsc --noEmit && vite build` succeeded cleanly with zero errors.
- Changed files:
  - `cakra/src/frontend/Cakra.Web/src/stores/theme.ts`
  - `cakra/src/frontend/Cakra.Web/src/main.ts`


---

## P2 - App Shell Modernization & Base Components

Implementation Status: IMPLEMENTED
Review Status: GO

### P2-S03

Title: Modernize App Shell (App.vue) with Topbar Theme Toggle & Sidebar

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Update [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue) to render the deep slate canvas, add the theme switcher button in the Topbar right-hand header, and restyle the Sidebar with deep slate gradients and cyan indicators.

Depends On: P1-S01, P1-S02

Repository: `cakra/src/frontend/Cakra.Web`

Completion Criteria:
- Root container styled with `bg-slate-950 text-slate-100 min-h-screen`.
- Topbar styled with `bg-slate-900/90 backdrop-blur border-b border-slate-800`.
- Sun/Moon icon button added to Topbar right-hand header invoking `themeStore.toggleTheme()`.
- Sidebar styled with `linear-gradient(180deg, #020617 0%, #0f172a 50%, #1e1b4b 100%)` and `border-r border-slate-800`.
- Active sidebar items styled with cyan pill background and left border indicator.

Notes:
- Imported `useThemeStore` and integrated `themeStore.toggleTheme()` with Sun/Moon icon button and accessibility attributes (`title`, `aria-label`) in the Topbar right utilities.
- Styled root container with `bg-slate-950 text-slate-100 min-h-screen`.
- Styled Topbar with `bg-slate-900/90 backdrop-blur border-b border-slate-800 text-slate-100`.
- Styled Sidebar with `linear-gradient(180deg, #020617 0%, #0f172a 50%, #1e1b4b 100%)` and `border-r border-slate-800`.
- Styled active navigation links with cyan accent pill background (`active bg-cyan-500/10 text-cyan-300 border-l-2 border-cyan-400`).
- Styled App Footer and unauthenticated layout (`.cakra-unauth-layout`) to match executive dark palette.
- Updated `src/assets/main.css` design tokens and shell class declarations for dark canvas, topbar, sidebar, active nav indicator, and footer.
- Verified build and type checks: `vue-tsc --noEmit && vite build` completed cleanly with zero errors.
- Changed files:
  - `cakra/src/frontend/Cakra.Web/src/App.vue`
  - `cakra/src/frontend/Cakra.Web/src/assets/main.css`

---

### P2-S04

Title: Modernize Base UI Components for Dark Slate Surfaces

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Update reusable components in `src/components/base/` to natively render executive dark surfaces, glassmorphic borders, and high-contrast typography.

Depends On: P1-S01

Repository: `cakra/src/frontend/Cakra.Web`

Completion Criteria:
- `BaseCard.vue`: Defaults to `bg-slate-900/90 border border-slate-800 rounded-xl shadow-lg`, with `flat` variant using `bg-slate-950/60` and hover glow.
- `BaseBadge.vue`: Updated with translucent pill styles (`bg-*-500/10 text-*-400 border border-*-500/20`).
- `PageHeader.vue`: High-contrast text, tight tracking, and cyan indicator dot.
- `StatusStrip.vue`: Metric cards styled with `text-3xl font-black text-white`, uppercase labels, and dark progress tracks (`bg-slate-800 h-1.5 rounded-full`).
- `SlideOverDrawer.vue`: Drawer backdrop and body updated with dark slate panels and `border-slate-800`.

Notes:
- Modernized `BaseCard.vue`: default/bordered variant styles `bg-slate-900/90 border border-slate-800 rounded-xl shadow-lg dark:text-slate-100`; flat variant `bg-slate-950/60 border border-slate-800/80 rounded-xl`; elevated variant `bg-slate-900 border border-slate-700/80 shadow-2xl rounded-2xl`; hover effect `hover:border-slate-700 hover:shadow-cyan-500/5 hover:-translate-y-0.5`; cleanly supports light mode fallback.
- Modernized `BaseBadge.vue`: executive pill badge with 10% opacity fills, 20% opacity borders (`bg-*-500/10 text-*-400 border border-*-500/20`), uppercase tracking-wider typography, animated pulse indicator, and full palette support (`cyan`, `indigo`, `emerald`, `purple`, `amber`, `rose`, `slate`), retaining semantic compatibility.
- Modernized `PageHeader.vue`: high-contrast title typography with tight tracking (`tracking-tight text-slate-900 dark:text-white`), cyan accent dot indicator (`bg-cyan-400 shadow-[0_0_8px_rgba(6,182,212,0.6)]`), uppercase metadata badge, and border divider (`border-slate-200 dark:border-slate-800`).
- Modernized `StatusStrip.vue`: renders executive KPI metric cards with `text-3xl font-black text-white` (or color family accents), uppercase tracking-wider labels (`text-xs uppercase tracking-wider font-semibold text-slate-500 dark:text-slate-400`), dynamic micro-gauge values, and dark gauge tracks (`bg-slate-200 dark:bg-slate-800 h-1.5 rounded-full overflow-hidden`).
- Modernized `SlideOverDrawer.vue`: dark slate surfaces (`bg-slate-900/95 backdrop-blur border-l border-slate-800 text-slate-100`), dark backdrop (`bg-slate-950/70 backdrop-blur-sm`), matching header and footer panels, and light mode fallback.
- Verified build compiles cleanly (`npm run build` / `vue-tsc --noEmit && vite build` passed with 0 errors).
- Verified backend test suite runs cleanly (`dotnet test Cakra.sln` passed all 705 unit and integration tests).
- Changed files:
  - `cakra/src/frontend/Cakra.Web/src/components/base/BaseCard.vue`
  - `cakra/src/frontend/Cakra.Web/src/components/base/BaseBadge.vue`
  - `cakra/src/frontend/Cakra.Web/src/components/base/PageHeader.vue`
  - `cakra/src/frontend/Cakra.Web/src/components/base/StatusStrip.vue`
  - `cakra/src/frontend/Cakra.Web/src/components/base/SlideOverDrawer.vue`

---

## P3 - Executive Dashboards & Application Views Overhaul

Implementation Status: IMPLEMENTED
Review Status: GO

### P3-S05

Title: Overhaul Operations Cockpit and Work Package Screen

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Refactor [OperationsCockpitView.vue](file:///d:/Project.Aktif/b20-agentic-dev\cakra\src\frontend\Cakra.Web\src\views\OperationsCockpitView.vue) and [WorkPackageView.vue](file:///d:/Project.Aktif/b20-agentic-dev\cakra\src\frontend\Cakra.Web\src\views\WorkPackageView.vue) to natively exhibit the BOD Infographic styling.

Depends On: P2-S03, P2-S04

Repository: `cakra/src/frontend/Cakra.Web`

Completion Criteria:
- Operations Cockpit: 2D Pressure × Health Triage Matrix styled with dark slate cells, cyan/indigo/emerald borders, and clean monospace metric counters.
- Work Package View: Cards, tables, and detail drawer styled with `slate-900` surfaces, `slate-800` dividers, and 14-day flow barcodes.
- Zero functional regression in matrix filtering, data fetching, or drawer actions.

Notes:
- Modernized `OperationsCockpitView.vue`:
  - 2D Operational Triage Matrix: styled with `bg-slate-900/90 border border-slate-800 rounded-xl shadow-lg`, dark slate header surfaces (`bg-slate-950/60`), executive cell severity tints (empty, critical rose, warning amber/yellow, nominal emerald, active neutral slate), cyan focus ring on selected cells (`outline: 2px solid #22d3ee`), and monospace metric counters.
  - Exception and Nominal Feed cards: styled with dark slate surfaces, `bg-slate-950/60` metric grid containers, translucent pill badges, and dark 14-day flow barcodes.
  - Active filter banner and collapsible nominal section styled with executive dark slate gradients and cyan accents.
- Modernized `WorkPackageView.vue`:
  - Screen Header, toolbar, and create modal: styled with dark slate cards (`bg-slate-900/90`, `bg-slate-950/80`), dark inputs/selects, and cyan action buttons.
  - Work package table & cards: styled with `bg-slate-900/90 border border-slate-800`, high-contrast text, translucent status pills, and dark 14-day activity barcode strips.
  - Side detail panel / drawer: styled with dark slate surfaces (`bg-slate-900/95 border border-slate-800`), telemetry spotlight blocks, and high-contrast metadata.
  - Interactive Decision Workbench Simulator: styled with dark slate inputs, cyan/indigo accent cards, and live simulation comparison banner.
- Verified build: `npm run build` (`vue-tsc --noEmit && vite build`) passed with 0 errors.
- Verified full test suite: `dotnet test Cakra.sln` passed all 705 tests cleanly.
- Changed files:
  - `cakra/src/frontend/Cakra.Web/src/views/OperationsCockpitView.vue`
  - `cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue`

---

### P3-S06

Title: Overhaul Operational Feed and Work In Progress Views

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Refactor [FeedView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/FeedView.vue), [WorkInProgressView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkInProgressView.vue), and feed subcomponents (`FeedTimelineCard.vue`, `FeedLedgerRow.vue`, `FeedDossierPanel.vue`).

Depends On: P2-S03, P2-S04

Repository: `cakra/src/frontend/Cakra.Web`

Completion Criteria:
- Operational feed cards, comment bubbles, and ledger rows use dark slate surfaces and clean borders.
- WIP cards display high-contrast status badges and telemetry counters.
- Infinite scroll and slide-over panels function seamlessly.

Notes:
- Modernized `FeedView.vue`:
  - Styled screen header, events counter, and view mode switcher buttons with executive slate backgrounds and cyan accents.
  - Styled universal search input with deep slate canvas (`bg-slate-950/80 border-slate-800 text-slate-100`) and quick filter chips (`All`, `Exceptions`, `Requests`, `System Facts`).
  - Styled stream container, high-density ledger table wrapper, empty/loading states, retry alerts, and end-of-stream milestone box (`border-slate-800`).
  - Styled sticky operational stream summary metrics card with dark slate surfaces and readable monospace metrics.
- Modernized `FeedTimelineCard.vue`:
  - Styled timeline card article containers with `bg-slate-900/90 border border-slate-800 rounded-xl text-slate-100 shadow-lg`.
  - Upgraded reference links, status accents (`status-accent-cyan`, `status-accent-blue`, `status-accent-red`), and author/timestamp metadata.
  - Modernized comment section with dark slate speech bubbles (`bg-slate-900/90 border border-slate-800 text-slate-200`), dark comment composer input, and floating reaction palette.
- Modernized `FeedLedgerRow.vue`:
  - Styled ledger table rows with dark hover highlights, readable monospace timestamps, reference links, accountable owner badges, and clean operational signal indicators (`ACK`, `EXP`).
- Modernized `FeedDossierPanel.vue`:
  - Styled pinned operational decision context panel with dark slate surfaces, cyan/indigo/rose semantic pills, decision required callout, context grid, audited thread discussion cards, and action buttons.
- Modernized `WorkInProgressView.vue`:
  - Styled filter toolbar pills, search input, empty states, and person cards with dark slate surfaces.
  - Active task spotlight modernized with cyan accent border (`border-left: 3px solid #22d3ee`), monospace elapsed time counter, and inspection trigger.
  - Styled paused work rows and inspection slide-over drawer contents with dark slate styling and high-contrast typography.
- Updated `src/assets/main.css` with dark theme overrides for operational feed components (`.op-feed-card`, `.op-feed-row`, `.op-reaction-palette`, `.op-comment-bubble`, `.op-feed-end-milestone`, `.op-feed-retry-box`).
- Verified build: `vue-tsc --noEmit && vite build` completed cleanly with zero errors.
- Verified test suite: `dotnet test Cakra.sln` passed all 705 unit and integration tests.
- Changed files:
  - `cakra/src/frontend/Cakra.Web/src/views/FeedView.vue`
  - `cakra/src/frontend/Cakra.Web/src/components/FeedTimelineCard.vue`
  - `cakra/src/frontend/Cakra.Web/src/components/FeedLedgerRow.vue`
  - `cakra/src/frontend/Cakra.Web/src/components/FeedDossierPanel.vue`
  - `cakra/src/frontend/Cakra.Web/src/views/WorkInProgressView.vue`
  - `cakra/src/frontend/Cakra.Web/src/assets/main.css`
  - `cakra/docs/implementation-plan/CR-026-IMPLEMENTATION-PLAN.md`

---

### P3-S07

Title: Overhaul Remaining Analytics, Request, Admin & Auth Views

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Refactor remaining views and modals across Cakra.Web to complete the all-in-one overhaul.

Depends On: P2-S03, P2-S04

Repository: `cakra/src/frontend/Cakra.Web`

Completion Criteria:
- Analytics screens (`CustomerPortfolioView.vue`, `ProgrammerWorkloadView.vue`, `ProgrammerPerformanceView.vue`, `ProductCatalogView.vue`) styled with executive cards and charts.
- Request screens (`RequestDetailView.vue`, `RequestSearchView.vue`, `CreateRequestView.vue`, `MyRequestsView.vue`) updated.
- Administrative management views (`CustomerManagementView.vue`, `PersonManagementView.vue`, `UserManagementView.vue`) and `LoginView.vue` updated with dark slate cards and form inputs.
- Modals (`CustomerModal.vue`, `PersonModal.vue`, `UserAccountModal.vue`, etc.) updated with dark backdrop blur and clean borders.

Notes:
- Modernized Analytics views:
  - `CustomerPortfolioView.vue`: Selector dropdown and request status cards/tables updated with dark slate cards (`bg-slate-900/90 border border-slate-800 text-slate-100`).
  - `ProgrammerWorkloadView.vue`: Programmer select, workload breakdown card/header, and active queue card/header styled with dark surfaces and cyan accents.
  - `ProgrammerPerformanceView.vue`: Filter controls and monthly/daily snapshot tables styled with dark slate classes.
  - `ProductCatalogView.vue`: Inline create/edit form cards, form inputs, and catalog table card styled with dark surfaces and cyan focus highlights.
- Modernized Request views:
  - `RequestDetailView.vue`: Request header, description panel, properties card, subtasks card, action bar, state history card, comments card, and all action dialogs (pause, cancel, edit) styled with dark slate surfaces and inputs.
  - `RequestSearchView.vue`: Filter inputs, results table card, pagination footer, and state history preview panel styled with dark slate classes.
  - `CreateRequestView.vue`: Specifications form card, inputs, textareas, selects, and subtasks list styled with `bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400`.
  - `MyRequestsView.vue`: Top executive KPI metric cards, tabs, universal search, filter controls, request cards grid, and assigned subtasks queue table styled with dark slate surfaces.
- Modernized Admin & Auth views:
  - `LoginView.vue`: Hero card styled with `bg-slate-900/90 border border-slate-800 text-slate-100 shadow-2xl`, inputs with `bg-slate-950/60 border-slate-700 text-slate-100 focus:border-cyan-400`, cyan lock icon, and mode toggle.
  - `CustomerManagementView.vue`: Metric ribbon, search input, status dropdown, customer table, and loading skeletons styled with dark slate classes.
  - `PersonManagementView.vue`: Metric ribbon, search input, status dropdown, persons table, and loading skeletons styled with dark slate classes.
  - `UserManagementView.vue`: Metric ribbon, search input, status dropdown, accounts table, and loading skeletons styled with dark slate classes.
- Modernized Modals:
  - `CustomerModal.vue`, `PersonModal.vue`, `UserAccountModal.vue`, `CreateRequestModal.vue`, `CustomerContactModal.vue`: Updated dialog surfaces with `bg-slate-900/95 border border-slate-800 text-slate-100 shadow-2xl`, header with `bg-slate-950/60 border-b border-slate-800`, footer with `bg-slate-950/60 border-t border-slate-800`, form controls with `bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400`, and backdrop glassmorphism `rgba(2, 6, 23, 0.8)` with `backdrop-filter: blur(4px)`.
- Global styling enhancements in `src/assets/main.css`:
  - Added dark theme overrides for form controls, focus borders (`#22d3ee`), cards, card headers, tables, modal dialogs, backdrops, and outline secondary buttons.
- Verification:
  - `npm run build` / `vue-tsc --noEmit && vite build` completed cleanly with 0 errors.
- Changed files:
  - `cakra/src/frontend/Cakra.Web/src/assets/main.css`
  - `cakra/src/frontend/Cakra.Web/src/views/CustomerPortfolioView.vue`
  - `cakra/src/frontend/Cakra.Web/src/views/ProgrammerWorkloadView.vue`
  - `cakra/src/frontend/Cakra.Web/src/views/ProgrammerPerformanceView.vue`
  - `cakra/src/frontend/Cakra.Web/src/views/ProductCatalogView.vue`
  - `cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue`
  - `cakra/src/frontend/Cakra.Web/src/views/RequestSearchView.vue`
  - `cakra/src/frontend/Cakra.Web/src/views/CreateRequestView.vue`
  - `cakra/src/frontend/Cakra.Web/src/views/MyRequestsView.vue`
  - `cakra/src/frontend/Cakra.Web/src/views/LoginView.vue`
  - `cakra/src/frontend/Cakra.Web/src/views/CustomerManagementView.vue`
  - `cakra/src/frontend/Cakra.Web/src/views/PersonManagementView.vue`
  - `cakra/src/frontend/Cakra.Web/src/views/UserManagementView.vue`
  - `cakra/src/frontend/Cakra.Web/src/components/CustomerModal.vue`
  - `cakra/src/frontend/Cakra.Web/src/components/PersonModal.vue`
  - `cakra/src/frontend/Cakra.Web/src/components/UserAccountModal.vue`
  - `cakra/src/frontend/Cakra.Web/src/components/CreateRequestModal.vue`
  - `cakra/src/frontend/Cakra.Web/src/components/CustomerContactModal.vue`
  - `cakra/docs/implementation-plan/CR-026-IMPLEMENTATION-PLAN.md`

---

## P4 - Full System Verification & Type-Check

Implementation Status: IMPLEMENTED
Review Status: GO

### P4-S08

Title: Full-Stack Verification, Type-Check, and Visual Fidelity Testing

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Verify that the entire frontend compiles cleanly without errors, the theme switcher functions across all views, and the visual styling matches the BOD infographic.

Depends On: P3-S05, P3-S06, P3-S07

Repository: `cakra/src/frontend/Cakra.Web`

Completion Criteria:
- `vue-tsc --noEmit && vite build` succeeds with zero errors.
- Default page load displays executive dark theme (`slate-950`).
- Toggle button switches between dark and light themes smoothly, saving preference to `localStorage`.
- Typography renders with `Inter` and `JetBrains Mono`.

Notes:
- Executed `npm run build` (`vue-tsc --noEmit && vite build`) in `cakra/src/frontend/Cakra.Web`: 183 modules transformed, 0 type errors, clean production bundle emitted.
- Executed full test suite `dotnet test Cakra.sln` in backend repository: 705 tests passed (524 unit tests, 181 integration tests) with 0 failures and 0 regressions.
- Verified Visual Fidelity against BOD Infographic (`EXECUTIVE-INFOGRAPHIC-BOD-REV-6.html`): executive dark slate canvas (`#020617` / `slate-950`), surface cards (`#0f172a` / `slate-900`), borders (`#1e293b` / `slate-800`), translucent pill badges (`bg-*-500/10 text-*-400 border border-*-500/20`), and cyan/indigo/emerald accents.
- Verified Default Dark Appearance: Pre-mount FOUT script in `index.html` and `useThemeStore.initTheme()` guarantee default executive dark rendering on first load without prior `localStorage`.
- Verified Dual-Theme Switcher: Sun/Moon button in Topbar invokes `themeStore.toggleTheme()`, seamlessly updating both Tailwind `.dark` and Bootstrap `data-bs-theme`, persisting selection to `localStorage['cakra_theme']`.
- Verified Typography: Google Fonts `Inter` (body, headings, navigation) and `JetBrains Mono` (metrics, badges, timestamps, codes, telemetry) loaded via `main.css` and applied across all views.
- Verified Responsive Layout: Tested sidebar collapse toggle, mobile drawer with backdrop overlay, and responsive container widths (`narrow` and `wide`).
- Changed files:
  - `cakra/docs/implementation-plan/CR-026-IMPLEMENTATION-PLAN.md`

---

# 6. Change Log

- 2026-10-07: Initial creation of CR-026-IMPLEMENTATION-PLAN.md covering 4 phases and 8 executable slices. Released with `Execution Approval: APPROVED`.
