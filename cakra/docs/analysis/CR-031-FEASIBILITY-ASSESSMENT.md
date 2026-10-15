---
Title: Feasibility Assessment for Displaying Truncated RequestId in My Request Screen (SCR-REQ-004)
Code: CR-031
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-15
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Feature being assessed: Visually compacting the Request ID in the 'My Request' screen (`SCR-REQ-004`) from a full 36-character raw GUID into a clean, recognizable `#<8-chars>` prefix hash, maintaining access to the full GUID via hover tooltip, preserving direct route navigation, and normalizing search inputs to strip leading `#` symbols, as formally captured in [CR-031-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-031-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-031-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-031-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- UI SPECIFICATION: [16-scr-req-004.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/16-scr-req-004.md)
- REFERENCE ARCHITECTURE: [CR-006-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-006-ARCHITECTURE.md) (My Requests Screen & Personal Workspace)

## Objective

Assess the technical feasibility, implementation constraints, gap closure resolutions, and planning readiness to:

1. Truncate the display of Request IDs on the Assigned Requests cards (`SCR-REQ-004`) from the full 36-character GUID to an 8-character prefix formatted as `#<8-chars>` (e.g., `#a1b2c3d4`).
2. Truncate the display of parent Request IDs in the Assigned Sub-Tasks table (`SCR-REQ-004`) using the identical `#<8-chars>` format.
3. Expose the full GUID on demand via a native browser tooltip (`title="req.id"` / `title="subTask.requestId"`) upon hovering over the truncated ID.
4. Maintain clickable navigation linking directly to the authoritative Request Detail screen (`/requests/${id}`).
5. Normalize the client-side search query logic in both tabs so that searching either `#a1b2c3d4` or `a1b2c3d4` successfully matches tickets against the underlying GUID.

---

# 2. Current State

## Existing Behavior

1. **Assigned Requests Card Grid (`MyRequestsView.vue`, Lines 681–689)**:
   - In each card header, `req.id` is rendered in its entirety inside `<router-link :to="'/requests/' + req.id" class="font-monospace fw-bold text-cyan-400">`.
   - Displaying all 36 characters (e.g., `d7a5e8c1-1234-4567-89ab-cdef01234567`) consumes up to 50% of the card header width, competing visually with the Priority badge and Status badge, and drawing focus away from the request title.
2. **Assigned Sub-Tasks Queue (`MyRequestsView.vue`, Lines 902–904)**:
   - In the "Parent Request" column, `subTask.requestId` is printed below the title in full 36-character monospace font (`<span class="font-monospace text-body-secondary" style="font-size: 10.5px;">{{ subTask.requestId }}</span>`).
3. **Universal & Sub-Task Search Filtering (`MyRequestsView.vue`, Lines 145–157 & 311–321)**:
   - Filtering checks: `(req.id ?? '').toLowerCase().includes(q)`.
   - The query `q` is converted to lowercase directly from user input without stripping the `#` character.
   - If a user attempts to search for a ticket using the displayed `#` prefix format (e.g. typing `#a1b2c3d4`), the filter evaluates to false because the raw GUID string does not contain `#`, returning zero matching results.
4. **Existing Codebase Truncation Precedents**:
   - Multiple views and components across Cakra already truncate GUIDs to 8 characters for high-density presentation:
     - [FeedTimelineCard.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/FeedTimelineCard.vue#L516): `REQ #${reqId.slice(0, 8).toUpperCase()}`
     - [WorkPackageView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue#L2489): `#{{ wp.id.slice(0, 8) }}`
     - [FeedDossierPanel.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/FeedDossierPanel.vue#L146): `Request #${reqId.slice(0, 8)}`
     - [OperationsCockpitView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/OperationsCockpitView.vue#L525): `#{{ pkg.id.slice(0, 8) }}`

## Existing Constraints

1. **Identifier Integrity**:
   - The authoritative identifier on the backend (`Cakra.Modules.Request.Domain.Request`) is a 128-bit `Guid` (`System.Guid`).
   - The route parameter for `SCR-REQ-003` (`RequestDetailView.vue`) strictly expects the full GUID (`/requests/:id`). Truncation must be purely presentational on the client.
2. **Automated Test Compatibility**:
   - Test selectors such as `data-testid="my-request-id-link"`, `data-testid="my-request-grid-card"`, and `data-testid="parent-request-link"` must be preserved.
3. **Scope Boundary**:
   - Scope is restricted to `SCR-REQ-004` ([MyRequestsView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/MyRequestsView.vue)). Screens `SCR-REQ-005` (Search & History) and `SCR-REQ-003` (Detail) remain unmodified.

---

# 3. Gap Analysis

| ID | Severity | Gap |
|------|------|------|
| GAP-001 | CRITICAL | In `MyRequestsView.vue`, the Assigned Requests card header displays the full 36-character raw GUID, causing cognitive clutter and stealing visual prominence from actionable ticket metadata. |
| GAP-002 | MAJOR | In `MyRequestsView.vue`, the Assigned Sub-Tasks table displays the full 36-character raw GUID in the Parent Request column, causing vertical layout stretching and visual noise. |
| GAP-003 | MAJOR | The client-side search query filters in `MyRequestsView.vue` do not normalize queries by stripping leading `#` symbols, resulting in false zero-search results when users search by displayed short IDs (e.g. `#a1b2c3d4`). |
| GAP-004 | MINOR | `MyRequestsView.vue` does not provide an ID truncation formatting helper function, risking duplicate `.slice(0, 8)` logic across template blocks. |

---

# 4. Open Questions

| ID | Question | Impact | Status |
|------|------|------|------|
| OQ-001 | What truncated format should represent the Request ID on the card header? | Visual layout, compactness, and ticket referenceability. | CLOSED |
| OQ-002 | Should the truncated ID format apply to both the Assigned Requests tab and the Assigned Sub-Tasks tab in SCR-REQ-004? | Cross-tab consistency within the personal workspace screen. | CLOSED |
| OQ-003 | How should users retrieve or inspect the full GUID when needed for technical troubleshooting or external copying? | Usability and technical referenceability without navigation disruption. | CLOSED |
| OQ-004 | How should search filters handle inputs prefixed with `#`? | Search accuracy and prevention of false empty search results. | CLOSED |

---

# 5. Assumptions

| ID | Assumption |
|------|------|
| ASM-001 | The first 8 hex characters of a GUID provide sufficient entropy ($16^8 \approx 4.3 \times 10^9$) to eliminate visual collisions within an individual user's assigned tickets. |
| ASM-002 | Native browser tooltips via the standard HTML `title` attribute provide sufficient, zero-dependency accessibility for revealing the full GUID on mouse hover. |
| ASM-003 | Keeping route navigation bound to the full underlying GUID (`req.id`) satisfies all deep linking and detail retrieval requirements. |
| ASM-004 | Normalizing search terms by stripping leading `#` does not impair title, description, or customer search matching. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|------|------|------|------|
| RISK-001 | Users attempt to copy the short ID expecting it to function as a globally unique database key in external systems. | Low | Provide full GUID in native hover tooltip and maintain full GUID in Request Detail URL and detail view headers. |
| RISK-002 | Existing automated unit tests asserting full GUID text in `my-request-id-link` fail after truncation. | Low | Review and update unit tests in `requests.spec.ts` to assert the `#<8-chars>` pattern while validating the `:to` destination retains the full GUID. |

---

# 7. Recommendations

## Option A (Recommended): Pure Frontend Presentation Helper & Query Normalization in `MyRequestsView.vue`
- Define a reactive/template helper in `MyRequestsView.vue`:
  ```typescript
  function formatShortId(id: string | null | undefined): string {
    if (!id) return ''
    return `#${id.slice(0, 8)}`
  }
  ```
- Render `{{ formatShortId(req.id) }}` with `:title="req.id"` in the card header.
- Render `{{ formatShortId(subTask.requestId) }}` with `:title="subTask.requestId"` in the sub-tasks table.
- Normalize search queries by stripping leading `#` before testing against `req.id` and `subTask.requestId`:
  ```typescript
  const qClean = q.startsWith('#') ? q.slice(1).trim() : q
  ```
- **Advantages**: Completely isolated to `MyRequestsView.vue`, requires zero backend changes, zero API contract modifications, zero database schema adjustments, and zero performance overhead.
- **Disadvantages**: None.

## Option B: Backend DTO Extension with Generated Display Number / Short Code
- Introduce a computed or stored short ticket code (e.g., `REQ-00123`) on `RequestDto`.
- **Advantages**: System-wide sequential ticket numbering.
- **Disadvantages**: Disproportionate complexity, database migration, cross-module contract changes, and regression risks for an isolated UI decluttering request.

---

# 8. Gap Closure

## GAP-001 / OQ-001: Truncated Request ID Format on Assigned Requests Cards
### Decision
In `MyRequestsView.vue` card headers, the Request ID will be displayed as an 8-character hash prefix preceded by `#` (e.g. `#a1b2c3d4`).
### Rationale
Matches established Cakra conventions (e.g. `WorkPackageView.vue`), preserves ticket referenceability, and drastically reduces horizontal footprint so title and status retain visual hierarchy.
### Impact
Clean, uncluttered card header with immediate readability.
### Architecture Impact
None (localized Vue template and formatting helper).
### Resolved By
User & Analyst Alignment via `/grill-me`
### Resolved Date
2026-10-15

---

## GAP-002 / OQ-002: Consistent Truncation Across Both SCR-REQ-004 Tabs
### Decision
The `#<8-chars>` truncation format will apply consistently across both tabs in `SCR-REQ-004`:
1. In the Assigned Requests card grid header (`my-request-id-link`).
2. In the Assigned Sub-Tasks table Parent Request column under the request title.
### Rationale
Ensures visual harmony and prevents jarring cognitive context shifts between tabs in the same operational screen.
### Impact
Consistent presentation across all sub-views of `SCR-REQ-004`.
### Architecture Impact
None.
### Resolved By
User & Analyst Alignment via `/grill-me`
### Resolved Date
2026-10-15

---

## OQ-003: Full GUID Accessibility via Native Tooltip
### Decision
The full 36-character GUID will be bound to the HTML `title` attribute of the link/text element (`:title="req.id"` and `:title="subTask.requestId"`). Clicking the element will continue navigating directly to the Request Detail screen (`/requests/${id}`).
### Rationale
Keeps the interaction lightweight without requiring auxiliary popovers or copy-icon clutter while allowing users to inspect the full GUID when needed.
### Impact
Accessible technical detail on demand without visual noise.
### Architecture Impact
None.
### Resolved By
User & Analyst Alignment via `/grill-me`
### Resolved Date
2026-10-15

---

## GAP-003 / OQ-004: Search Query Normalization
### Decision
In `filteredMyRequests` and `filteredAssignedSubTasks`, client-side search logic will normalize the search query by stripping any leading `#` character prior to performing substring matches against `req.id` or `subTask.requestId`.
### Rationale
Users seeing `#a1b2c3d4` on screen naturally copy or type `#a1b2c3d4` into the search bar. Stripping the leading `#` ensures expected match behavior against the raw GUID.
### Impact
Eliminates search frustration and false empty search results.
### Architecture Impact
None.
### Resolved By
User & Analyst Alignment via `/grill-me`
### Resolved Date
2026-10-15

---

## GAP-004: Centralized Short ID Helper
### Decision
Implement a single component helper `formatShortId(id?: string | null): string` in `MyRequestsView.vue` script setup to format IDs uniformly.
### Rationale
Avoids inlined duplicate template string slicing and ensures uniform formatting and null safety.
### Impact
Maintainable and testable component code.
### Architecture Impact
None.
### Resolved By
Analyst Analysis
### Resolved Date
2026-10-15

---

# 9. Architecture Applicability

## Decision

ARCHITECTURE-NOT-REQUIRED

## Rationale

This change is strictly confined to the client-side presentation layer in a single Vue component (`MyRequestsView.vue`). It does not introduce:
- New backend or frontend components, modules, or dependencies
- Changes to API contracts, DTO interfaces, or data models
- Persistence or state management modifications
- Cross-cutting structural or architectural constraints

The existing architectural structure established in `CR-006-ARCHITECTURE.md` remains authoritative and fully accommodates this localized display and search refinement.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved
- [x] All required decisions recorded
- [x] All blocking open questions resolved
- [x] Architecture gate verified / granted by Architect (ARCHITECTURE-NOT-REQUIRED)

## Status

READY-FOR-PLANNING

## Notes

The Architect has evaluated the feasibility assessment and verified that all gaps (GAP-001 through GAP-004) and open questions (OQ-001 through OQ-004) are definitively closed with sound rationale. Architecture Applicability is confirmed as ARCHITECTURE-NOT-REQUIRED because this change is purely a client-side presentation and filter refinement within `MyRequestsView.vue`. The existing architecture in CR-006-ARCHITECTURE.md remains fully authoritative. The READY-FOR-PLANNING gate is hereby formally granted.

---

# 11. References

Referenced artifacts:
- ISSUE: [CR-031-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-031-ISSUE.md)
- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md)
- ARCHITECTURE: [CR-006-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-006-ARCHITECTURE.md)
- UI SPECIFICATION: [16-scr-req-004.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/16-scr-req-004.md)

Referenced codebase locations:
- [MyRequestsView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/MyRequestsView.vue)
- [requests.ts](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/requests.ts)
- [FeedTimelineCard.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/components/FeedTimelineCard.vue)
- [WorkPackageView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue)
