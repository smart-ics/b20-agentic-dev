# Request Collaboration Navigation Specification

This document details the navigation flows, screen transitions, and actor interactions for the **Request Collaboration** operational category, derived from:
* **Scenarios:** `SC-COL-001` through `SC-COL-004` (`operational/scenarios/request-collaboration-scenarios.md`)
* **Use Cases:** `UC-COL-001` through `UC-COL-004` (`operational/use-cases/collaboration-use-cases.md`)
* **User Journeys:** `UJ-COL-001` through `UJ-COL-004` (`operational/user-journey/collaboration-user-journeys.md`)
* **Actor Model:** Implementator (`operational/actors/actor-model.md`)
* **Domain Models:** Request Domain (`operational/domains/request-domain.md`), Post Domain (`operational/domains/post-domain.md`)

---

## 1. Collaboration Navigation Hierarchy

```text
Requests / Collaboration Area
├── SCR-REQ-005: My Assigned Requests (Personal Operational Queue)
│   └── [Action: Select Request] ───────────► SCR-REQ-003: Request Detail
│
├── SCR-REQ-004: Request Search & History (Knowledge Retrieval)
│   └── [Action: Open Historical Record] ───► SCR-REQ-003: Request Detail
│
└── SCR-REQ-003: Request Detail (Collaborative Context)
    ├── [UC-COL-001] Add Supporting Information / Post / Comment / Attachment
    └── [UC-COL-003] Track Request Progress Timeline & Step History
```

---

## 2. Journey Navigation Mappings

### UJ-COL-001: Record Supporting Information
* **Primary Actor:** Implementator (and collaborating handlers)
* **Entry Point:** `SCR-REQ-003: Request Detail` (accessed via `SCR-REQ-001` or `SCR-REQ-005`)
* **Destination:** `SCR-REQ-003: Request Detail`
* **Navigation Interaction:**
  1. Actor accesses target request in `SCR-REQ-003`.
  2. Actor reviews existing context and prior discussion.
  3. Actor invokes supporting information action.
  4. Actor inputs observation notes, customer clarification, or technical findings, and attaches reference materials/evidence.
  5. Actor submits; information is attached as an operational Post/Comment to the Request record and updates in place.

### UJ-COL-002: Search Request History
* **Primary Actor:** Implementator
* **Entry Point:** Global Navigation (`Requests > Search & History`) or link from `SCR-REQ-001`
* **Destination:** `SCR-REQ-004: Request Search & History`
* **Navigation Interaction:**
  1. Implementator navigates to `SCR-REQ-004`.
  2. Enters search criteria (keywords, customer, product, date range, resolution outcome).
  3. Executes query and browses matching historical records.
  4. Selects a historical request to navigate to `SCR-REQ-003: Request Detail` to inspect historical demand, progress milestones, and resolution outcomes.

### UJ-COL-003: Track Request Progress
* **Primary Actor:** Implementator
* **Entry Point:** `SCR-REQ-001: Request List`, `SCR-REQ-005: My Assigned Requests`, or `SCR-REQ-004: Search`
* **Destination:** `SCR-REQ-003: Request Detail`
* **Navigation Interaction:**
  1. Implementator locates the request and opens `SCR-REQ-003`.
  2. Views authoritative operational status, currently assigned owner, and milestone standing.
  3. Inspects chronological progression of steps and recorded activities to determine if work is advancing normally.

### UJ-COL-004: Review Assigned Requests
* **Primary Actor:** Implementator (and Request Owners)
* **Entry Point:** Global Navigation (`Requests > My Assigned Requests`) or Home Workspace
* **Destination:** `SCR-REQ-005: My Assigned Requests`
* **Navigation Interaction:**
  1. Actor navigates to `SCR-REQ-005`.
  2. Reviews list of requests for which they are currently responsible with statuses, priorities, and deadlines.
  3. Applies grouping/filters (e.g., by customer, urgency, or state).
  4. Selects highest priority request to navigate into `SCR-REQ-003: Request Detail` for execution or evaluation.
