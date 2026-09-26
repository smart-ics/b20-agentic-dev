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
* Work Packages
* Requests
* Development workflow
* Roadmaps
* Operational performance

---

# 2. Ubiquitous Language

| Term           | Meaning                                                       |
| -------------- | ------------------------------------------------------------- |
| Product        | A business product provided, maintained, or developed by ICS. |
| Product Owner  | Ownership of the Product.                                     |
| Request Owner  | Ownership of a Request.                                       |
| Work Package Owner | Ownership of a Work Package.                                |
| Organization   | Authoritative source of Person identity.                     |
| Product Status | The authoritative current lifecycle state of a Product.      |

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

A Product is not a Work Package.

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

The Product Domain does not own Customer–Product relationships.

Customer relationships are owned by the Customer Domain.

---

## Product → Work Package

Work Packages may reference Products.

The Product Domain does not own Work Package–Product relationships.

Work Packages are owned by the Work Package Domain.

---

## Product → Request

Requests may reference Products.

The Product Domain does not own Request–Product relationships.

Requests are owned by the Request Domain.

---

# 8. Business Rules

1. Every Product must have a unique identity.

2. Every active Product must have exactly one Product Owner.

3. Product Owner must reference a Person from the Organization Domain.

4. Product ownership does not imply Work Package ownership.

5. Product ownership does not imply Request ownership.

6. Product ownership does not determine organizational responsibility assignments.

7. Product identity must remain stable throughout its lifecycle.

8. A Product may become inactive while preserving historical references.

9. Product Status is the authoritative current lifecycle state of the Product.

No feature, analytics model, or UI may invent an alternative authoritative Product state such as "Product Health" or "Product Condition"; such states must be derived projections.

---

# 9. State Machines & Lifecycles

## Product Lifecycle

```text
ACTIVE
    ↓
INACTIVE
```

### ACTIVE

Currently provided, maintained, or developed.

### INACTIVE

No longer actively provided or maintained, but retained for historical reference.

---

# 10. Domain Events

Examples:

```text
ProductCreated
ProductActivated
ProductDeactivated

ProductOwnerChanged
```

Events represent changes in Product state.

