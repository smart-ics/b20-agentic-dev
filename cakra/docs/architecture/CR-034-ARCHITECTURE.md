---
Title: Compact Operational Awareness Board Redesign with Dual-Signal Elapsed and Effort-to-Limit Indicators Architecture (CR-034)
Code: CR-034
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-15
---

# 1. Overview

This architecture defines the technical realization for Change Request `CR-034`: Compact redesign of the Work in Progress (WIP) operational awareness board (`SCR-REQ-006`), featuring two independent visual urgency signals (pure elapsed time heat and effort-to-limit budget burn) and quick-glance workload triage indicators.

It consumes and realizes the approved feasibility decisions from [CR-034-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-034-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`), establishing:

1. Server-side budget and burn calculation during WIP read-model projection in `RequestQueryService.GetWorkInProgressOverviewAsync`, mapping domain `Complexity` (1–5) to budget hours (2h, 4h, 8h, 16h, 24h), computing `BurnPercentage`, and determining `IsOverBudget`.
2. Extension of `TaskWorkInProgressDto` in both C# backend and TypeScript frontend contracts to expose `Complexity`, `BudgetHours`, `BurnPercentage`, and `IsOverBudget`.
3. High-density frontend presentation in `WorkInProgressView.vue` replacing spacious multi-card nesting with tight, single-line continuous feed rows based on the validated prototype `scr-req-006-redesign.html`.
4. Dual time signal visualization per task:
   - **Signal 1 (Pure Elapsed Time)**: Large monospaced counter heat-colored by absolute duration (≤4h normal, >4h watch, >8h escalate).
   - **Signal 2 (Budget Burn)**: Horizontal burn bar showing percentage of budget consumed, companion elapsed/budget fraction, complexity pill (`C{n}`), and an `OVER` badge when over 100%.
5. Workload overload indicators: Assignee left-border color escalation (green for 0–1 paused, amber for 2–3 paused, red for 4+ paused) and paused count badges.
6. Enhanced StatusStrip ribbon with an `over budget` KPI count, and an `Over Budget` quick-filter pill.
7. 100% preservation of all existing automated test selectors (`data-screen-id="SCR-REQ-006"`, `data-testid` attributes).

---

# 2. Architectural Basis

## Business Context

- ISSUE: [CR-034-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-034-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- FEATURE: [FEAT-MGT-004-review-programmer-workload.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-MGT-004-review-programmer-workload.md)
- PRIOR ARCHITECTURE: [CR-021-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-021-ARCHITECTURE.md) (WIP tracking and single in-progress task policy)
- PRIOR ARCHITECTURE: [CR-022-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-022-ARCHITECTURE.md) (UI foundation and base components)
- APPROVED PROTOTYPE: [scr-req-006-redesign.html](file:///C:/Users/drury/.gemini/antigravity/brain/4ee110c7-9733-4e04-845f-cdd74ba21e36/scr-req-006-redesign.html)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-034-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-034-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

This architecture consumes and realizes the approved decisions:
- `GAP-001` & `OQ-001`: Server-side `Complexity` selection, row mapping, and budget/burn calculation.
- `GAP-002`: Frontend TypeScript DTO contract extension.
- `GAP-003`: Dense continuous feed layout re-implementation of `WorkInProgressView.vue`.
- `GAP-004` & `OQ-002`: Elapsed time visual urgency tiers (≤4h, >4h, >8h) as frontend constants.
- `GAP-005`: Dual-signal visualization with burn progress bar and `OVER` badge.
- `GAP-006`: Assignee workload left-border escalation (0–1, 2–3, 4+) and badges.
- `GAP-007`: StatusStrip `over budget` KPI item and quick-filter pill.
- `OQ-003`: Strict UI labeling as "Complexity" to align with domain and database terminology.

---

# 3. Scope

## Included

1. **Backend Query & Projection (`Cakra.Modules.Request.Services.RequestQueryService`)**:
   - Update `activeRequestsSql` to include `r.[Complexity]`.
   - Update `WorkInProgressRequestRow` internal class to include `public int Complexity { get; init; }`.
   - Introduce static calculation helper `GetBudgetHoursForComplexity(int complexity)`.
   - Compute `BurnPercentage` and `IsOverBudget` during `TaskWorkInProgressDto` instantiation in `GetWorkInProgressOverviewAsync`.
2. **Backend Read Model DTO (`Cakra.Modules.Request.Models.WorkInProgressDto`)**:
   - Extend `TaskWorkInProgressDto` with:
     - `public int Complexity { get; init; } = 1;`
     - `public double BudgetHours { get; init; } = 2.0;`
     - `public int BurnPercentage { get; init; }`
     - `public bool IsOverBudget { get; init; }`
3. **Frontend API Client Contract (`src/frontend/Cakra.Web/src/api/requests.ts`)**:
   - Extend `TaskWorkInProgressDto` interface with `complexity`, `budgetHours`, `burnPercentage`, and `isOverBudget`.
4. **Frontend Screen Redesign (`src/frontend/Cakra.Web/src/views/WorkInProgressView.vue`)**:
   - High-density continuous feed layout.
   - Dual-signal display componentry on task rows.
   - Color-coded left border on person rows reflecting paused workload tiers.
   - `Over Budget` KPI in `StatusStrip` and `Over Budget` filter pill.
   - Contextual `SlideOverDrawer` updated to display the dual time signals and `Complexity: {complexity} ({budgetHours}h)`.
   - Scoped styles implementing heat colors, subtle row glows, and progress bar animation.
5. **Selector Preservation & Testing**:
   - 100% preservation of all existing `data-testid` and `data-screen-id="SCR-REQ-006"` attributes.

## Excluded

1. Database schema changes (the `[Complexity]` column already exists on `[request].[Requests]`).
2. Domain aggregate mutations (commands `StartWork`, `PauseWork`, `SetComplexity` remain unchanged).
3. Configurable database threshold tables (thresholds remain display-layer constants for this release).

---

# 4. Technical Decisions

## TD-001: Server-Side Complexity-to-Budget and Burn Calculation

The domain aggregate `Request` persists numerical complexity as an integer between 1 and 5 (`[request].[Requests].[Complexity]`). In accordance with the approved decision in `GAP-001` and `OQ-001`, the conversion of complexity to effort budget hours and the resulting burn percentage are calculated on the backend during read-model projection.

### Budget Hours Formula

```csharp
public static double GetBudgetHoursForComplexity(int complexity) => complexity switch
{
    1 => 2.0,   // 1/4 day
    2 => 4.0,   // 1/2 day
    3 => 8.0,   // 1 day
    4 => 16.0,  // 2 days
    5 => 24.0,  // 3 days
    _ => 2.0    // Fallback for unassigned/out-of-range
};
```

### Burn Metrics Calculation

For each active task:
```csharp
var budgetHours = GetBudgetHoursForComplexity(req.Complexity);
var burnPercentage = budgetHours > 0
    ? (int)Math.Round((totalHours / budgetHours) * 100.0)
    : 0;
var isOverBudget = totalHours >= budgetHours;
```

This ensures that:
- The calculation is authoritative and identical for all clients.
- The frontend is strictly presentational and does not duplicate mapping formulas.
- If future endpoints or reports consume `TaskWorkInProgressDto`, budget burn metrics remain fully consistent.

---

## TD-002: Backend Read Model & Query Enhancement

In `Cakra.Modules.Request/Services/RequestQueryService.cs`:

1. Update `activeRequestsSql`:
```sql
SELECT
    r.[Id],
    r.[Title],
    r.[Description],
    r.[RequestType],
    r.[Status],
    r.[Priority],
    r.[Complexity],
    r.[CustomerId],
    r.[ProductId],
    r.[WorkPackageId],
    r.[OwnerPersonId],
    r.[CreatedAt],
    r.[UpdatedAt]
FROM [request].[Requests] r
WHERE r.[Status] IN ('IN_PROGRESS', 'PAUSED')
  AND r.[OwnerPersonId] IS NOT NULL
ORDER BY r.[UpdatedAt] DESC, r.[CreatedAt] DESC, r.[Id] ASC;
```

2. Update `WorkInProgressRequestRow`:
```csharp
private sealed class WorkInProgressRequestRow
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string RequestType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = "NORMAL";
    public int Complexity { get; init; } = 1;
    public Guid? CustomerId { get; init; }
    public Guid? ProductId { get; init; }
    public Guid? WorkPackageId { get; init; }
    public Guid? OwnerPersonId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
```

3. Update `TaskWorkInProgressDto` in `Cakra.Modules.Request/Models/WorkInProgressDto.cs`:
```csharp
public sealed record TaskWorkInProgressDto
{
    // ... existing properties ...

    /// <summary>Numerical complexity rating (1 to 5) indicating estimated effort scope.</summary>
    public int Complexity { get; init; } = 1;

    /// <summary>Allocated budget hours derived from Complexity (×1=2h, ×2=4h, ×3=8h, ×4=16h, ×5=24h).</summary>
    public double BudgetHours { get; init; } = 2.0;

    /// <summary>Percentage of allocated budget hours elapsed (TotalInProgressHours / BudgetHours * 100).</summary>
    public int BurnPercentage { get; init; }

    /// <summary>Indicates whether cumulative elapsed hours meet or exceed the allocated budget.</summary>
    public bool IsOverBudget { get; init; }
}
```

---

## TD-003: Frontend TypeScript Contract Alignment

In `src/frontend/Cakra.Web/src/api/requests.ts`:

```typescript
export interface TaskWorkInProgressDto {
  requestId: string
  title: string
  description: string
  requestType: string
  status: string
  priority: string
  complexity: number
  budgetHours: number
  burnPercentage: number
  isOverBudget: boolean
  customerId?: string | null
  customerName?: string | null
  customerCode?: string | null
  productId?: string | null
  workPackageId?: string | null
  totalInProgressSeconds: number
  totalInProgressHours: number
  totalInProgressFormatted: string
  createdAt: string
  updatedAt?: string | null
  lastStartedAt?: string | null
}
```

---

## TD-004: Visual Urgency and Workload Overload Tier Specification

In `WorkInProgressView.vue`, visual urgency is determined using typed constants and helper functions:

### 1. Pure Elapsed Duration Tiers (Signal 1: Programmer in Trouble)

| Elapsed Hours | Heat Tier | CSS Class | Visual Cue | Operational Meaning |
|---|---|---|---|---|
| `≤ 4.0h` | Normal | `heat-ok` (`#22c55e`) | Calm green monospaced text | Healthy progress |
| `> 4.0h` and `≤ 8.0h` | Watch | `heat-warn` (`#f59e0b`) | Amber text with subtle glow | Watch closely / potential blocker |
| `> 8.0h` | Escalate | `heat-crit` (`#ef4444`) | Bright red text with high-intensity text-shadow glow | Immediate supervisor escalation |

```typescript
function elapsedDurationTier(hours: number): 'ok' | 'warn' | 'crit' {
  if (hours > 8.0) return 'crit'
  if (hours > 4.0) return 'warn'
  return 'ok'
}
```

### 2. Budget Burn Tiers (Signal 2: Scope & Budget Consumption)

| Burn % | Burn Tier | Bar Fill Class | Visual Treatment |
|---|---|---|---|
| `< 75%` | On Track | `bh-ok` (`#22c55e`) | Thin green bar fill |
| `75%–99%` | Near Limit | `bh-warn` (`#f59e0b`) | Thin amber bar fill |
| `≥ 100%` | Over Budget | `bh-crit` (`#ef4444`) | Thin red bar fill + animated pulsing overflow pip + `OVER` red badge |

### 3. Assignee Workload Tiers (Programmer in Busy)

| Paused Task Count | Workload Tier | Left Border Style | Badge |
|---|---|---|---|
| `0–1` | Normal | `3px solid rgba(34, 197, 94, 0.45)` | Standard `N active` |
| `2–3` | Busy | `3px solid #f59e0b` | `N paused` (amber) |
| `4+` | Overloaded | `3px solid #ef4444` | `N paused ⚠` (red) |

```typescript
function personWorkloadTier(pausedCount: number): 'normal' | 'busy' | 'overloaded' {
  if (pausedCount >= 4) return 'overloaded'
  if (pausedCount >= 2) return 'busy'
  return 'normal'
}
```

---

## TD-005: Dual Time Signal Component Architecture

Each task row (both the prominent in-progress card and compact paused task rows) houses a stacked right-hand column rendering both signals independently:

```text
┌──────────────────────────────────────────────────────────────┐
│ [⚡] Task Title here...    [Priority] [Customer]             │
│      REQ-1042 · Started Oct 15 · 08:00                       │
│                                           11h 0m      [①]    │
│                                      [███████████]    [②]    │
│                                      11h/8h · C3 OVER        │
└──────────────────────────────────────────────────────────────┘
```

### Signal Decoupling Principle
Because Signal 1 (absolute elapsed hours) and Signal 2 (budget burn percentage) use independent calculations, they can legitimately disagree, surfacing crucial operational insights:
- **Elapsed Green + Burn Red (OVER)**: Low-complexity task (e.g. `Complexity 1`, 2h limit) that has run for 3 hours. Absolute time is low, but budget is blown by 150%.
- **Elapsed Red + Burn Green**: High-complexity task (e.g. `Complexity 5`, 24h limit) running for 10 hours. Duration is long, but well within the allocated budget (41%).

---

## TD-006: Layout Density, Operational Ribbon & Filter Architecture

1. **Information Density**:
   - Person feed rows use a compact border layout (`padding-left: 12px; border-left: 3px solid ...`) without bulky outer cards.
   - Paused tasks render as tight single-line interactive rows (`padding: 4px 8px`).
   - Active tasks use streamlined padding (`padding: 8px 12px`).
2. **Top StatusStrip Ribbon**:
   - `working`: Count of persons with active work (`kpi-active-people`).
   - `tasks active`: Count of active in-progress tasks (`kpi-in-progress-tasks`).
   - `paused`: Count of total paused tasks (`kpi-paused-tasks`).
   - `over budget`: Count of persons with at least one over-budget task (`kpi-over-budget-people`), rendered with a red dot.
3. **Filter Toolbar**:
   - `All`: All active people.
   - `Working`: People currently working on an in-progress task.
   - `Busy`: People with at least 1 paused task.
   - `Over Budget`: People where `inProgressTask?.isOverBudget || pausedTasks.some(t => t.isOverBudget)`.
4. **SlideOverDrawer Inspection**:
   - Side drawer preserves all existing metadata and adds the dual signal metrics block:
     - Pure Elapsed Time with heat tier text.
     - Budget Burn with bar, percentage, and `OVER` badge.
     - Labeled strictly as `Complexity: {complexity} ({budgetHours}h)`.

---

## TD-007: Preservation of Automated Test Selectors

Implementers must strictly preserve all automated test attributes:

| Selector Attribute | Element / Purpose |
|---|---|
| `data-screen-id="SCR-REQ-006"` | Root board container |
| `data-testid="wip-screen"` | Root screen container |
| `data-testid="wip-kpis"` | StatusStrip container |
| `data-testid="kpi-active-people"` | Working people KPI |
| `data-testid="kpi-in-progress-tasks"` | Active tasks KPI |
| `data-testid="kpi-paused-tasks"` | Paused tasks KPI |
| `data-testid="refresh-wip-button"` | Manual refresh button |
| `data-testid="wip-error-alert"` | Error alert message |
| `data-testid="wip-loading"` | Loading indicator |
| `data-testid="wip-empty-state"` | Empty state card |
| `data-testid="wip-persons-container"` | Persons feed container |
| `data-testid="person-card"` | Individual person row container |
| `:data-person-id="person.personId"` | Person identifier on row |
| `data-testid="person-status-active"` | Person working badge |
| `data-testid="person-status-paused"` | Person paused badge |
| `data-testid="person-tasks-count-badge"` | Total active tasks badge |
| `data-testid="in-progress-card"` | In-progress task row |
| `data-testid="task-title-link"` | Router link to request detail |
| `data-testid="task-priority-badge"` | Priority badge |
| `data-testid="task-customer-badge"` | Customer identifier badge |
| `data-testid="task-id-link"` | Request ID link |
| `data-testid="in-progress-time-badge"` | Elapsed time counter |
| `data-testid="task-view-link"` | Open request action link |
| `data-testid="idle-placeholder"` | Idle person placeholder |
| `data-testid="paused-tasks-list"` | Paused tasks list container |
| `data-testid="paused-task-card"` | Individual paused task row |
| `data-testid="paused-task-title-link"` | Paused task title router link |
| `data-testid="paused-task-priority-badge"` | Paused task priority badge |
| `data-testid="paused-task-customer-badge"` | Paused task customer badge |
| `data-testid="paused-task-id-link"` | Paused task ID link |
| `data-testid="paused-time-badge"` | Paused task elapsed time badge |
| `data-testid="paused-task-view-link"` | Paused task open link |
| `data-testid="empty-paused-tasks"` | Empty paused state |

---

# 5. Component Responsibilities

| Component | Responsibility |
|---|---|
| `RequestQueryService.cs` (`Cakra.Modules.Request`) | Queries active requests with `Complexity`; computes cumulative elapsed time; maps complexity to budget hours; computes `BurnPercentage` and `IsOverBudget`; groups tasks by owner into DTOs. |
| `WorkInProgressDto.cs` (`Cakra.Modules.Request`) | Authoritative backend read-model contracts (`TaskWorkInProgressDto`, `PersonWorkInProgressDto`). |
| `RequestsController.cs` (`Cakra.Api`) | Exposes `GET /api/v1/requests/wip` to authenticated callers. |
| `requests.ts` (`src/frontend/Cakra.Web/src/api/`) | Frontend TypeScript API client and interface definitions. |
| `WorkInProgressView.vue` (`src/frontend/Cakra.Web/src/views/`) | Renders compact awareness board, dual time signals, workload escalation, KPI ribbon, filter pills, search filtering, and inspection drawer. |
| `SlideOverDrawer.vue` (`src/frontend/Cakra.Web/src/components/base/`) | Contextual slide-over drawer for deep inspection of active/paused task telemetry. |

---

# 6. Integration Design

| Source | Target | Purpose |
|---|---|---|
| `WorkInProgressView.vue` | `requests.ts` (`getWorkInProgressOverview`) | Dispatches HTTP GET to retrieve real-time WIP board data. |
| `requests.ts` | `RequestsController.cs` (`GET /api/v1/requests/wip`) | Authenticated REST API request across network boundary. |
| `RequestsController.cs` | `IRequestQueryService.cs` | Dispatches query execution to application query service. |
| `RequestQueryService.cs` | `[request].[Requests]` & `[request].[RequestAssignments]` | Queries active requests (including `Complexity`) and interval audit records via Dapper. |
| `RequestQueryService.cs` | `IOrganizationQueryService` / `ICustomerQueryService` | Enriches person full names and customer display codes. |
| `WorkInProgressView.vue` | `router` (`/requests/{id}`) | Navigates operator directly to request details on click. |

---

# 7. Data Ownership

| Data | Owner | Storage Location |
|---|---|---|
| Request Core Attributes & `Complexity` | Request Module | `[request].[Requests].[Complexity]` |
| Request Assignment Intervals | Request Module | `[request].[RequestAssignments]` |
| WIP Aggregated Read Model | Request Module | Transient query projection (`PersonWorkInProgressDto`) |
| Organization Person Names | Organization Module | `[org].[Persons]` |
| Customer Names & Codes | Customer Module | `[cust].[Customers]` |

---

# 8. Database Design

## New Tables
None.

## Modified Tables
None. The `[Complexity]` column already exists on `[request].[Requests]` (added in `0011_request_complexity.sql`, `INT NOT NULL CONSTRAINT [DF_Requests_Complexity] DEFAULT (1)`).

## Relationships
Unchanged.

## Migration Considerations
Zero database migrations required. The query simply includes `r.[Complexity]` in the existing Dapper query.

---

# 9. Cross-Cutting Concerns

1. **Performance & Scalability**:
   - `Complexity` is retrieved within the single existing SQL query against `[request].[Requests]`. Zero additional roundtrips or joins are introduced.
   - Budget hours mapping and burn percentage computation are O(1) arithmetic operations executed in-memory during DTO assembly.
2. **Resilience & Fallbacks**:
   - If a request has an unassigned or anomalous complexity (< 1 or > 5), the fallback logic maps it safely to `Complexity = 1` and `BudgetHours = 2.0`.
   - If `BudgetHours` is 0, `BurnPercentage` safely returns 0 without divide-by-zero exceptions.
3. **Responsive Display**:
   - Compact layout supports viewports from mobile (≥360px) to desktop (1440px+), using CSS flex wrapping (`flex-col flex-sm-row`) on narrow screens to prevent horizontal overflow.

---

# 10. Implementation Constraints

1. **Authoritative Calculation in Backend**:
   - `BudgetHours`, `BurnPercentage`, and `IsOverBudget` must be calculated in `RequestQueryService.cs` and returned in `TaskWorkInProgressDto`. Do not re-compute budget hours in frontend template logic.
2. **Naming Consistency**:
   - Use `Complexity` (not `Effort`) in all backend properties, DTOs, and UI labels.
3. **Automated Test Compatibility**:
   - Do not remove, rename, or reorder any existing `data-testid` or `data-screen-id` attributes.
   - Ensure `data-testid="in-progress-card"` and `data-testid="paused-task-card"` remain clickable to trigger drawer inspection.
4. **Zero Third-Party Frontend Dependencies**:
   - The dual-signal progress bars and glows must be styled using pure Tailwind CSS utility classes and scoped CSS, without external chart or UI widget packages.

---

# 11. Acceptance Conditions

1. **Backend Query & DTO**:
   - `GET /api/v1/requests/wip` returns `TaskWorkInProgressDto` items containing populated `complexity`, `budgetHours`, `burnPercentage`, and `isOverBudget`.
   - Tasks with `Complexity = 1` have `budgetHours = 2.0`; `Complexity = 3` has `budgetHours = 8.0`; `Complexity = 5` has `budgetHours = 24.0`.
   - When `totalInProgressHours >= budgetHours`, `isOverBudget` is `true`.
2. **Frontend Contract**:
   - `src/api/requests.ts` defines `complexity`, `budgetHours`, `burnPercentage`, and `isOverBudget` on `TaskWorkInProgressDto` with clean TypeScript compilation (`npm run build` succeeds).
3. **Screen Density & Aesthetics**:
   - `WorkInProgressView.vue` renders in a dense continuous feed layout matching `scr-req-006-redesign.html`.
4. **Dual Time Signal Visualization**:
   - Active and paused tasks display the elapsed time counter colored according to duration thresholds (≤4h green, >4h amber, >8h red).
   - Tasks display the thin burn progress bar colored by burn percentage, accompanied by elapsed/budget hours text and `C{n}` badge.
   - Over-budget tasks prominently display the red `OVER` badge and have subtle row tinting.
5. **Workload Overload Indicators**:
   - Person rows with 2–3 paused tasks display an amber left border and `N paused` badge.
   - Person rows with 4+ paused tasks display a red left border and `N paused ⚠` badge.
6. **KPI Ribbon & Filtering**:
   - `StatusStrip` displays the `over budget` count of persons.
   - The `Over Budget` filter pill isolates persons with over-budget tasks.
7. **Test Suite Verification**:
   - All existing automated frontend tests for `SCR-REQ-006` pass without regression.
