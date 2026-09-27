# Request Lifecycle User Journeys

Derived from **Request Lifecycle Use Cases** (`operational/use-cases/request-lifecycle-use-cases.md`) using the **User Journey Generation Skill** (`operational/user-journey/user-journey-generation-skill.md`).

---

## UJ-REQ-001: Record Customer Request

**Use Case Reference:** UC-REQ-001

### Actor
Implementator

### Goal
Capture a customer operational demand as a recorded request.

### Trigger
Customer communicates an operational demand, need, or issue.

### Main Journey
1. Implementator begins recording a new operational demand.
2. Implementator identifies the customer and enters the request details.
3. Implementator associates relevant context, such as the affected product or work package if known.
4. Implementator reviews the entered information and submits the request.

### Alternative Paths
* **Internal demand:** Implementator records an internal operational demand without associating a customer.

### Success Outcome
The operational demand is recorded as a new Request and is ready for ownership assignment.

### Information Needed
* Customer identity and contact information
* Demand description and request type
* Relevant operational context (Product, Work Package) if applicable

---

## UJ-REQ-002: Assign Request Owner

**Use Case Reference:** UC-REQ-002

### Actor
Implementator

### Goal
Ensure a Request has a responsible owner.

### Trigger
A captured Request requires an operational owner.

### Main Journey
1. Implementator locates a Request requiring ownership.
2. Implementator reviews the request details to understand the required capability.
3. Implementator selects a suitable handler from the organization.
4. Implementator confirms the assignment.

### Alternative Paths
* **Reassigning owner:** Implementator selects an already-assigned Request and assigns it to a different handler.

### Success Outcome
A handler is assigned as the responsible owner for the Request.

### Information Needed
* Request details and operational context
* Organization members

---

## UJ-REQ-003: Evaluate Request

**Use Case Reference:** UC-REQ-003

### Actor
Assigned Handler

### Goal
Determine whether the Request can be handled.

### Trigger
Assigned Handler becomes aware of a Request assigned to them.

### Main Journey
1. Assigned Handler accesses the assigned Request.
2. Assigned Handler reviews the demand details and context.
3. Assigned Handler evaluates the request and decides whether it can be handled within their authority and capability.

### Alternative Paths
* **Request clarification:** Assigned Handler identifies missing or ambiguous details and requests clarification before concluding evaluation.

### Success Outcome
Assigned Handler determines the handling disposition: accept, reject, escalate, or request a decision.

### Information Needed
* Request details, demand context, and supporting information

---

## UJ-REQ-004: Accept Request Responsibility

**Use Case Reference:** UC-REQ-004

### Actor
Assigned Handler

### Goal
Accept responsibility for maintaining and resolving the Request.

### Trigger
Assigned Handler determines the Request can be handled.

### Main Journey
1. Assigned Handler selects the evaluated Request.
2. Assigned Handler confirms acceptance of responsibility for resolving the Request.

### Alternative Paths
* **Acceptance with notes:** Assigned Handler records initial notes or target expectations when confirming acceptance.

### Success Outcome
Assigned Handler becomes the confirmed Request Owner, and the Request becomes active.

### Information Needed
* Request details

---

## UJ-REQ-005: Reject Request

**Use Case Reference:** UC-REQ-005

### Actor
Assigned Handler

### Goal
Decline responsibility for the Request during evaluation.

### Trigger
Assigned Handler determines the Request cannot be handled.

### Main Journey
1. Assigned Handler selects the evaluated Request.
2. Assigned Handler decides to decline the Request.
3. Assigned Handler provides an explanatory reason for declining.
4. Assigned Handler confirms the rejection.

### Alternative Paths
* **Duplicate request:** Assigned Handler identifies the request as a duplicate and references the existing request.

### Success Outcome
The Request is marked as rejected with an explanatory reason and closed.

### Information Needed
* Request details and rejection reason

---

## UJ-REQ-006: Escalate Request

**Use Case Reference:** UC-REQ-006

### Actor
Request Owner

### Goal
Escalate the Request when it exceeds current authority or capability.

### Trigger
Request Owner determines the Request cannot be resolved within their authority or capability.

### Main Journey
1. Request Owner identifies that the active Request exceeds their authority or capability.
2. Request Owner provides the reason for escalation and the required assistance.
3. Request Owner submits the escalation.

### Alternative Paths
* **Canceling escalation:** Request Owner discovers a path to resolution before submitting and continues active handling.

### Success Outcome
The Request is escalated to a higher authority level for intervention.

### Information Needed
* Request details, reason for escalation, and required assistance

---

## UJ-REQ-007: Request Management Decision

**Use Case Reference:** UC-REQ-007

### Actor
Request Owner

### Goal
Elevate the Request for a management-level decision.

### Trigger
Request Owner identifies that resolving the Request requires a policy, risk, or resource decision.

### Main Journey
1. Request Owner identifies the policy, risk, or resource question requiring management determination.
2. Request Owner prepares the decision request, summarizing the situation and options.
3. Request Owner submits the request for management decision.

### Alternative Paths
* **Supplementing decision context:** Request Owner adds further context or analysis requested by Management.

### Success Outcome
The Request is flagged as awaiting management decision with the decision context documented.

### Information Needed
* Request details, decision question, and context/options

---

## UJ-REQ-008: Review Request Completion

**Use Case Reference:** UC-REQ-008

### Actor
Implementator

### Goal
Review completed work on a Request and decide whether to accept or reject the resolution.

### Trigger
Request Owner reports that work on the Request is complete.

### Main Journey
1. Implementator locates the completed Request awaiting review.
2. Implementator inspects the completed work and recorded resolution.
3. Implementator decides whether the resolution satisfies the original demand.
4. Implementator confirms acceptance of the resolution.

### Alternative Paths
* **Return for rework:** Implementator determines the resolution is incomplete or deficient, provides feedback, and returns the Request to the Request Owner for rework.

### Success Outcome
The resolution is accepted and the Request is closed, or returned to the Request Owner for rework.

### Information Needed
* Original demand, completed work evidence, and resolution summary
