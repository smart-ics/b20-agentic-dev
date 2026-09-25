---
Title: ICS Organization Domain
Code: ORG
Artifact: DOMAIN
Version: 2.1
LastUpdated: 2026-09-26
---

# 1. Business Overview

The Organization domain defines the authoritative organizational knowledge required by the ICS Operational System.

The domain identifies:

- who participates in the organization;
- how people are grouped into teams;
- which organizational roles exist;
- which responsibilities are recognized by the organization;
- who currently holds those responsibilities.

The Organization domain exists to provide a stable organizational reference for all operational activities.

The domain answers:

- Who is part of ICS?
- Which teams exist?
- Which organizational roles exist?
- Which responsibilities exist?
- Who is responsible for what?
- What is the current organizational structure?
- What was the historical organizational structure?

The Organization domain does **not** manage:

- Human Resources
- Recruitment
- Payroll
- Attendance
- Leave Management
- Compensation
- Employee Evaluation
- Performance Appraisal
- Request Assignment
- Work Execution
- Operational Workflow

Organization provides authoritative organizational knowledge only.

---

# 2. Ubiquitous Language

| Term | Meaning |
|--------|---------|
| Organization | The company represented by the Operational System. |
| Person | An individual participating in organizational operations. |
| Team | A stable organizational group with a defined purpose. |
| Role | A recognized organizational position. |
| Responsibility | A defined area of accountability recognized by the organization. |
| Membership | The relationship between a Person and a Team. |
| Role Assignment | The relationship between a Person and a Role. |
| Responsibility Assignment | The relationship between a Person or Team and a Responsibility. |
| Active | Currently valid and recognized. |
| Inactive | No longer active but preserved for history. |
| Ended | A relationship that previously existed but is no longer active. |

Responsibility is an organizational accountability concept.

Responsibility is not:

- a task;
- a workflow;
- a work package;
- an operational assignment.

---

# 3. Domain Capabilities

The Organization domain provides the following capabilities:

- Organization Management
- Person Management
- Team Management
- Role Management
- Responsibility Management
- Membership Management
- Role Assignment Management
- Responsibility Assignment Management

These capabilities maintain authoritative organizational knowledge.

They do not define operational workflows.

---

# 4. Actors & Roles

## Actors

### Management

Responsible for defining organizational structure and accountability.

### Administrator

Responsible for maintaining organizational master data.

### Person

Represents an individual participant in the organization.

---

## Organizational Roles

Examples:

```text
Board Director
Chief Operating Officer
Product Owner
Project Manager
Programmer
Trainer
Implementor
```

Roles represent organizational positions.

Roles do not automatically grant ownership of Requests, Work Packages, Products, or Customers.

Such ownership belongs to their respective domains.

---

# 5. Domain Objects

## Organization

Represents the organizational entity managed by the system.

Attributes:

```text
OrganizationId
Name
Status
```

Typically there is one Organization within the system.

---

## Person

Represents an individual participating in organizational activities.

Attributes:

```text
PersonId
Name
Status
```

A Person may:

- belong to multiple Teams;
- hold multiple Roles;
- carry multiple Responsibilities.

The Organization domain does not maintain HR information.

---

## Team

Represents a stable organizational group.

Attributes:

```text
TeamId
Name
Status
```

Examples:

```text
Development
Implementation
Management
```

A Team must represent a real organizational structure.

Temporary collaboration belongs to Work Package.

---

## Role

Represents a recognized organizational position.

Attributes:

```text
RoleId
Name
Status
```

Examples:

```text
Programmer
Trainer
Product Owner
Project Manager
COO
Board Director
```

Roles describe positions, not operational assignments.

---

## Responsibility

Represents a recognized area of accountability.

Attributes:

```text
ResponsibilityId
Name
Description
Status
```

Examples:

```text
Product Ownership
Customer Relationship
Software Development
Customer Implementation
Operational Management
```

Responsibilities define accountability.

They do not define workflow.

---

## Membership

Represents participation of a Person within a Team.

Attributes:

```text
MembershipId
PersonId
TeamId
Status
StartDate
EndDate
```

Membership preserves organizational history.

---

## Role Assignment

Represents assignment of a Role to a Person.

Attributes:

```text
RoleAssignmentId
PersonId
RoleId
Status
StartDate
EndDate
```

A Person may hold multiple Roles simultaneously.

---

## Responsibility Assignment

Represents assignment of Responsibility to a Person or Team.

Attributes:

```text
ResponsibilityAssignmentId
ResponsibilityId
PersonId
TeamId
Status
StartDate
EndDate
```

A Responsibility Assignment identifies current accountability.

It does not define how work is performed.

---

# 6. Aggregates

## Organization Aggregate

```text
Organization
```

The Organization aggregate owns organizational identity.

---

## Person Aggregate

```text
Person
├── Memberships
└── Role Assignments
```

The Person aggregate owns relationships intrinsic to a Person.

---

## Team Aggregate

```text
Team
```

Represents a stable organizational grouping.

---

## Role Aggregate

```text
Role
```

Represents a recognized organizational position.

---

## Responsibility Aggregate

```text
Responsibility
```

Represents a recognized area of accountability.

Responsibility Assignments are relationships to that responsibility.

---

# 7. Business Rules

1. The Organization must have a unique identity.

2. Every Person must have a unique identity.

3. A Person may belong to multiple Teams.

4. A Person may hold multiple Roles.

5. A Team must represent a stable organizational structure.

6. A Team must not be used as a temporary operational grouping.

7. Temporary operational grouping belongs to Work Package.

8. A Role must represent a recognized organizational position.

9. A Responsibility must represent an area of accountability.

10. A Responsibility must not be used as a workflow definition.

11. Organizational relationships must preserve history.

12. Historical relationships must not be silently overwritten.

13. Inactive Persons remain valid historical references.

14. Inactive Teams remain valid historical references.

15. Inactive Roles remain valid historical references.

16. Inactive Responsibilities remain valid historical references.

17. Organization is the authoritative source of:
    - People
    - Teams
    - Roles
    - Responsibilities
    - Organizational Relationships

18. Other domains must reference organizational entities rather than redefining them.

19. Organization does not determine Request ownership.

20. Organization does not determine Work Package ownership.

21. Organization does not determine Product ownership.

22. Organization does not determine Customer ownership.

23. Organization must remain focused on organizational knowledge and must not evolve into an HR system.

---

# 8. State Machines & Lifecycles

## Person Lifecycle

```text
ACTIVE
   ↓
INACTIVE
```

---

## Team Lifecycle

```text
ACTIVE
   ↓
INACTIVE
```

---

## Role Lifecycle

```text
ACTIVE
   ↓
INACTIVE
```

---

## Responsibility Lifecycle

```text
ACTIVE
   ↓
INACTIVE
```

---

## Membership Lifecycle

```text
ACTIVE
   ↓
ENDED
```

Membership history must be preserved.

---

## Role Assignment Lifecycle

```text
ACTIVE
   ↓
ENDED
```

Role Assignment history must be preserved.

---

## Responsibility Assignment Lifecycle

```text
ACTIVE
   ↓
ENDED
```

Responsibility Assignment history must be preserved.

---

# 9. Domain Events

Examples:

```text
PersonCreated
PersonActivated
PersonInactivated

TeamCreated
TeamActivated
TeamInactivated

RoleCreated
RoleActivated
RoleInactivated

ResponsibilityCreated
ResponsibilityActivated
ResponsibilityInactivated

MembershipStarted
MembershipEnded

RoleAssigned
RoleAssignmentEnded

ResponsibilityAssigned
ResponsibilityAssignmentEnded
```

These events describe organizational state changes.

They do not define operational workflow.