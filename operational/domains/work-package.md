---

Title: Work Package Domain
Code: WORK-PACKAGE
Artifact: DOMAIN
Version: 1.0
LastUpdated: 2026-09-23
---

# 1. Business Overview

The Work Package domain defines a temporary package of related operational Requests that share a common objective.

A Work Package provides a business context for grouping Requests that belong to the same piece of work.

The domain answers:

* What work are we grouping together?
* What is the objective of the Work Package?
* Who owns the Work Package?
* Which Customer and Product are involved?
* What is the current state of the Work Package?

A Work Package is not a Request.

A Request may exist without a Work Package.

The Work Package domain does not define Request execution, estimation, commitment, scheduling, or operational workflow.

---

# 2. Ubiquitous Language

| Term                | Meaning                                                                   |
| ------------------- | ------------------------------------------------------------------------- |
| Work Package        | A temporary container for related Requests that share a common objective. |
| Objective           | The intended result that gives the Work Package its purpose.              |
| Work Package Owner  | The Person responsible for maintaining the Work Package.                  |
| Scope               | The set of Requests currently included in the Work Package.               |
| Work Package Status | The current lifecycle state of the Work Package.                          |

---

# 3. Domain Capabilities

The Work Package domain provides:

* Work Package Management
* Work Package Ownership Management
* Work Package Scope Management
* Work Package Lifecycle Management

These capabilities maintain the identity and state of a Work Package.

They do not define how Requests are processed or delivered.

---

# 4. Actors & Roles

## Management

May create, change, or close Work Packages when management involvement is required.

## Work Package Owner

Responsible for maintaining the Work Package and its current operational state.

The Work Package Owner is a Person defined by the Organization domain.

## Request Owner

Owns an individual Request.

Request ownership belongs to the Request domain and is independent from Work Package ownership.

---

# 5. Domain Objects

## Work Package

Represents a temporary package of related Requests with a common objective.

Attributes:

```text id="rq6h2m"
WorkPackageId
Name
Objective
Status
OwnerPersonId
CustomerId
ProductId
```

`CustomerId` and `ProductId` are optional because a Work Package may not always be customer-specific or product-specific.

Examples:

```text id="1p2vfg"
RSUD A Go-Live Preparation
MyHospital Billing Improvement
PenaEl MVP Preparation
SATUSEHAT Integration Preparation
```

---

## Work Package Request

Represents the relationship between a Work Package and a Request.

Attributes:

```text id="w3a9vs"
WorkPackageId
RequestId
```

A Request may belong to zero or one Work Package.

A Work Package may contain zero or more Requests.

The relationship does not transfer ownership of the Request.

---

# 6. Aggregates

## Work Package Aggregate

```text id="tq7m5r"
WorkPackage
└── Requests
```

The Work Package Aggregate is the authoritative source of:

* Work Package identity
* Objective
* Owner
* Current lifecycle state
* Work Package membership

The Request itself remains owned by the Request domain.

---

# 7. Relationships

## Work Package → Organization

Work Package Owner references a Person from the Organization Domain.

The Work Package domain does not define organizational structure.

---

## Work Package → Customer

A Work Package may reference a Customer from the Customer Domain.

Customer ownership remains defined by the Customer Domain.

---

## Work Package → Product

A Work Package may reference a Product from the Product Domain.

Product ownership remains defined by the Product Domain.

---

## Work Package → Request

A Work Package groups related Requests.

```text id="f2l3z6"
Work Package
    ├── Request A
    ├── Request B
    └── Request C
```

The Work Package does not own the Request's lifecycle.

Request lifecycle remains authoritative in the Request domain.

---

# 8. Business Rules

1. Every Work Package must have a unique identity.

2. Every active Work Package must have exactly one Work Package Owner.

3. Work Package Owner must reference a Person from the Organization Domain.

4. Every Work Package must have an objective.

5. A Work Package must represent a temporary and meaningful grouping of work.

6. A Work Package may reference zero or one Customer.

7. A Work Package may reference zero or one Product.

8. A Work Package may contain zero or more Requests.

9. A Request may belong to zero or one Work Package.

10. A Request may exist without a Work Package.

11. Adding a Request to a Work Package does not change the Request Owner.

12. Removing a Request from a Work Package does not change the Request itself.

13. Closing a Work Package does not imply that every Request within it is closed.

14. Closing or completing all Requests does not automatically change the Work Package state unless the Work Package's business condition has actually been satisfied.

15. Historical Work Package membership must remain traceable.

---

# 9. State Machines & Lifecycles

## Work Package Lifecycle

```text id="kzj9p2"
DRAFT
   ↓
ACTIVE
   ↓
CLOSED
```

### DRAFT

The Work Package has been created but is not yet an active operational matter.

### ACTIVE

The Work Package represents current operational work.

### CLOSED

The Work Package objective has been completed, cancelled, or is otherwise no longer an active operational matter.

A Work Package may move directly from `DRAFT` to `CLOSED` when no active work is required.

---

## Work Package Request Relationship

The relationship itself does not have an independent lifecycle.

It exists while the Request belongs to the Work Package and is removed when the relationship ends.

---

# 10. Domain Events

Examples:

```text id="f9j1nx"
WorkPackageCreated
WorkPackageActivated
WorkPackageClosed

WorkPackageOwnerChanged

RequestAddedToWorkPackage
RequestRemovedFromWorkPackage
```

Events represent changes in Work Package knowledge.

They do not define the operational workflow for Requests.

---

# 11. Related Features

The Work Package domain may participate in:

* Manage Work Packages
* Manage Work Package Scope
* Manage Work Package Ownership

Projects, Requests, Customers, and Products may consume or reference Work Package information.

The Work Package domain does not define those domains' workflows.
