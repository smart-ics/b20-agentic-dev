---

Title: ICS Organization Domain
Code: ORG
Artifact: DOMAIN
Version: 1.0
LastUpdated: 2026-09-23
---

# 1. Business Overview

The Organization domain defines the organizational structure of ICS required to identify people, teams, roles, and responsibilities.

The domain provides authoritative organizational knowledge used by other operational domains.

The domain answers:

* Who belongs to ICS?
* What organizational unit does a person belong to?
* What role does a person hold?
* What responsibilities are assigned to a person or organizational unit?

The Organization domain does not define how work is performed, how requests are assigned, or how management monitors operations.

---

# 2. Ubiquitous Language

| Term                | Meaning                                                                                                            |
| ------------------- | ------------------------------------------------------------------------------------------------------------------ |
| Organization        | The company structure represented by the Operational System.                                                       |
| Person              | An individual who participates in the organization.                                                                |
| Team                | A group of people organized around a shared responsibility or area of work.                                        |
| Role                | A defined responsibility or position that a person may hold within the organization.                               |
| Responsibility      | An area of accountability assigned to a person, team, or role.                                                     |
| Organizational Unit | A structural grouping within the organization. A Team is the primary practical organizational unit in this domain. |
| Membership          | The relationship between a Person and an organizational unit.                                                      |
| Assignment          | The relationship between a Person and a Role or Responsibility.                                                    |

---

# 3. Domain Capabilities

The Organization domain provides these responsibilities:

* Organization Structure Management
* Person Management
* Team Management
* Role Management
* Responsibility Management
* Organizational Membership Management

These capabilities define the knowledge maintained by the domain.

They do not define operational workflows such as assigning a Request, managing a Project, or evaluating Performance.

---

# 4. Actors & Roles

## Actors

### Management

Maintains or approves organizational structure and responsibilities.

### Administrator

Maintains organizational master data.

### Person

Represents an individual participating in the organization.

## Roles

Roles are organizational responsibilities represented in the domain.

Examples:

```text
Board Director
Chief Operating Officer
Project Manager
Programmer
Trainer
Implementor
```

A Person may hold one or more Roles.

A Role does not imply a workflow or a specific operational task.

---

# 5. Domain Objects

## Organization

Represents the company represented by the system.

Attributes:

```text
OrganizationId
Name
Status
```

ICS normally has one primary Organization in the Operational System.

---

## Person

Represents an individual participating in ICS.

Attributes:

```text
PersonId
Name
Status
```

A Person may belong to one or more Teams and may hold one or more Roles.

---

## Team

Represents an organizational group with a defined area of responsibility.

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

Team structure must represent actual organizational responsibility rather than arbitrary grouping.

---

## Role

Represents a recognized organizational role.

Attributes:

```text
RoleId
Name
Status
```

Examples:

```text
Programmer
Project Manager
Trainer
COO
Board Director
```

---

## Responsibility

Represents an area of organizational accountability.

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
Project Management
Software Development
Customer Implementation
Operational Management
```

Responsibility describes accountability.

It does not describe a procedure.

---

## Membership

Represents a Person's membership in a Team.

Attributes:

```text
PersonId
TeamId
Status
StartDate
EndDate
```

---

## Role Assignment

Represents a Person's assignment to a Role.

Attributes:

```text
PersonId
RoleId
Status
StartDate
EndDate
```

---

# 6. Aggregates

## Organization Aggregate

The Organization aggregate represents the authoritative organizational identity.

```text
Organization
```

The Organization aggregate owns organizational existence and identity.

---

## Person Aggregate

The Person aggregate represents an individual and the organizational relationships that are intrinsic to that person.

```text
Person
├── Memberships
└── Role Assignments
```

Team and Role are referenced organizational concepts and are not embedded as copies inside Person.

---

## Team Aggregate

```text
Team
```

Represents an independently managed organizational group.

---

## Role Aggregate

```text
Role
```

Represents an independently recognized organizational role.

---

## Responsibility Aggregate

```text
Responsibility
```

Represents an independently recognized area of accountability.

---

# 7. Business Rules

1. Every Person must have a unique identity within the Organization.

2. A Person may belong to one or more Teams when organizationally required.

3. A Person may hold one or more Roles when organizationally required.

4. A Role must represent an actual organizational responsibility or position.

5. A Team must represent an actual organizational grouping with a meaningful responsibility.

6. A Responsibility must represent an area of accountability and must not be used as a workflow definition.

7. Organizational relationships must have an explicit status.

8. Historical membership and role assignments must not be silently overwritten when the relationship changes.

9. A Person may become inactive without being removed from historical organizational records.

10. Removing a Person from an active organizational relationship does not delete historical operational ownership references.

11. Organization data is authoritative for organizational identity and responsibility. Other domains reference it rather than redefining the same person, team, or role independently.

---

# 8. State Machines & Lifecycles

## Person Lifecycle

```text
ACTIVE
   ↓
INACTIVE
```

A Person becomes `INACTIVE` when they are no longer an active participant in the Organization.

Historical references to the Person remain valid.

---

## Team Lifecycle

```text
ACTIVE
   ↓
INACTIVE
```

A Team may become inactive when it is no longer an active organizational unit.

Historical references remain valid.

---

## Role Lifecycle

```text
ACTIVE
   ↓
INACTIVE
```

A Role may become inactive when it is no longer used by the Organization.

Historical assignments remain valid.

---

## Responsibility Lifecycle

```text
ACTIVE
   ↓
INACTIVE
```

A Responsibility may become inactive when the Organization no longer recognizes it as an active area of accountability.

---

## Membership Lifecycle

```text
ACTIVE
   ↓
ENDED
```

Membership represents a relationship, not an organizational entity.

---

## Role Assignment Lifecycle

```text
ACTIVE
   ↓
ENDED
```

Role Assignment represents a relationship between Person and Role.

---

# 9. Domain Events

The Organization domain may produce events representing significant changes to organizational knowledge.

Examples:

```text
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

TeamMembershipStarted
TeamMembershipEnded

RoleAssignmentStarted
RoleAssignmentEnded
```

Events represent changes in organizational state.

They do not define downstream workflows or operational processes.

---

# 10. Related Features

The Organization domain may participate in Features that require organizational knowledge, including:

* Manage Organization
* Manage People
* Manage Teams
* Manage Roles
* Manage Responsibilities

These references identify consuming Features only.

Operational workflows, request assignment, project execution, performance analysis, and management reporting are not defined here.
