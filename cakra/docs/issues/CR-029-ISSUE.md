# ISSUE

## Metadata

ID: CR-029
Type: CHANGE-REQUEST
Status: DRAFT
Title: Restrict Decision Workbench (De-scope Simulation) to Administrator and Management Roles

## Source

Reported By: User
Reported Date: 2026-10-15

## Description

The Decision Workbench section in the Work Package Screen (`SCR-WP-001`) — which includes the de-scope candidate selection checkboxes, simulation counter, deadline extension simulator, and live metric readouts — is currently visible to all authenticated users regardless of role. The feature should only be accessible to users with the Administrator or Management role, and should be hidden from all other roles.

## Desired Outcome

- The Decision Workbench section and its de-scope candidate selection controls are only rendered for users with the Administrator or Management role.
- Users without these roles do not see the Decision Workbench or any de-scope simulation controls in the Work Package Screen.

## Current Situation

- The Decision Workbench `<div>` at `WorkPackageView.vue:2634` has no role-based visibility guard (`v-if`) — it renders for all users.
- The de-scope simulation checkboxes (`data-testid="descope-request-checkbox"`) at lines 2728 and 3289 are visible to all users.
- `canModifyPackage` (line 313) only checks whether the Work Package is closed, not user roles.
- `WorkPackageView.vue` does not currently import `useAuthStore`.
- Other views (e.g., `PersonManagementView.vue:29`, `CustomerManagementView.vue:35-36`) already implement role-based access using `authStore.roles.includes('Administrator')`.

## Evidence

- `WorkPackageView.vue:2634` — Decision Workbench div with no role guard
- `WorkPackageView.vue:2728,3289` — de-scope checkboxes with no role guard
- `WorkPackageView.vue:313` — `canModifyPackage` computed (no role check)
- `CR-025-IMPLEMENTATION-PLAN.md:409-412` — feature specification for de-scope simulation

## Notes

- The exact role name string for "Management" should be confirmed against the system's role definitions (e.g., `Management`, `Manager`, or another variant) during analysis.
