# Feature Review Skill

## Purpose

Review discovered Features and verify that they are:

* Correctly derived from approved artifacts.
* Properly sized.
* Fully traceable.
* Implementation-independent.
* Free from duplication.
* Ready for implementation planning.

This skill does not create Features.

This skill only validates and reviews existing Feature artifacts.

---

# Inputs

Read:

```text
operational/features/
```

Including:

```text
feature-catalog.md
feature-traceability.md
feature-coverage-validation.md
FEAT-*.md
```

And all upstream approved artifacts:

```text
operational/domains/
operational/actors/
operational/scenarios/
operational/use-cases/
operational/user-journey/
operational/navigation/
operational/ui-layout/
```

---

# Review Objective

Verify that every Feature is a legitimate implementation-ready system capability derived from approved operational design artifacts.

The review must follow:

```text
Domain
    ↓
Scenario
    ↓
Use Case
    ↓
User Journey
    ↓
Navigation
    ↓
UI Layout
    ↓
Feature
```

Features are downstream artifacts.

They must never become a source of business requirements.

---

# Review Principles

## Principle 1

Features must originate from approved Use Cases.

Every Feature must trace to at least one approved Use Case.

If no Use Case exists:

```text
INVALID FEATURE
```

---

## Principle 2

Features must support approved User Journeys.

Every Feature must enable at least one User Journey step.

If no Journey support exists:

```text
INVALID FEATURE
```

---

## Principle 3

Features must support approved UI interactions.

Every Feature must be observable through at least one approved Screen or interaction.

If no UI interaction exists:

```text
SUSPICIOUS FEATURE
```

Investigate whether:

* UI is missing
* Feature is unnecessary
* Feature should be implementation-only

---

## Principle 4

Features must remain implementation-independent.

Reject Features containing:

```text
API
Endpoint
Controller
Repository
Database Table
SQL
Stored Procedure
Service
Handler
Event Bus
Kafka
Redis
Cache
```

These belong to implementation.

---

## Principle 5

Features are not Screens.

Reject Features such as:

```text
Request Detail Screen
Dashboard
Customer Page
Product Page
```

Screens contain Features.

Screens are not Features.

---

## Principle 6

Features are not Domains.

Reject Features such as:

```text
Request Management
Customer Management
Organization Management
Product Management
```

These are capability collections.

Not independently implementable Features.

---

# Review Areas

---

# Review 1

# Traceability Validation

Verify every Feature contains:

```text
Domain
Scenario
Use Case
User Journey
Screen
```

Required review matrix:

| Feature | Domain | Scenario | Use Case | Journey | Screen |
| ------- | ------ | -------- | -------- | ------- | ------ |

Flag:

```text
MISSING TRACEABILITY
```

when any link is absent.

---

# Review 2

# Coverage Validation

Verify:

```text
Every Use Case
    →
At least one Feature
```

Verify:

```text
Every User Journey
    →
At least one Feature
```

Verify:

```text
Every Screen Interaction
    →
At least one Feature
```

Flag:

```text
UNCOVERED USE CASE
UNCOVERED JOURNEY
UNCOVERED INTERACTION
```

---

# Review 3

# Duplication Analysis

Identify Features that describe the same capability.

Examples:

```text
Assign Request Owner
Change Request Owner
Update Request Owner
```

May represent the same capability.

Recommend:

```text
MERGE CANDIDATE
```

Provide reasoning.

---

# Review 4

# Feature Size Validation

Classify Feature size.

---

## Too Large

Exampl
