# Management Oversight User Journeys

Derived from **Management Oversight Use Cases** (`operational/use-cases/management-oversight-use-cases.md`) using the **User Journey Generation Skill** (`operational/user-journey/user-journey-generation-skill.md`).

Canonical actors follow the **CAKRA - ICS Operational Actor Model** (`operational/actors/actor-model.md`):
* **Management:** Organizational Role responsible for defining organizational structure, monitoring operational health, assessing workload and performance, and intervening in assignments or operational decisions.

---

## UJ-MGT-001: Reassign Request Ownership

**Use Case Reference:** UC-MGT-001

### Actor
Management

### Goal
Change responsibility for handling a Request.

### Trigger
A Request requires reassignment to a different handler.

### Main Journey
1. Management locates the Request requiring reassignment.
2. Management reviews the current assignment and handling situation.
3. Management selects another handler from the organization.
4. Management confirms the ownership transfer.

### Alternative Paths
* **Reassignment from escalation:** Management reassigns the Request following an escalation or decision request.
* **Canceling reassignment:** Management decides to retain the current handler and cancels the transfer.

### Success Outcome
Responsibility for handling the Request is transferred to the new handler.

### Information Needed
* Request details and current owner
* Organization members

---

## UJ-MGT-002: Review Customer Request Progress

**Use Case Reference:** UC-MGT-002

### Actor
Management

### Goal
Monitor operational progress and request status for a Customer.

### Trigger
Management needs to assess operational progress for a Customer.

### Main Journey
1. Management identifies and selects the target Customer.
2. Management reviews the current status and progress of Requests associated with the Customer.
3. Management identifies any blocked, delayed, or critical Requests requiring attention.
4. Management decides whether follow-up or intervention is needed.

### Alternative Paths
* **Intervening on delayed requests:** Management initiates reassignment or requests an update from the Request Owner.
* **Filtering requests:** Management filters the Customer's requests by status or date range to focus on specific work.

### Success Outcome
Current Request statuses and progress for the Customer are reviewed.

### Information Needed
* Customer identity
* Associated Requests and current progress

---

## UJ-MGT-003: Review Programmer Request Performance

**Use Case Reference:** UC-MGT-003

### Actor
Management

### Goal
Evaluate the volume, completion rates, and outcomes of Requests handled by a Programmer.

### Trigger
Management needs to assess the performance of a Programmer.

### Main Journey
1. Management selects the target Programmer.
2. Management reviews the Programmer's request processing history, completion rates, and resolution outcomes.
3. Management identifies any performance patterns or operational concerns.
4. Management decides whether feedback, support, or organizational adjustment is needed.

### Alternative Paths
* **Inspecting specific outcomes:** Management examines individual completed or rejected Requests to understand resolution context.
* **Filtering by timeframe:** Management adjusts the review period to observe recent versus historical performance.

### Success Outcome
Request volume, completion rates, and outcomes for the Programmer are reviewed.

### Information Needed
* Programmer identity
* Handled Requests and resolution outcomes

---

## UJ-MGT-004: Review Programmer Workload

**Use Case Reference:** UC-MGT-004

### Actor
Management

### Goal
Assess the current active assignment load of a Programmer.

### Trigger
Management needs to assess the current workload of a Programmer.

### Main Journey
1. Management selects the target Programmer.
2. Management inspects the Programmer's active Request assignments and current workload status.
3. Management assesses whether the Programmer has capacity or is facing workload imbalance.
4. Management decides whether new assignments can be made or existing work needs rebalancing.

### Alternative Paths
* **Initiating rebalancing:** Management determines the Programmer is overloaded and initiates reassignment of one or more Requests.
* **Reviewing across team members:** Management checks multiple programmers to compare capacity before making an assignment decision.

### Success Outcome
Active Request assignments and current workload for the Programmer are reviewed.

### Information Needed
* Programmer identity
* Active Request assignments and current status
