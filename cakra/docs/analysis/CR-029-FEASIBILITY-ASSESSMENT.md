---
Title: CR-029 — Restrict Decision Workbench to Administrator and Management Roles
Code: CR-029
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-15
Status: NOT-READY
---

# 1. Request Summary

Restrict the Decision Workbench section (de-scope simulation, deadline extension simulator, and live metric readouts) in the Work Package Screen (`SCR-WP-001`) so it is only visible to users with the Administrator or Management role. All other roles should not see the Decision Workbench or its de-scope candidate selection controls.

Referenced artifacts:

- ISSUE: `cakra/docs/issues/CR-029-ISSUE.md`

## Objective

Assess the feasibility of adding role-based visibility to the Decision Workbench and identify gaps, risks, and open questions before architecture work begins.

---

# 2. Current State

## Existing Behavior

- The Decision Workbench `<div>` at `WorkPackageView.vue:2634` renders for all authenticated users — no role-based `v-if` guard exists.
- De-scope simulation checkboxes (`data-testid="descope-request-checkbox"`) at lines 2728 and 3289 render for all users.
- The `canModifyPackage` computed property (line 313) only checks `!isSelectedClosed.value` — no role check.
- `WorkPackageView.vue` does not import `useAuthStore`.

## Existing Role-Based Access Patterns

- `PersonManagementView.vue:29` and `CustomerManagementView.vue:35-36` define `isAdmin` as `authStore.roles.includes('Administrator') || authStore.roles.includes('Admin')`.
- `RequestDetailView.vue:253-256` checks `userRoles.includes('ADMINISTRATOR') || userRoles.includes('ADMIN') || userRoles.includes('MANAGER')` for edit permissions.
- `authStore.roles` is a `string[]` populated from the backend login/register response (`auth.ts:62`).

## Existing Constraints

- Role name strings are not centralized — each view hardcodes its own role-check logic.
- The exact role name for "Management" in this system has not been confirmed (candidates: `Management`, `Manager`, `MANAGER`).

---

# 3. Gap Analysis

| ID | Severity | Gap |
|------|------|------|
| GAP-001 | CRITICAL | Decision Workbench and de-scope controls are visible to all users regardless of role; no role-based visibility guard exists. |
| GAP-002 | MINOR | `WorkPackageView.vue` does not import `useAuthStore`; role-check infrastructure is absent from this view. |
| GAP-003 | MINOR | Role name for "Management" is unconfirmed; different views use inconsistent role strings (`Manager` vs `Management`). |

---

# 4. Open Questions

| ID | Question | Impact |
|------|------|------|
| OQ-001 | What is the exact role name string for "Management" in the system's role definitions? | Determines the correct role-check condition; using the wrong string would grant or deny access incorrectly. |

---

# 5. Assumptions

| ID | Assumption |
|------|------|
| ASM-001 | The backend already assigns roles to users and exposes them via `authStore.roles`. |
| ASM-002 | The Decision Workbench is a self-contained UI section; hiding it does not break other Work Package Screen functionality. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|------|------|------|------|
| RISK-001 | Incorrect role name string grants access to unintended users or denies access to intended users. | Security/access control issue. | Confirm exact role name from backend role definitions during implementation. |
| RISK-002 | Hiding the Decision Workbench may confuse users who previously relied on it. | User experience impact. | Consider a read-only indicator or tooltip explaining why the section is unavailable. |

---

# 7. Recommendations

## Option A

Add a role-check computed property in `WorkPackageView.vue` using the established `authStore.roles.includes(...)` pattern, and guard the Decision Workbench `<div>` and de-scope checkboxes with it.

### Advantages

- Follows existing patterns already used in other views.
- Minimal, localized change.

### Disadvantages

- Adds another instance of hardcoded role strings (no centralized authorization).

## Option B

Create a shared authorization utility/composable (e.g., `useRoleCheck`) that centralizes role-check logic, then use it in `WorkPackageView.vue`.

### Advantages

- Centralizes role logic for consistency across views.
- Easier to maintain as role names evolve.

### Disadvantages

- Larger scope; touches multiple files or introduces new shared module.

---

# 8. Gap Closure

## GAP-001

### Decision

Restrict Decision Workbench visibility to Administrator and Management roles using a role-based `v-if` guard.

### Rationale

The feature is a planning/simulation tool intended for decision-makers (management), not for all users. Other views in the system already implement role-based access control using `authStore.roles`.

### Impact

Users without Administrator or Management roles will no longer see the Decision Workbench or de-scope controls.

Architecture Impact:

Frontend-only change to `WorkPackageView.vue` visibility logic. No backend or API changes required.

Resolved By: Analyst

Resolved Date: 2026-10-15

## GAP-002

### Decision

Import `useAuthStore` into `WorkPackageView.vue` to enable role checks.

### Rationale

The view currently has no access to auth/role state. Other views already import and use `useAuthStore` from `@/stores/auth`.

### Impact

Enables role-based visibility logic within the Work Package Screen.

Architecture Impact:

No architectural impact — standard Vue 3 composable usage.

Resolved By: Analyst

Resolved Date: 2026-10-15

## GAP-003

### Decision

Use `Management` as the role name string, consistent with the issue title and common naming convention. Confirm against backend role definitions during implementation.

### Rationale

The issue explicitly states "Management role." Backend role definitions should be verified to ensure the string matches exactly.

### Impact

If the actual role name differs, the role check will fail silently (no access granted).

Architecture Impact:

None.

Resolved By: Analyst

Resolved Date: 2026-10-15

## OQ-001

### Decision

The exact management role string is `Management`, confirmed from backend role definitions (`AnalyticsController.cs:16`, `ProbeController.cs:79`, seed SQL `0019_seed_operational_user_role.sql:7`).

### Rationale

Backend controllers use `[Authorize(Roles = "Management")]` and the seed SQL file explicitly lists `Management` as a standard role alongside Administrator, Programmer, and Implementator.

### Impact

The role-check condition can now be finalized as `authStore.roles.includes('Management')`.

Architecture Impact:

None.

Resolved By: Analyst

Resolved Date: 2026-10-15

---

# 9. Architecture Applicability

## Decision

ARCHITECTURE-NOT-REQUIRED

## Rationale

This is a frontend-only visibility change following established patterns already used in other views. No new components, no backend changes, no data model changes, and no cross-cutting technical concerns. The existing Vue 3 composition API patterns are sufficient.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved
- [x] All required decisions recorded
- [x] All blocking open questions resolved
- [x] Architecture can be finalized or updated

## Status

NOT-READY

## Notes

All gaps and open questions are resolved. The Architect can now set Status to READY-FOR-PLANNING and proceed to planning.

---

# 11. References

Referenced artifacts:

- `cakra/docs/issues/CR-029-ISSUE.md`

Referenced codebase locations:

- `cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue` (lines 2634, 2728, 3289, 313)
- `cakra/src/frontend/Cakra.Web/src/stores/auth.ts`
- `cakra/src/frontend/Cakra.Web/src/views/PersonManagementView.vue` (line 29)
- `cakra/src/frontend/Cakra.Web/src/views/CustomerManagementView.vue` (lines 35-36)
- `cakra/src/frontend/Cakra.Web/src/views/RequestDetailView.vue` (lines 253-256)
