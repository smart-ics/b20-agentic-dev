# Request Lifecycle Navigation Specification

This document details the destinations and movement paths for the **Request Lifecycle** operational category, derived from:
* **Scenarios:** `SC-REQ-001` through `SC-REQ-008` (`operational/scenarios/request-lifecycle-scenarios.md`)
* **Use Cases:** `UC-REQ-001` through `UC-REQ-008` (`operational/use-cases/request-lifecycle-use-cases.md`)
* **User Journeys:** `UJ-REQ-001` through `UJ-REQ-008` (`operational/user-journey/request-lifecycle-user-journeys.md`)

---

## 1. Request Lifecycle Navigation Hierarchy

```text
Requests Area
├── SCR-REQ-005: My Assigned Requests [Primary Global Navigation]
│   ├── Request Selection ──────────► SCR-REQ-003: Request Detail
│   └── All Requests Link ──────────► SCR-REQ-001: Request List
│
├── SCR-REQ-001: Request List [Contextual Navigation]
│   ├── Create Request Link ────────► SCR-REQ-002: Create Request
│   └── Request Selection ──────────► SCR-REQ-003: Request Detail
│
├── SCR-REQ-002: Create Request
│   └── Submission / Return ────────► SCR-REQ-001: Request List
│                                    └── SCR-REQ-003: Request Detail
│
└── SCR-REQ-003: Request Detail
    └── Back / Return ──────────────► SCR-REQ-001: Request List
                                     └── SCR-REQ-005: My Assigned Requests
```

---

## 2. Journey Movement Paths

### UJ-REQ-001: Record Customer Request
* **Primary Actor:** Implementator
* **Entry Point:** Global Action (`+ New Request`) or Request List (`SCR-REQ-001`, accessed contextually from `SCR-REQ-005: My Assigned Requests` via "All Requests" link or direct URL)
* **Destination:** `SCR-REQ-002: Create Request`
* **Exit / Destination:** `SCR-REQ-001: Request List` or `SCR-REQ-003: Request Detail`
* **Movement Path:**
  1. Actor arrives at `SCR-REQ-002` from contextual `SCR-REQ-001` or via Global Action. Primary global sidebar navigation leads to `SCR-FEED-001: Operational Feed` or `SCR-REQ-005: My Assigned Requests`, from which `SCR-REQ-001` is accessed contextually via the "All Requests" link.
  2. Upon completing the form, actor navigates to `SCR-REQ-001: Request List` or `SCR-REQ-003: Request Detail`.

### UJ-REQ-002: Assign Request Owner
* **Primary Actor:** Implementator
* **Entry Point:** Contextual `SCR-REQ-001: Request List` (accessed from primary global sidebar entry point `SCR-REQ-005: My Assigned Requests` via "All Requests" link, `SCR-FEED-001: Operational Feed`, or direct URL)
* **Destination:** `SCR-REQ-003: Request Detail`
* **Exit / Destination:** `SCR-REQ-001: Request List` or `SCR-REQ-005: My Assigned Requests`
* **Movement Path:**
  1. Actor accesses the system via primary global sidebar entry points (`SCR-FEED-001` or `SCR-REQ-005`). From `SCR-REQ-005: My Assigned Requests`, actor navigates contextually to `SCR-REQ-001: Request List` via the "All Requests" link.
  2. Actor locates a Request on `SCR-REQ-001`.
  3. Actor selects the Request, navigating to `SCR-REQ-003: Request Detail`.
  4. Actor remains on `SCR-REQ-003` or returns to `SCR-REQ-001` / `SCR-REQ-005`.

### UJ-REQ-003: Evaluate Request
* **Primary Actor:** Request Owner
* **Entry Point:** `SCR-REQ-001: Request List` or `SCR-REQ-005: My Assigned Requests`
* **Destination:** `SCR-REQ-003: Request Detail`
* **Exit / Destination:** `SCR-REQ-001: Request List` or `SCR-REQ-005: My Assigned Requests`
* **Movement Path:**
  1. Actor selects an assigned Request from `SCR-REQ-001` or `SCR-REQ-005`.
  2. Actor arrives at `SCR-REQ-003: Request Detail` to review the record.

### UJ-REQ-004: Accept Request Responsibility
* **Primary Actor:** Request Owner
* **Entry Point:** `SCR-REQ-003: Request Detail`
* **Destination:** `SCR-REQ-003: Request Detail`
* **Exit / Destination:** `SCR-REQ-001: Request List` or `SCR-REQ-005: My Assigned Requests`
* **Movement Path:**
  1. Actor is on `SCR-REQ-003: Request Detail`.
  2. Actor remains on `SCR-REQ-003` upon completion or returns to their assigned queue (`SCR-REQ-005`).

### UJ-REQ-005: Reject Request
* **Primary Actor:** Request Owner
* **Entry Point:** `SCR-REQ-003: Request Detail`
* **Destination:** `SCR-REQ-003: Request Detail`
* **Exit / Destination:** `SCR-REQ-001: Request List` or `SCR-REQ-005: My Assigned Requests`
* **Movement Path:**
  1. Actor is on `SCR-REQ-003: Request Detail`.
  2. Actor remains on `SCR-REQ-003` or returns to `SCR-REQ-001` / `SCR-REQ-005`.

### UJ-REQ-006: Escalate Request
* **Primary Actor:** Request Owner
* **Entry Point:** `SCR-REQ-003: Request Detail`
* **Destination:** `SCR-REQ-003: Request Detail`
* **Exit / Destination:** `SCR-REQ-001: Request List` or `SCR-REQ-005: My Assigned Requests`
* **Movement Path:**
  1. Actor is on `SCR-REQ-003: Request Detail`.
  2. Actor remains on `SCR-REQ-003` or returns to `SCR-REQ-001` / `SCR-REQ-005`.

### UJ-REQ-007: Request Management Decision
* **Primary Actor:** Request Owner
* **Entry Point:** `SCR-REQ-003: Request Detail`
* **Destination:** `SCR-REQ-003: Request Detail`
* **Exit / Destination:** `SCR-REQ-001: Request List` or `SCR-REQ-005: My Assigned Requests`
* **Movement Path:**
  1. Actor is on `SCR-REQ-003: Request Detail`.
  2. Actor remains on `SCR-REQ-003` or returns to `SCR-REQ-001` / `SCR-REQ-005`.

### UJ-REQ-008: Review Request Completion
* **Primary Actor:** Implementator
* **Entry Point:** Contextual `SCR-REQ-001: Request List` (accessed from primary global sidebar entry point `SCR-REQ-005: My Assigned Requests` via "All Requests" link, `SCR-FEED-001: Operational Feed`, or direct URL)
* **Destination:** `SCR-REQ-003: Request Detail`
* **Exit / Destination:** `SCR-REQ-001: Request List` or `SCR-REQ-005: My Assigned Requests`
* **Movement Path:**
  1. Actor accesses the system via primary global sidebar entry points (`SCR-FEED-001` or `SCR-REQ-005`). From `SCR-REQ-005: My Assigned Requests`, actor navigates contextually to `SCR-REQ-001: Request List` via the "All Requests" link.
  2. Actor locates a completed Request on `SCR-REQ-001`.
  3. Actor selects the Request, navigating to `SCR-REQ-003: Request Detail`.
  4. Actor remains on `SCR-REQ-003` or returns to `SCR-REQ-001` / `SCR-REQ-005`.
