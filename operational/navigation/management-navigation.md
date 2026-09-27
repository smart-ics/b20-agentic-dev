# Management Oversight Navigation Specification

This document details the navigation flows, screen transitions, and actor interactions for the **Management Oversight** operational category, derived from:
* **Scenarios:** `SC-MGT-001` through `SC-MGT-004` (`operational/scenarios/management-oversight-scenarios.md`)
* **Use Cases:** `UC-MGT-001` through `UC-MGT-004` (`operational/use-cases/management-oversight-use-cases.md`)
* **User Journeys:** `UJ-MGT-001` through `UJ-MGT-004` (`operational/user-journey/management-oversight-user-journeys.md`)
* **Actor Model:** Management (`operational/actors/actor-model.md`)
* **Domain Models:** Request Domain (`operational/domains/request-domain.md`), Customer Domain (`operational/domains/customer-domain.md`), Organization Domain (`operational/domains/organization-domain.md`)

---

## 1. Management Oversight Navigation Hierarchy

```text
Management Oversight Area
├── SCR-MGT-001: Customer Progress Review (Customer Demand Visibility)
│   └── [Action: Intervene / Inspect Request] ────────► SCR-REQ-003: Request Detail
│
├── SCR-MGT-003: Programmer Workload Review (Capacity Assessment)
│   └── [Action: Initiate Reassignment] ──────────────► SCR-REQ-003: Request Detail
│
├── SCR-MGT-002: Programmer Performance Review (Throughput & Outcomes)
│   └── [Action: Inspect Resolved / Rejected Item] ──► SCR-REQ-003: Request Detail
│
└── [Cross-Cutting Management Action]
    └── SCR-REQ-003: Request Detail (Ownership Transfer Workspace)
        └── [UC-MGT-001] Reassign Request Ownership to new Person
```

---

## 2. Journey Navigation Mappings

### UJ-MGT-001: Reassign Request Ownership
* **Primary Actor:** Management
* **Entry Point:** `SCR-REQ-003: Request Detail` (accessed directly, or from `SCR-MGT-001`, `SCR-MGT-003`, or `SCR-REQ-001`)
* **Destination:** `SCR-REQ-003: Request Detail`
* **Navigation Interaction:**
  1. Management locates request requiring reassignment (due to workload imbalance, escalation, or skill mismatch).
  2. Opens `SCR-REQ-003: Request Detail`.
  3. Triggers reassignment action and selects a new handler from the organization.
  4. Confirms ownership transfer; Request Owner updates and change is recorded in request history.

### UJ-MGT-002: Review Customer Request Progress
* **Primary Actor:** Management
* **Entry Point:** Global Navigation (`Management > Customer Progress`)
* **Destination:** `SCR-MGT-001: Customer Progress Review`
* **Navigation Interaction:**
  1. Management navigates to `SCR-MGT-001`.
  2. Selects target Customer organization.
  3. Reviews list and statuses of all requests associated with the Customer.
  4. Identifies delayed, blocked, or high-priority requests.
  5. Selects a blocked request to navigate into `SCR-REQ-003: Request Detail` to intervene, request updates, or trigger reassignment.

### UJ-MGT-003: Review Programmer Request Performance
* **Primary Actor:** Management
* **Entry Point:** Global Navigation (`Management > Programmer Performance`)
* **Destination:** `SCR-MGT-002: Programmer Performance Review`
* **Navigation Interaction:**
  1. Management navigates to `SCR-MGT-002`.
  2. Selects target Programmer (Person holding the Programmer role in the organization).
  3. Sets review timeframe.
  4. Reviews handled volume, completion rates, and resolution outcomes (accepted vs. rejected).
  5. Selects specific completed or rejected requests to open `SCR-REQ-003: Request Detail` for detailed audit.

### UJ-MGT-004: Review Programmer Workload
* **Primary Actor:** Management
* **Entry Point:** Global Navigation (`Management > Programmer Workload`)
* **Destination:** `SCR-MGT-003: Programmer Workload Review`
* **Navigation Interaction:**
  1. Management navigates to `SCR-MGT-003`.
  2. Inspects team-wide active request counts and individual programmer workload distributions.
  3. Assesses handler capacity and detects overloaded individuals.
  4. Selects an active request on an overloaded programmer to navigate to `SCR-REQ-003: Request Detail` to reassign ownership to an available handler.
