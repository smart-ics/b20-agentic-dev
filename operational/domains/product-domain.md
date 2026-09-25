---

Title: Product Domain
Code: PRODUCT
Artifact: DOMAIN
Version: 2.1
LastUpdated: 2026-09-26
---

# 1. Business Overview

The Product Domain defines the products provided, maintained, and developed by ICS.

The Product Domain is the authoritative source of product identity, ownership, and lifecycle state.

The domain answers:

* What products exist?
* Who owns a product?
* What is the current state of a product?

The Product Domain does not manage:

* Customers
* Projects
* Requests
* Development workflow
* Roadmaps
* Operational performance

---

# 2. Ubiquitous Language

| Term           | Meaning                                                       |
| -------------- | ------------------------------------------------------------- |
| Product        | A business product provided, maintained, or developed by ICS. |
| Product Owner  | The Person currently responsible for the Product.             |
| Product Status | The current lifecycle state of a Product.                     |

---

# 3. Domain Capabilities

* Product Management
* Product Ownership Management
* Product Lifecycle Management

---

# 4. Actors

## Management

Approves creation, retirement, and ownership of Products.

## Product Owner

Responsible for the Product.

## Administrator

Maintains Product master data.

---

# 5. Domain Object

## Product

Represents a business product.

Attributes:

```text
ProductId
Name
Code
Description
Status
OwnerPersonId
```

Examples:

```text
MyHospital
PenaEl
BTrade3
Jetset
```

A Product is not a Customer.

A Product is not a Project.

A Product is not a Request.

---

# 6. Aggregate

## Product Aggregate

```text
Product
```

The Product Aggregate is the authoritative source of Product identity and current Product state.

---

# 7. Relationships

## Product → Organization

Product Owner references a Person from the Organization Domain.

---

## Product → Customer

Customers may use Products.

Customer relationships are owned by the Customer Domain.

---

## Product → Project

Projects may reference Products.

Projects are owned by the Project Domain.

---

## Product → Request

Requests may reference Products.

Requests are owned by the Request Domain.

---

# 8. Business Rules

1. Every Product must have a unique identity.

2. Every active Product must have exactly one Product Owner.

3. Product Owner must reference a Person from the Organization Domain.

4. Product ownership does not imply Project ownership.

5. Product ownership does not imply Request ownership.

6. Product identity must remain stable throughout its lifecycle.

7. A Product may become inactive while preserving historical references.

---

# 9. State Machines & Lifecycles

## Product Lifecycle

```text
PLANNED
    ↓
ACTIVE
    ↓
DEPRECATED
    ↓
RETIRED
```

### PLANNED

Recognized by ICS but not yet active.

### ACTIVE

Currently provided, maintained, or developed.

### DEPRECATED

Still supported but no longer strategically expanded.

### RETIRED

No longer an active business product.

---

# 10. Domain Events

Examples:

```text
ProductCreated
ProductActivated
ProductDeprecated
ProductRetired

ProductOwnerChanged
```

Events represent changes in Product state.

