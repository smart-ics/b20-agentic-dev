# SCR-REQ-003: Request Detail

## Purpose

Provide a comprehensive view of a specific Request record, enabling actors to understand current state, evaluate the request, take lifecycle actions, record supporting information, track progress, and manage ownership.

This is the central operational screen of the system. It serves Implementators, Request Owners, and Management across 10 use cases and 10 user journeys.

## Primary Actors

* Implementator
* Request Owner
* Management

## Entry Points

* `SCR-REQ-001: Request List` → select a Request
* `SCR-REQ-002: Create Request` → after successful submission
* `SCR-REQ-004: Request Search & History` → select a search result
* `SCR-REQ-005: My Assigned Requests` → select an assigned Request
* `SCR-MGT-001: Customer Progress Review` → select a Customer's Request
* `SCR-MGT-002: Programmer Performance Review` → select a historical Request
* `SCR-MGT-003: Programmer Workload Review` → select an active Request
* Direct URL / Permanent Link

## Related Use Cases

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

## Related User Journeys

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

## Information Sections

### Section: Request Header

Provides immediate identification and current state awareness.

Displays:

* Request ID
* Title
* Request Status (CAPTURED, VALIDATED, ACTIVE, CLOSED)
* Request Type
* Created date
* Last updated date

**Traceability:** Supports all use cases on this screen — every journey begins by identifying the Request and understanding its current state.

### Section: Request Ownership

Shows who is currently responsible for this Request.

Displays:

* Request Owner (Person name from Organization domain, or unassigned indicator)
* Requester information (Person or Customer Contact who originated the demand)

**Traceability:** UJ-REQ-002 (awareness of current ownership for assignment). UJ-REQ-003 (Request Owner reviewing their assignment). UJ-MGT-001 (Management reviewing current assignment before reassignment). Actor Model: distinguishes Requester from Request Owner.

### Section: Request Demand Details

Shows the full details of what is being requested.

Displays:

* Description — detailed description of the operational demand
* Special instructions or additional notes (if any)

**Traceability:** UJ-REQ-003 step 2 (reviews demand details and context). UJ-REQ-008 step 2 (inspects completed work against original demand). Information Needed from UJ-REQ-003: Request details, demand context, and supporting information.

### Section: Request Context

Shows the operational context associations of this Request.

Displays:

* Customer name and status (if associated, from Customer domain)
* Customer Contact (if associated)
* Product name (if associated, from Product domain)
* Work Package name and status (if associated, from Work Package domain)

**Traceability:** UJ-REQ-003 step 2 (reviews context). UJ-REQ-001 step 3 (associates relevant context). Domain relationships: Request → Customer, Request → Product, Request → Work Package.

### Section: Resolution

Shows the recorded outcome when the Request is closed. Visible only when the Request has a Resolution.

Displays:

* Resolution Outcome
* Resolution Description
* Resolved By (Person name)
* Resolved At (date/time)

**Traceability:** UJ-REQ-008 step 2 (inspects completed work and recorded resolution). UJ-REQ-005 (rejection reason as resolution). Domain: Request → Resolution aggregate.

### Section: Request Progress & History

Shows the chronological progression of the Request through its lifecycle.

Displays:

* Timeline of significant state changes (status transitions, ownership changes, context changes)
* Date and actor for each change
* Current milestone/state indicator

**Traceability:** UJ-COL-003 steps 2–4 (views current status, assigned owner, recent updates, and chronological progression of steps and recorded activities). UJ-REQ-008 step 2 (reviews completed work evidence). Information Needed from UJ-COL-003: step history and milestone progress timeline.

### Section: Supporting Information (Posts & Comments)

Shows the operational discussion and supporting evidence associated with this Request.

Displays:

* List of Posts referencing this Request (from Post domain)
* For each Post: Author, timestamp, content, source (system-generated or human-authored)
* Comments on each Post (flat discussion model per Post domain)
* Reactions on Posts/Comments

**Traceability:** UJ-COL-001 steps 2–5 (reviews existing context, enters supporting details, attaches evidence, submits information). UJ-COL-003 alternative path (inspecting detailed discussions to understand rationale behind status changes). Domain: Post → Post Reference to Request. Post aggregate: Comments, Reactions.

## Available Actions

### Action: Assign Request Owner

* **Actor:** Implementator
* **Precondition:** Request has no owner, or ownership change is needed.
* **Expected Outcome:** A Person from the Organization domain is selected and assigned as Request Owner. Request ownership is updated.
* **Traceability:** UC-REQ-002, UJ-REQ-002 steps 3–4.

### Action: Accept Request Responsibility

* **Actor:** Request Owner
* **Precondition:** Request is assigned to the current actor and has not yet been accepted.
* **Expected Outcome:** Request Owner confirms acceptance. Request transitions to ACTIVE state.
* **Traceability:** UC-REQ-004, UJ-REQ-004 step 2.

### Action: Reject Request

* **Actor:** Request Owner
* **Precondition:** Request is assigned to the current actor during evaluation.
* **Expected Outcome:** Request Owner provides rejection reason. Request transitions to CLOSED state with rejection resolution.
* **Traceability:** UC-REQ-005, UJ-REQ-005 steps 2–4.

### Action: Escalate Request

* **Actor:** Request Owner
* **Precondition:** Request is active and exceeds the current owner's authority or capability.
* **Expected Outcome:** Request Owner provides escalation reason and required assistance. Request status is updated to reflect escalation.
* **Traceability:** UC-REQ-006, UJ-REQ-006 steps 2–3.

### Action: Request Management Decision

* **Actor:** Request Owner
* **Precondition:** Request is active and requires a policy, risk, or resource decision.
* **Expected Outcome:** Request Owner provides decision question and context. Request is flagged as awaiting management decision.
* **Traceability:** UC-REQ-007, UJ-REQ-007 steps 2–3.

### Action: Accept Resolution (Review Request Completion)

* **Actor:** Implementator
* **Precondition:** Request Owner has reported work complete. Request has a recorded resolution.
* **Expected Outcome:** Implementator confirms acceptance. Request transitions to CLOSED state with accepted resolution.
* **Traceability:** UC-REQ-008, UJ-REQ-008 steps 3–4.

### Action: Return for Rework (Review Request Completion)

* **Actor:** Implementator
* **Precondition:** Request Owner has reported work complete. Resolution is incomplete or deficient.
* **Expected Outcome:** Implementator provides rework feedback. Request returns to ACTIVE state for the Request Owner.
* **Traceability:** UC-REQ-008, UJ-REQ-008 alternative path.

### Action: Reassign Request Ownership

* **Actor:** Management
* **Precondition:** Request requires reassignment.
* **Expected Outcome:** Management selects another Person from the Organization domain. Request Owner is changed.
* **Traceability:** UC-MGT-001, UJ-MGT-001 steps 2–4.

### Action: Record Supporting Information

* **Actor:** Implementator, Request Owner
* **Precondition:** Request exists.
* **Expected Outcome:** A new Post is created referencing this Request, with content, optional attachments, and optional reference to related Requests or Work Packages.
* **Traceability:** UC-COL-001, UJ-COL-001 steps 3–5.

## Navigation Destinations

* `SCR-REQ-001: Request List` (back navigation)
* `SCR-REQ-005: My Assigned Requests` (back navigation, when originated from there)
* Originating screen (back navigation — SCR-REQ-004, SCR-MGT-001, SCR-MGT-002, SCR-MGT-003)

## Layout Sketch

```
+--------------------------------------------------+
| Global Navigation Bar                            |
+--------------------------------------------------+

+--------------------------------------------------+
| Request Header                                   |
| ID: REQ-123 | Status: ACTIVE | Type: Bug Fix     |
| Created: 2026-01-15 | Updated: 2026-01-20        |
| Title: Fix incorrect pharmacy report              |
+--------------------------------------------------+

+----------------------+---------------------------+
| Request Ownership    | Request Context            |
| Owner: Person A      | Customer: RSUD A          |
| Requester: Contact B | Product: MyHospital       |
|                      | Work Package: Go-Live Prep |
+----------------------+---------------------------+

+--------------------------------------------------+
| Request Demand Details                           |
| Description: [full demand description]            |
| Special Instructions: [if any]                    |
+--------------------------------------------------+

+--------------------------------------------------+
| Resolution (if closed)                           |
| Outcome: Resolved | By: Person A                 |
| At: 2026-01-20 | Description: [resolution detail] |
+--------------------------------------------------+

+--------------------------------------------------+
| Available Actions                                |
| [Assign Owner] [Accept] [Reject] [Escalate]     |
| [Request Decision] [Accept Resolution]           |
| [Return for Rework] [Reassign Owner]             |
+--------------------------------------------------+

+--------------------------------------------------+
| Request Progress & History                       |
| 2026-01-15 Created by Implementator X            |
| 2026-01-15 Assigned to Person A                  |
| 2026-01-16 Accepted by Person A                  |
| 2026-01-20 Marked complete                       |
| ...                                              |
+--------------------------------------------------+

+--------------------------------------------------+
| Supporting Information (Posts & Comments)         |
| [+ Add Supporting Information]                   |
|                                                  |
| Post #1: Author, Timestamp                       |
|   Content...                                     |
|   Comment 1: Author, Timestamp, Content          |
|   Comment 2: Author, Timestamp, Content          |
|   Reactions: [SEEN: 2] [EXPERIENCED: 1]          |
|                                                  |
| Post #2: System-Generated, Timestamp             |
|   Content...                                     |
| ...                                              |
+--------------------------------------------------+
```

## Action Visibility Rules

Actions are displayed based on the actor's role and the Request's current state. The system determines which actions are contextually valid:

| Action | Actor | Visible When |
| :--- | :--- | :--- |
| Assign Owner | Implementator | Request has no owner or needs reassignment |
| Accept | Request Owner | Request is assigned to actor, not yet accepted |
| Reject | Request Owner | Request is assigned to actor, during evaluation |
| Escalate | Request Owner | Request is active, owned by actor |
| Request Decision | Request Owner | Request is active, owned by actor |
| Accept Resolution | Implementator | Request has recorded resolution awaiting review |
| Return for Rework | Implementator | Request has recorded resolution awaiting review |
| Reassign Owner | Management | Request exists and has a current owner |
| Add Supporting Info | Implementator, Request Owner | Request exists |

> Note: Action visibility is an operational behavior derived from the use cases and actor model. It does not introduce new business behavior — it surfaces existing authority boundaries defined in the Actor Model.
