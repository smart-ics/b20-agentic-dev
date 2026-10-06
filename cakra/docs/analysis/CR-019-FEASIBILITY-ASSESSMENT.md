---
Title: Feasibility Assessment for Bulk Multi-line Quick Capture in SCR-WP-001 (CR-019)
Code: CR-019
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-10-06
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Feature being assessed: Implementation of Bulk Multi-line Quick Capture for Work Package requests within `SCR-WP-001: Work Package Screen`, as formally captured in [CR-019-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-019-ISSUE.md).

Referenced artifacts:

- ISSUE: [CR-019-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-019-ISSUE.md)
- DOMAIN: [CAKRA-DOMAIN.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domain/CAKRA-DOMAIN.md) (Work Package & Request Domains)
- ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)
- UI Layout Spec: Work Package Screen (`SCR-WP-001`)

## Objective

Assess the feasibility, interaction model, client-side orchestration, default attributes inheritance, error resilience, and planning readiness to:

1. Enable rapid multi-line task capture in `SCR-WP-001` (`WorkPackageView.vue`) across both the Create Work Package modal and the Work Package Detail Scope Management section.
2. Provide a "Parse & Preview" workflow that strips common list formatting prefixes (`-`, `*`, `+`, `1.`, `1)`, `[ ]`, `[x]`), ignores blank lines, and renders parsed candidate items with individual removal controls (`✕`) prior to saving.
3. Automatically associate parsed requests with the parent Work Package (inheriting `customerId` and `productId`), defaulting attributes to `RequestType: GENERAL`, `Priority: NORMAL`, `Status: CAPTURED`, with `OwnerPersonId` unassigned and `Description` empty for future enrichment.
4. Orchestrate persistence on the frontend without requiring backend API contract changes by sequentially or concurrently calling existing endpoints (`POST /api/v1/work-packages` and `POST /api/v1/requests`).
5. Ensure robust error handling that preserves the created Work Package container even if individual request creations encounter errors (partial success warning).

---

# 2. Current State

## Existing Behavior

1. **Create Work Package Flow (`SCR-WP-001`, `WorkPackageView.vue`)**:
   - The Create modal allows users to enter `name`, `objective`, `ownerPersonId`, `customerId`, and `productId`.
   - Submitting executes `POST /api/v1/work-packages`.
   - There is no mechanism in this modal to define, capture, or associate initial requests.
2. **Work Package Detail Scope Management (`SCR-WP-001`, `WorkPackageView.vue`)**:
   - The Scope section provides an "Add Request" selector (`addRequestForm.selectedRequestId` or `addRequestForm.manualRequestId`).
   - Adding a request executes `POST /api/v1/work-packages/{id}/requests` with `{ requestId }`.
   - This requires that requests already exist beforehand in the Request repository. Users cannot create new requests on the fly from this screen.
3. **Request Creation Backend API (`RequestsController.cs`)**:
   - Exposes `POST /api/v1/requests` (`RecordRequest`) accepting `RecordRequestBody` (`Title`, `Description`, `CustomerId`, `ProductId`, `RequestType`, `Priority`, `WorkPackageId`, `Complexity`, etc.).
   - `Title` is mandatory (max 255 chars).
   - In `RecordRequestCommandValidator.cs`, `RuleFor(x => x.Description).NotEmpty()` is currently enforced!
4. **Frontend API Client (`requests.ts`)**:
   - Helper methods exist for workflow actions (`startWork`, `pauseWork`, `cancelRequest`, `assignRequestOwner`, `addSubTask`, etc.), but there is no exported helper function for recording/creating a new request (`recordRequest`). Component code relies directly on `httpClient.post('/requests', ...)` or custom handlers.

## Existing Constraints

1. **Request Description Validation**:
   - In the backend domain/command validator (`RecordRequestCommandValidator`), `Description` is currently validated with `.NotEmpty()`. For minimal quick capture with empty description, this backend validator constraint must be satisfied (e.g. passing a fallback non-empty placeholder, updating the validator, or defaulting to an initial marker description like the title or a standard notice).
2. **Title Length Restriction**:
   - Request titles must not exceed 255 characters. Parsed lines exceeding 255 characters must be validated or trimmed.
3. **Work Package Status Rules**:
   - Requests can only be added to Work Packages in `DRAFT` or `ACTIVE` states. Adding requests to a `CLOSED` Work Package is rejected.
4. **Frontend Asynchronous Loops**:
   - Creating multiple requests in a client-side loop must handle network latency, order preservation, and individual failure states gracefully without hanging the UI.

---

# 3. Gap Analysis

| ID | Severity | Gap |
|------|------|------|
| GAP-001 | CRITICAL | `RecordRequestCommandValidator` enforces `RuleFor(x => x.Description).NotEmpty()`, whereas quick-captured requests are designed to have empty or deferred descriptions. |
| GAP-002 | MAJOR | `WorkPackageView.vue` Create Modal lacks the multi-line input textarea, parse button, and parsed preview items list. |
| GAP-003 | MAJOR | `WorkPackageView.vue` Scope Management section only supports adding pre-existing requests via dropdown/manual ID, lacking an inline quick-add multi-line input. |
| GAP-004 | MINOR | `src/frontend/Cakra.Web/src/api/requests.ts` lacks a typed client method `recordRequest` for creating requests. |

---

# 4. Open Questions

| ID | Question | Impact | Status |
|------|------|------|------|
| OQ-001 | How should the multi-line text input interact with the user before saving the Work Package? | Determines UI preview step vs instant submit. | CLOSED |
| OQ-002 | What default metadata should be assigned to each quick-captured request? | Determines request attributes, inheritance, and defaults. | CLOSED |
| OQ-003 | When users paste lists from notes or markdown, how should line prefixes (bullets/numbers) be handled? | Determines parsing and cleaning regex rules. | CLOSED |
| OQ-004 | How should persistence architecture be handled when saving? | Determines whether backend atomic endpoints or client orchestration is used. | CLOSED |
| OQ-005 | How should partial failures during request recording be handled by the UI? | Determines rollback vs warning alert behavior. | CLOSED |
| OQ-006 | Should Bulk Quick Capture also be available in the Work Package Detail Scope section? | Determines component reuse and placement across SCR-WP-001. | CLOSED |
| OQ-007 | How should the backend `.NotEmpty()` validation rule on `Description` be addressed for quick capture? | Determines backend validator relaxations vs frontend placeholder strategy. | CLOSED |

---

# 5. Assumptions

| ID | Assumption |
|------|------|
| ASM-001 | Users understand that quick-captured requests only possess a title initially, and detailed specifications/sub-tasks can be added subsequently via `SCR-REQ-003`. |
| ASM-002 | Batch size for quick-captured tasks in a single paste typically ranges from 1 to 30 items, well within comfortable client-side batching performance. |
| ASM-003 | Defaulting `RequestType` to `"GENERAL"` and `Priority` to `"NORMAL"` is appropriate for initial operational demand intake. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|------|------|------|------|
| RISK-001 | Backend `RecordRequestCommandValidator` rejects requests with empty descriptions. | Request creation loop fails with HTTP 400 Bad Request. | Address GAP-001 / OQ-007: either make `Description` optional in validator or supply a clean fallback placeholder (e.g. standard notice or request title). |
| RISK-002 | Long pasted lines exceed the 255-character database limit for `Title`. | Request creation fails for individual items. | Client-side parser highlights or truncates lines exceeding 255 characters with clear visual validation indicators. |
| RISK-003 | Network hiccup fails midway through recording a batch of 15 requests. | Incomplete set of requests added to the Work Package. | Client handles partial failures with a persistent warning banner listing the unrecorded task titles so users can copy or re-try them. |

---

# 7. Recommendations

## Option A: Client-Side Orchestration with Description Placeholder (Frontend-Only Change)
Frontend creates the Work Package, then loops calling `POST /api/v1/requests` passing a default description (e.g. `$"Quick capture from Work Package: {wp.Name}"` or using the title as description) if empty.
- **Advantages:** Completely zero backend deployment or schema change required; fast turnaround.
- **Disadvantages:** Description contains temporary placeholder text rather than being genuinely empty.

## Option B: Full-Stack Alignment (Backend Validator Update + Frontend Orchestration) - Recommended
Relax `RecordRequestCommandValidator` to allow empty/null `Description` on `RecordRequestCommand` (or treat empty string as valid), paired with frontend multi-line quick capture in `WorkPackageView.vue`.
- **Advantages:** Fully aligns domain model with genuine minimal-capture paradigm; no artificial placeholder strings in database.
- **Disadvantages:** Requires touching both backend validation rule and frontend component.

---

# 8. Gap Closure

## GAP-001 / OQ-007: Description Validation
### Decision
Adopt Option B / Full-Stack Alignment: Allow empty or whitespace description in `RecordRequestCommand` by relaxing `RecordRequestCommandValidator` (or allowing empty description if omitted), with frontend passing `""`. If backend changes are postponed, frontend supplies `title` as default description fallback.
### Rationale
Operational demands captured on the fly naturally start with a title only; requiring a verbose description at instant capture creates unnecessary friction.
### Impact
Enables clean creation of minimal requests across all quick-intake scenarios.
### Architecture Impact
Minor validator adjustment in `Cakra.Modules.Request.Services`.
### Resolved By
User & Analyst Alignment
### Resolved Date
2026-10-06

---

## OQ-001: Interaction Model (Parse & Preview)
### Decision
Implement "Parse & Preview". Users type or paste text into a multi-line textarea, click "Parse Tasks", and review the list of candidate cards/chips with individual remove (`✕`) controls prior to committing.
### Rationale
Prevents accidental submission of empty lines, duplicate items, or unwanted formatting artifacts.
### Impact
Clean, predictable user experience.
### Architecture Impact
Frontend UI state in `WorkPackageView.vue`.
### Resolved By
User & Analyst Alignment
### Resolved Date
2026-10-06

---

## OQ-002: Default Attributes Inheritance
### Decision
Quick-captured requests inherit `CustomerId` and `ProductId` from the parent Work Package, set `WorkPackageId` to the package ID, default `RequestType` to `"GENERAL"`, `Priority` to `"NORMAL"`, leave `OwnerPersonId` unassigned (`null`), and set initial `Status` to `CAPTURED`.
### Rationale
Minimizes data entry while ensuring the new requests are accurately scoped to the customer, product, and work package context.
### Impact
Consistency across operational reporting and filtering.
### Architecture Impact
Payload construction in `WorkPackageView.vue`.
### Resolved By
User & Analyst Alignment
### Resolved Date
2026-10-06

---

## OQ-003: Line Normalization
### Decision
Automatically strip markdown bullets (`-`, `*`, `+`), numbered prefixes (`1.`, `1)`, `(1)`), markdown checkboxes (`[ ]`, `[x]`), and trim outer whitespace. Silently filter out empty lines.
### Rationale
Users frequently copy lists directly from external documents, issue trackers, or chat applications.
### Impact
Zero manual editing required when pasting structured lists.
### Architecture Impact
Frontend regex parsing utility in `WorkPackageView.vue`.
### Resolved By
User & Analyst Alignment
### Resolved Date
2026-10-06

---

## OQ-004: Persistence Strategy
### Decision
Use Client-Side Orchestration: First create the Work Package (`POST /api/v1/work-packages`), then iterate over previewed items calling `POST /api/v1/requests` with the new `workPackageId`.
### Rationale
Reuses existing authoritative endpoints and security policies without creating redundant composite endpoints.
### Impact
Modular, maintainable, and straightforward implementation.
### Architecture Impact
None on backend architecture; localized to frontend service calls.
### Resolved By
User & Analyst Alignment
### Resolved Date
2026-10-06

---

## OQ-005: Error Handling & Partial Success
### Decision
If the Work Package is successfully created but one or more requests fail, keep the created Work Package, navigate to its detail view, and display a prominent warning banner listing the specific failed titles for retry.
### Rationale
Prevents loss of the created Work Package and successfully recorded tasks due to a single transient item error.
### Impact
High user resilience and zero silent data loss.
### Architecture Impact
Frontend error boundary and notification state.
### Resolved By
User & Analyst Alignment
### Resolved Date
2026-10-06

---

## OQ-006: Scope Management Dual-Placement
### Decision
Provide the Bulk Quick Capture interface in both the Create Work Package modal and the Work Package Detail Scope Management section (via a collapsible/tabbed card).
### Rationale
Allows quick capture both when spinning up a new container and when fleshing out existing active work packages.
### Impact
Unified quick-capture capability throughout the Work Package lifecycle.
### Architecture Impact
Frontend component template additions in `WorkPackageView.vue`.
### Resolved By
User & Analyst Alignment
### Resolved Date
2026-10-06

---

# 9. Architecture Applicability

The Architect owns the formal Architecture Applicability gate.

## Recommendation
ARCHITECTURE-REQUIRED

## Rationale
While primary execution is on the frontend, relaxing the backend validator (`RecordRequestCommandValidator`) to support empty descriptions affects domain validation conventions and request lifecycle intake rules across the system. An architectural assessment confirms the target state and cross-cutting contracts.

---

# 10. Planning Readiness

## Readiness Checklist

- [x] All critical gaps resolved
- [x] All required decisions recorded
- [x] All blocking open questions resolved
- [x] Architecture can be finalized or updated

## Status

READY-FOR-PLANNING

## Notes

All open questions (OQ-001 through OQ-007) and critical gaps have been addressed and closed with concrete decisions and rationales. Architect review confirms feasibility decisions are sufficient and grants the READY-FOR-PLANNING gate.

---

# 11. References

Referenced artifacts:

- ISSUE: [CR-019-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-019-ISSUE.md)
- DOMAIN: [CAKRA-DOMAIN.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domain/CAKRA-DOMAIN.md)

Referenced codebase locations:

- [`WorkPackageView.vue`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue)
- [`requests.ts`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/api/requests.ts)
- [`WorkPackagesController.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/WorkPackagesController.cs)
- [`RequestsController.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/RequestsController.cs)
- [`RequestCommands.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs)
