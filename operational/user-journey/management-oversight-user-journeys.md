# Management Oversight User Journeys

Derived from **Management Oversight Use Cases** (`operational/use-cases/management-oversight-use-cases.md`) using the **User Journey Generation Skill** (`operational/user-journey/user-journey-generation-skill.md`).

Canonical actors follow the **ICS Operational Actor Model** (`operational/actors/actor-model.md`):
* **Management:** Organizational Role responsible for defining organizational structure, monitoring operational health, assessing workload and performance, and intervening in assignments or operational decisions.

---

## UJ-MGT-001: Reassign Request Ownership

**Use Case Reference:** UC-MGT-001

### Actor
Management

### Goal
Change responsibility for handling a Request to another handler.

### Trigger
Management identifies a Request that requires reassignment due to workload imbalance, specialized expertise requirements, priority shifts, or an operational bottleneck.

### Main Journey
1. Management locates and accesses the target Request requiring reassignment.
2. Management reviews the Request details, current progress, complexity, and current assigned owner.
3. Management evaluates candidate handlers within the organization based on available capacity and necessary capability.
4. Management selects the new handler from the organization to assume Request ownership.
5. Management records the operational rationale or handover context for the reassignment.
6. Management confirms the ownership transfer.

### Alternative Paths
* **Reassignment after escalation or decision request:** Management reassigns the Request directly following review of an escalation (UC-REQ-006) or a management decision request (UC-REQ-007).
* **Temporary unassignment:** Management removes the current ownership without immediately assigning a new owner when active handling must pause pending strategic determination.
* **Canceling reassignment:** Management evaluates the situation and decides to retain the current Request Owner, leaving ownership unchanged.

### Success Outcome
Responsibility for the Request is transferred to the new Request Owner, preserving ownership history and context for all operational collaborators.

### Information Needed
* Request identifier, summary, current status, and operational context
* Current assigned owner and recent progress or discussion history
* Organization members (candidates for assignment, their roles, and availability)
* Reassignment rationale and handover notes

---

## UJ-MGT-002: Review Customer Request Progress

**Use Case Reference:** UC-MGT-002

### Actor
Management

### Goal
Monitor operational progress and request status across all demands associated with a Customer.

### Trigger
Management conducts a periodic customer review, prepares for customer executive meetings, or responds to customer stakeholder inquiries.

### Main Journey
1. Management navigates to customer operational oversight.
2. Management identifies and selects the target Customer.
3. Management reviews the Customer's operational standing and active maintenance contract status.
4. Management inspects the list of associated Requests, examining their current lifecycle states, priorities, assigned owners, and recent activity.
5. Management drills into specific high-priority, overdue, or stalled Requests to evaluate operational bottlenecks and progress blockers.

### Alternative Paths
* **Filtering by lifecycle stage or timeframe:** Management filters associated Requests by specific state (e.g., active, waiting, completed) or date range to focus on immediate deliverables.
* **Initiating management intervention:** Management identifies an at-risk customer Request and initiates reassignment (UJ-MGT-001) or requests direct status clarification from the assigned owner.
* **No active Requests:** Management confirms that no open Requests exist for the Customer and that all historical deliverables have been fulfilled.

### Success Outcome
Management gains comprehensive visibility into the status, health, and progress of all operational demands associated with the Customer.

### Information Needed
* Customer identity, status, and maintenance contract standing
* Customer Request portfolio (identifiers, titles, types, current statuses, priorities, assigned owners, target dates)
* Request progress history, milestone activity, and reported blockers

---

## UJ-MGT-003: Review Programmer Request Performance

**Use Case Reference:** UC-MGT-003

### Actor
Management

### Goal
Evaluate the volume, completion rates, and resolution outcomes of Requests handled by a Programmer.

### Trigger
Management conducts periodic operational reviews, evaluates team delivery velocity, or assesses individual handling effectiveness.

### Main Journey
1. Management navigates to personnel operational oversight.
2. Management selects the target Programmer to review.
3. Management examines the historical volume of Requests assigned to and processed by the Programmer over a selected evaluation period.
4. Management reviews throughput and completion metrics, including resolved requests, turnaround duration, and resolution outcomes (e.g., resolved, rejected, cancelled).
5. Management inspects specific completed Requests to assess resolution quality, complexity, and feedback from review stages.

### Alternative Paths
* **Adjusting evaluation timeframe:** Management modifies the time window (e.g., monthly, quarterly, annual) to observe trends and consistency over time.
* **Inspecting rework history:** Management reviews Requests that required rework during completion reviews (UC-REQ-008) to identify areas requiring additional technical support or domain training.
* **Distinguishing role responsibilities:** When the Programmer holds multiple responsibilities (such as Module PIC), Management separates request resolution outcomes from broader module accountability.

### Success Outcome
Management establishes an accurate, objective understanding of the Programmer's request throughput, turnaround times, and resolution outcomes.

### Information Needed
* Programmer identity, organizational role, and team membership
* Historical Request handling records (volume assigned, completed, rejected, cancelled)
* Completion timelines, turnaround duration, and recorded resolution summaries
* Review feedback and rework records

---

## UJ-MGT-004: Review Programmer Workload

**Use Case Reference:** UC-MGT-004

### Actor
Management

### Goal
Assess the current active assignment load and operational commitments of a Programmer.

### Trigger
Management plans new task allocations, balances team assignments, or investigates potential delivery delays.

### Main Journey
1. Management navigates to personnel workload oversight.
2. Management selects the target Programmer to inspect.
3. Management reviews the complete list of Requests currently assigned to the Programmer.
4. Management examines the distribution of active work across priorities, complexity levels, associated products, and current progress stages.
5. Management determines whether the Programmer has available capacity, is balanced, or is facing overload.

### Alternative Paths
* **Initiating workload rebalancing:** Management identifies an overload situation or impending deadline conflicts and initiates reassignment of one or more Requests (UJ-MGT-001).
* **Comparative team load review:** Management compares the Programmer's active workload against other team members to identify underutilized capacity.
* **Inspecting blocked items:** Management identifies active Requests blocked by external dependencies or awaiting decisions, and coordinates necessary escalation or support.

### Success Outcome
Management obtains clear visibility into the Programmer's active workload, enabling informed decisions regarding new assignments, capacity planning, and workload redistribution.

### Information Needed
* Programmer identity, role, and team affiliation
* Active assigned Requests (identifiers, titles, priorities, complexity, current operational condition, target dates)
* Associated Request dependencies, blockers, and pending decision flags
