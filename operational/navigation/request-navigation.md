# Request Lifecycle Navigation Specification

This document details the navigation flows, screen transitions, and actor interactions for the **Request Lifecycle** operational category, derived from:
* **Scenarios:** `SC-REQ-001` through `SC-REQ-008` (`operational/scenarios/request-lifecycle-scenarios.md`)
* **Use Cases:** `UC-REQ-001` through `UC-REQ-008` (`operational/use-cases/request-lifecycle-use-cases.md`)
* **User Journeys:** `UJ-REQ-001` through `UJ-REQ-008` (`operational/user-journey/request-lifecycle-user-journeys.md`)
* **Actor Model:** Implementator, Request Owner (`operational/actors/actor-model.md`)
* **Domain Model:** Request Domain (`operational/domains/request-domain.md`)

---

## 1. Request Lifecycle Navigation Hierarchy

```text
Requests Area
├── SCR-REQ-001: Request List (Triage & Overview)
│   ├── [Action: + New Request] ────────────► SCR-REQ-002: Create Request
│   └── [Action: Select Request] ───────────► SCR-REQ-003: Request Detail
│
├── SCR-REQ-002: Create Request (Intake)
│   └── [Action: Submit Request] ───────────► SCR-REQ-001: Request List (or SCR-REQ-003)
│
└── SCR-REQ-003: Request Detail (Lifecycle Workspace)
    ├── [Actor: Implementator] ─────────────► Assign Request Owner (UC-REQ-002)
    ├── [Actor: Request Owner] ─────────────► Evaluate Request (UC-REQ-003)
    ├── [Actor: Request Owner] ─────────────► Accept Responsibility -> ACTIVE (UC-REQ-004)
    ├── [Actor: Request Owner] ─────────────► Reject Request -> CLOSED (UC-REQ-005)
    ├── [Actor: Request Owner] ─────────────► Escalate Request (UC-REQ-006)
    ├── [Actor: Request Owner] ─────────────► Request Management Decision (UC-REQ-007)
    └── [Actor: Implementator] ─────────────► Review Completion -> Accept/Rework (UC-REQ-008)
```

---

## 2. Journey Navigation Mappings

### UJ-REQ-001: Record Customer Request
* **Primary Actor:** Implementator
* **Entry Point:** Global Action (`+ New Request`) or button on `SCR-REQ-001: Request List`
* **Destination:** `SCR-REQ-002: Create Request`
* **Navigation Interaction:**
  1. Implementator navigates to `SCR-REQ-002`.
  2. Implementator enters demand details, selects optional customer, product, or work package context.
  3. Implementator submits; system creates request in `CAPTURED` state.
  4. User is redirected to `SCR-REQ-001: Request List` (with new request visible) or directly into `SCR-REQ-003: Request Detail`.

### UJ-REQ-002: Assign Request Owner
* **Primary Actor:** Implementator
* **Entry Point:** `SCR-REQ-001: Request List` (filter by unassigned / `CAPTURED`)
* **Destination:** `SCR-REQ-003: Request Detail`
* **Navigation Interaction:**
  1. Implementator locates unassigned request on `SCR-REQ-001`.
  2. Implementator selects request to open `SCR-REQ-003`.
  3. In `SCR-REQ-003`, Implementator triggers ownership assignment, selects an organization member, and confirms.
  4. Request reflects new owner; user remains on `SCR-REQ-003` or returns to `SCR-REQ-001`.

### UJ-REQ-003: Evaluate Request
* **Primary Actor:** Request Owner
* **Entry Point:** `SCR-REQ-005: My Assigned Requests` or `SCR-REQ-001: Request List`
* **Destination:** `SCR-REQ-003: Request Detail`
* **Navigation Interaction:**
  1. Request Owner opens assigned request in `SCR-REQ-003`.
  2. Reviews demand description, customer context, and operational requirements.
  3. Prepares disposition decision (proceed to Accept, Reject, Escalate, or Decision Request).

### UJ-REQ-004: Accept Request Responsibility
* **Primary Actor:** Request Owner
* **Screen:** `SCR-REQ-003: Request Detail`
* **Navigation Interaction:**
  1. Within `SCR-REQ-003`, Request Owner confirms acceptance.
  2. System transitions request state to `ACTIVE`.
  3. Screen updates in place to reflect `ACTIVE` lifecycle status and enables active operational handling actions.

### UJ-REQ-005: Reject Request
* **Primary Actor:** Request Owner
* **Screen:** `SCR-REQ-003: Request Detail`
* **Navigation Interaction:**
  1. Within `SCR-REQ-003`, Request Owner selects reject action.
  2. Request Owner enters explanatory rejection reason.
  3. System transitions request state to `CLOSED` (Resolution: Rejected).
  4. Screen updates in place to show closed resolution summary.

### UJ-REQ-006: Escalate Request
* **Primary Actor:** Request Owner
* **Screen:** `SCR-REQ-003: Request Detail`
* **Navigation Interaction:**
  1. Within `SCR-REQ-003`, Request Owner initiates escalation.
  2. Request Owner enters rationale and required assistance level.
  3. System flags request as escalated; status badge and escalation context update in place.

### UJ-REQ-007: Request Management Decision
* **Primary Actor:** Request Owner
* **Screen:** `SCR-REQ-003: Request Detail`
* **Navigation Interaction:**
  1. Within `SCR-REQ-003`, Request Owner selects request management decision.
  2. Request Owner documents policy/risk/resource options.
  3. System flags request as awaiting management decision; update displays in place.

### UJ-REQ-008: Review Request Completion
* **Primary Actor:** Implementator
* **Entry Point:** `SCR-REQ-001: Request List` (filter by pending completion review)
* **Destination:** `SCR-REQ-003: Request Detail`
* **Navigation Interaction:**
  1. Implementator locates completed request on `SCR-REQ-001` and navigates to `SCR-REQ-003`.
  2. Reviews recorded resolution and evidence.
  3. If satisfied: confirms acceptance; request transitions to `CLOSED` (Resolution: Resolved).
  4. If rework needed: enters feedback; request returns to Request Owner in `ACTIVE` state.
