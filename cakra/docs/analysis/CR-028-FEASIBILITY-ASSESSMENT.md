---
Title: Feasibility Assessment for Quick Capture Complexity Detection in SCR-WP-001
Code: CR-028
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-08
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Feature being assessed: Automated numerical complexity detection and recording during quick capture in `SCR-WP-001` (`WorkPackageView.vue`), as formally captured in [CR-028-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-028-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-028-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-028-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- DOMAIN: [work-package-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/work-package-domain.md)
- REFERENCE ISSUE: [CR-019-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-019-ISSUE.md)
- REFERENCE ARCHITECTURE: [CR-019-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-019-ARCHITECTURE.md)

## Objective

Assess the technical feasibility, implementation constraints, gap closure resolutions, and planning readiness to:

1. Parse complexity ratings from multi-line task strings during quick capture in `SCR-WP-001` (`WorkPackageView.vue`).
2. Detect trailing numeric point notations (1 to 5 points, supporting `pt`, `pts`, `point`, `points` with optional brackets/parentheses) anchored to the end of the line.
3. Strip the matched complexity expression from the recorded task title to ensure clean Request titles.
4. Default to 1 point when no valid complexity pattern is detected or when numbers fall outside the 1–5 range.
5. Display a read-only complexity badge on candidate task items in both the Create Work Package modal and the Scope Management Quick Bulk Add panel.
6. Pass the parsed numerical `complexity` in the request creation payload (`recordRequest`) so captured requests are persisted with their designated complexity.

---

# 2. Current State

## Existing Behavior

1. **Backend Command & API Support (`Cakra.Modules.Request` / `RequestsController.cs`)**:
   - `RecordRequestCommand` in [RequestCommands.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs#L15-L27) already includes `int? Complexity = null`.
   - `RecordRequestCommandValidator` enforces that when `Complexity` is present, it must be `InclusiveBetween(1, 5)`.
   - `RequestsController.cs` maps `request?.Complexity` directly from the incoming HTTP body into `RecordRequestCommand`.
   - When omitted or null, the backend domain defaults request complexity to `1`.
2. **Frontend API Client (`src/frontend/Cakra.Web/src/api/requests.ts`)**:
   - `CreateRequestPayload` interface already defines `complexity?: number`.
   - `recordRequest` passes `CreateRequestPayload` directly to `POST /api/v1/requests`.
   - Task parsing utility `parseTaskListText(rawText: string)` only splits lines by newline, normalizes prefixes (bullets, numbers, checkboxes), and caps length at 255 characters, returning `string[]` (titles only).
3. **Frontend Work Package View (`src/frontend/Cakra.Web/src/views/WorkPackageView.vue`)**:
   - In the Create Work Package modal, candidate tasks are tracked in `createCandidateTasks = ref<string[]>([])`.
   - In the Work Package Detail Scope Quick Bulk Add panel, candidate tasks are tracked in `scopeCandidateTasks = ref<string[]>([])`.
   - When invoking `recordRequest`, neither flow passes `complexity`, leaving it undefined so all created requests default to 1 point.
   - The candidate preview lists (`create-candidate-preview-container` and `scope-candidate-preview-container`) only display task title text and a delete button (`✕`), with no complexity badge.

## Existing Constraints

1. **Complexity Value Range**:
   - The backend strictly enforces `1 <= Complexity <= 5`. Any value `< 1` or `> 5` causes a validation error (`400 Bad Request`).
2. **Title Maximum Length**:
   - Request titles must not exceed 255 characters.
3. **End-of-Line Anchoring**:
   - Sentences frequently contain numbers in their body (e.g. `"Allow user re-login for 3 consecutive failed 3 points"`). Complexity detection must only match trailing tokens to avoid false positives.
4. **Scope Placement**:
   - SCR-WP-001 has two distinct quick capture entry points: the Create modal and the Work Package Scope Management Quick Bulk Add panel. Both must behave consistently.

---

# 3. Gap Analysis

| ID | Severity | Gap |
|------|------|------|
| GAP-001 | CRITICAL | `requests.ts` lacks a structured task parser that detects end-of-line complexity patterns, strips the pattern from the title, and returns `{ title: string, complexity: number }`. |
| GAP-002 | CRITICAL | `WorkPackageView.vue` candidate state (`createCandidateTasks`, `scopeCandidateTasks`) only stores string titles, and quick capture submission loops omit `complexity` from `recordRequest`. |
| GAP-003 | MAJOR | Candidate preview lists in `WorkPackageView.vue` do not render a complexity indicator/badge. |
| GAP-004 | MINOR | `requests.ts` exported helpers do not have dedicated unit tests covering complexity extraction, unit variants, bracketed expressions, and fallback behavior. |

---

# 4. Open Questions

| ID | Question | Impact | Status |
|------|------|------|------|
| OQ-001 | How should the complexity pattern be detected and anchored? | Determines regex pattern and prevents false positives in titles containing numbers. | CLOSED |
| OQ-002 | Should the complexity notation be stripped from the request title? | Determines whether saved titles are clean or retain point annotations. | CLOSED |
| OQ-003 | What should the default complexity be if no pattern is found? | Determines fallback value for tasks without point tags. | CLOSED |
| OQ-004 | How should out-of-range numbers (e.g. `8 pts` or `0 pts`) be handled? | Determines error handling vs ignoring and defaulting to 1 point. | CLOSED |
| OQ-005 | How should the parsed complexity be represented in the preview list? | Determines UI layout and interactivity of candidate task preview items. | CLOSED |
| OQ-006 | Which areas of SCR-WP-001 should support complexity detection? | Determines scope of implementation across Create modal and Detail panel. | CLOSED |

---

# 5. Assumptions

| ID | Assumption |
|------|------|
| ASM-001 | Backend `RecordRequestCommand` and REST API already fully support `int? Complexity` (1 to 5), requiring zero backend changes. |
| ASM-002 | Supported units are `pt`, `pts`, `point`, and `points` (case-insensitive) with optional whitespace and optional enclosing parentheses or brackets (`[]`, `()`, `{}`). |
| ASM-003 | When complexity notation is stripped from the end of the line, trailing whitespace is trimmed cleanly. |
| ASM-004 | Existing callers of `parseTaskListText` (if any outside `WorkPackageView.vue`) can remain intact or be complemented by a structured helper `parseCandidateTaskLine` / `parseTaskListTasks`. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|------|------|------|------|
| RISK-001 | Numbers mentioned in the body of a task (e.g., "3 consecutive failed attempts") trigger incorrect complexity matches. | Medium | Anchor regex strictly to the end of the string (`$`) with optional trailing whitespace/brackets. |
| RISK-002 | Out-of-range numbers (e.g., "8 pts") passed to backend trigger validation rejection (`InclusiveBetween(1, 5)`). | Medium | Restrict regex match strictly to digits `1` through `5` (`[1-5]`). Any other number is ignored as complexity, remains in the title, and defaults to 1 point. |
| RISK-003 | Empty title resulting from a line that contains only a complexity tag (e.g., `"- 3pts"`). | Low | After stripping complexity, lines with empty remaining title are filtered out during parsing. |

---

# 7. Recommendations

## Option A (Recommended): Shared Structured Helper in `requests.ts` & Typed Candidate State
- Define a candidate task interface:
  ```ts
  export interface CandidateTaskItem {
    title: string
    complexity: number
  }
  ```
- Implement `parseCandidateTaskLine(line: string): CandidateTaskItem | null` and `parseTaskListTasks(rawText: string): CandidateTaskItem[]` in `@/api/requests`.
- Regex: `/(?:[\(\[\{])?\s*([1-5])\s*(?:pts?|points?)\s*(?:[\)\]\}])?\s*$/i`.
- In `WorkPackageView.vue`:
  - Update `createCandidateTasks` and `scopeCandidateTasks` to `CandidateTaskItem[]`.
  - In candidate preview lists, render a styled read-only badge next to each title (e.g., `{{ task.complexity }} pt{{ task.complexity > 1 ? 's' : '' }}`).
  - In `handleCreateWorkPackage` and `handleBulkAddTasksToScope`, forward `complexity: task.complexity` to `recordRequest`.
- **Advantages:** Clean architectural separation, high testability, reusable across any screen performing quick capture, zero backend modifications needed.
- **Disadvantages:** None.

## Option B: Inline Regex Logic in `WorkPackageView.vue` Only
- Perform regex parsing directly inside `handleParseCreateTasks` and `handleParseScopeTasks`.
- **Advantages:** Localized to a single component file.
- **Disadvantages:** Code duplication between Create modal and Scope panel, hard to test parsing in isolation.

---

# 8. Gap Closure

## GAP-001 / OQ-001 / OQ-002: Complexity Regex Detection and Title Stripping
### Decision
Implement trailing regex pattern `/(?:[\(\[\{])?\s*([1-5])\s*(?:pts?|points?)\s*(?:[\)\]\}])?\s*$/i`. When a match occurs, extract the digit as integer complexity (1..5) and strip the matched suffix from the task title, trimming trailing whitespace.
### Rationale
End-of-line anchoring prevents accidental matches with numbers in user story sentences. Stripping the notation produces clean request titles without redundant text.
### Impact
Clean title extraction and accurate complexity detection.
### Architecture Impact
Client-side parsing helper in `src/frontend/Cakra.Web/src/api/requests.ts`.
### Resolved By
User & Analyst Alignment via `/grill-me`
### Resolved Date
2026-10-08

---

## OQ-003: Default Fallback Complexity
### Decision
If a task line does not contain any recognized points pattern, assign default complexity `1`.
### Rationale
Matches Cakra's standard baseline complexity and backend domain default.
### Impact
Every captured request will have an explicit, valid complexity score.
### Architecture Impact
None.
### Resolved By
User & Analyst Alignment via `/grill-me`
### Resolved Date
2026-10-08

---

## OQ-004: Out-of-Range Number Handling
### Decision
Strictly restrict regex detection to digits `1` to `5`. Any number outside this range (e.g., `8 pts`, `0 pts`) is not matched as complexity, remains untouched in the title, and receives the default complexity `1`.
### Rationale
Ensures 100% compliance with backend validator `InclusiveBetween(1, 5)` without rejecting quick-capture submissions.
### Impact
Eliminates HTTP 400 validation failures during batch request recording.
### Architecture Impact
None.
### Resolved By
User & Analyst Alignment via `/grill-me`
### Resolved Date
2026-10-08

---

## GAP-003 / OQ-005: Candidate Preview Presentation
### Decision
Display a read-only complexity badge (e.g. `1 pt` or `3 pts`) alongside each parsed candidate task item in the preview list.
### Rationale
Gives immediate visual confirmation of the parsed point score prior to saving.
### Impact
Enhanced user trust and error prevention.
### Architecture Impact
Component template update in `WorkPackageView.vue`.
### Resolved By
User & Analyst Alignment via `/grill-me`
### Resolved Date
2026-10-08

---

## GAP-002 / OQ-006: Dual Placement across SCR-WP-001
### Decision
Support complexity detection in both quick capture locations: the Create Work Package modal (`createCandidateTasks`) and the Work Package Scope Management Quick Bulk Add panel (`scopeCandidateTasks`). Forward `complexity` in all `recordRequest` payload calls.
### Rationale
Provides unified and consistent user experience across the entire Work Package screen.
### Impact
Consistent data capture across both initial planning and in-flight scope additions.
### Architecture Impact
State and handler updates in `WorkPackageView.vue`.
### Resolved By
User & Analyst Alignment via `/grill-me`
### Resolved Date
2026-10-08

---

# 9. Architecture Applicability

## Decision

ARCHITECTURE-NOT-REQUIRED

## Rationale

The backend domain (`RecordRequestCommand`), validation rules (`InclusiveBetween(1, 5)`), database persistence, and API controllers (`RequestsController.cs`) already fully support `Complexity` and have for multiple releases. This change is entirely localized to frontend parsing in `src/frontend/Cakra.Web/src/api/requests.ts` and UI candidate state and preview badges in `src/frontend/Cakra.Web/src/views/WorkPackageView.vue`. No structural changes, new components, cross-module boundaries, or schema migrations are introduced.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved
- [x] All required decisions recorded
- [x] All blocking open questions resolved
- [x] Architecture can be finalized or updated (Architecture evaluated as ARCHITECTURE-NOT-REQUIRED)

## Status

READY-FOR-PLANNING

## Notes

All feasibility gaps and open questions have been closed based on user alignment. The Architect has verified the criteria and formally granted the `READY-FOR-PLANNING` gate. Architecture Applicability is confirmed as `ARCHITECTURE-NOT-REQUIRED`.
