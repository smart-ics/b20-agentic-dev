# ISSUE

## Metadata

ID: CR-028
Type: CHANGE-REQUEST
Status: OPEN
Title: Record Numerical Complexity in SCR-WP-001 Quick Capture via Pattern Detection

## Source

Reported By: User
Reported Date: 2026-10-08

## Description

During quick capture in `SCR-WP-001` (`WorkPackageView.vue`), users draft or paste multi-line task lists to rapidly generate requests for a work package. Currently, all requests created through quick capture have their complexity omitted or defaulted to 1, requiring users to manually update each request's complexity later. Users request that quick capture automatically detect and record complexity ratings (1 to 5 points) directly from task lines using recognizable expressions (such as `1pts`, `3 points`, `2 pts`, `[4 pts]`, `(5 points)`), stripping the point notation from the recorded title and setting the request's numerical complexity accordingly.

## Desired Outcome

1. **Automated Complexity Detection in Quick Capture**:
   - Detect numerical complexity ratings between 1 and 5 when users paste or type task lines in `SCR-WP-001`.
   - Support variations of points notation (e.g., `pt`, `pts`, `point`, `points`, case-insensitive) anchored to the end of the line, with optional surrounding brackets or parentheses (e.g., `1pts`, `3 points`, `2 pts`, `[4 pts]`, `(5 points)`).
   - Ensure detection is anchored to the end of the line to prevent false matches from numbers in the body of the title (e.g., `"Allow user re-login for 3 consecutive failed 3 points"` accurately captures `3 points` at the end and ignores `"3 consecutive failed"`).
   - Strictly restrict complexity detection to integer values 1 through 5. Numbers outside this range (such as `8 pts` or `0 pts`) are ignored as complexity, retained in the title, and assigned default complexity.

2. **Clean Title Extraction**:
   - Strip the trailing complexity expression and surrounding whitespace from the task title so the resulting Request has a clean title (e.g., `"Create quick login 1pts"` produces title `"Create quick login"`).

3. **Default Fallback Complexity**:
   - If a task line does not contain a recognized complexity expression, assign the standard default complexity of 1 point (matching Cakra's baseline complexity).

4. **Candidate Task Preview List Presentation**:
   - In both candidate preview lists (Create Work Package modal and Scope Quick Bulk Add), display a read-only complexity badge (e.g., `1 pt`, `3 pts`) alongside each parsed task title before submission.

5. **End-to-End Persistence**:
   - Include the parsed numerical complexity in the request creation payload (`recordRequest`) so that newly captured requests are immediately persisted with their designated complexity.

6. **Dual Placement in SCR-WP-001**:
   - Support complexity detection in both the Create Work Package modal and the Work Package Detail Scope Management Quick Bulk Add panel.

## Current Situation

1. In `SCR-WP-001` (`WorkPackageView.vue`), `parseTaskListText` only normalizes task text and strips bullet prefixes/checkboxes, returning a list of plain strings.
2. Quick capture calls to `recordRequest` in both the Create modal and Scope Quick Bulk Add do not pass `complexity`, causing all quick-captured requests to default to 1 in the backend.
3. The Candidate Tasks preview lists in `SCR-WP-001` only show task titles with no indication or visualization of complexity.
4. Users must navigate to individual request views (`SCR-REQ-001` / `SCR-REQ-002`) after creation to adjust complexity ratings.

## Evidence

- Screen component: [WorkPackageView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue#L971-L992)
- Request API client helper: [requests.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/requests.ts#L121-L140)
- Backend Request Command: [RequestCommands.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs#L15-L27)
- Intake Alignment Interview: Conducted on 2026-10-08 via `/grill-me`, confirming trailing pattern anchoring, bracket support, 1-5 digit restriction, title stripping, 1-point default fallback, read-only preview badge, and dual placement across Create modal and Scope panel.

## Notes

- Intake interview confirmed:
  - Anchor: Trailing suffix only (`(?:[\(\[\{])?\s*([1-5])\s*(?:pts?|points?)\s*(?:[\)\]\}])?\s*$`).
  - Range: Strictly digits 1 to 5; out-of-range numbers remain in the title and default to 1 point.
  - Stripping: Strip complexity notation from request title.
  - Preview: Read-only badge per candidate item.
  - Dual placement: Create Work Package modal + Scope Management Quick Bulk Add panel.
- Downstream workflow routing:
  - Stage: Discovery & Feasibility Assessment
  - Owning Role: `ica-analyst`
