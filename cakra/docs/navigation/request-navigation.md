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
│   └── Request Selection ──────────► SCR-REQ-003: Request Detail
│
├── SCR-REQ-002: Create Request
│   ├── Dynamic Return ─────────────► Originating Screen (Fallback: SCR-FEED-001)
│   └── Creation Transition ────────► SCR-REQ-003: Request Detail
│
└── SCR-REQ-003: Request Detail
    ├── Dynamic Return ─────────────► Originating Screen (Fallback: SCR-FEED-001)
    └── Assigned Return ────────────► SCR-REQ-005: My Assigned Requests

[Note: SCR-REQ-001 Request List is retired; operational requests are visualized in SCR-FEED-001]
```

---

## 2. Journey Movement Paths

### UJ-REQ-001: Record Customer Request
* **Primary Actor:** Implementator
* **Entry Point:** Global Action (`+ New Request`), Operational Feed (`SCR-FEED-001`), or direct URL (`/requests/create`)
* **Destination:** `SCR-REQ-002: Create Request`
* **Exit / Destination:** Originating Screen (Dynamic history return, fallback to `SCR-FEED-001`) or `SCR-REQ-003: Request Detail`
* **Movement Path:**
  1. Actor arrives at `SCR-REQ-002` from `SCR-FEED-001: Operational Feed` or via Global Action (`+ New Request`).
  2. Upon completing the form, actor navigates to `SCR-REQ-003: Request Detail`, or returns to origin (`SCR-FEED-001`) via dynamic return on cancel or back.

### UJ-REQ-002: Assign Request Owner
* **Primary Actor:** Implementator
* **Entry Point:** Operational Feed (`SCR-FEED-001`), `SCR-REQ-005: My Assigned Requests`, or direct link
* **Destination:** `SCR-REQ-003: Request Detail`
* **Exit / Destination:** Originating Screen (Dynamic history return, fallback to `SCR-FEED-001`) or `SCR-REQ-005: My Assigned Requests`
* **Movement Path:**
  1. Actor accesses the system via primary global sidebar entry points (`SCR-FEED-001: Operational Feed` or `SCR-REQ-005: My Assigned Requests`).
  2. Actor locates a Request on `SCR-FEED-001` or `SCR-REQ-005`.
  3. Actor selects the Request, navigating to `SCR-REQ-003: Request Detail`.
  4. Actor remains on `SCR-REQ-003` or returns to originating screen via dynamic back navigation (fallback to `SCR-FEED-001`) or `SCR-REQ-005`.

### UJ-REQ-003: Evaluate Request
* **Primary Actor:** Request Owner
* **Entry Point:** Operational Feed (`SCR-FEED-001`) or `SCR-REQ-005: My Assigned Requests`
* **Destination:** `SCR-REQ-003: Request Detail`
* **Exit / Destination:** Originating Screen (Dynamic history return, fallback to `SCR-FEED-001`) or `SCR-REQ-005: My Assigned Requests`
* **Movement Path:**
  1. Actor selects an assigned Request from `SCR-FEED-001` or `SCR-REQ-005`.
  2. Actor arrives at `SCR-REQ-003: Request Detail` to review the record.

### UJ-REQ-004: Accept Request Responsibility
* **Primary Actor:** Request Owner
* **Entry Point:** `SCR-REQ-003: Request Detail`
* **Destination:** `SCR-REQ-003: Request Detail`
* **Exit / Destination:** Originating Screen (Dynamic history return, fallback to `SCR-FEED-001`) or `SCR-REQ-005: My Assigned Requests`
* **Movement Path:**
  1. Actor is on `SCR-REQ-003: Request Detail`.
  2. Actor remains on `SCR-REQ-003` upon completion or returns to their assigned queue (`SCR-REQ-005`).

### UJ-REQ-005: Reject Request
* **Primary Actor:** Request Owner
* **Entry Point:** `SCR-REQ-003: Request Detail`
* **Destination:** `SCR-REQ-003: Request Detail`
* **Exit / Destination:** Originating Screen (Dynamic history return, fallback to `SCR-FEED-001`) or `SCR-REQ-005: My Assigned Requests`
* **Movement Path:**
  1. Actor is on `SCR-REQ-003: Request Detail`.
  2. Actor remains on `SCR-REQ-003` or returns to `SCR-REQ-005` or originating screen.

### UJ-REQ-006: Escalate Request
* **Primary Actor:** Request Owner
* **Entry Point:** `SCR-REQ-003: Request Detail`
* **Destination:** `SCR-REQ-003: Request Detail`
* **Exit / Destination:** Originating Screen (Dynamic history return, fallback to `SCR-FEED-001`) or `SCR-REQ-005: My Assigned Requests`
* **Movement Path:**
  1. Actor is on `SCR-REQ-003: Request Detail`.
  2. Actor remains on `SCR-REQ-003` or returns to `SCR-REQ-005` or originating screen.

### UJ-REQ-007: Request Management Decision
* **Primary Actor:** Request Owner
* **Entry Point:** `SCR-REQ-003: Request Detail`
* **Destination:** `SCR-REQ-003: Request Detail`
* **Exit / Destination:** Originating Screen (Dynamic history return, fallback to `SCR-FEED-001`) or `SCR-REQ-005: My Assigned Requests`
* **Movement Path:**
  1. Actor is on `SCR-REQ-003: Request Detail`.
  2. Actor remains on `SCR-REQ-003` or returns to `SCR-REQ-005` or originating screen.

### UJ-REQ-008: Review Request Completion
* **Primary Actor:** Implementator
* **Entry Point:** Operational Feed (`SCR-FEED-001`), `SCR-REQ-005: My Assigned Requests`, or direct link
* **Destination:** `SCR-REQ-003: Request Detail`
* **Exit / Destination:** Originating Screen (Dynamic history return, fallback to `SCR-FEED-001`) or `SCR-REQ-005: My Assigned Requests`
* **Movement Path:**
  1. Actor accesses the system via primary global sidebar entry points (`SCR-FEED-001: Operational Feed` or `SCR-REQ-005: My Assigned Requests`).
  2. Actor locates a completed Request on `SCR-FEED-001` or `SCR-REQ-005`.
  3. Actor selects the Request, navigating to `SCR-REQ-003: Request Detail`.
  4. Actor remains on `SCR-REQ-003` or returns to originating screen via dynamic back navigation (fallback to `SCR-FEED-001`) or `SCR-REQ-005`.
