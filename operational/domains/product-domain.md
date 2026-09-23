---

Title: Product Domain
Code: PRODUCT
Artifact: DOMAIN
Version: 1.0
LastUpdated: 2026-09-23
---

# 1. Business Overview

The Product Domain defines the products provided, maintained, and developed by ICS.

The domain provides authoritative product knowledge used by other operational domains.

The domain answers:

* What products does ICS provide?
* What is the current status of a product?
* Who owns the product?
* How are products classified?

The Product Domain does not manage Projects, Requests, Customers, roadmaps, development workflows, or operational performance.

---

# 2. Ubiquitous Language

| Term             | Meaning                                              |
| ---------------- | ---------------------------------------------------- |
| Product          | A product provided, maintained, or developed by ICS. |
| Product Family   | A grouping of related Products.                      |
| Product Owner    | The Person currently responsible for a Product.      |
| Product Status   | The current lifecycle state of a Product.            |
| Product Category | A classification of Product.                         |

---

# 3. Domain Capabilities

* Product Management
* Product Family Management
* Product Ownership Management
* Product Classification Management
* Product Lifecycle Management

---

# 4. Actors

## Management

Approves Product creation, retirement, ownership, and classification.

## Product Owner

Responsible for a Product.

## Administrator

Maintains Product master data.

---

# 5. Domain Objects

## Product

Represents a business product.

Attributes:

```text
ProductId
Name
Code
Description
Status
Category
ProductFamilyId
OwnerPersonId
```

Examples:

```text
MyHospital
PenaEl
BTrade3
Jetset
```

A Product is not a Project.

A Product is not a Request.

A Product is not a Customer.

---

## Product Family

Represents a grouping of related Products.

Attributes:

```text
ProductFamilyId
Name
Description
Status
```

Examples:

```text
Hospital Solutions
Business Solutions
Internal Solutions
```

---

# 6. Aggregates

## Product Aggregate

```text
Product
```

The Product Aggregate is the authoritative source of Product identity, ownership, classification, and lifecycle state.

---

## Product Family Aggregate

```text
ProductFamily
```

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

4. A Product may belong to zero or one Product Family.

5. A Product Family may contain multiple Products.

6. A Product may exist without a Product Family.

7. A Product may become inactive while preserving historical references.

8. Product ownership does not imply Project ownership.

9. Product ownership does not imply Request ownership.

10. Product identity must remain stable throughout its lifecycle.

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

Still exists but no longer strategically expanded.

### RETIRED

No longer an active business product.

---

## Product Family Lifecycle

```text
ACTIVE
   ↓
INACTIVE
```

---

# 10. Domain Events

Examples:

```text
ProductCreated
ProductActivated
ProductDeprecated
ProductRetired

ProductOwnerChanged

ProductFamilyCreated
ProductFamilyActivated
ProductFamilyInactivated

ProductAssignedToFamily
ProductRemovedFromFamily
```

Events represent changes in Product state.

---

# 11. Related Features

* Manage Products
* Manage Product Families
* Manage Product Ownership

The Product Domain supplies Product information to Projects and Requests.
