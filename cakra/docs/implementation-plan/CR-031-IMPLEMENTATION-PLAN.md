---
Title: Display Truncated RequestId in My Request Screen (SCR-REQ-004) Implementation Plan
Code: CR-031
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-15
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-031`: Visually compact the Request ID in the 'My Request' screen (`SCR-REQ-004`) from a full 36-character raw GUID into a clean, recognizable `#<8-chars>` prefix hash, maintaining access to the full GUID via hover tooltip, preserving direct route navigation, and normalizing search inputs to strip leading `#` symbols.

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- ISSUE: [CR-031-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-031-ISSUE.md)
- FEASIBILITY-ASSESSMENT: [CR-031-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-031-FEASIBILITY-ASSESSMENT.md) (Planning Readiness: `READY-FOR-PLANNING`)
- REFERENCE ARCHITECTURE: [CR-006-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-006-ARCHITECTURE.md) (My Requests Screen & Personal Workspace)

Architecture Applicability: ARCHITECTURE-NOT-REQUIRED

No architectural target-state artifact was required. Implementation relies on existing technical structure. Approved feasibility decisions are authoritative for the change. The current codebase is the source of current technical truth.

---

# 2. Planning Scope

This plan covers the frontend presentation and client-side filtering changes in `MyRequestsView.vue` along with automated unit testing and build verification:

1. **Truncated Request ID Presentation (`MyRequestsView.vue`)**:
   - Introduce a formatting helper `formatShortId(id?: string | null): string` returning `#${id.slice(0, 8)}`.
   - Update the Assigned Requests card grid header: replace `{{ req.id }}` with `{{ formatShortId(req.id) }}` and bind `:title="req.id"`.
   - Update the Assigned Sub-Tasks table Parent Request column: replace `{{ subTask.requestId }}` with `{{ formatShortId(subTask.requestId) }}` and bind `:title="subTask.requestId"`.
   - Retain full GUID route linking (`:to="'/requests/' + req.id"` / `navigateToDetail`) and all existing test attributes.
2. **Search Query Normalization (`MyRequestsView.vue`)**:
   - Normalize user search inputs in `filteredMyRequests` by stripping leading `#` before testing against `req.id`.
   - Normalize user search inputs in `filteredAssignedSubTasks` by stripping leading `#` before testing against `st.requestId`.
3. **Automated Unit Testing & Verification**:
   - Add unit tests verifying short ID formatting and search query normalization logic.
   - Run vitest test suite (`npm test`).
   - Run TypeScript type checks (`npm run type-check`) and production build (`npm run build`).

---

# 3. Dependencies

**External Dependencies:** None

**Slice Dependencies:**
- `P1-S01` has no dependencies (`Depends On: None`).
- `P2-S02` depends on `P1-S01` (`Depends On: P1-S01`).

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Truncated RequestId Presentation & Query Normalization | IMPLEMENTED | GO | 1/1 |
| P2 - Automated Verification & Test Coverage | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Truncated RequestId Presentation & Query Normalization

Implementation Status: IMPLEMENTED  
Review Status: GO

### P1-S01

Title: Truncate RequestId & Normalize Search Query in MyRequestsView

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective:
1. In `cakra/src/frontend/Cakra.Web/src/views/MyRequestsView.vue`, declare a component formatting helper function:
   ```typescript
   function formatShortId(id: string | null | undefined): string {
     if (!id) return ''
     return `#${id.slice(0, 8)}`
   }
   ```
2. In the Assigned Requests card grid template (lines 681–689):
   - Replace `{{ req.id }}` with `{{ formatShortId(req.id) }}`.
   - Bind `:title="req.id"` on `router-link` (`data-testid="my-request-id-link"`).
   - Ensure `:to="'/requests/' + req.id"` and `@click.stop` navigation remain bound to the full GUID.
3. In the Assigned Sub-Tasks table template (lines 902–904):
   - In the Parent Request column, replace `{{ subTask.requestId }}` with `{{ formatShortId(subTask.requestId) }}`.
   - Bind `:title="subTask.requestId"` on the monospace span element.
4. In the `filteredMyRequests` computed property (lines 145–157):
   - Normalize the search query by stripping a leading `#` (e.g. `const cleanQ = q.startsWith('#') ? q.slice(1).trim() : q`) so searching either `#a1b2c3d4` or `a1b2c3d4` matches against `req.id.toLowerCase()`.
5. In the `filteredAssignedSubTasks` computed property (lines 311–321):
   - Normalize `subTaskSearchQuery` by stripping a leading `#` so searching `#a1b2c3d4` matches against `st.requestId.toLowerCase()`.

Depends On: None

Repository: `cakra/src/frontend/Cakra.Web`

Completion Criteria:
- Assigned Requests cards render `#<8-chars>` in the card header.
- Assigned Sub-Tasks table renders `#<8-chars>` under the parent request title.
- Native tooltip reveals full 36-character GUID on hover over the ID in both tabs.
- Clicking the ID navigates to `/requests/:id` with the full GUID.
- Searching by short ID (with or without leading `#`) filters correctly in both tabs.

Implementation Notes:
- Declared and exported helper `formatShortId(id: string | null | undefined): string` returning `#${id.slice(0, 8)}` (or empty string if falsy).
- Bound `:title="req.id"` on `data-testid="my-request-id-link"` and replaced `{{ req.id }}` with `{{ formatShortId(req.id) }}` while preserving route destination `:to="`/requests/${req.id}`"`.
- Bound `:title="subTask.requestId"` on the parent request ID span and replaced `{{ subTask.requestId }}` with `{{ formatShortId(subTask.requestId) }}`.
- Normalized search queries in `filteredMyRequests` and `filteredAssignedSubTasks` using `cleanQ = q.startsWith('#') ? q.slice(1).trim() : q` matching against `req.id` / `subTask.requestId`.
- Verified type check passes with 0 errors via `npm run type-check` (`vue-tsc --noEmit`).
- Verified unit test suite passes with 73/73 tests via `npm test`.

Changed Files:
- `cakra/src/frontend/Cakra.Web/src/views/MyRequestsView.vue`

Notes:
- Non-breaking UI change: existing test IDs (`my-request-id-link`, `my-request-grid-card`, `parent-request-link`) are preserved.

---

## P2 - Automated Verification & Test Coverage

Implementation Status: IMPLEMENTED  
Review Status: GO

### P2-S02

Title: Automated Unit Testing & Frontend Verification

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective:
1. Add unit tests in `cakra/src/frontend/Cakra.Web/tests/unit/requests.spec.ts` (or a dedicated unit test suite) validating:
   - `formatShortId` formats a 36-character GUID to `#` followed by first 8 characters.
   - `formatShortId` handles null, undefined, and empty string safely.
   - Search query normalization correctly strips leading `#` and matches raw GUID.
2. Run vitest test suite (`npm test`) and confirm all unit tests pass.
3. Run TypeScript validation (`npm run type-check`) to ensure zero typing errors.
4. Run production build (`npm run build`) to ensure bundle compiles cleanly without warnings or errors.

Depends On: P1-S01

Repository: `cakra/src/frontend/Cakra.Web`

Completion Criteria:
- Unit tests for short ID formatting and search query normalization pass.
- All existing frontend unit tests continue to pass.
- `vue-tsc --noEmit` passes with 0 errors.
- `vite build` succeeds with 0 errors.

Implementation Notes:
- Added dedicated unit test suite in `cakra/src/frontend/Cakra.Web/tests/unit/my-requests.spec.ts` covering 24 comprehensive test cases:
  - `formatShortId`: formats 36-character GUIDs to `#<8-chars>`, safely handles shorter strings, null, undefined, and empty string.
  - `normalizeSearchQuery`: strips leading `#`, normalizes case and trims whitespace, handles queries with only `#`, empty string, null, and undefined.
  - `matchesRequestId`: validates case-insensitive raw GUID matching with or without `#`, partial ID substrings, and negative assertions for non-matching or empty queries.
  - Assigned Requests search filtering integration: verifies filtered list behavior against mock request datasets with short ID queries.
- Refactored `MyRequestsView.vue` script section to export `normalizeSearchQuery` and `matchesRequestId` helpers and used `normalizeSearchQuery` in `filteredMyRequests` and `filteredAssignedSubTasks`.
- Ran Vitest suite (`npm test`): 4/4 test files passed, 97/97 tests passed.
- Ran TypeScript validation (`npm run type-check`): `vue-tsc --noEmit` exited cleanly with 0 errors.
- Ran production build (`npm run build`): bundle compiled successfully with 0 errors.

Changed Files:
- `cakra/src/frontend/Cakra.Web/src/views/MyRequestsView.vue`
- `cakra/src/frontend/Cakra.Web/tests/unit/my-requests.spec.ts`

Notes:
- Ensures high regression safety for the personal workspace screen.

---

# 6. Change Log

- 2026-10-15: Initial plan created for CR-031 by Architect based on approved Feasibility Assessment (`ARCHITECTURE-NOT-REQUIRED`) and `READY-FOR-PLANNING` gate. Execution Approval granted.
