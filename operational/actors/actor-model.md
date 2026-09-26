---
Title: ICS Actor Model
Artifact: ACTOR_MODEL
Version: 1.0
---
## Purpose

Defines the operational actors, responsibilities, and assignment model used by the ICS Operational System.

This artifact exists to ensure consistent interpretation across:

* Domain Design
* Scenario Design
* Use Case Design
* Navigation Design
* UI Design
* Feature Design
* Implementation

---

# Core Principle

The Operational System distinguishes between:

1. Organizational Role
2. Responsibility
3. Operational Assignment

These concepts must not be treated as equivalent.

---

# Organizational Role

Organizational Role describes who a person is within the organization.

Examples:

```text
Programmer
Implementator
Management
```

A Person may hold multiple Organizational Roles.

Organizational Roles do not determine ownership of operational work.

---

# Responsibility

Responsibility defines operational accountability for an area.

Responsibilities define who is accountable for operational results.

Examples:

```text
Module PIC
Customer Pimpro
```

A Responsibility:

* defines accountability;
* does not define current work assignment;
* does not require performing all work personally.

A Person may hold multiple Responsibilities.

Examples:

```text
Programmer A
    → PIC of Pharmacy Module

Implementator B
    → Pimpro of Customer XYZ
```

---

# Operational Assignment

Operational Assignment defines ownership of a specific operational object.

Examples:

```text
Request Owner
Work Package Owner
```

Operational Assignment:

* defines who is currently responsible for executing or managing a specific operational object;
* is workload-oriented;
* exists to execute work.

The assignment may change over time and must preserve relevant history.

Operational Assignment does not imply accountability for an entire operational area.

Examples:

```text
Request #123
    Owner = Programmer B

Request #124
    Owner = Implementator C
```

Assignments may be made regardless of PIC or Pimpro responsibility.

---

# Accountability Model

Module performance is accountable to the Module PIC.

Customer performance is accountable to the Customer Pimpro.

Request performance is accountable to the Request Owner.

Work Package performance is accountable to the Work Package Owner.

Accountability and assignment are intentionally separated.

---

# Assignment Rules

1. A Request may be assigned to any Person considered appropriate for the work.

2. The assignment is independent of Module PIC and Customer Pimpro responsibility.

3. A Request Owner does not need to be the Module PIC.

4. A Request Owner does not need to be the Customer Pimpro.

5. A Module PIC remains accountable for Module operational performance regardless of Request assignment.

6. A Customer Pimpro remains accountable for Customer operational performance regardless of Request assignment.

7. Operational assignment may change without changing accountability.

---

# Design Implications

Design decisions throughout the Operational System must respect:

```text
Role
    ≠ Responsibility
    ≠ Assignment
```

The system must never assume:

```text
Module PIC
    = Request Owner

Customer Pimpro
    = Request Owner
```

These relationships are independent.

---

# Actor Discovery Rule

The Actor Model defines broad organizational actors.

Additional actors must not be created unless they represent a materially different operational perspective.

Operational scenarios are the primary mechanism for discovering whether a new actor is required.

---