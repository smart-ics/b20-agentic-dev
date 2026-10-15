# ISSUE

## Metadata

ID: CR-034
Type: CHANGE-REQUEST
Status: OPEN
Title: Compact Operational Awareness Board Redesign with Dual-Signal Elapsed and Effort-to-Limit Indicators (SCR-REQ-006)

## Source

Reported By: User
Reported Date: 2026-10-15

## Description

The Work in Progress (WIP) operational awareness board (`SCR-REQ-006`) provides team leads and operational supervisors with real-time visibility into active and paused work across all personnel. However, the current interface utilizes a spacious, card-heavy layout that limits vertical information density and requires significant scrolling.

Furthermore, the board lacks high-contrast visual cues necessary for rapid operational triage:
1. **Difficulty Spotting Programmers in Trouble**: Supervisors cannot instantly identify assignees struggling with excessive elapsed task durations or tasks exceeding their expected effort.
2. **Difficulty Spotting Overloaded ("Busy") Programmers**: Programmers juggling high numbers of paused tasks (frequent context switching) are not visually differentiated from those with one or zero paused tasks.
3. **Absence of Effort-to-Limit Comparison**: While requests maintain an assigned effort value (scale of 1 to 5), the board currently presents only raw elapsed duration. Operational leads cannot evaluate whether a long-running task is legitimately within a high-effort budget or if a low-effort task has blown past its expected time limit.

A redesigned, high-density presentation is requested to deliver immediate visual triage capabilities via dual time signals (pure elapsed time alongside effort-to-limit budget utilization) and quick-glance workload indicators.

## Desired Outcome

1. **Compact and Dense Information Architecture**:
   - Redesign `SCR-REQ-006` to feature a dense, continuous feed layout that maximizes visible personnel and active assignments without visual degradation.
   - Maintain tight vertical footprint for active and paused task rows.

2. **Visual Urgency for Tasks in Trouble (Duration Signals)**:
   - Provide immediate visual cues for elapsed duration, enabling supervisors to spot tasks entering warning and critical duration thresholds at a glance.

3. **Visual Cues for Busy / Overloaded Programmers**:
   - Visually distinguish personnel with multiple paused tasks (e.g., escalating indicators when paused tasks reach or exceed busy and overloaded thresholds).

4. **Dual Time Signal Visualization (Pure Elapsed vs. Effort Budget Burn)**:
   - Present two complementary, independent time metrics for each task:
     - **Pure Elapsed Time**: Absolute elapsed working duration.
     - **Time Usage to Limit by Effort**: Ratio of elapsed hours against the effort budget cap, based on the agreed effort baseline:
       - 1 Effort = 2 hours (1/4 day)
       - 2 Effort = 4 hours (1/2 day)
       - 3 Effort = 8 hours (1 day)
       - 4 Effort = 16 hours (2 days)
       - 5 Effort = 24 hours (3 days)
     - Highlight tasks that are nearing budget or have exceeded 100% of their effort allocation (over budget), allowing leads to differentiate between naturally large tasks and stalled small tasks.

5. **Operational Consistency & Selector Preservation**:
   - Maintain existing detailed contextual inspection (drawer / slide-over view) and navigation links to request details.
   - Strictly preserve all existing automated test selectors (`data-screen-id="SCR-REQ-006"`, `data-testid` attributes).

## Current Situation

1. In `SCR-REQ-006` (`WorkInProgressView.vue`), personnel and task spotlight cards use spacious card padding and multi-layer nesting that consume extensive vertical screen real estate.
2. The view displays elapsed time formatted as a string (`totalInProgressFormatted`) without indicating the task's effort rating or comparing elapsed time against effort limits.
3. `TaskWorkInProgressDto` in `src/api/requests.ts` does not include the request's `effort` field, meaning the front-end lacks effort data for comparison.
4. Paused tasks are displayed in uniform styling without visual thresholds or indicators signaling when an assignee has accumulated an excessive number of paused tasks.

## Evidence

- Existing Screen Implementation: [WorkInProgressView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkInProgressView.vue).
- Current API DTOs: [requests.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/requests.ts#L409-L440) (`TaskWorkInProgressDto` lacks `effort`).
- Operational Prototype: [scr-req-006-redesign.html](file:///C:/Users/drury/.gemini/antigravity/brain/4ee110c7-9733-4e04-845f-cdd74ba21e36/scr-req-006-redesign.html) (prototype validating compact layout, duration heat levels, busy paused thresholds, and dual elapsed/burn visual signals).

## Notes

- This change request originates from operational usability feedback and prototype verification on `SCR-REQ-006`.
- Downstream analysis will determine whether `effort` can be populated from the existing domain request entity into the WIP read-model query.
