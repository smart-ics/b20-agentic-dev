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
A customer communicates an operational demand, need, problem, or change request to the Implementator.

### Main Journey
1. **Navigates to Request Recording:** Implementator navigates to the request intake area with the intent to capture a new operational demand.
2. **Identifies Customer:** Implementator identifies and selects the customer organization and customer contact originating the demand.
3. **Enters Demand Details:** Implementator enters the core demand information, including a clear title, detailed description of the operational demand, and request type classification.
4. **Captures Operational Context:** Implementator associates any immediately known operational context, such as affected product or ongoing work package if applicable.
5. **Reviews for Completeness:** Implementator reviews the entered demand information to ensure it is self-contained and provides sufficient clarity for subsequent handling.
6. **Submits Request:** Implementator confirms and submits the recorded demand.
7. **Confirms Capture:** Implementator receives confirmation that the request has been captured in the system and is ready for ownership assignment.

### Alternative Paths
* **Alternative Path A (Customer-independent demand):** The demand originates internally or without an immediate customer association; Implementator proceeds to enter the demand details while leaving the customer reference blank.
* **Alternative Path B (Missing critical demand details):** Implementator notes that vital information is missing from the customer's communication; Implementator records the demand with available facts and attaches notes highlighting specific clarifications needed from the customer.

### Success Outcome
The operational demand is recorded in the system as a captured request with defined origin and demand details, ready for ownership assignment.

### Information Needed
* Customer identity and contact information (when customer-originated)
* Demand title and narrative description
* Request type classification
* Optional operational context (Product, Work Package reference)

---

## UJ-REQ-002: Assign Request Owner

**Use Case Reference:** UC-REQ-002

### Actor
Implementator

### Goal
Ensure a Request has a designated responsible owner.

### Trigger
A captured Request requires an operational owner, or an unassigned request is identified.

### Main Journey
1. **Navigates to Unassigned Requests:** Implementator navigates to the request management area to locate requests awaiting ownership.
2. **Inspects Request Details:** Implementator selects an unassigned Request and reviews its demand description, request type, customer context, and operational urgency.
3. **Identifies Suitable Handler:** Implementator reviews organizational members to identify an appropriate handler based on expertise, domain familiarity, and operational capability.
4. **Designates Request Owner:** Implementator selects the designated handler to assign ownership of the Request.
5. **Provides Contextual Guidance:** Implementator adds optional assignment notes or instructions to orient the designated handler.
6. **Submits Assignment:** Implementator confirms and submits the assignment.
7. **Verifies Assignment:** Implementator sees that the Request is updated with the assigned handler and that ownership notification has been issued.

### Alternative Paths
* **Alternative Path A (Reassigning an existing request):** Implementator determines that a currently assigned handler is unable to continue; Implementator selects the active Request, chooses a replacement handler, enters a reassignment justification, and submits the update.
* **Alternative Path B (Self-assignment):** Implementator determines that they possess the direct capability and context to handle the demand; Implementator selects themselves as the assigned handler and confirms.
* **Alternative Path C (Deferred assignment due to resource unavailability):** Implementator finds that no suitable handler is presently available; Implementator leaves the request unassigned, adds an operational note regarding the resource constraint, and flags it for follow-up.

### Success Outcome
The Request is associated with a designated handler responsible for evaluating and driving the request forward.

### Information Needed
* Request summary, demand description, customer context, and type
* Organization member roster and domain/module expertise
* Assignment rationale or preliminary handling instructions

---

## UJ-REQ-003: Evaluate Request

**Use Case Reference:** UC-REQ-003

### Actor
Assigned Handler

### Goal
Determine whether the Request can be feasibly handled within capability and authority.

### Trigger
The assigned handler is notified or becomes aware of a Request assigned to them.

### Main Journey
1. **Accesses Assigned Work:** Assigned Handler navigates to their personal inbox or assigned requests view.
2. **Inspects Request Details:** Assigned Handler opens the assigned Request to review the complete operational demand, customer background, and associated context.
3. **Consumes Supporting Information:** Assigned Handler examines any attached evidence, communication posts, or related request history.
4. **Assesses Operational Feasibility:** Assigned Handler evaluates technical feasibility, required effort, scope boundaries, and contractual or operational constraints against their own expertise and authority.
5. **Determines Next Step:** Assigned Handler concludes whether to accept responsibility, reject the request, escalate the issue, or request a management decision.

### Alternative Paths
* **Alternative Path A (Information gap during evaluation):** Assigned Handler finds critical details ambiguous or missing; Assigned Handler posts a query for clarification to the Implementator or Requester while keeping the evaluation in progress.
* **Alternative Path B (Immediate acceptance on routine request):** Assigned Handler identifies a familiar, standard demand and transitions immediately to accepting responsibility.

### Success Outcome
The Assigned Handler reaches an informed understanding of the request's feasibility and scope, enabling a definitive decision on acceptance, rejection, escalation, or management referral.

### Information Needed
* Full request details (title, description, customer, product context, type)
* Supporting documents, screenshots, error logs, or communication notes
* Understanding of personal authority boundaries and team capability

---

## UJ-REQ-004: Accept Request Responsibility

**Use Case Reference:** UC-REQ-004

### Actor
Assigned Handler

### Goal
Accept responsibility for maintaining, executing, and resolving the Request.

### Trigger
The assigned handler completes evaluation and concludes that the Request can be handled within their capability and authority.

### Main Journey
1. **Selects Evaluated Request:** Assigned Handler selects the evaluated Request awaiting acceptance.
2. **Confirms Intent to Own:** Assigned Handler initiates the acceptance action to take official ownership.
3. **Enters Initial Handling Notes:** Assigned Handler optionally enters target milestones, planned approach, or an acknowledgement note for the customer or team.
4. **Submits Acceptance:** Assigned Handler confirms and submits the acceptance decision.
5. **Verifies Active Status:** Assigned Handler observes that the Request has transitioned to active state and reflects their confirmed status as Request Owner.

### Alternative Paths
* **Alternative Path A (Acceptance with scoped caveats):** Assigned Handler agrees to take ownership but records specific assumptions, boundary conditions, or out-of-scope items within the acceptance remarks.

### Success Outcome
The Assigned Handler officially becomes the confirmed Request Owner, and the Request enters the active lifecycle state for active resolution.

### Information Needed
* Request identity and summary
* Planned execution approach, target timeline, or acknowledgement notes (optional)

---

## UJ-REQ-005: Reject Request

**Use Case Reference:** UC-REQ-005

### Actor
Assigned Handler

### Goal
Decline responsibility for the Request during evaluation.

### Trigger
The assigned handler evaluates the Request and determines it cannot or should not be fulfilled (e.g., technically infeasible, invalid demand, duplicate, or outside operational boundaries).

### Main Journey
1. **Selects Evaluated Request:** Assigned Handler views the evaluated Request.
2. **Initiates Rejection:** Assigned Handler chooses the rejection action.
3. **Enters Rejection Rationale:** Assigned Handler provides a clear, detailed justification explaining why the Request cannot be fulfilled.
4. **Selects Rejection Category:** Assigned Handler categorizes the rejection reason (e.g., infeasible, duplicate, out of scope, cancelled by requester).
5. **Submits Rejection:** Assigned Handler reviews the explanation and submits the rejection.
6. **Confirms Closed Outcome:** Assigned Handler verifies that the Request is marked as rejected, closed, and preserved with the recorded resolution rationale.

### Alternative Paths
* **Alternative Path A (Rejection as duplicate):** Assigned Handler identifies that an identical request is already active or resolved; Assigned Handler selects duplicate as the reason and enters the reference to the existing request.
* **Alternative Path B (Rejection requiring commercial renegotiation):** Assigned Handler identifies that the demand contradicts existing contracts; Assigned Handler records guidance for the Implementator to discuss renewal or change orders with the customer.

### Success Outcome
The Request is formally declined and closed with a clear, auditable resolution explanation available to the Implementator and Requester.

### Information Needed
* Request details and evaluation context
* Detailed rejection rationale and explanatory justification
* Reference to existing duplicate requests (if applicable)

---

## UJ-REQ-006: Escalate Request

**Use Case Reference:** UC-REQ-006

### Actor
Request Owner

### Goal
Escalate the Request when it exceeds the current owner's authority or capability.

### Trigger
The Request Owner encounters technical complexity, specialized skill gaps, inter-team blockers, or authority thresholds that prevent resolution at their level.

### Main Journey
1. **Opens Active Request:** Request Owner navigates to the active Request experiencing blockers.
2. **Identifies Escalation Need:** Request Owner determines that resolving the request requires higher authority or specialized senior expertise.
3. **Initiates Escalation:** Request Owner selects the escalation action.
4. **Articulates Escalation Details:** Request Owner describes the specific impediment, skill gap, or authority boundary exceeded, along with what intervention is requested.
5. **Identifies Escalation Target:** Request Owner specifies the recommended target group, technical lead, or authority level.
6. **Submits Escalation:** Request Owner confirms and submits the escalation.
7. **Verifies Escalated State:** Request Owner sees that the Request is marked as escalated and flagged for attention by the relevant authority.

### Alternative Paths
* **Alternative Path A (Informal resolution before submission):** Request Owner consults a senior peer and obtains an immediate solution; Request Owner cancels the escalation flow and resumes active handling.

### Success Outcome
The Request is flagged as escalated with a documented justification, drawing attention from higher authority personnel to assist, unblock, or reassign the request.

### Information Needed
* Current request progress and attempted approaches
* Clear articulation of the roadblock, skill gap, or authority boundary exceeded
* Desired outcome from escalation (e.g., senior intervention, team reassignment)

---

## UJ-REQ-007: Request Management Decision

**Use Case Reference:** UC-REQ-007

### Actor
Request Owner

### Goal
Elevate the Request for a management-level decision.

### Trigger
The Request Owner cannot resolve the Request because it involves organizational policy, risk tolerance, contractual implications, or resource allocation conflicts beyond their mandate.

### Main Journey
1. **Opens Active Request:** Request Owner navigates to the active Request requiring a policy or strategic decision.
2. **Synthesizes Decision Dilemma:** Request Owner defines the policy conflict, resource dispute, or contractual question that requires executive determination.
3. **Initiates Management Decision Request:** Request Owner selects the action to request a management decision.
4. **Documents Context and Options:** Request Owner provides a structured overview of the situation, outlining viable decision options, trade-offs, financial or operational risks, and a recommended direction.
5. **Submits Decision Request:** Request Owner submits the request for management determination.
6. **Verifies Awaiting Decision Status:** Request Owner verifies that the Request is clearly flagged as awaiting management decision and notified to designated management actors.

### Alternative Paths
* **Alternative Path A (Urgent operational / contractual impact):** Request Owner flags the decision request with high business criticality, citing imminent customer SLA or operational breach.
* **Alternative Path B (Management requests supplementary analysis):** Management requests additional cost or impact data; Request Owner adds the requested analysis to the existing decision request without resetting the workflow.

### Success Outcome
The Request is formally elevated for management determination with comprehensive context, explicit options, and risk evaluations documented for executive action.

### Information Needed
* Request summary and customer operational stakes
* The specific decision question or policy contradiction
* Analysis of options, associated risks, trade-offs, and recommended course of action

---

## UJ-REQ-008: Review Request Completion

**Use Case Reference:** UC-REQ-008

### Actor
Implementator

### Goal
Review completed work on a Request and decide whether to accept or reject the resolution.

### Trigger
The Request Owner reports that work on the Request is complete and submits the resolution for review.

### Main Journey
1. **Navigates to Completed Requests for Review:** Implementator accesses the queue of requests pending completion review.
2. **Inspects Resolution Deliverables:** Implementator selects the completed Request and reviews the documented resolution summary, work evidence, and notes provided by the Request Owner.
3. **Compares Against Original Demand:** Implementator verifies the resolution against the original customer demand, acceptance criteria, and operational expectations.
4. **Decides on Resolution Validity:** Implementator confirms that the resolution satisfactorily fulfills the demand.
5. **Records Acceptance Decision:** Implementator records positive review comments and confirms acceptance.
6. **Verifies Closed Resolution:** Implementator verifies that the Request transitions to the closed state with verified resolution history.

### Alternative Paths
* **Alternative Path A (Resolution rejected — Returned for rework):** Implementator inspects the delivered work and identifies deficiencies, unmet requirements, or unresolved defects; Implementator selects the rework action, enters detailed corrective feedback explaining what must be addressed, and returns the Request to the Request Owner in active state.
* **Alternative Path B (Verification with customer contact):** Implementator demonstrates or reviews the completed work with the customer contact; upon obtaining customer sign-off, Implementator proceeds to record formal acceptance in the system.

### Success Outcome
The delivered work on the Request is thoroughly evaluated, leading to either successful closure with customer-ready resolution or timely return to the Request Owner with actionable rework guidance.

### Information Needed
* Original customer demand details, acceptance criteria, and context
* Recorded resolution summary, release/fix evidence, and Request Owner notes
* Customer verification feedback (if applicable)
* Evaluative review remarks or detailed rework instructions
