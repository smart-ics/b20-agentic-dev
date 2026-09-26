---

Title: Request Domain
Code: REQUEST
Artifact: DOMAIN
Version: 1.1
LastUpdated: 2026-09-26
---

# 1. Business Overview

The Request Domain defines a request for ICS to perform, change, investigate, provide, or resolve something.

A Request represents an operational demand placed on ICS.

A Request may originate from:
- a Person;
- a Customer Contact; or
- another recognized operational actor.

The domain answers:

* What is being requested?
* Who requested it?
* Who owns it?
* What is the current state?
* What Customer, Product, or Work Package is relevant?

A Request is not a Commitment.

A Request is not a Work Package.

A Request does not imply that ICS has agreed to perform the requested work.

---

# 2. Ubiquitous Language

| Term            | Meaning                                                                                   |
| --------------- | ----------------------------------------------------------------------------------------- |
| Request         | A recorded demand for ICS to perform, change, investigate, provide, or resolve something. |
| Requester       | The Person or external party that originated the Request.                                 |
| Request Owner   | The Person responsible for maintaining and resolving the Request.                         |
| Request Type    | A classification describing the nature of the Request.                                    |
| Request Status  | The authoritative current state of the Request.                                           |
| Request Context | The Customer, Product, Work Package, or other context associated with the Request.        |
| Resolution      | The outcome that satisfies or terminates the Request.                                     |

---

# 3. Domain Capabilities

The Request Domain provides:

* Request Management
* Request Ownership Management
* Request Classification Management
* Request Context Management
* Request Lifecycle Management
* Request Resolution Management

These capabilities maintain authoritative Request knowledge.

They do not define the operational workflow used to process Requests.

---

# 4. Actors & Roles

## Requester

The Person or external party that originates a Request.

A Requester may be:

* a Person;
* a Customer Contact; or
* another recognized operational actor.

## Request Owner

The Person responsible for maintaining the current state of a Request and resolving or escalating it when necessary.

The Request Owner is a Person defined by the Organization Domain.

## Manager

May review, decide, or intervene when the Request exceeds the Request Owner's authority or requires management attention.

---

# 5. Domain Objects

## Request

Represents a demand placed on ICS.

Attributes:

```text
RequestId
Title
Description
Type
Status
Requester
OwnerPersonId
CustomerId
ProductId
WorkPackageId
Resolution
```

`CustomerId`, `ProductId`, and `WorkPackageId` are optional.

A Request may exist without any of them.

Examples:

```text
Add BPJS validation to billing.
Fix incorrect pharmacy report.
Investigate slow login.
Provide training for new users.
Change hospital workflow configuration.
```

---

## Requester

Identifies the origin of the Request.

A Requester may reference:

```text
Person
Customer Contact
```

The Request domain does not own either Person or Customer Contact.

---

## Resolution

Represents the recorded outcome when a Request is closed.

Attributes:

```text
Outcome
Description
ResolvedBy
ResolvedAt
```

Resolution is part of the Request's final state and history.

It is not a separate operational domain.

---

# 6. Aggregates

## Request Aggregate

```text
Request
└── Resolution
```

The Request Aggregate is the authoritative source of:

* Request identity
* Current Request state
* Request ownership
* Request context
* Request resolution

Requester, Customer, Product, Work Package, and Person remain owned by their respective domains.

---

# 7. Relationships

## Request → Organization

Request Owner references a Person from the Organization Domain.

The Request domain does not own organizational identity.

---

## Request → Customer

A Request may reference a Customer.

The Customer Domain remains authoritative for Customer identity.

---

## Request → Product

A Request may reference a Product.

The Product Domain remains authoritative for Product identity.

---

## Request → Work Package

A Request may reference a Work Package.

The Work Package Domain defines the grouping relationship.

A Request does not require a Work Package.

---

## Request → Requester

The Request records who originated the demand.

The referenced Person or Customer Contact remains owned by its source domain.

---

# 8. Business Rules

1. Every Request must have a unique identity.

2. Every active Request must have exactly one Request Owner.

3. Request Owner must reference a Person from the Organization Domain.

4. A Request must contain sufficient information to understand what is being requested.

5. A Request may exist without a Customer.

6. A Request may exist without a Product.

7. A Request may exist without a Work Package.

8. A Request may belong to at most one Work Package.

9. Assigning a Request to a Work Package does not change its Request Owner.

10. A Request is not a Commitment.

11. A Request being accepted or actively worked does not by itself imply that a delivery commitment exists.

12. A Request may be rejected without being executed.

13. A Request may be resolved directly without creating a Work Package.

14. Request history must preserve significant ownership, state, and resolution changes.

15. Closing a Request must record its meaningful outcome when an outcome exists.

16. A closed Request remains historically retrievable.

---

# 9. State Machines & Lifecycles

## Request Lifecycle

The Request lifecycle follows the generic operational lifecycle while allowing direct closure when appropriate.

```text
CAPTURED
    ↓
VALIDATED
    ↓
ACTIVE
    ↓
CLOSED
```

### CAPTURED

The Request has been recorded but has not yet been sufficiently validated.

### VALIDATED

The Request is sufficiently understood to be treated as reliable operational knowledge.

`VALIDATED` is a knowledge condition, not a mandatory workflow step. It may be skipped when the Request is already sufficiently understood at capture.

Validation does not mean acceptance or commitment.

### ACTIVE

The Request represents an active operational matter.

It may be:

* being resolved;
* awaiting information;
* escalated;
* awaiting a decision;
* otherwise requiring operational attention.

These are operational conditions, not additional lifecycle states.

### CLOSED

The Request no longer requires active operational work.

Examples:

```text
Resolved
Rejected
Cancelled
No Longer Required
```

A Request may move directly:

```text
CAPTURED → CLOSED
```

when no active operational work is required.

---

## Request State vs Workflow Activity

The following are workflow activities and must not automatically become Request lifecycle states:

```text
Assessing
Estimating
Discussing
Escalating
Waiting
Implementing
Reviewing
```

The authoritative Request state remains based on its operational condition.

---

# 10. Domain Events

Examples:

```text
RequestCreated
RequestOwnerChanged
RequestCustomerChanged
RequestProductChanged
RequestWorkPackageChanged
RequestStatusChanged
RequestResolved
RequestRejected
RequestCancelled
```

Events represent changes in authoritative Request state.

They do not define the workflow used to process the Request.
