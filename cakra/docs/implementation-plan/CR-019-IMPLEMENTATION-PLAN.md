---
Title: Implementation Plan for Bulk Multi-line Quick Capture in SCR-WP-001 (CR-019)
Code: CR-019
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-10-06
Status: NOT-STARTED
Execution Approval: APPROVED
---

# 1. Objective

Implement Change Request `CR-019`: Bulk Multi-line Quick Capture for Work Package requests within `SCR-WP-001: Work Package Screen`.

Deliver end-to-end realization across:
1. Backend command validation in `RecordRequestCommandValidator` to support rapid minimal demand intake without requiring upfront descriptions.
2. Frontend typed API client helper `recordRequest` in `src/frontend/Cakra.Web/src/api/requests.ts`.
3. Deterministic line parsing and formatting normalization stripping bullets, numbers, and checkboxes.
4. "Parse & Preview" interactive experience within the Create Work Package modal in `WorkPackageView.vue`.
5. "⚡ Quick Bulk Add" interactive experience within the Scope Management detail panel of `WorkPackageView.vue`.
6. Client-side sequential and concurrent orchestration with partial failure warnings preserving the created container.

Planning Mode: FEATURE-PLANNING

Referenced artifacts:

- ISSUE: [CR-019-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-019-ISSUE.md)
- FEASIBILITY-ASSESSMENT: [CR-019-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-019-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)
- ARCHITECTURE: [CR-019-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CR-019-ARCHITECTURE.md) (authoritative target architecture)

Architecture Applicability: ARCHITECTURE-REQUIRED

---

# 2. Planning Scope

This plan covers all backend validator updates, frontend API additions, UI parsing components, client-side orchestration, and verification required to realize `CR-019`:

1. **Backend Command Validator Update (`Cakra.Modules.Request.Services`)**:
   - Update `RecordRequestCommandValidator` so `Description` is optional (allows empty/null strings).
   - Ensure `RecordRequestCommand` handles null/empty descriptions gracefully without domain error.
2. **Frontend Typed API Client (`Cakra.Web`)**:
   - Add `recordRequest` helper function and `RecordRequestPayload` interface in `src/frontend/Cakra.Web/src/api/requests.ts`.
3. **Frontend Parsing & Interactive UI Components (`Cakra.Web`)**:
   - Implement `parseTaskListText` and `normalizeTaskLine` functions in `WorkPackageView.vue`.
   - Add multi-line textarea, "Parse Tasks" action button, and parsed candidate preview list with individual delete buttons (`✕`) in the Create Work Package modal.
   - Implement client-side orchestration creating the Work Package container and subsequently recording child requests.
   - Add "⚡ Quick Bulk Add" tab/toggle in the Work Package Detail Scope Management section with direct persistence to the active package.
4. **Resilience & Feedback Handling (`Cakra.Web`)**:
   - Add partial-success alert banner retaining the created package while identifying any uncreated task titles for immediate retry.
5. **Full-Stack Verification**:
   - Backend compilation and unit test execution (`dotnet test`).
   - Frontend TypeScript verification and production build (`npm run build`).

---

# 3. Dependencies

- .NET 8 SDK (`Cakra.Modules.Request`, `Cakra.Api`)
- Node.js & npm (Vue 3 / Vite / TypeScript in `src/frontend/Cakra.Web`)
- Existing REST endpoints `POST /api/v1/work-packages` and `POST /api/v1/requests` (no schema migrations or new routes needed)

For slice dependencies:
- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires the referenced slice to have implementation status `IMPLEMENTED`.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Backend Validation Realignment | NOT-STARTED | NOT-REVIEWED | 0/1 |
| P2 - Frontend API Client & Normalization Utility | NOT-STARTED | NOT-REVIEWED | 0/1 |
| P3 - Work Package View Quick Capture Integration | NOT-STARTED | NOT-REVIEWED | 0/1 |
| P4 - Verification & Build Validation | NOT-STARTED | NOT-REVIEWED | 0/1 |

---

# 5. Phases

## P1 - Backend Validation Realignment

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED

### P1-S01

Title: Relax Description Validation in RecordRequestCommandValidator

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

Objective:
1. Modify `RecordRequestCommandValidator.cs` in `cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs` to remove the `.NotEmpty()` constraint on `Description`, allowing minimal quick-captured demands without upfront descriptions.
2. Verify that `RecordRequestCommand` handles empty or whitespace description without throwing exceptions.
3. Ensure existing unit tests for `RecordRequestCommand` in `Cakra.Tests.Unit` pass or are updated to reflect that empty descriptions are valid.

Depends On: None  
Repository: smart-ics/b20-agentic-dev  

Completion Criteria:
- `RecordRequestCommandValidator` accepts `RecordRequestCommand` with empty string or null `Description`.
- `dotnet test cakra/tests/backend/Cakra.Tests.Unit` builds and passes without regressions.

Notes:
- Aligns domain intake validation with genuine quick-capture brainstorming.

---

## P2 - Frontend API Client & Normalization Utility

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED

### P2-S02

Title: Add recordRequest API Client Method and Normalization Helper

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

Objective:
1. In `src/frontend/Cakra.Web/src/api/requests.ts`:
   - Define TypeScript interface `RecordRequestPayload` with properties:
     - `title: string` (required)
     - `description?: string`
     - `customerId?: string | null`
     - `productId?: string | null`
     - `workPackageId?: string | null`
     - `requestType?: string`
     - `priority?: string`
     - `complexity?: number`
   - Implement and export `recordRequest(payload: RecordRequestPayload): Promise<RequestDto>` calling `POST /api/v1/requests`.
   - Export `recordRequest` on the default export object `requestService`.
2. Ensure types match backend API serialization contract.

Depends On: P1-S01  
Repository: smart-ics/b20-agentic-dev  

Completion Criteria:
- `src/frontend/Cakra.Web/src/api/requests.ts` exports `recordRequest` function and `RecordRequestPayload` interface.
- Frontend TypeScript type check compiles cleanly without typing errors.

Notes:
- Reusable across any view requiring quick operational demand creation.

---

## P3 - Work Package View Quick Capture Integration

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED

### P3-S03

Title: Integrate Bulk Quick Capture into Create Modal and Scope Management

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

Objective:
1. In `src/frontend/Cakra.Web/src/views/WorkPackageView.vue`:
   - Add line cleaning and parsing utility functions:
     - `normalizeTaskLine(line: string): string` stripping markdown checkboxes (`[ ]`, `[x]`), bullets (`-`, `*`, `+`), and numbered prefixes (`1.`, `1)`), trimming whitespace.
     - `parseTaskListText(rawText: string): string[]` splitting by newline, ignoring blank lines, enforcing maximum 255-character title limit.
   - **Create Work Package Modal Integration**:
     - Add a dedicated "Quick Capture Tasks (Optional)" collapsible or card section beneath the container fields with a multi-line `<textarea>`.
     - Add "Parse Tasks" button that parses the text into a reactive `candidateTasks` string array.
     - Render candidate tasks in a preview list with a counter and delete button (`✕`) per item.
     - Update `handleCreateWorkPackage` to:
       1. Call `POST /api/v1/work-packages` to create the container.
       2. If `candidateTasks` has items, execute `Promise.allSettled` calling `recordRequest` for each title with `workPackageId: created.id`, `customerId: createForm.customerId || null`, `productId: createForm.productId || null`, `requestType: "GENERAL"`, `priority: "NORMAL"`.
       3. If all succeed, select the created package and show success toast.
       4. If any fail, preserve the created package, select it, and display a warning alert listing the specific failed titles.
   - **Scope Management Section Integration**:
     - Add a toggle/tab ("Existing Request" vs "⚡ Quick Bulk Add").
     - In "Quick Bulk Add", provide a multi-line textarea, "Parse Tasks" button, candidate preview list, and "Add Tasks to Scope" submit button.
     - Submitting creates each request linked to `selectedWorkPackage.id`, inherits customer/product, and reloads `loadScopeItems`.

Depends On: P2-S02  
Repository: smart-ics/b20-agentic-dev  

Completion Criteria:
- Create modal renders the bulk text input, parse button, and candidate preview list.
- Creating a Work Package with candidate items successfully creates both the package and the requests, linking them together.
- Scope section provides "⚡ Quick Bulk Add" and creates requests directly linked to the active package.
- Form handles partial request failures gracefully without losing the Work Package.

Notes:
- Seamlessly satisfies both initial container brainstorming and post-creation task expansion.

---

## P4 - Verification & Build Validation

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED

### P4-S04

Title: Execute Backend and Frontend Verification Builds

Implementation Status: NOT-STARTED  
Review Status: NOT-REVIEWED  

Objective:
1. Run backend unit tests (`dotnet test cakra/tests/backend/Cakra.Tests.Unit`) to verify validator changes and overall test integrity.
2. Run backend integration tests (`dotnet test cakra/tests/backend/Cakra.Tests.Integration`) to verify end-to-end endpoint stability.
3. Run frontend TypeScript checks and production packaging (`npm run build` in `cakra/src/frontend/Cakra.Web`) to guarantee zero compilation or bundle errors.
4. Manually test or verify against acceptance conditions defined in `CR-019-ARCHITECTURE.md`.

Depends On: P3-S03  
Repository: smart-ics/b20-agentic-dev  

Completion Criteria:
- All backend test suites exit with code 0.
- Frontend build succeeds with zero errors.
- All acceptance conditions in CR-019-ARCHITECTURE.md are satisfied.

Notes:
- Final verification gate before releasing for testing and deployment.

---

# 6. Change Log

- 2026-10-06: Initial release of `CR-019-IMPLEMENTATION-PLAN.md` with Execution Approval `APPROVED`.
