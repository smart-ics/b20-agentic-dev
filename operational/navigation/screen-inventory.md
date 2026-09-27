# Screen Inventory

Derived from approved **User Journeys**, **Use Cases**, **Operational Scenarios**, **Actor Model**, and **Domain Models** in accordance with the **Navigation Creation Skill** (`operational/navigation/navigation-creation-skill.md`).

---

## Inventory Summary

| Screen ID | Screen Name | Navigation Area | Primary Actors | Supported Journeys | Supported Use Cases |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `SCR-REQ-001` | Request List | Requests | Implementator, Request Owner | UJ-REQ-002, UJ-REQ-008, UJ-COL-003 | UC-REQ-002, UC-REQ-008, UC-COL-003 |
| `SCR-REQ-002` | Create Request | Requests | Implementator | UJ-REQ-001 | UC-REQ-001 |
| `SCR-REQ-003` | Request Detail | Requests | Implementator, Request Owner, Management | UJ-REQ-002, UJ-REQ-003, UJ-REQ-004, UJ-REQ-005, UJ-REQ-006, UJ-REQ-007, UJ-REQ-008, UJ-COL-001, UJ-COL-003, UJ-MGT-001 | UC-REQ-002, UC-REQ-003, UC-REQ-004, UC-REQ-005, UC-REQ-006, UC-REQ-007, UC-REQ-008, UC-COL-001, UC-COL-003, UC-MGT-001 |
| `SCR-REQ-004` | Request Search & History | Requests | Implementator | UJ-COL-002 | UC-COL-002 |
| `SCR-REQ-005` | My Assigned Requests | Requests | Implementator, Request Owner | UJ-COL-004 | UC-COL-004 |
| `SCR-MGT-001` | Customer Progress Review | Management Oversight | Management | UJ-MGT-002 | UC-MGT-002 |
| `SCR-MGT-002` | Programmer Performance Review | Management Oversight | Management | UJ-MGT-003 | UC-MGT-003 |
| `SCR-MGT-003` | Programmer Workload Review | Management Oversight | Management | UJ-MGT-004 | UC-MGT-004 |

---

## Screen Definitions

### SCR-REQ-001

**Screen Name:**
Request List

**Purpose:**
Provide a central operational view to inspect, filter, and locate recorded Requests across all lifecycle conditions (Captured, Active, Closed), enabling handlers to find unassigned requests, items pending review, or active operational work.

**Primary Actors:**
* Implementator
* Request Owner

**Supported Use Cases:**
* UC-REQ-002: Assign Request Owner
* UC-REQ-008: Review Request Completion
* UC-COL-003: Track Request Progress

**Supported User Journeys:**
* UJ-REQ-002: Assign Request Owner (locating captured requests requiring ownership assignment)
* UJ-REQ-008: Review Request Completion (locating completed requests awaiting verification)
* UJ-COL-003: Track Request Progress (locating requests to check standing and progression)

**Entry Points:**
* Global Navigation (`Requests > All Requests`)
* Home Workspace

**Primary Actions:**
* Filter Requests by lifecycle state (`CAPTURED`, `ACTIVE`, `CLOSED`)
* Filter Requests by assignment status (`Unassigned`, `Assigned to Me`, `Assigned to Other`)
* Filter Requests by review status (`Awaiting Review`, `In Rework`, `Resolved`)
* Select Request (navigates to `SCR-REQ-003: Request Detail`)
* Initiate Request Creation (navigates to `SCR-REQ-002: Create Request`)

---

### SCR-REQ-002

**Screen Name:**
Create Request

**Purpose:**
Capture a customer operational demand or internal operational need and record it as authoritative Request knowledge.

**Primary Actors:**
* Implementator

**Supported Use Cases:**
* UC-REQ-001: Record Customer Request

**Supported User Journeys:**
* UJ-REQ-001: Record Customer Request

**Entry Points:**
* Request List (`SCR-REQ-001`)
* Global Navigation Quick Action (`+ New Request`)

**Primary Actions:**
* Enter Demand Description and Title
* Select Request Type
* Identify and Associate Customer (optional; omitted for internal demands)
* Associate Operational Context (Product, Work Package if applicable)
* Submit Request (creates Request in `CAPTURED` state and returns to `SCR-REQ-001` or navigates to `SCR-REQ-003`)

---

### SCR-REQ-003

**Screen Name:**
Request Detail

**Purpose:**
Serve as the comprehensive operational destination to inspect request demand details and context, evaluate operational feasibility, execute lifecycle decisions (acceptance, rejection, escalation, management decision, resolution review), collaborate through supporting notes and evidence, and monitor step progress.

**Primary Actors:**
* Implementator
* Request Owner
* Management

**Supported Use Cases:**
* UC-REQ-002: Assign Request Owner
* UC-REQ-003: Evaluate Request
* UC-REQ-004: Accept Request Responsibility
* UC-REQ-005: Reject Request
* UC-REQ-006: Escalate Request
* UC-REQ-007: Request Management Decision
* UC-REQ-008: Review Request Completion
* UC-COL-001: Record Supporting Information
* UC-COL-003: Track Request Progress
* UC-MGT-001: Reassign Request Ownership

**Supported User Journeys:**
* UJ-REQ-002: Assign Request Owner
* UJ-REQ-003: Evaluate Request
* UJ-REQ-004: Accept Request Responsibility
* UJ-REQ-005: Reject Request
* UJ-REQ-006: Escalate Request
* UJ-REQ-007: Request Management Decision
* UJ-REQ-008: Review Request Completion
* UJ-COL-001: Record Supporting Information
* UJ-COL-003: Track Request Progress
* UJ-MGT-001: Reassign Request Ownership

**Entry Points:**
* Request List (`SCR-REQ-001`)
* My Assigned Requests (`SCR-REQ-005`)
* Request Search & History (`SCR-REQ-004`)
* Customer Progress Review (`SCR-MGT-001`)
* Programmer Performance Review (`SCR-MGT-002`)
* Programmer Workload Review (`SCR-MGT-003`)
* Permanent Link (`/request/:id`)

**Primary Actions:**
* Assign Request Owner: Select organization member to take initial ownership (Implementator)
* Evaluate Feasibility: Inspect demand context, customer, and operational scope (Request Owner)
* Accept Responsibility: Confirm ownership and transition request to `ACTIVE` (Request Owner)
* Reject Request: Record explanatory reason, mark resolution as rejected, and close request (Request Owner)
* Escalate Request: Submit escalation with rationale and required higher-level assistance (Request Owner)
* Request Management Decision: Elevate policy, risk, or resource question with decision context to Management (Request Owner)
* Record Supporting Information: Add contextual observations, comments, or evidence attachments (Implementator / Collaborator)
* Track Progress History: Review chronological milestone progression and step audit trail (Implementator / Collaborator)
* Review Completion: Accept resolution to close request or return request for rework with feedback (Implementator)
* Reassign Ownership: Transfer handling responsibility to another organization member (Management)

---

### SCR-REQ-004

**Screen Name:**
Request Search & History

**Purpose:**
Provide a dedicated search and retrieval destination to explore previous requests, past resolution outcomes, and historical knowledge across multiple operational criteria.

**Primary Actors:**
* Implementator

**Supported Use Cases:**
* UC-COL-002: Search Request History

**Supported User Journeys:**
* UJ-COL-002: Search Request History

**Entry Points:**
* Global Navigation (`Requests > Search & History`)
* Request List (`SCR-REQ-001`)

**Primary Actions:**
* Execute Multi-Criteria Query: Search by keyword, customer identity, product, date range, or resolution outcome
* Refine Query Constraints: Narrow broad result sets by status or timeframe
* Select Historical Request: Open full historical record in Request Detail (`SCR-REQ-003`)

---

### SCR-REQ-005

**Screen Name:**
My Assigned Requests

**Purpose:**
Provide a focused personal workspace for handlers to review, prioritize, and manage the active operational requests for which they are directly assigned.

**Primary Actors:**
* Implementator
* Request Owner

**Supported Use Cases:**
* UC-COL-004: Review Assigned Requests

**Supported User Journeys:**
* UJ-COL-004: Review Assigned Requests

**Entry Points:**
* Global Navigation (`Requests > My Assigned`)
* Home Workspace

**Primary Actions:**
* Inspect Assigned Queue: View requests for which current actor is responsible with current status and priority
* Sort & Group: Organize workload by urgency, customer, product, or lifecycle stage
* Select Request: Open Request Detail (`SCR-REQ-003`) for active handling, evaluation, or resolution

---

### SCR-MGT-001

**Screen Name:**
Customer Progress Review

**Purpose:**
Provide Management with an aggregated operational view of all requests associated with a customer organization to track progress, monitor status, and detect blocked or delayed demands.

**Primary Actors:**
* Management

**Supported Use Cases:**
* UC-MGT-002: Review Customer Request Progress

**Supported User Journeys:**
* UJ-MGT-002: Review Customer Request Progress

**Entry Points:**
* Global Navigation (`Management > Customer Progress`)

**Primary Actions:**
* Select Customer: Choose target customer organization
* Filter Customer Requests: Filter by operational status, priority, or date range
* Identify Bottlenecks: Inspect delayed, blocked, or critical customer requests
* Open Request: Navigate to Request Detail (`SCR-REQ-003`) to intervene, request updates, or reassign

---

### SCR-MGT-002

**Screen Name:**
Programmer Performance Review

**Purpose:**
Provide Management with historical and analytical visibility into the request volume, completion rates, and resolution outcomes (accepted vs. rejected) of individual programmers.

**Primary Actors:**
* Management

**Supported Use Cases:**
* UC-MGT-003: Review Programmer Request Performance

**Supported User Journeys:**
* UJ-MGT-003: Review Programmer Request Performance

**Entry Points:**
* Global Navigation (`Management > Programmer Performance`)

**Primary Actions:**
* Select Programmer: Choose target organization member holding the Programmer role
* Set Review Period: Filter by active date range or historical period
* Inspect Performance Metrics: Review total handled volume, resolution completion rate, and rework/rejection count
* Inspect Specific Outcomes: Select individual completed or rejected requests to open Request Detail (`SCR-REQ-003`)

---

### SCR-MGT-003

**Screen Name:**
Programmer Workload Review

**Purpose:**
Provide Management with visibility into active request assignments and workload distribution across programmers to assess capacity and make informed rebalancing and reassignment decisions.

**Primary Actors:**
* Management

**Supported Use Cases:**
* UC-MGT-004: Review Programmer Workload

**Supported User Journeys:**
* UJ-MGT-004: Review Programmer Workload

**Entry Points:**
* Global Navigation (`Management > Programmer Workload`)

**Primary Actions:**
* Select Programmer / Team View: Inspect active assignment count and status distribution
* Assess Capacity: Compare load across organization members to detect imbalances or overload
* Initiate Rebalancing: Select an overloaded request to reassign via Request Detail (`SCR-REQ-003`)
