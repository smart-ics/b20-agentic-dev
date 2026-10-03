# Request Collaboration Navigation Specification

This document details the destinations and movement paths for the **Request Collaboration** operational category, derived from:
* **Scenarios:** `SC-COL-001` through `SC-COL-004` (`operational/scenarios/request-collaboration-scenarios.md`)
* **Use Cases:** `UC-COL-001` through `UC-COL-004` (`operational/use-cases/collaboration-use-cases.md`)
* **User Journeys:** `UJ-COL-001` through `UJ-COL-004` (`operational/user-journey/collaboration-user-journeys.md`)

---

## 1. Collaboration Navigation Hierarchy

```text
Requests Area
├── SCR-REQ-005: My Assigned Requests [Primary Global Navigation]
│   └── Request Selection ──────────► SCR-REQ-003: Request Detail
│
├── SCR-REQ-004: Request Search & History [Contextual / Direct Navigation]
│   └── Result Selection ───────────► SCR-REQ-003: Request Detail
│
└── SCR-REQ-003: Request Detail
    └── Back / Return ──────────────► Originating Screen (SCR-REQ-005 / SCR-REQ-004 / SCR-REQ-001)
```

---

## 2. Journey Movement Paths

### UJ-COL-001: Record Supporting Information
* **Primary Actor:** Implementator
* **Entry Point:** `SCR-REQ-001: Request List` or `SCR-REQ-005: My Assigned Requests`
* **Destination:** `SCR-REQ-003: Request Detail`
* **Exit / Destination:** `SCR-REQ-001: Request List` or `SCR-REQ-005: My Assigned Requests`
* **Movement Path:**
  1. Actor navigates to `SCR-REQ-003: Request Detail` from `SCR-REQ-001` or `SCR-REQ-005`.
  2. Actor remains on `SCR-REQ-003` to view the updated Request record or navigates back to the originating queue.

### UJ-COL-002: Search Request History
* **Primary Actor:** Implementator
* **Entry Point:** Contextual Navigation / Direct URL (`/requests/search`) or `SCR-REQ-001: Request List`
* **Destination:** `SCR-REQ-004: Request Search & History`
* **Exit / Destination:** `SCR-REQ-003: Request Detail`
* **Movement Path:**
  1. Actor arrives at `SCR-REQ-004: Request Search & History` via direct URL or contextual link from `SCR-REQ-001`.
  2. Actor selects a historical record from search results, navigating to `SCR-REQ-003: Request Detail`.
  3. From `SCR-REQ-003`, actor can return to `SCR-REQ-004`.

### UJ-COL-003: Track Request Progress
* **Primary Actor:** Implementator
* **Entry Point:** `SCR-REQ-001: Request List`, `SCR-REQ-004: Request Search & History`, or `SCR-REQ-005: My Assigned Requests`
* **Destination:** `SCR-REQ-003: Request Detail`
* **Exit / Destination:** Originating Screen (`SCR-REQ-001`, `SCR-REQ-004`, or `SCR-REQ-005`)
* **Movement Path:**
  1. Actor selects a Request from `SCR-REQ-001`, `SCR-REQ-004`, or `SCR-REQ-005`.
  2. Actor arrives at `SCR-REQ-003: Request Detail` to inspect the record.
  3. Actor returns to the originating screen.

### UJ-COL-004: Review Assigned Requests
* **Primary Actor:** Implementator, Request Owner
* **Entry Point:** Global Navigation (`Requests > My Assigned`)
* **Destination:** `SCR-REQ-005: My Assigned Requests`
* **Exit / Destination:** `SCR-REQ-003: Request Detail`
* **Movement Path:**
  1. Actor navigates to `SCR-REQ-005: My Assigned Requests` via Global Navigation.
  2. Actor selects a Request from the list, navigating to `SCR-REQ-003: Request Detail`.
  3. From `SCR-REQ-003`, actor can return to `SCR-REQ-005`.
