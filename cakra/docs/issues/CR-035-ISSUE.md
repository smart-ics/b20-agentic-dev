# ISSUE

## Metadata

ID: CR-035
Type: CHANGE-REQUEST
Status: OPEN
Title: Work Package Re-Open Capability for Closed Packages with Unfinished Requests

## Source

Reported By: User
Reported Date: 2026-10-16

## Description

In operational workflows, users or team leads occasionally close a Work Package prematurely—either by mistake (such as when attempting to deactivate an unintentionally activated package) or before all constituent operational requests have reached completion.

Under the current system design, `CLOSED` is treated as an immutable, irreversible terminal state. Once a Work Package is closed, it cannot be transitioned out of `CLOSED`, preventing teams from resuming operational work under that package. As a result, when unfinished work remains, users have no supported way to revive the existing container and are forced to resort to manual database interventions or cumbersome manual re-creation of duplicate work packages and scope re-assignments.

A controlled capability is requested to allow reopening a closed Work Package back into a preparatory/draft state, provided the package still contains active, unfinished requests and does not violate request membership integrity.

## Desired Outcome

1. **Re-Open Action for Prematurely Closed Work Packages**:
   - Authorized users (Work Package Owner, Management, Administrator) can re-open a previously closed Work Package when unfinished operational work remains.
   - Re-opening transitions the Work Package back into `DRAFT` status (`CLOSED` -> `DRAFT`), allowing the owner and team to inspect scope, update deadlines, and make adjustments before re-activating it.

2. **Strict Eligibility & Integrity Gating**:
   - Re-open is permitted only when the closed Work Package contains at least one active constituent request that is not finished (i.e. not `COMPLETED`, `CANCELLED`, or `REJECTED`).
   - If all constituent requests are already completed or cancelled, or if the package has zero requests, closure remains permanent and re-opening is blocked.
   - If an unfinished constituent request was linked to another active or draft Work Package while this package was closed, re-opening must be prevented to preserve the business rule that a request belongs to at most one work package.

3. **Auditability & Justification**:
   - Re-opening requires a mandatory justification reason to explain why the package is being revived.
   - Prior closure details and the re-open event must be captured for traceability.

4. **Deadline Awareness**:
   - When re-opening, the user is alerted if the prior target deadline is already in the past, with the option to provide an updated target deadline.

5. **User Interface Controls**:
   - Provide an explicit "Re-Open Work Package" action in the Work Package detail view (`SCR-WP-001`) when viewing an eligible closed package.
   - Clearly explain via UI cues/tooltips when a closed package is permanently sealed and ineligible for re-opening.

## Current Situation

1. The Work Package domain state machine strictly enforces a unidirectional progression: `DRAFT` -> `ACTIVE` -> `CLOSED` (or `DRAFT` -> `CLOSED`).
2. `WorkPackage.Activate()` explicitly throws `InvalidWorkPackageStateTransitionException` when the package is `CLOSED` (`"Work Package is CLOSED and cannot be activated."`), and no transition from `CLOSED` to `DRAFT` exists.
3. In `WorkPackageView.vue` (`SCR-WP-001`), the `Activate` button is conditionally displayed only when `status === 'DRAFT'`, and all editing controls are locked when `status === 'CLOSED'`.
4. There is no API endpoint or user interface mechanism to re-open or return a closed Work Package to `DRAFT`.

## Evidence

- Domain Specification: [work-package-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/work-package-domain.md#L229-L253) (Section 9 Work Package Lifecycle defines unidirectional `DRAFT` -> `ACTIVE` -> `CLOSED`).
- Aggregate State Transition Implementation: [WorkPackage.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.WorkPackage/Domain/WorkPackage.cs#L126-L168) (throws on activation of closed package; no re-open method).
- UI State Logic: [WorkPackageView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue#L501-L504) (`canActivate` restricted to draft, `canModifyPackage` blocked for closed).
- Requirements Alignment Session: Grill-Me Review Session on 2026-10-16 resolving lifecycle destination (`CLOSED` -> `DRAFT`), eligibility preconditions, conflict handling with Business Rule 9, audit metadata, and authorization.

## Notes

- This change request originates from user operational intake following accidental activation/closure scenarios and subsequent grill-me requirements analysis.
- Downstream stage: Hand off to `ica-analyst` for Discovery and Feasibility Assessment (`CR-035-FEASIBILITY-ASSESSMENT.md`).
