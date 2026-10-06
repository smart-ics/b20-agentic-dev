---
Title: Bulk Multi-line Quick Capture Architecture (CR-019)
Code: CR-019
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-06
---

# 1. Overview

This architecture defines the technical realization for Change Request `CR-019`: Implementation of Bulk Multi-line Quick Capture for Work Package requests within `SCR-WP-001: Work Package Screen`.

It consumes and realizes the approved feasibility decisions from [CR-019-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-019-FEASIBILITY-ASSESSMENT.md), establishing:
1. Relaxation of the backend `RecordRequestCommandValidator` to allow empty or whitespace descriptions on `RecordRequestCommand` for rapid operational intake.
2. A typed client helper `recordRequest` in `src/frontend/Cakra.Web/src/api/requests.ts`.
3. A reusable multi-line parsing utility that normalizes pasted task lists by automatically stripping list bullets, numbering, checkboxes, and blank lines.
4. An interactive "Parse & Preview" component within `WorkPackageView.vue` (`SCR-WP-001`) in both the **Create Work Package Modal** and the **Detail Panel Scope Management section**.
5. Client-side sequential/concurrent orchestration that creates the Work Package container first (if in Create mode) and then records constituent requests with inherited context (`workPackageId`, `customerId`, `productId`), default status (`CAPTURED`), and standard priorities.
6. A partial-success notification strategy to ensure that transient request recording errors never discard the newly created container.

---

# 2. Architectural Basis

## Business Context

- ISSUE: [CR-019-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-019-ISSUE.md)
- DOMAIN: [CAKRA-DOMAIN.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domain/CAKRA-DOMAIN.md) (Work Package & Request domains)
- UI LAYOUT: [Work Package Screen (`SCR-WP-001`)](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-019-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-019-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

This architecture consumes and realizes the approved decisions:
- `GAP-001` & `OQ-007`: Relaxation of `RecordRequestCommandValidator` so minimal requests without upfront descriptions are permitted.
- `GAP-002` & `OQ-001`: Multi-line text entry with "Parse & Preview" candidate list in Create Work Package modal.
- `GAP-003` & `OQ-006`: Dual-placement supporting multi-line quick capture in Scope Management of `WorkPackageView.vue`.
- `GAP-004`: Typed client method `recordRequest` added to `requests.ts`.
- `OQ-002`: Context attribute inheritance (`customerId`, `productId`, `workPackageId`, defaults: `GENERAL`, `NORMAL`, unassigned owner).
- `OQ-003`: Regex-based normalization of bullet points, numbered items, and markdown checkboxes.
- `OQ-004` & `OQ-005`: Client-side orchestration with partial-success preservation and warning alert.

---

# 3. Scope

## Included

1. **Backend Command Validation Adjustment (`Cakra.Modules.Request.Services`)**:
   - Update `RecordRequestCommandValidator.cs` to make `Description` optional (allow empty or whitespace string without rejecting).
2. **Frontend Typed API Client (`src/frontend/Cakra.Web/src/api/requests.ts`)**:
   - Interface `RecordRequestPayload`.
   - Function `recordRequest(payload: RecordRequestPayload): Promise<RequestDto>`.
3. **Frontend Parsing & Normalization Utility (`WorkPackageView.vue`)**:
   - Parsing function `parseTaskListText(rawText: string): string[]` using regex matching to strip leading `-`, `*`, `+`, `\d+[\.\)]`, `\(\d+\)`, and `\[[ xX]?\]`.
   - Length validation ensuring titles do not exceed 255 characters.
4. **Create Work Package Modal Integration (`WorkPackageView.vue`)**:
   - Multi-line textarea for quick task entry.
   - "Parse Tasks" action button with count badge.
   - Candidate preview list showing parsed task items with individual delete buttons (`✕`).
   - "Clear Parsed" action button.
   - Submission handler orchestrating `POST /api/v1/work-packages` followed by `recordRequest` calls.
5. **Scope Management Quick Add Integration (`WorkPackageView.vue`)**:
   - Tabbed or collapsible interface in Scope Management allowing toggle between "Select Existing Request" and "⚡ Quick Bulk Add".
   - Same parse & preview experience persisting requests directly to the active selected Work Package.
6. **Error Handling & Feedback**:
   - Preserves created container upon partial failure.
   - Displays clear warning alert with list of uncreated task titles for immediate retry.

## Excluded

- New database schema migrations (tables `[request].[Requests]` and `[workpackage].[WorkPackages]` already support all required columns).
- New composite or batch backend controller endpoints (client-side orchestration reuses existing authoritative APIs).
- Sub-task checklist generation during quick capture (sub-tasks can be added subsequently via `SCR-REQ-003`).

---

# 4. Technical Decisions

## TD-001: Backend Validator Relaxation for Minimal Quick Capture
In `Cakra.Modules.Request.Services.RecordRequestCommandValidator`:
- Currently:
  ```csharp
  RuleFor(x => x.Description)
      .NotEmpty().WithMessage("Request description is required.");
  ```
- Target:
  Remove the `.NotEmpty()` requirement on `Description` (or allow empty string). `Description` remains a valid string property on `RecordRequestCommand`, defaulting to `string.Empty` if null.
- Rationale: Minimal intake demands captured during fast brainstorming only have a title. Requiring verbose descriptions at intake contradicts quick capture.

## TD-002: Line Normalization Regex Pattern
The text parser in `WorkPackageView.vue` processes lines as follows:
```typescript
export function normalizeTaskLine(line: string): string {
  // 1. Strip markdown checkboxes like [ ], [x], [X]
  let cleaned = line.replace(/^\s*\[[ xX]?\]\s*/, '')
  // 2. Strip bullet prefixes (-, *, +) and numeric prefixes (1., 1), (1))
  cleaned = cleaned.replace(/^\s*(?:[-*+]|\d+[\.\)]|\(\d+\))\s*/, '')
  // 3. Trim whitespace
  return cleaned.trim()
}
```
Any cleaned line with `length === 0` is discarded. Any line exceeding 255 characters is truncated or flagged.

## TD-003: Client-Side Orchestration Sequence (Create Modal)
When the user submits the Create modal with parsed candidate requests:
```text
┌─────────────────────────────────────────────────────────────┐
│ 1. POST /api/v1/work-packages                               │
│    Payload: { name, objective, ownerPersonId, customerId,   │
│               productId }                                   │
└──────────────────────────────┬──────────────────────────────┘
                               │ Success -> createdPackage.id
                               ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. Parallel / Settled Requests                              │
│    For each candidateTitle:                                 │
│    POST /api/v1/requests                                    │
│    Payload: {                                               │
│      title: candidateTitle,                                 │
│      description: "",                                       │
│      customerId: createForm.customerId || null,             │
│      productId: createForm.productId || null,               │
│      workPackageId: createdPackage.id,                      │
│      requestType: "GENERAL",                                │
│      priority: "NORMAL"                                     │
│    }                                                        │
└──────────────────────────────┬──────────────────────────────┘
                               ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. Evaluate Results                                         │
│    - If all succeeded: Close modal, select package, refresh.│
│    - If partial failure: Close modal, select package,       │
│      display warning alert with failed task titles.         │
└─────────────────────────────────────────────────────────────┘
```

## TD-004: Detail Scope Section Quick Add
In the Scope Management section of `WorkPackageView.vue`:
- A UI segment control / pill button allows switching between:
  1. `Existing Request` (existing dropdown and manual ID input).
  2. `Bulk Quick Add` (textarea, Parse Tasks button, preview list, and Add to Scope button).
- On submit, requests are created with `workPackageId: selectedWorkPackage.value.id`, `customerId: selectedWorkPackage.value.customerId`, `productId: selectedWorkPackage.value.productId`.
- Once recorded, the scope items table is reloaded via `loadScopeItems(selectedWorkPackage.value.id)`.

---

# 5. Component Responsibilities

| Component | Responsibility |
|------------|---------------|
| `RecordRequestCommandValidator` (`Backend`) | Validates `RecordRequestCommand`, allowing empty descriptions for minimal quick intake. |
| `requests.ts` (`Frontend API`) | Provides typed API call `recordRequest(payload)` wrapping `POST /api/v1/requests`. |
| `WorkPackageView.vue` (`Frontend View`) | Hosts the multi-line input, line normalization parser, candidate preview chips/list, and orchestration logic. |
| `WorkPackagesController` (`Backend API`) | Processes `POST /api/v1/work-packages` to create the container aggregate. |
| `RequestsController` (`Backend API`) | Processes `POST /api/v1/requests` to create individual request aggregate records linked to `workPackageId`. |

---

# 6. Integration Design

| Source | Target | Purpose |
|----------|----------|----------|
| `WorkPackageView.vue` | `WorkPackagesController` (`POST /api/v1/work-packages`) | Creates container Work Package in `DRAFT` status. |
| `WorkPackageView.vue` | `RequestsController` (`POST /api/v1/requests`) | Records individual minimal requests linked to the Work Package container. |
| `WorkPackageView.vue` | `WorkPackagesController` (`GET /api/v1/work-packages/{id}/scope`) | Refreshes and displays the updated scope items including the newly created requests. |

---

# 7. Data Ownership

| Data | Owner |
|--------|--------|
| Work Package container attributes (`Name`, `Objective`, `OwnerPersonId`, `CustomerId`, `ProductId`, `Status`) | `Cakra.Modules.WorkPackage` |
| Request attributes (`Title`, `Description`, `CustomerId`, `ProductId`, `WorkPackageId`, `Status`, `Priority`, `RequestType`) | `Cakra.Modules.Request` |
| Scope membership (`[workpackage].[WorkPackageRequests]`) | Synchronized via `RequestRecorded` event or domain linkage |

---

# 8. Database Design

## New Tables
None.

## Modified Tables
None. Existing tables `[workpackage].[WorkPackages]`, `[workpackage].[WorkPackageRequests]`, and `[request].[Requests]` already support all required columns.

## Relationships
Existing foreign key and GUID references between `[request].[Requests].[WorkPackageId]` and `[workpackage].[WorkPackages].[Id]` are reused.

## Migration Considerations
No schema migration required. Existing database structure is 100% compatible.

---

# 9. Cross-Cutting Concerns

1. **Validation & Normalization**:
   - Leading/trailing whitespace and list formatting prefixes must be stripped deterministically.
   - Title length capped at 255 characters.
2. **Performance & Concurrency**:
   - `Promise.allSettled` is used for concurrent request recording, ensuring non-blocking execution and full failure reporting.
3. **Observability & User Feedback**:
   - Detailed feedback on created count and any failed titles.
   - Logs request creation with correlated `WorkPackageId`.

---

# 10. Implementation Constraints

1. **Existing Endpoint Compatibility**:
   - Must use existing REST endpoints `POST /api/v1/work-packages` and `POST /api/v1/requests`.
2. **TypeScript & Vue Conventions**:
   - Vue 3 `<script setup lang="ts">` with Bootstrap 5 styling conforming to existing `WorkPackageView.vue` patterns.
3. **Domain Event Propagation**:
   - Creating requests via `POST /api/v1/requests` triggers standard domain events (`RequestRecorded`), ensuring feed synchronization and audit integrity without duplicate logic.

---

# 11. Acceptance Conditions

1. `RecordRequestCommandValidator` permits requests with empty `Description` without validation errors.
2. Users can paste a bulleted or numbered list into the Create Work Package modal and click "Parse Tasks" to see clean, normalized candidate titles.
3. Users can remove candidate items individually before submitting.
4. Submitting the Create modal creates the Work Package, records all candidate requests with `workPackageId`, selects the new package, and displays all new requests in the Scope table.
5. In the Detail Panel Scope section, users can toggle to "⚡ Quick Bulk Add", paste tasks, and add them directly to the active package.
6. If a request recording fails, the Work Package is preserved and a clear warning banner lists the failed task titles.
