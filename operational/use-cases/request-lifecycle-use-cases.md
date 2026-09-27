# Request Lifecycle Use Cases

Derived from **Request Lifecycle Scenarios** using the **Use‑Case Discovery Skill**.

---

## UC-REQ-001: Record Customer Request

**Actor:** Implementator

**Goal:** Capture a customer operational demand.

**System Interaction:** Implementator records the customer's operational demand in the system, associating the Customer and capturing request details; the system stores the Request in the captured state.

**Related Domains:** Request, Customer

**Related Scenario:** SC-REQ-001

---

## UC-REQ-002: Assign Request Owner

**Actor:** Implementator

**Goal:** Ensure a Request has a responsible owner.

**System Interaction:** Implementator identifies a Request requiring ownership, selects a handler from the organization, and assigns them as the Request Owner in the system.

**Related Domains:** Request, Organization

**Related Scenario:** SC-REQ-002

---

## UC-REQ-003: Evaluate Request

**Actor:** Assigned Handler

**Goal:** Determine whether the Request can be handled.

**System Interaction:** Assigned Handler accesses the assigned Request in the system and inspects its demand details, context, and operational requirements to evaluate feasibility.

**Related Domains:** Request

**Related Scenario:** SC-REQ-003

---

## UC-REQ-004: Accept Request Responsibility

**Actor:** Assigned Handler

**Goal:** Accept responsibility for maintaining and resolving the Request.

**System Interaction:** Assigned Handler confirms acceptance of the Request in the system; the system transitions the Request to the active state and establishes the handler as the confirmed Request Owner.

**Related Domains:** Request

**Related Scenario:** SC-REQ-004

---

## UC-REQ-005: Reject Request

**Actor:** Assigned Handler

**Goal:** Decline responsibility for the Request during evaluation.

**System Interaction:** Assigned Handler records the rejection with an explanatory reason in the system; the system marks the Request as rejected, records the resolution outcome, and transitions the Request to the closed state.

**Related Domains:** Request

**Related Scenario:** SC-REQ-005

---

## UC-REQ-006: Escalate Request

**Actor:** Request Owner

**Goal:** Escalate the Request when it exceeds the current owner's authority or capability.

**System Interaction:** Request Owner identifies that the Request exceeds their authority or capability and submits an escalation in the system; the system updates the Request status to escalated for higher authority attention.

**Related Domains:** Request

**Related Scenario:** SC-REQ-006

---

## UC-REQ-007: Request Management Decision

**Actor:** Request Owner

**Goal:** Elevate the Request for a management-level decision.

**System Interaction:** Request Owner specifies the operational policy, risk, or resource conflict requiring management determination and submits the decision request in the system; the system flags the Request as awaiting management decision.

**Related Domains:** Request, Organization

**Related Scenario:** SC-REQ-007

---

## UC-REQ-008: Review Request Completion

**Actor:** Implementator

**Goal:** Review completed work on a Request and decide whether to accept or reject the resolution.

**System Interaction:** Implementator inspects the completed work and recorded resolution in the system and records the review outcome—either accepting the resolution (closing the Request) or rejecting it (returning the Request to the Request Owner for rework).

**Related Domains:** Request

**Related Scenario:** SC-REQ-008

---

*These use‑cases describe what actors accomplish through system interaction to support operational scenarios, without specifying UI, database schema, or implementation mechanics.*
