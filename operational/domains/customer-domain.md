# Customer Domain

## Business Overview

### Purpose

The Customer Domain manages the organizations served by ICS.

It provides the authoritative current state of each customer organization, its operational relationship with ICS, and the contact information required to support ongoing operational activities.

The Customer Domain exists to answer:

```text
Who is the customer?
Who represents the customer?
Who is responsible for the customer within ICS?
What is the current relationship status?
```

---

### Scope

The Customer Domain is responsible for:

* Customer identity
* Customer profile
* Customer contacts
* Customer relationship status
* Customer ownership

The Customer Domain is not responsible for:

* Product management
* Request management
* Work Package management
* Operational communication
* Operational analytics
* Operational performance

---

## Ubiquitous Language

### Customer

An organization that receives products or services from ICS.

Examples:

```text
RSUP Dr. Sardjito
RSUD Wonosari
RSUD Sleman
```

---

### Customer Contact

A person representing a Customer organization.

Examples:

```text
Hospital Director
IT Manager
Head of Medical Records
Billing Coordinator
```

---

### Customer Owner

The ICS person currently responsible for maintaining the operational relationship with a Customer.

A Customer has at most one active Customer Owner.

---

### Relationship Status

The current operational relationship between ICS and a Customer.

Examples:

```text
Prospect
Active
Inactive
```

---

## Domain Capabilities

### Customer Management

Maintain Customer identity and profile information.

---

### Customer Contact Management

Maintain Customer representatives and contact information.

---

### Relationship Management

Maintain the current operational relationship status between ICS and a Customer.

---

### Customer Ownership Management

Maintain the current ICS owner responsible for a Customer relationship.

---

## Actors & Roles

### Customer Owner

Responsible for maintaining the operational relationship with a Customer.

---

### Customer Representative

Represents the Customer organization when communicating with ICS.

---

### Management

Reviews Customer information and relationship status.

---

## Domain Objects

### Customer

Represents an organization served by ICS.

#### Attributes

```text
CustomerId
CustomerCode
CustomerName
RelationshipStatus
OwnerPersonId
Status
CreatedAt
UpdatedAt
```

---

### Customer Contact

Represents a Customer representative.

#### Attributes

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

## Aggregate Structure

```text
Customer
└── Customer Contacts
```

The Customer Aggregate is the authoritative source of Customer information.

Customer Contacts cannot exist without a Customer.

---

## Relationships

### Customer → Organization

A Customer Owner references a Person managed by the Organization Domain.

The Organization Domain remains authoritative for People and organizational structure.

---

### Customer → Request

Requests may reference a Customer.

The Request Domain remains authoritative for Requests and their lifecycle.

---

### Customer → Work Package

Work Packages may reference a Customer.

The Work Package Domain remains authoritative for Work Packages and their lifecycle.

---

### Customer → Product

Products may be used by Customers.

The Product Domain remains authoritative for Products and product ownership.

---

### Customer → Post

Posts may reference a Customer.

The Post Domain remains authoritative for Posts, comments, reactions, visibility, and discussion history.

---

## Business Rules

### Customer Identity

A Customer represents an organization, not an individual person.

---

### Contact Ownership

A Customer Contact must belong to exactly one Customer.

---

### Customer Ownership

A Customer may have at most one active Customer Owner.

The Customer Owner is responsible for the operational relationship with the Customer.

Customer ownership does not imply ownership of Requests.

Customer ownership does not imply ownership of Work Packages.

Customer ownership does not imply ownership of Products.

---

### Relationship Status

A Customer must have exactly one Relationship Status.

---

### Historical Preservation

A Customer may become inactive while preserving all historical references.

A Customer must not be physically removed when historical operational objects reference it.

---

## Lifecycle

### Customer Lifecycle

```text
ACTIVE
    ↓
INACTIVE
```

An inactive Customer remains available for historical reference.

---

### Customer Contact Lifecycle

```text
ACTIVE
    ↓
INACTIVE
```

An inactive Contact remains available for historical reference.

---

## Domain Events

### Customer Events

```text
CustomerCreated
CustomerUpdated
CustomerActivated
CustomerInactivated
RelationshipStatusChanged
CustomerOwnerChanged
```

---

### Customer Contact Events

```text
CustomerContactAdded
CustomerContactUpdated
CustomerContactActivated
CustomerContactInactivated
```

---

## Authority

The Customer Domain is the authoritative source for:

```text
Customer identity
Customer profile
Customer contacts
Customer relationship status
Customer ownership
```

The Customer Domain is not authoritative for:

```text
People
Products
Requests
Work Packages
Posts
Operational analytics
Operational performance
```

