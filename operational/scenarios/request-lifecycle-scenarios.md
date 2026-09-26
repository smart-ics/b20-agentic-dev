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
Assigned Handler Evaluates Request

Primary Actor:
Assigned Handler

Goal:
Determine whether the Request can be handled.

Related Domains:
Request

Trigger:
The assigned handler becomes aware of the Request.

Expected Outcome:
The Request is accepted, rejected, escalated, or sent for decision.
```

## SC-REQ-004

```text id="sc-req-004"
Scenario ID:
SC-REQ-004

Scenario Name:
Assigned Handler Accepts Request

Primary Actor:
Assigned Handler

Goal:
Accept responsibility for maintaining and resolving the Request.

Related Domains:
Request

Trigger:
The assigned handler evaluates the Request and determines it can be handled.

Expected Outcome:
The assigned handler becomes the Request Owner and the Request becomes active.
```

## SC-REQ-005

```text id="sc-req-005"
Scenario ID:
SC-REQ-005

Scenario Name:
Assigned Handler Rejects Request

Primary Actor:
Assigned Handler

Goal:
Decline responsibility for the Request during evaluation.

Related Domains:
Request

Trigger:
The assigned handler evaluates the Request and determines it cannot be handled.

Expected Outcome:
The Request is marked as rejected and closed.
```

## SC-REQ-006

```text id="sc-req-006"
Scenario ID:
SC-REQ-006

Scenario Name:
Request Owner Escalates Request

Primary Actor:
Request Owner

Goal:
Escalate the Request when it exceeds the current owner's authority or capability.

Related Domains:
Request

Trigger:
The Request Owner cannot resolve the Request within their authority.

Expected Outcome:
The Request is escalated to a higher authority level.
```

## SC-REQ-007

```text id="sc-req-007"
Scenario ID:
SC-REQ-007

Scenario Name:
Request Owner Requests Management Decision

Primary Actor:
Request Owner

Goal:
Elevate the Request for a management-level decision.

Related Domains:
Request
Organization

Trigger:
The Request Owner cannot resolve the Request because it involves policy, risk, or resource allocation beyond their authority.

Expected Outcome:
The Request is flagged for management decision.
```

## SC-REQ-008

```text id="sc-req-008"
Scenario ID:
SC-REQ-008

Scenario Name:
Request Completion Is Reviewed

Primary Actor:
Implementator

Goal:
Review completed work on a Request and decide whether to accept or reject the resolution.

Related Domains:
Request

Trigger:
The Request Owner reports that work on the Request is complete.

Expected Outcome:
The resolution is accepted (and the Request is resolved) or rejected (and the Request is returned for rework).
```