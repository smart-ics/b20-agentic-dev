---
Title: Feasibility Assessment for Compact WIP Board Redesign with Dual-Signal Elapsed and Effort-to-Limit Indicators (CR-034)
Code: CR-034
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.1
LastUpdated: 2026-10-15
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Change being assessed: Compact redesign of the Work in Progress operational awareness board (`SCR-REQ-006`) with two complementary visual urgency signals: (1) pure elapsed time heat and (2) effort-to-limit budget burn, as formally captured in [CR-034-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-034-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-034-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-034-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- FEATURE: [FEAT-MGT-004-review-programmer-workload.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-MGT-004-review-programmer-workload.md)
- ARCHITECTURE (prior): [CR-021-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-021-ARCHITECTURE.md)
- ARCHITECTURE (prior): [CR-022-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-022-ARCHITECTURE.md)

## Objective

Assess the feasibility and identify gaps required to:

1. Redesign `WorkInProgressView.vue` (`SCR-REQ-006`) for compact, dense information presentation that maximizes visible personnel and task data per viewport.
2. Introduce visual urgency tiers for elapsed time (pure duration heat: ≤4h green → >4h amber → >8h red) to identify programmers in trouble.
3. Introduce visual overload indicators by paused task count (left-border escalation: 0–1 green → 2–3 amber → 4+ red) to identify busy programmers.
4. Introduce a budget burn signal: ratio of elapsed hours against effort-derived hour limits (×1=2h, ×2=4h, ×3=8h, ×4=16h, ×5=24h), rendered as a thin progress bar alongside the elapsed time display, with an OVER badge when ≥100%.
5. Surface `Complexity` (the domain field that the issue refers to as "effort") in the `TaskWorkInProgressDto` read model and `GET /api/v1/requests/wip` endpoint, propagating it through the backend query pipeline and frontend DTO to enable client-side budget burn computation.
6. Preserve all automated test selectors (`data-screen-id="SCR-REQ-006"`, `data-testid` attributes) and the contextual inspection drawer.

---

# 2. Current State

## Existing Behavior

1. **Domain Entity — `Request.Complexity`**:
   - [`Request.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs#L33-L34) exposes `public int Complexity { get; private set; } = 1;` — an integer 1–5, enforced by `CK_Requests_Complexity_Range`.
   - The field is persisted in `[request].[Requests].[Complexity]` via migration [`0011_request_complexity.sql`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Migrations/Scripts/0011_request_complexity.sql).
   - `SetComplexity()` domain method raises `RequestComplexityUpdated` event and validates the 1–5 range.
   - In the domain and the issue, the user calls this field "effort". The domain field name is `Complexity`. These refer to the same concept. The effort-to-hours mapping (×1=2h, ×2=4h, ×3=8h, ×4=16h, ×5=24h) is a display-layer interpretation convention, not a persisted rule.

2. **WIP Query Pipeline — `Complexity` is absent**:
   - [`GetWorkInProgressOverviewAsync`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestQueryService.cs#L726-L909) issues a SQL query against `[request].[Requests]` that does **not** `SELECT r.[Complexity]`.
   - The internal `WorkInProgressRequestRow` private class (lines 1312–1326) does not include a `Complexity` property.
   - Consequently, `Complexity` is never mapped into `TaskWorkInProgressDto` during assembly (lines 843–862), and `TaskWorkInProgressDto` has no `Complexity` field.
   - The backend DTO [`WorkInProgressDto.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Models/WorkInProgressDto.cs) and the frontend contract [`requests.ts`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/requests.ts#L409-L427) both omit `Complexity`/`effort` entirely.

3. **Frontend — `WorkInProgressView.vue`**:
   - The current implementation ([`WorkInProgressView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkInProgressView.vue)) uses spacious `BaseCard` containers with `padding="lg"`, multi-layer nesting, and a card-per-person layout.
   - Elapsed time is displayed (`totalInProgressFormatted`) without any color-coded urgency tier or effort-derived budget context.
   - Paused task rows are uniformly styled with no count-based visual escalation on the person card.
   - All `data-testid` attributes are present and are consumed by the existing automated test suite.

4. **Frontend — Complexity exposed in other DTOs**:
   - `RequestDto` and `RequestListItemDto` in [`requests.ts`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/requests.ts) already include `complexity: number` for the request detail and list views.
   - The WIP-specific `TaskWorkInProgressDto` is the only read-model DTO that omits it.

5. **Existing UI Primitives — `BaseCard`, `BaseAvatar`, `BaseBadge`, `StatusStrip`, `PageHeader`**:
   - Established in CR-022 and available in `@/components/base/`. The compact redesign will continue to use these primitives while reducing padding and nesting depth.

## Existing Constraints

1. **Test Selector Preservation**: Automated integration tests reference `data-screen-id="SCR-REQ-006"`, `data-testid="wip-screen"`, `data-testid="person-card"`, `data-testid="in-progress-card"`, `data-testid="paused-task-card"`, `data-testid="in-progress-time-badge"`, `data-testid="paused-time-badge"`, and related attributes. These must be preserved verbatim.
2. **Single In-Progress Task Policy**: The backend enforces one active `IN_PROGRESS` task per person. The WIP query groups by this policy. This constraint is unchanged.
3. **Effort-to-Hours Mapping is a display convention**: The mapping (×1=2h, ×2=4h, ×3=8h, ×4=16h, ×5=24h) is not stored in the database or enforced by domain rules. It is a visualization interpretation applied at the frontend layer.
4. **`Complexity` is the authoritative domain field**: The issue uses the term "effort"; the domain uses `Complexity`. Both refer to `[request].[Requests].[Complexity]` (INT 1–5). No renaming of the domain field is required.
5. **Budget burn computed client-side**: The effort-to-hours limit and burn percentage are derived from `Complexity` and `TotalInProgressHours` purely in frontend JavaScript. No server-side computation is needed.

---

# 3. Gap Analysis

| ID | Severity | Gap |
|------|------|------|
| GAP-001 | CRITICAL | `Complexity` is not selected in the WIP SQL query (`GetWorkInProgressOverviewAsync`) in `RequestQueryService.cs`, not mapped in `WorkInProgressRequestRow`, not assembled into `TaskWorkInProgressDto` in the backend, and not present in `WorkInProgressDto.cs`. Without this field, the frontend cannot compute effort-derived budget burn. |
| GAP-002 | CRITICAL | `TaskWorkInProgressDto` TypeScript interface in `requests.ts` does not include `complexity: number`, so the frontend has no contract for the effort field even after the backend exposes it. |
| GAP-003 | MAJOR | `WorkInProgressView.vue` uses spacious `BaseCard` card-per-person layout with `padding="lg"` and multi-level nesting. The current structure does not achieve the compact/dense single-line task row layout requested. A structural redesign of the template and scoped styles is required. |
| GAP-004 | MAJOR | No elapsed time visual urgency tiers exist. The elapsed time badge (`data-testid="in-progress-time-badge"`, `data-testid="paused-time-badge"`) applies uniform styling with no color differentiation based on absolute hours. |
| GAP-005 | MAJOR | No budget burn visualization exists. There is no progress bar, burn fraction, or OVER badge anywhere in the current `WorkInProgressView.vue`. |
| GAP-006 | MAJOR | No paused task count–based visual escalation exists on person rows. All person cards have the same left-border appearance regardless of paused task accumulation. |
| GAP-007 | MINOR | The KPI strip currently has no "over budget" counter. Adding an aggregated "over budget" KPI count requires client-side computation across all persons after `Complexity` becomes available. |

---

# 4. Open Questions

| ID | Question | Impact |
|------|------|------|
| OQ-001 | Should the effort-to-hours mapping (×1=2h, ×2=4h, ×3=8h, ×4=16h, ×5=24h) be stored in a configuration artifact (e.g., a frontend constant, a domain constant, or a future persisted table) rather than hardcoded in `WorkInProgressView.vue`? | If it may change or be configurable per-organization in the future, it should be extracted to a named constant module rather than inlined. Impacts architecture boundary for the mapping table. |
| OQ-002 | Should elapsed time urgency thresholds (≤4h, >4h, >8h) and paused-task overload thresholds (2+, 4+) be configurable or hardcoded constants? | Hardcoding simplifies implementation; configurable thresholds would require a settings surface and backend support. |
| OQ-003 | Is the `Complexity` field name appropriate to surface in the frontend UI as "effort" (e.g., in the effort badge label `×3`), or should the frontend use `complexity`/`Complexity` terminology consistently? | Purely a UX labeling question. Does not affect data model. |

---

# 5. Assumptions

| ID | Assumption |
|------|------|
| ASM-001 | The `Complexity` column in `[request].[Requests]` is already populated for all active WIP tasks (all requests have `Complexity` ≥ 1 due to the column's `DEFAULT (1)` and `NOT NULL` constraint). No backfill migration is required. |
| ASM-002 | The effort-to-hours budget mapping (×1=2h, ×2=4h, ×3=8h, ×4=16h, ×5=24h) is a fixed, hardcoded convention for this release. It does not require persistence, configuration, or domain enforcement. |
| ASM-003 | Budget burn computation (elapsed hours ÷ effort-limit hours × 100%) is computed entirely in the frontend JavaScript layer. The backend only needs to expose `Complexity` as an integer. |
| ASM-004 | All existing `data-testid` selector values are preserved verbatim. No existing test selectors are renamed or removed. |
| ASM-005 | The compact redesign replaces the Vue template and scoped styles of `WorkInProgressView.vue` in-place. No new screen route or new Vue component file is introduced. |
| ASM-006 | The `Complexity` field is surfaced in the DTO as `complexity: number` (camelCase, matching existing TypeScript DTO naming conventions in `requests.ts`). |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|------|------|------|------|
| RISK-001 | Existing automated integration tests validate `data-testid` attributes and may also assert DOM structure (element hierarchy, CSS classes, or text content) that will change with the compact redesign. | If structural assertions break, test suite fails. | Preserve all `data-testid` attributes verbatim. Review test suite for structural assertions before merging; update brittle structural assertions if needed. |
| RISK-002 | The `Complexity` SQL column addition to the WIP query is a low-risk additive change, but introduces a regression path if the `WorkInProgressRequestRow` mapping is incomplete. | Backend API returns default 0 for `Complexity` if not mapped, causing incorrect burn calculation showing 0%. | Unit-test the mapping; add `complexity: 0` guard in frontend computation with a fallback to treat 0 as effort ×1. |
| RISK-003 | Compact layout may reduce readability on small viewport widths (<600px). | Operations team members using tablets may experience degraded usability. | Apply responsive breakpoints using Tailwind `sm:` variants for line wrapping on narrow viewports, consistent with existing `d-flex flex-column flex-sm-row` patterns in the current view. |
| RISK-004 | The effort-to-hours mapping is a fixed assumption. If the business redefines effort thresholds after deployment, a code change is required. | Business expectation mismatch in urgency signals. | Extract mapping into a named constant module (e.g., `effortHoursMap.ts`) to centralize future changes. |

---

# 7. Recommendations

## Option A — Additive `Complexity` propagation with in-place view redesign (Recommended)

Extend the existing WIP query pipeline additively:

1. **Backend SQL**: Add `r.[Complexity]` to the `SELECT` clause in `GetWorkInProgressOverviewAsync`.
2. **Backend mapping**: Add `Complexity` property to `WorkInProgressRequestRow` and map it into `TaskWorkInProgressDto`.
3. **Backend DTO**: Add `public int Complexity { get; init; }` to `TaskWorkInProgressDto` in `WorkInProgressDto.cs`.
4. **Frontend contract**: Add `complexity: number` to `TaskWorkInProgressDto` in `requests.ts`.
5. **Frontend view**: Redesign `WorkInProgressView.vue` template to compact single-line task rows with dual-signal right column (elapsed heat number + burn progress bar + effort pip), left-border person row urgency, and KPI strip "over budget" count.
6. **Constants module**: Extract effort-to-hours mapping and urgency thresholds into a dedicated constant (e.g., inline constants in the view, or a separate `wipConstants.ts` for testability).

### Advantages

- Purely additive backend change — no breaking API change; existing consumers of the WIP endpoint receive an additional field.
- All test selectors preserved verbatim.
- Complexity field is already persisted and available; no migration required.
- Frontend-only budget burn computation keeps the backend simple and stateless.
- Prototype is already validated (`scr-req-006-redesign.html`).

### Disadvantages

- `WorkInProgressView.vue` template will be substantially rewritten, requiring careful review to ensure no `data-testid` attributes are accidentally dropped.

## Option B — Pure frontend-only change with Complexity fetched from a separate endpoint

Do not modify the WIP endpoint. Instead, fetch `Complexity` for each request from an additional API call per task.

### Advantages

- Zero backend change to the WIP endpoint.

### Disadvantages

- Introduces N+1 API calls for each WIP task — significant performance and latency concern.
- Overly complex for an additive field on an already-existing query.
- Not recommended.

---

# 8. Gap Closure

## GAP-001

Status: CLOSED

### Decision

Extend `GetWorkInProgressOverviewAsync` in `RequestQueryService.cs` to select `r.[Complexity]` from `[request].[Requests]`, add `public int Complexity { get; init; }` to internal `WorkInProgressRequestRow`, and map `Complexity`, `BudgetHours`, `BurnPercentage`, and `IsOverBudget` into `TaskWorkInProgressDto`. Calculate `BudgetHours` on the backend using the agreed mapping:
- Complexity 1 = 2.0 hours
- Complexity 2 = 4.0 hours
- Complexity 3 = 8.0 hours
- Complexity 4 = 16.0 hours
- Complexity 5 = 24.0 hours

Compute `BurnPercentage = (int)Math.Round((totalHours / budgetHours) * 100.0)` and `IsOverBudget = totalHours >= budgetHours`.

### Rationale

Centralizing the authoritative calculation on the backend keeps the frontend presentation purely display-focused, ensures consistent burn metrics across all present and future clients, and avoids duplicating mapping logic.

### Impact

Backend `TaskWorkInProgressDto` in `WorkInProgressDto.cs` gains `Complexity`, `BudgetHours`, `BurnPercentage`, and `IsOverBudget`.

### Architecture Impact

Extends WIP read-model query, row mapping, and DTO contract in architecture TD-003 and TD-004.

### Resolved By

User & Analyst

### Resolved Date

2026-10-15

---

## GAP-002

Status: CLOSED

### Decision

Update the TypeScript `TaskWorkInProgressDto` interface in `src/api/requests.ts` to include:
- `complexity: number`
- `budgetHours: number`
- `burnPercentage: number`
- `isOverBudget: boolean`

### Rationale

Ensures end-to-end type safety between backend responses and the frontend UI components.

### Impact

Frontend API contract updated without breaking existing callers.

### Architecture Impact

Frontend read-model contract update in architecture TD-005.

### Resolved By

User & Analyst

### Resolved Date

2026-10-15

---

## GAP-003

Status: CLOSED

### Decision

Re-implement `WorkInProgressView.vue` layout into a high-density, compact continuous feed using single-line task rows, reduced card padding, and tight vertical spacing, faithfully reproducing the validated prototype `scr-req-006-redesign.html`.

### Rationale

Significantly increases information density (~3x more visible items per viewport), eliminating nested card sprawl while keeping all essential operational telemetry visible.

### Impact

`WorkInProgressView.vue` template and scoped styles updated in-place. All existing `data-testid` and `data-screen-id` attributes are preserved verbatim.

### Architecture Impact

Frontend view architecture update in TD-006.

### Resolved By

User & Analyst

### Resolved Date

2026-10-15

---

## GAP-004

Status: CLOSED

### Decision

Implement elapsed time urgency tiers using fixed frontend styling classes and thresholds:
- `≤ 4.0h`: Normal (calm green)
- `> 4.0h`: Watch (amber with subtle glow)
- `> 8.0h`: Escalate (red with prominent glow)

Render the elapsed time as a prominent monospaced counter on the right-hand column of the task row.

### Rationale

Provides an immediate, high-contrast visual signal allowing supervisors to spot tasks with excessive duration at a single glance.

### Impact

Computed styling properties and CSS classes in `WorkInProgressView.vue`.

### Architecture Impact

Included in frontend view design specification.

### Resolved By

User & Analyst

### Resolved Date

2026-10-15

---

## GAP-005

Status: CLOSED

### Decision

Render the budget burn signal directly beneath the pure elapsed time display:
- A thin horizontal progress bar displaying `burnPercentage` (green for `<75%`, amber for `75%–99%`, red for `≥100%`).
- Companion label showing elapsed vs budget (e.g., `4h 0m / 8h`).
- A complexity badge showing `C{complexity}` (e.g. `C3`).
- A bright red `OVER` badge when `isOverBudget` is true.

### Rationale

Fulfills the dual-signal operational requirement: supervisors can immediately see whether a task is consuming its allocated effort budget, allowing them to differentiate between naturally large tasks and stalled or underestimated small tasks.

### Impact

Dual-signal layout componentry in active and paused task rows in `WorkInProgressView.vue`.

### Architecture Impact

Included in frontend view design specification.

### Resolved By

User & Analyst

### Resolved Date

2026-10-15

---

## GAP-006

Status: CLOSED

### Decision

Apply visual workload escalation to person rows using a color-coded left border and paused count badges:
- `0–1` paused tasks: Normal (green left border)
- `2–3` paused tasks: Busy (amber left border, `N paused` amber badge)
- `4+` paused tasks: Overloaded (red left border, `N paused ⚠` red badge)

### Rationale

Allows operational leads scanning down the board to instantly spot assignees struggling with excessive context-switching or blocked tasks.

### Impact

Person feed item dynamic classes and badge formatting in `WorkInProgressView.vue`.

### Architecture Impact

Included in frontend view design specification.

### Resolved By

User & Analyst

### Resolved Date

2026-10-15

---

## GAP-007

Status: CLOSED

### Decision

Enhance the top StatusStrip ribbon with an `over budget` KPI metric counting the number of persons who have at least one over-budget task (`isOverBudget`), and add an `Over Budget` quick-filter pill alongside `All`, `Working`, and `Busy`.

### Rationale

Aligns the top-level awareness strip with the person-centric feed, allowing one-click filtering to personnel requiring immediate budget intervention.

### Impact

StatusStrip items configuration and filter computed properties in `WorkInProgressView.vue`.

### Architecture Impact

Included in frontend view design specification.

### Resolved By

User & Analyst

### Resolved Date

2026-10-15

---

## OQ-001

Status: CLOSED

### Decision

Compute the Complexity-to-hours mapping (1=2h, 2=4h, 3=8h, 4=16h, 5=24h), `BudgetHours`, `BurnPercentage`, and `IsOverBudget` on the backend during DTO assembly in `RequestQueryService.cs`, returning them directly in `TaskWorkInProgressDto`.

### Rationale

Centralizes authoritative calculation logic on the server, ensuring all clients receive consistent pre-calculated budget and burn figures while keeping the frontend purely presentational.

### Impact

Backend query service logic and DTO properties.

### Architecture Impact

Defines backend calculation responsibility in TD-003 and TD-004.

### Resolved By

User & Analyst

### Resolved Date

2026-10-15

---

## OQ-002

Status: CLOSED

### Decision

Define visual urgency thresholds (elapsed time: 4h watch / 8h escalate; paused tasks: 2 busy / 4 overloaded) as fixed constants in the frontend presentation layer.

### Rationale

Provides simple, direct threshold evaluation without introducing unnecessary database tables or deployment dependencies.

### Impact

Frontend constants in `WorkInProgressView.vue`.

### Architecture Impact

Frontend presentation tier responsibility.

### Resolved By

User & Analyst

### Resolved Date

2026-10-15

---

## OQ-003

Status: CLOSED

### Decision

Label the rating and budget strictly as "Complexity" in the UI (e.g., `Complexity: 3 (8h)` in the inspection drawer and `Complexity 3` in task details), maintaining 100% naming consistency with the domain model and database column.

### Rationale

Eliminates terminology divergence between the user interface and domain aggregates/codebase.

### Impact

Labels in `WorkInProgressView.vue` and `SlideOverDrawer`.

### Architecture Impact

UI specification alignment.

### Resolved By

User & Analyst

### Resolved Date

2026-10-15

---

# 9. Architecture Applicability

## Decision

ARCHITECTURE-REQUIRED

## Rationale

This change requires formal target-state definition because it involves:

1. A backend read-model change: `TaskWorkInProgressDto` gains new fields (`Complexity`, `BudgetHours`, `BurnPercentage`, `IsOverBudget`), and the SQL query and row mapping in `GetWorkInProgressOverviewAsync` must be updated.
2. A frontend contract change: `TaskWorkInProgressDto` TypeScript interface in `requests.ts` must be extended.
3. A substantial structural redesign of `WorkInProgressView.vue` (template, scoped styles, computed properties for dual-signal and urgency tiers).
4. Backend calculation rules for Complexity-to-hours mapping and burn percentage.

These span both backend and frontend layers and include meaningful structural changes to an existing screen and read-model contract.

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

All critical gaps (GAP-001 through GAP-007) and open questions (OQ-001 through OQ-003) have been formally resolved and recorded with explicit decisions, rationales, and architectural impacts. Gate READY-FOR-PLANNING is formally granted by the Architect. Target architecture is defined in CR-034-ARCHITECTURE.md.

READY-FOR-PLANNING means that feasibility is sufficiently resolved for the Architecture skill to finalize or update the target architecture. It does not mean that architecture is complete or that the architecture step may be skipped. Planning begins only after the required architecture work is complete.

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-034-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-034-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- FEATURE: [FEAT-MGT-004-review-programmer-workload.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-MGT-004-review-programmer-workload.md)
- PRIOR ARCHITECTURE: [CR-021-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-021-ARCHITECTURE.md)
- PRIOR ARCHITECTURE: [CR-022-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-022-ARCHITECTURE.md)
- PRIOR FEASIBILITY: [CR-021-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-021-FEASIBILITY-ASSESSMENT.md)

Referenced codebase locations:

- [Request.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs#L33-L34) — `Complexity` domain field
- [0011_request_complexity.sql](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Migrations/Scripts/0011_request_complexity.sql) — `[Complexity]` column migration
- [RequestQueryService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestQueryService.cs#L726-L909) — `GetWorkInProgressOverviewAsync` and `WorkInProgressRequestRow`
- [WorkInProgressDto.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Models/WorkInProgressDto.cs) — backend `TaskWorkInProgressDto` and `PersonWorkInProgressDto`
- [requests.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/requests.ts#L409-L440) — frontend `TaskWorkInProgressDto` interface
- [WorkInProgressView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkInProgressView.vue) — current screen implementation

Referenced documents:

- [scr-req-006-redesign.html](file:///C:/Users/drury/.gemini/antigravity/brain/4ee110c7-9733-4e04-845f-cdd74ba21e36/scr-req-006-redesign.html) — validated interactive prototype for the dual-signal compact design
