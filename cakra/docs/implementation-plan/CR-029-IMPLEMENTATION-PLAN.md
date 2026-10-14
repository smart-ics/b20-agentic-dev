---
Title: Restrict Decision Workbench to Administrator and Management Roles Implementation Plan
Code: CR-029
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-15
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-029`: Restrict the Decision Workbench section (de-scope simulation, deadline extension simulator, and live metric readouts) in `SCR-WP-001: Work Package Screen` so it is only visible to users with the `Administrator` or `Management` role.

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- ISSUE: [CR-029-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-029-ISSUE.md)
- FEASIBILITY-ASSESSMENT: [CR-029-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-029-FEASIBILITY-ASSESSMENT.md) (Planning Readiness: `READY-FOR-PLANNING`)

Architecture Applicability: ARCHITECTURE-NOT-REQUIRED

No architectural target-state artifact was required. Implementation relies on existing technical structure. Approved feasibility decisions are authoritative for the change. The current codebase is the source of current technical truth.

---

# 2. Planning Scope

This plan covers the frontend-only change to `WorkPackageView.vue` to add role-based visibility for the Decision Workbench:

1. **Role-Based Visibility Guard (`src/frontend/Cakra.Web/src/views/WorkPackageView.vue`)**:
   - Import `useAuthStore` from `@/stores/auth`.
   - Add a computed property `canAccessDecisionWorkbench` that returns `true` when `authStore.roles` includes `Administrator` or `Management`.
   - Add `v-if="canAccessDecisionWorkbench"` to the Decision Workbench `<div>` (line 2634).
   - Guard the de-scope simulation checkboxes (lines 2728, 3289) with the same condition, since they appear in the scope request list outside the workbench section.
2. **Verification**:
   - Verify TypeScript type-checking passes (`vue-tsc --noEmit`).
   - Verify production build succeeds (`vite build`).

---

# 3. Dependencies

**External Dependencies:** None

**Slice Dependencies:**
- `P1-S02` depends on `P1-S01`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Role-Based Visibility Guard | IMPLEMENTED | GO | 1/1 |
| P2 - Verification | IMPLEMENTED | GO | 1/1 |

---

# 5. Phases

## P1 - Role-Based Visibility Guard

Implementation Status: IMPLEMENTED
Review Status: GO

### P1-S01

Title: Add role-based visibility to Decision Workbench in WorkPackageView.vue

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Import `useAuthStore`, add `canAccessDecisionWorkbench` computed property checking for `Administrator` or `Management` roles, and apply `v-if="canAccessDecisionWorkbench"` to the Decision Workbench section (line 2634) and de-scope checkboxes (lines 2728, 3289).

Depends On: None

Repository: `Cakra.Web`

Completion Criteria:
- `useAuthStore` imported from `@/stores/auth`.
- `canAccessDecisionWorkbench` computed property defined and returning correct boolean.
- Decision Workbench `<div>` at line 2634 guarded with `v-if="canAccessDecisionWorkbench"`.
- De-scope checkboxes at lines 2728 and 3289 guarded with `v-if="canAccessDecisionWorkbench"`.

Notes:
- Role strings: `Administrator` and `Management` (confirmed from backend `AnalyticsController.cs:16`).
- Follows existing pattern from `PersonManagementView.vue:29`.

Implementation Notes:
- Import added at line 30 of `WorkPackageView.vue`.
- `authStore` instantiation and `canAccessDecisionWorkbench` computed added after `canModifyPackage` (lines 316-319).
- Decision Workbench `<div>` guarded with `v-if="canAccessDecisionWorkbench"` (line 2640).
- De-scope checkbox #1 `<label>` guarded with `v-if="canAccessDecisionWorkbench"` (line 2727).
- De-scope checkbox #2 `<div>` container guarded with `v-if="canAccessDecisionWorkbench"` (line 3292).
- Verified: `npm run build` (`vue-tsc --noEmit && vite build`) completed successfully with 0 errors.

Changed Files:
- `src/frontend/Cakra.Web/src/views/WorkPackageView.vue`

---

## P2 - Verification

Implementation Status: IMPLEMENTED
Review Status: NOT-REVIEWED

### P1-S02

Title: Verify TypeScript type-checking and production build

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:
Run `npm run build` (`vue-tsc --noEmit && vite build`) in `src/frontend/Cakra.Web` and confirm 0 errors.

Depends On: P1-S01

Repository: `Cakra.Web`

Completion Criteria:
- `vue-tsc --noEmit` passes with 0 type errors.
- `vite build` completes successfully.

Notes:
- No backend changes expected; backend tests not required for this slice.

Implementation Notes:
- `npm run build` (`vue-tsc --noEmit && vite build`) executed in `src/frontend/Cakra.Web`.
- `vue-tsc --noEmit` passed with 0 type errors.
- `vite build` completed successfully in 2.90s (183 modules transformed).
- Build output: `dist/index.html` + 18 JS/CSS/asset files.
- Only informational warning: chunk size > 500 kB (non-blocking).

Changed Files:
- None (verification only; no source changes required).

---

# 6. Change Log

- 2026-10-15: Initial plan created (v1.0).
- 2026-10-15: P1-S02 IMPLEMENTED — `npm run build` passed with 0 type errors.
