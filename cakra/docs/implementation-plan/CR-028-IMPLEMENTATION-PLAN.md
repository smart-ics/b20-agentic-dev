---
Title: Quick Capture Complexity Detection in SCR-WP-001 Implementation Plan
Code: CR-028
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-08
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-028`: Automated numerical complexity detection and recording during quick capture in `SCR-WP-001: Work Package Screen` (`WorkPackageView.vue`).

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- ISSUE: [CR-028-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-028-ISSUE.md)
- FEASIBILITY-ASSESSMENT: [CR-028-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-028-FEASIBILITY-ASSESSMENT.md) (Planning Readiness: `READY-FOR-PLANNING`)

Architecture Applicability: ARCHITECTURE-NOT-REQUIRED

No architectural target-state artifact was required. Implementation relies on existing technical structure. Approved feasibility decisions are authoritative for the change. The current codebase is the source of current technical truth.

---

# 2. Planning Scope

This plan covers the complete frontend implementation, UI enrichment, and automated testing across `Cakra.Web`:

1. **Parser Utility & Typed Contracts (`src/frontend/Cakra.Web/src/api/requests.ts`)**:
   - Define and export `CandidateTaskItem { title: string; complexity: number }`.
   - Implement `parseCandidateTaskLine(line: string): CandidateTaskItem | null` with trailing regex matching `/(?:[\(\[\{])?\s*([1-5])\s*(?:pts?|points?)\s*(?:[\)\]\}])?\s*$/i`.
   - Implement `parseTaskListTasks(rawText: string): CandidateTaskItem[]` handling multiline splitting, prefix normalization, title trimming, length cap (255 characters), and 1 pt default fallback.
   - Maintain backward compatibility for `parseTaskListText`.
2. **Create Work Package Modal Integration (`src/frontend/Cakra.Web/src/views/WorkPackageView.vue`)**:
   - Transition `createCandidateTasks` to `CandidateTaskItem[]`.
   - Update `handleParseCreateTasks` to parse into structured candidate tasks.
   - Render a read-only complexity badge (`[N pts]`) next to each candidate task in `create-candidate-preview-container`.
   - Pass `complexity: task.complexity` in `handleCreateWorkPackage` when recording requests.
3. **Scope Management Quick Bulk Add Integration (`src/frontend/Cakra.Web/src/views/WorkPackageView.vue`)**:
   - Transition `scopeCandidateTasks` to `CandidateTaskItem[]`.
   - Update `handleParseScopeTasks` to parse into structured candidate tasks.
   - Render a read-only complexity badge (`[N pts]`) next to each candidate task in `scope-candidate-preview-container`.
   - Pass `complexity: task.complexity` in `handleBulkAddTasksToScope` when recording requests.
4. **Verification & Automated Testing**:
   - Add automated unit tests covering all point notation formats, bracketed expressions, embedded numbers, out-of-range inputs, and defaults.
   - Verify frontend TypeScript type-checking and production build.

---

# 3. Dependencies

**External Dependencies:** None

**Slice Dependencies:**
- `P1-S01` has no dependencies (`Depends On: None`).
- `P2-S02` depends on `P1-S01`.
- `P2-S03` depends on `P1-S01`.
- `P3-S04` depends on `P2-S02, P2-S03`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Core Parser Utility & Data Contracts | IMPLEMENTED | GO | 1/1 |
| P2 - SCR-WP-001 Quick Capture UI Integration | IMPLEMENTED | GO | 2/2 |
| P3 - Verification & Automated Testing | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Core Parser Utility & Data Contracts

Implementation Status: IMPLEMENTED  
Review Status: GO

### P1-S01

Title: Task Complexity Parsing Utility & Data Contracts in `@/api/requests`

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective:
1. Define and export `CandidateTaskItem` interface in `src/frontend/Cakra.Web/src/api/requests.ts`:
   ```ts
   export interface CandidateTaskItem {
     title: string
     complexity: number
   }
   ```
2. Implement and export `parseCandidateTaskLine(line: string): CandidateTaskItem | null` to:
   - Normalize prefixes (bullets, numbers, checkboxes via `normalizeTaskLine`).
   - Match trailing regex `/(?:[\(\[\{])?\s*([1-5])\s*(?:pts?|points?)\s*(?:[\)\]\}])?\s*$/i`.
   - Strip matched complexity notation from title, trim trailing whitespace, and cap title at 255 characters.
   - If points matched (digits 1..5), set `complexity` to that integer; otherwise default `complexity` to 1.
   - If line contains an out-of-range number (e.g. `8 pts`), retain the entire string in title and default complexity to 1.
   - Return `null` if the remaining title is empty.
3. Implement and export `parseTaskListTasks(rawText: string): CandidateTaskItem[]` parsing multiline strings into candidate objects.
4. Maintain `parseTaskListText(rawText: string): string[]` returning title strings for backward compatibility.

Depends On: None  
Repository: smart-ics/b20-agentic-dev  

Completion Criteria:
- `CandidateTaskItem`, `parseCandidateTaskLine`, and `parseTaskListTasks` exported from `src/frontend/Cakra.Web/src/api/requests.ts`.
- Trailing pattern matching properly handles `1pts`, `1 pts`, `3 points`, `2 pt`, `[4 pts]`, `(5 points)`.
- Numbers in the middle of sentences (e.g., `"3 consecutive failed 3 points"`) do not interfere with end-of-line detection.
- `parseTaskListText` remains functional.

Notes:
- Defined and exported `CandidateTaskItem` interface (`{ title: string; complexity: number }`).
- Implemented and exported `parseCandidateTaskLine` using trailing regex `/(?:[\(\[\{])?\s*([1-5])\s*(?:pts?|points?)\s*(?:[\)\]\}])?\s*$/i`. Strips trailing notation, trims whitespace, defaults to 1 for unannotated or out-of-range notations (e.g., `8 pts`), caps title length at 255, and filters empty titles to `null`.
- Implemented and exported `parseTaskListTasks` splitting multiline input, parsing candidate lines, and filtering nulls.
- Maintained `parseTaskListText` returning string titles via `parseTaskListTasks(rawText).map(t => t.title)` for backward compatibility.
- Exported `parseCandidateTaskLine` and `parseTaskListTasks` on `requestService` export object.
- Verified TypeScript compilation (`vue-tsc --noEmit`) and production build (`npm run build`).

Changed Files:
- `cakra/src/frontend/Cakra.Web/src/api/requests.ts`

---

## P2 - SCR-WP-001 Quick Capture UI Integration

Implementation Status: IMPLEMENTED  
Review Status: GO

### P2-S02

Title: Create Work Package Modal Quick Capture Complexity Integration

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective:
1. In `src/frontend/Cakra.Web/src/views/WorkPackageView.vue`, update `createCandidateTasks` ref to `CandidateTaskItem[]`.
2. Update `handleParseCreateTasks` to parse `createRawTasks.value` using `parseTaskListTasks`.
3. In `data-testid="create-candidate-preview-container"`, render a read-only complexity badge next to each candidate task:
   ```html
   <span
     class="px-1.5 py-0.5 text-[10px] font-mono rounded bg-amber-500/15 text-amber-300 border border-amber-500/30 font-semibold shrink-0"
     data-testid="create-candidate-complexity-badge"
   >
     {{ task.complexity }} pt{{ task.complexity > 1 ? 's' : '' }}
   </span>
   ```
4. In `handleCreateWorkPackage`, update request submission loop to pass `complexity: task.complexity` to `recordRequest`.
5. Support fallback parsing when raw text is submitted directly without clicking "Parse Tasks".

Depends On: P1-S01  
Repository: smart-ics/b20-agentic-dev  

Completion Criteria:
- Create modal preview displays complexity badge on each parsed task.
- `recordRequest` payload includes `complexity: task.complexity`.
- Deleting candidate items from preview list works with candidate objects.
- Modal reset clears `createCandidateTasks` properly.

Notes:
- Imported `CandidateTaskItem` and `parseTaskListTasks` from `@/api/requests` into `WorkPackageView.vue`.
- Updated `createCandidateTasks` ref type from `ref<string[]>([])` to `ref<CandidateTaskItem[]>([])`.
- Updated `handleParseCreateTasks` to parse raw input using `parseTaskListTasks`.
- Enriched create modal preview items (`create-candidate-task-item`) with the required `create-candidate-complexity-badge` displaying `{{ task.complexity }} pt{{ task.complexity > 1 ? 's' : '' }}` and `{{ task.title }}`.
- Updated `handleCreateWorkPackage` to type `tasksToRecord` as `CandidateTaskItem[]`, fallback-parse raw text if needed, record requests with `complexity: task.complexity`, and track unrecorded task titles.
- Verified TypeScript compilation (`vue-tsc --noEmit`) and production packaging (`npm run build`).

Changed Files:
- `cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue`

---

### P2-S03

Title: Scope Management Quick Bulk Add Complexity Integration

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective:
1. In `src/frontend/Cakra.Web/src/views/WorkPackageView.vue`, update `scopeCandidateTasks` ref to `CandidateTaskItem[]`.
2. Update `handleParseScopeTasks` to parse `scopeRawTasks.value` using `parseTaskListTasks`.
3. In `data-testid="scope-candidate-preview-container"`, render a read-only complexity badge next to each candidate task:
   ```html
   <span
     class="px-1.5 py-0.5 text-[10px] font-mono rounded bg-amber-500/15 text-amber-300 border border-amber-500/30 font-semibold shrink-0"
     data-testid="scope-candidate-complexity-badge"
   >
     {{ task.complexity }} pt{{ task.complexity > 1 ? 's' : '' }}
   </span>
   ```
4. In `handleBulkAddTasksToScope`, update request submission loop to pass `complexity: task.complexity` to `recordRequest`.
5. Handle partial failures retaining uncreated `CandidateTaskItem`s in `scopeCandidateTasks`.

Depends On: P1-S01  
Repository: smart-ics/b20-agentic-dev  

Completion Criteria:
- Scope quick add preview displays complexity badge on each parsed task.
- `recordRequest` payload includes `complexity: task.complexity`.
- Deleting candidate items from preview list works with candidate objects.
- Scope panel reset clears `scopeCandidateTasks` properly.

Notes:
- Updated `scopeCandidateTasks` ref type from `ref<string[]>([])` to `ref<CandidateTaskItem[]>([])`.
- Updated `handleParseScopeTasks` to parse `scopeRawTasks` using `parseTaskListTasks`.
- Enriched scope preview list items (`scope-candidate-task-item`) with `scope-candidate-complexity-badge` displaying `{{ task.complexity }} pt{{ task.complexity > 1 ? 's' : '' }}` and `{{ task.title }}`.
- Updated `handleBulkAddTasksToScope` to type `tasksToAdd` as `CandidateTaskItem[]`, fallback-parse raw text if needed, record requests with `complexity: task.complexity`, and retain unrecorded `CandidateTaskItem` items in `scopeCandidateTasks` on partial failure.
- Removed unused imports in `WorkPackageView.vue` script setup.
- Verified TypeScript compilation (`vue-tsc --noEmit`) and production build (`npm run build`).

Changed Files:
- `cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue`

---

## P3 - Verification & Automated Testing

Implementation Status: IMPLEMENTED  
Review Status: GO

### P3-S04

Title: Unit Testing & TypeScript Build Verification

Implementation Status: IMPLEMENTED  
Review Status: GO

Objective:
1. Create unit tests for `parseCandidateTaskLine` and `parseTaskListTasks` verifying:
   - Trailing points detection (`1pts`, `2 pts`, `3 points`, `4 point`).
   - Bracketed expressions (`[3 pts]`, `(2 points)`, `{5 pts}`).
   - Numbers embedded in the sentence before the points tag (e.g., `"Allow user re-login for 3 consecutive failed 3 points"`).
   - Clean title extraction (stripping points expression and trimming whitespace).
   - Default fallback of 1 point when no points expression exists.
   - Out-of-range numbers (e.g., `8 pts`, `0 pts`) left in title with default 1 point.
   - Bullet, number, and checkbox prefix normalization.
   - 255 character length capping.
2. Verify TypeScript compilation and Vite build (`npm run build`).

Depends On: P2-S02, P2-S03  
Repository: smart-ics/b20-agentic-dev  

Completion Criteria:
- All unit tests pass cleanly.
- `npm run build` succeeds without type errors or bundler warnings.

Notes:
- Installed `vitest` as a devDependency in `cakra/src/frontend/Cakra.Web` and added `"test": "vitest run"` script to `package.json`.
- Added `"tests/**/*.ts"` to `tsconfig.json` `include` list to enable TypeScript type-checking for test files.
- Authored comprehensive test suite `tests/unit/requests.spec.ts` (43 test assertions) covering:
  - Trailing points expressions (`1pts`, `1 pts`, `2 pt`, `2 pts`, `3 point`, `3 points`, `4 pts`, `5 points`).
  - Case insensitivity (`PTS`, `Pts`, `Point`, `POINTS`).
  - Bracketed and parenthesized notations (`[3 pts]`, `(2 points)`, `{4 pt}`).
  - Sentence-embedded numbers preserving preceding numerals (`"Allow user re-login for 3 consecutive failed 3 points"`, `"Delete user which has been dormant for 1 years 2 pts"`).
  - Clean title extraction (stripping notation, trimming whitespace, empty-title rejection).
  - Default fallback (1 point when absent).
  - Out-of-range notations (`8 pts`, `0 pts`, `[9 points]`) preserved in title with default 1 point.
  - Prefix normalization (bullets, numbers, checkboxes `[ ]`, `[x]`, `[X]`).
  - Multiline parsing (`parseTaskListTasks`), empty line filtering, line trimming, and 255-character length capping.
  - Backward compatibility for `parseTaskListText`.
- Verified test suite execution: all 43 unit tests passed cleanly (`npm test`).
- Verified production build and type checking (`npm run build` executing `vue-tsc --noEmit && vite build`).

Changed Files:
- `cakra/src/frontend/Cakra.Web/package.json`
- `cakra/src/frontend/Cakra.Web/package-lock.json`
- `cakra/src/frontend/Cakra.Web/tsconfig.json`
- `cakra/src/frontend/Cakra.Web/tests/unit/requests.spec.ts`

---

# 6. Change Log

- 2026-10-08: Initial plan creation for CR-028 following approval of FEASIBILITY-ASSESSMENT. Execution Approval granted.
