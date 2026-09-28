# Feature

## Identity

ID: FEAT-REQ-003
Name: Evaluate Request
Type: Workflow

## Purpose

Enables the designated Request Owner to perform the triage evaluation workflow on an assigned Request, assessing its feasibility, operational requirements, and organizational authority before committing to an operational disposition.

## User Outcome

The Request Owner arrives at a clear operational determination regarding whether the request can be accepted for active execution, rejected, escalated, or elevated for management decision.

## Traceability

### Domains
- Request

### Scenarios
- SC-REQ-003

### Use Cases
- UC-REQ-003

### User Journeys
- UJ-REQ-003

### Screens
- SCR-REQ-003

## Preconditions

- Target Request exists and is in `CAPTURED` state (or assigned pending evaluation).
- The actor is the designated Request Owner (or authorized delegate).

## Capability

The system provides an evaluation workspace within `SCR-REQ-003: Request Detail` that presents core demand attributes, context links (Customer, Product, Work Package), and supporting evidence, and surfaces actionable evaluation pathways (Accept Responsibility, Reject Request, Escalate Request, Request Management Decision). The owner can record clarification inquiries or preliminary findings prior to concluding the evaluation.

## Business Rules

- Evaluation is an active triage workflow, distinct from passive screen viewing (Feature Review Standard Principle 5).
- The Request Owner is accountable for verifying whether the request falls within their technical capability and operational authority (Actor Model Core Principle; Request Domain Section 4).
- The evaluation process itself does not prematurely mutate the authoritative lifecycle state; the request remains in `CAPTURED` state until an explicit disposition action is executed (Request Domain Section 9.2).
- The owner may request customer or team clarification via inline notes/comments without completing evaluation (UJ-REQ-003 Alternative Paths).
- Once evaluated, the workflow guides the owner to one of four authoritative actions: Accept (`FEAT-REQ-004`), Reject (`FEAT-REQ-005`), Escalate (`FEAT-REQ-006`), or Request Management Decision (`FEAT-REQ-007`).

## Success Result

The Request Owner completes triage evaluation and initiates an authoritative lifecycle transition or condition update based on their operational assessment.

## Failure Conditions

- Target Request is in `CLOSED` state (state conflict).
- Actor is not the assigned Request Owner or an authorized manager (authority failure).

## Acceptance Criteria

- [ ] Evaluation workflow is accessible on `SCR-REQ-003: Request Detail` for the assigned Request Owner.
- [ ] Evaluation view displays demand details, customer/product context, and activity history.
- [ ] Interface clearly provides disposition pathways: Accept, Reject, Escalate, and Request Decision.
- [ ] The owner can submit clarification notes during evaluation without closing the request.
- [ ] Evaluating the request does not prematurely alter its authoritative lifecycle state.

## Implementation Notes

Triage evaluation workflow container guiding the owner through lifecycle disposition options on the Request Aggregate.
