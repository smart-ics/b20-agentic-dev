# Request Lifecycle Scenarios

## SC-REQ-001

```text id="6xg5bf"
Scenario ID:
SC-REQ-001

Scenario Name:
Implementator Receives Customer Request

Primary Actor:
Implementator

Goal:
Capture a customer operational demand.

Related Domains:
Request
Customer

Trigger:
Customer communicates a need, issue, or change request.

Expected Outcome:
A Request is recorded and becomes operational knowledge.
```

## SC-REQ-002

```text id="t8n06o"
Scenario ID:
SC-REQ-002

Scenario Name:
Implementator Assigns Request Owner

Primary Actor:
Implementator

Goal:
Ensure a Request has a responsible owner.

Related Domains:
Request
Organization

Trigger:
A Request requires ownership.

Expected Outcome:
A Request Owner is assigned.
```

## SC-REQ-003

```text id="5f9pxq"
Scenario ID:
SC-REQ-003

Scenario Name:
Programmer Evaluates Request

Primary Actor:
Programmer

Goal:
Determine whether the Request can be handled.

Related Domains:
Request

Trigger:
The Programmer becomes aware of the Request.

Expected Outcome:
The Request is accepted, rejected, escalated, or sent for decision.
```

## SC-REQ-004

```text id="sc-req-004"
Scenario ID:
SC-REQ-004

Scenario Name:
Programmer Accepts Request

Primary Actor:
Programmer

Goal:
Take responsibility for handling the Request.

Related Domains:
Request

Trigger:
The Programmer evaluates the Request and determines it can be handled.

Expected Outcome:
The Programmer becomes the Request Owner.
```

## SC-REQ-005

```text id="sc-req-005"
Scenario ID:
SC-REQ-005

Scenario Name:
Programmer Rejects Request

Primary Actor:
Programmer

Goal:
Decline responsibility for the Request.

Related Domains:
Request

Trigger:
The Programmer evaluates the Request and determines it cannot be handled.

Expected Outcome:
The Request is marked as rejected.
```

## SC-REQ-006

```text id="sc-req-006"
Scenario ID:
SC-REQ-006

Scenario Name:
Request Requires Escalation

Primary Actor:
System

Goal:
Signal that the Request exceeds the current handler's authority or capability.

Related Domains:
Request

Trigger:
The Request cannot be resolved within the current level of authority or expertise.

Expected Outcome:
The Request is escalated to a higher authority level.
```

## SC-REQ-007

```text id="sc-req-007"
Scenario ID:
SC-REQ-007

Scenario Name:
Request Requires Management Decision

Primary Actor:
System

Goal:
Signal that the Request requires a management-level decision.

Related Domains:
Request
Management

Trigger:
The Request involves policy, risk, or resource allocation beyond operational scope.

Expected Outcome:
The Request is flagged for management decision.
```

## SC-REQ-008

```text id="sc-req-008"
Scenario ID:
SC-REQ-008

Scenario Name:
Programmer Reports Request Completion

Primary Actor:
Programmer

Goal:
Notify that work on the Request is complete.

Related Domains:
Request

Trigger:
The Programmer finishes work on the Request.

Expected Outcome:
The Request is marked as completed and ready for resolution review.
```

## SC-REQ-009

```text id="sc-req-009"
Scenario ID:
SC-REQ-009

Scenario Name:
Implementator Accepts Resolution

Primary Actor:
Implementator

Goal:
Approve the completed work on the Request.

Related Domains:
Request

Trigger:
The Request is reported as completed.

Expected Outcome:
The Request resolution is accepted.
```

## SC-REQ-010

```text id="sc-req-010"
Scenario ID:
SC-REQ-010

Scenario Name:
Implementator Rejects Resolution

Primary Actor:
Implementator

Goal:
Reject the completed work on the Request.

Related Domains:
Request

Trigger:
The Request is reported as completed but does not meet standards.

Expected Outcome:
The Request resolution is rejected and returned for rework.
```

## SC-REQ-011

```text id="sc-req-011"
Scenario ID:
SC-REQ-011

Scenario Name:
Request Is Resolved

Primary Actor:
System

Goal:
Close the Request as successfully completed.

Related Domains:
Request

Trigger:
The Request resolution is accepted.

Expected Outcome:
The Request is marked as resolved and closed.
```

## SC-REQ-012

```text id="sc-req-012"
Scenario ID:
SC-REQ-012

Scenario Name:
Request Is Rejected

Primary Actor:
System

Goal:
Close the Request as rejected.

Related Domains:
Request

Trigger:
The Request is rejected during evaluation or resolution.

Expected Outcome:
The Request is marked as rejected and closed.
```