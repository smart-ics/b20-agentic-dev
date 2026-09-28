# User Journey Generation Skill

## Purpose

Generate User Journey artifacts from existing Domain, Actor, Operational Scenario, and Use Case artifacts.

The goal is to describe how an actor achieves a specific use case from the actor's perspective, providing a bridge between business requirements and UI design.

---

## Inputs

Required:

* Domain Definitions
* Actor / Role Definitions
* Operational Scenarios
* Use Cases

Optional:

* Business Rules
* Existing Navigation
* Existing UI Concepts

---

## Output

One User Journey artifact per Use Case.

Example:

```text
UJ-REQ-001 Submit Request
UJ-REQ-002 Assign Owner
UJ-REQ-003 Assess Request
UJ-REQ-004 Resolve Request
```

---

## Journey Structure

Each User Journey must contain:

### Actor

The actor performing the journey.

### Goal

The outcome the actor wants to achieve.

### Trigger

The event that starts the journey.

### Main Journey

The normal successful flow from trigger to outcome.

### Alternative Paths

Meaningful variations or exceptions that may occur during the journey.

### Success Outcome

The resulting state when the journey completes successfully.

### Information Needed

Information required by the actor during the journey.

---

## Generation Rules

### Rule 1 — User Perspective

Describe actions from the actor's perspective.

Correct:

```text
Trainer submits request.
Product Owner assesses request.
COO reviews decision.
```

Avoid:

```text
System inserts database record.
Workflow status changes to Assigned.
```

---

### Rule 2 — Focus on User Actions

Describe what the actor does and experiences.

Include:

* Navigation intent
* User decisions
* Information consumption
* Information entry

Avoid implementation details.

---

### Rule 3 — One Journey per Use Case

Generate a separate User Journey for each Use Case.

Do not merge multiple Use Cases into a single journey.

---

### Rule 4 — Technology Agnostic

Do not describe:

* Database operations
* APIs
* Internal workflows
* Technical implementation

---

### Rule 5 — Outcome Driven

Every journey must start with a trigger and end with a clear success outcome.

---

## Exclusions

User Journeys must not contain:

* Domain design
* Database design
* Permission matrices
* Validation rules
* UI layouts
* Navigation structures
* Feature specifications
* Technical implementation details

---

## Relationship to Other Artifacts

```text
Domain
    ↓
Actor / Role
    ↓
Operational Scenario
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
    ↓
Implementation
```

User Journey is the transition point between operational design and user experience design.
