---

Title: Customer Domain
Code: CUSTOMER
Artifact: DOMAIN
Version: 1.0
LastUpdated: 2026-09-23
---

# 1. Business Overview

The Customer Domain defines the customers served by ICS and the customer representatives who interact with ICS.

The domain provides authoritative customer knowledge used by operational domains.

The domain answers:

* Who is the customer?
* Who represents the customer?
* What is the current relationship status?
* How can ICS identify and contact the customer?

The Customer Domain does not manage Projects, Requests, operational work, performance metrics, or financial transactions.

Those concerns belong to other domains.

---

# 2. Ubiquitous Language

| Term                | Meaning                                                                      |
| ------------------- | ---------------------------------------------------------------------------- |
| Customer            | An organization that receives products or services from ICS.                 |
| Customer Contact    | A person representing a Customer in interactions with ICS.                   |
| Relationship Status | The current operational relationship between ICS and the Customer.           |
| Primary Contact     | The primary representative used for operational communication.               |
| Customer Ownership  | The organizational responsibility for maintaining the customer relationship. |

---

# 3. Domain Capabilities

The Customer Domain provides:

* Customer Management
* Customer Contact Management
* Relationship Status Management
* Customer Ownership Management

The domain exists to maintain authoritative customer information.

The domain does not manage operational execution.

---

# 4. Actors

## Management

Maintains strategic customer information.

## Administrator

Maintains customer master data.

## Customer Representative

Represents the customer organization during operational interactions.

---

# 5. Domain Objects

## Customer

Represents an organization served by ICS.

Attributes:

```text
CustomerId
Name
ShortName
Status
RelationshipStatus
```

Examples:

```text
RSUD Kota A
RSUD Kota B
RS Swasta C
```

A Customer represents an organization.

A Customer is not a project.

A Customer is not a request.

---

## Customer Contact

Represents an individual acting on behalf of a Customer.

Attributes:

```text
ContactId
CustomerId
Name
Position
Phone
Email
Status
```

A Customer may have multiple Contacts.

---

## Customer Ownership

Represents the organizational responsibility for maintaining the customer relationship.

Attributes:

```text
CustomerId
OwnerPersonId
StartDate
EndDate
Status
```

Customer ownership identifies who within ICS is responsible for maintaining the customer relationship.

Ownership does not imply project ownership.

Ownership does not imply request ownership.

---

# 6. Aggregates

## Customer Aggregate

```text
Customer
├── Contacts
└── Ownerships
```

The Customer Aggregate is the authoritative source of customer identity and customer relationship information.

Customer Contacts and Customer Ownerships cannot exist without a Customer.

---

# 7. Relationships

## Customer → Organization

A Customer is served by the Organization.

The Customer Domain references the Organization Domain but does not own organizational structures.

---

## Customer → Person

Customer Ownership references a Person defined in the Organization Domain.

The Customer Domain does not define people belonging to ICS.

---

## Customer → Project

Projects may reference a Customer.

The Customer Domain does not manage Projects.

---

## Customer → Request

Requests may reference a Customer.

The Customer Domain does not manage Requests.

---

# 8. Business Rules

1. Every Customer must have a unique identity.

2. A Customer represents an organization, not an individual person.

3. A Customer may have multiple Contacts.

4. A Customer Contact must belong to exactly one Customer.

5. A Customer may have zero or more Ownership records.

6. Historical ownership changes must be preserved.

7. Customer ownership identifies relationship responsibility only.

8. Customer ownership does not automatically grant project ownership.

9. Customer ownership does not automatically grant request ownership.

10. A Customer may become inactive while preserving historical references.

11. Projects and Requests must reference existing Customers when customer context is required.

---

# 9. State Machines & Lifecycles

## Customer Lifecycle

```text
ACTIVE
   ↓
INACTIVE
```

Inactive customers remain valid historical references.

---

## Customer Contact Lifecycle

```text
ACTIVE
   ↓
INACTIVE
```

Inactive contacts remain valid historical references.

---

## Customer Ownership Lifecycle

```text
ACTIVE
   ↓
ENDED
```

Ownership records represent historical responsibility and must not be deleted when ownership changes.

---

# 10. Relationship Status

Relationship Status represents the operational relationship between ICS and the Customer.

```text
PROSPECT
    ↓
ACTIVE
    ↓
INACTIVE
```

Definitions:

```text
PROSPECT
Customer is known but has not yet become an active customer.

ACTIVE
Customer currently maintains an active relationship with ICS.

INACTIVE
Customer relationship is no longer active.
```

Relationship Status is independent of Customer Lifecycle.

---

# 11. Domain Events

Examples:

```text
CustomerCreated
CustomerActivated
CustomerInactivated

CustomerContactAdded
CustomerContactActivated
CustomerContactInactivated

CustomerOwnershipStarted
CustomerOwnershipEnded

RelationshipStatusChanged
```

Events represent changes in customer state.

Events do not define operational workflows.

---

# 12. Related Features

The Customer Domain may participate in:

* Manage Customer
* Manage Customer Contact
* Manage Customer Ownership

The domain supplies customer information to Projects and Requests.

The domain does not execute operational work.
