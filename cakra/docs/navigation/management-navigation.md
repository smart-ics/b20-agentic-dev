# Management Oversight Navigation Specification

This document details the destinations and movement paths for the **Management Oversight** operational category, derived from:
* **Scenarios:** `SC-MGT-001` through `SC-MGT-004` (`operational/scenarios/management-oversight-scenarios.md`)
* **Use Cases:** `UC-MGT-001` through `UC-MGT-004` (`operational/use-cases/management-oversight-use-cases.md`)
* **User Journeys:** `UJ-MGT-001` through `UJ-MGT-004` (`operational/user-journey/management-oversight-user-journeys.md`)

---

## 1. Management Oversight Navigation Hierarchy

```text
Management Oversight Area
├── SCR-MGT-001: Customer Progress Review
│   └── Request Selection ──────────► SCR-REQ-003: Request Detail
│
├── SCR-MGT-002: Programmer Performance Review
│   └── Request Selection ──────────► SCR-REQ-003: Request Detail
│
├── SCR-MGT-003: Programmer Workload Review
│   └── Request Selection ──────────► SCR-REQ-003: Request Detail
│
├── SCR-CUST-001: Create Customer Modal
│   └── On Save / Cancel ───────────► Customer Portfolio View
│
├── SCR-CUST-002: Edit Customer Modal
│   └── On Save / Cancel ───────────► Customer Portfolio View
│
└── SCR-REQ-003: Request Detail
    └── Back / Return ──────────────► Originating Screen (SCR-MGT-001 / SCR-MGT-002 / SCR-MGT-003)
```

---

## 2. Journey Movement Paths

### UJ-MGT-001: Reassign Request Ownership
* **Primary Actor:** Management
* **Entry Point:** `SCR-MGT-001`, `SCR-MGT-003`, or `SCR-REQ-001`
* **Destination:** `SCR-REQ-003: Request Detail`
* **Exit / Destination:** Originating Screen (`SCR-MGT-001`, `SCR-MGT-003`, or `SCR-REQ-001`)
* **Movement Path:**
  1. Actor arrives at `SCR-REQ-003: Request Detail` from a management review screen or the request list.
  2. Actor remains on `SCR-REQ-003` or returns to the originating screen.

### UJ-MGT-002: Review Customer Request Progress
* **Primary Actor:** Management
* **Entry Point:** Global Navigation (`Management > Customer Progress`)
* **Destination:** `SCR-MGT-001: Customer Progress Review`
* **Exit / Destination:** `SCR-REQ-003: Request Detail`
* **Movement Path:**
  1. Actor navigates to `SCR-MGT-001: Customer Progress Review` via Global Navigation.
  2. Actor selects a Request associated with the Customer, navigating to `SCR-REQ-003: Request Detail`.
  3. From `SCR-REQ-003`, actor returns to `SCR-MGT-001`.

### UJ-MGT-003: Review Programmer Request Performance
* **Primary Actor:** Management
* **Entry Point:** Global Navigation (`Management > Programmer Performance`)
* **Destination:** `SCR-MGT-002: Programmer Performance Review`
* **Exit / Destination:** `SCR-REQ-003: Request Detail`
* **Movement Path:**
  1. Actor navigates to `SCR-MGT-002: Programmer Performance Review` via Global Navigation.
  2. Actor selects a specific Request record, navigating to `SCR-REQ-003: Request Detail`.
  3. From `SCR-REQ-003`, actor returns to `SCR-MGT-002`.

### UJ-MGT-004: Review Programmer Workload
* **Primary Actor:** Management
* **Entry Point:** Global Navigation (`Management > Programmer Workload`)
* **Destination:** `SCR-MGT-003: Programmer Workload Review`
* **Exit / Destination:** `SCR-REQ-003: Request Detail`
* **Movement Path:**
  1. Actor navigates to `SCR-MGT-003: Programmer Workload Review` via Global Navigation.
  2. Actor selects an active Request, navigating to `SCR-REQ-003: Request Detail`.
  3. From `SCR-REQ-003`, actor returns to `SCR-MGT-003`.

### UJ-CUST-001: Customer Master Maintenance
* **Primary Actor:** Administrator, Management
* **Entry Point:** Customer Portfolio View (`CustomerPortfolioView.vue`)
* **Destinations:**
  * `SCR-CUST-001: Create Customer Modal` (via "Add Customer" button)
  * `SCR-CUST-002: Edit Customer Modal` (via customer card "Edit Customer" button)
* **Exit / Destination:** Customer Portfolio View (`CustomerPortfolioView.vue`)
* **Movement Path:**
  1. Actor navigates to Customer Portfolio View.
  2. Actor selects "Add Customer", triggering modal dialog `SCR-CUST-001` to record a new customer master record.
  3. Alternatively, actor selects "Edit Customer" on an existing customer card, triggering modal dialog `SCR-CUST-002` to update attributes or manage customer contacts.
  4. Upon saving or canceling, actor returns to Customer Portfolio View with refreshed data.

