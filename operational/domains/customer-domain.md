---
Title: Customer Domain
Code: CUSTOMER
Artifact: DOMAIN
Version: 2.0
LastUpdated: 2026-09-26
---

# 1. Business Overview

The Customer Domain manages the organizations served by ICS.

It provides the authoritative current state of each customer organization and the contact information required to support ongoing operational activities.

The Customer Domain exists to answer:

* Who is the customer?
* Who represents the customer?
* Is the customer currently active?
* Does the customer currently have an active maintenance contract?

The Customer Domain is not responsible for:

* Product management
* Request management
* Work Package management
* Operational communication
* Operational analytics
* Operational performance

---

# 2. Ubiquitous Language

| Term             | Meaning |
| ---------------- | ------- |
| Customer         | An organization that receives products or services from ICS. |
| Customer Contact | A person representing a Customer organization. |
| Active           | Currently valid and operational. |
| Inactive         | No longer active but preserved for historical reference. |

---

# 3. Domain Capabilities

The Customer Domain provides:

* Customer Management
* Customer Contact Management

These capabilities maintain authoritative customer master data.

---

# 4. Actors & Roles

## Administrator

Responsible for maintaining Customer master data.

---

# 5. Domain Objects

## Customer

Represents an organization served by ICS.

Attributes:

```text
CustomerId
CustomerCode
CustomerName
Status
HasActiveMaintenanceContract
CreatedAt
UpdatedAt
```

---

## Customer Contact

Represents a Customer representative.

Attributes:

```text
ContactId
CustomerId
Name
Position
PhoneNumber
Email
Status
CreatedAt
UpdatedAt
```

---

# 6. Aggregates

## Customer Aggregate

```text
Customer
└── Customer Contacts
```

The Customer Aggregate is the authoritative source of Customer information.

Customer Contacts cannot exist without a Customer.

---

# 7. Business Rules

1. A Customer represents an organization, not an individual person.

2. Every Customer must have a unique identity.

3. A Customer Contact must belong to exactly one Customer.

4. Customer identity must remain stable throughout its lifecycle.

5. A Customer may become inactive while preserving all historical references.

6. A Customer must not be physically removed when historical operational objects reference it.

7. Customer Status must be either ACTIVE or INACTIVE.

8. Customer Contact Status must be either ACTIVE or INACTIVE.

9. HasActiveMaintenanceContract reflects the current authoritative state only.

10. The Customer Domain does not manage Requests, Work Packages, Products, Posts, Analytics, or Performance.

11. Other domains may reference Customers but remain authoritative for their own state.

---

# 8. State Machines & Lifecycles

## Customer Lifecycle

```text
ACTIVE
    ↓
INACTIVE
```

An inactive Customer remains available for historical reference.

---

## Customer Contact Lifecycle

```text
ACTIVE
    ↓
INACTIVE
```

An inactive Contact remains available for historical reference.

---

# 9. Domain Events

```text
CustomerCreated
CustomerActivated
CustomerInactivated

CustomerContactAdded
CustomerContactActivated
CustomerContactInactivated
```

These events describe changes in Customer state.