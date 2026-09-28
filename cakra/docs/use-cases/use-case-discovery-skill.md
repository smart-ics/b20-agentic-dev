# Purpose

Discover and document system use-cases from existing operational knowledge.

The objective is not to invent features, screens, menus, or implementation details.

The objective is to identify what actors must be able to accomplish through the system in order to execute operational scenarios.

---

# Required Inputs

## Operational Scenarios

Source of business behavior.

Operational scenarios describe what happens in reality.

They are the primary source for use-case discovery.

---

## Actor Model

Source of authority and responsibility.

Defines:

* Actor
* Responsibility
* Authority
* Operational role

Actors initiate, participate in, approve, review, or receive outcomes from use-cases.

---

## Domain Model

Source of business objects.

Defines:

* Domain boundaries
* Domain ownership
* Domain capabilities
* Domain state

Domains help identify which business objects are affected by a use-case.

Domains do not create use-cases.

---

# Discovery Principle

Use-cases are discovered from:

```text
Scenario
    +
Actor Goal
    +
System Interaction
```

A use-case exists only if an actor must interact with the system to achieve a goal.

---

# Discovery Algorithm

For every operational scenario:

## Step 1

Identify participating actors.

Example:

```text
Trainer
Product Owner
COO
Board
```

---

## Step 2

Identify actor goals.

Ask:

```text
What is the actor trying to achieve?
```

Examples:

```text
Report request
Assign authority
Assess request
Make decision
Escalate issue
Review progress
```

---

## Step 3

Identify required system interaction.

Ask:

```text
What must the system allow the actor to do?
```

Examples:

```text
Record request
Assign owner
Submit assessment
Escalate authority
Publish update
Review summary
```

---

## Step 4

Create candidate use-case.

Format:

```text
Actor + Goal
```

Examples:

```text
Report Request
Assign Request Owner
Assess Request
Review Customer Health
Publish Operational Update
```

---

## Step 5

Merge duplicates.

If multiple scenarios produce the same actor goal:

```text
Approve Request
Approve Request
Approve Request
```

Create one authoritative use-case.

---

## Step 6

Validate.

A valid use-case must:

### Have an actor

```text
Trainer
PO
COO
```

### Have a goal

```text
Assign Owner
Make Decision
Review Status
```

### Require system interaction

If the goal can be completed without interacting with the system:

```text
Attend Meeting
Visit Customer
```

it is not a use-case.

---

# Discovery Rules

## Rule 1

Discover use-cases from scenarios.

Never discover use-cases directly from domains.

Incorrect:

```text
Request Domain
    →
Create Request
Edit Request
Delete Request
```

---

## Rule 2

Use actor goals.

Prefer:

```text
Assign Request Owner
```

instead of:

```text
Update Request Record
```

---

## Rule 3

Avoid UI concepts.

Never generate:

```text
Request Dashboard
Request Form
Request Page
Request Table
```

These are UI artifacts.

Not use-cases.

---

## Rule 4

Avoid implementation concepts.

Never generate:

```text
Generate API
Create Endpoint
Save Record
```

These are implementation details.

Not use-cases.

---

## Rule 5

One use-case represents one meaningful operational outcome.

Good:

```text
Escalate Request
```

Bad:

```text
Click Escalate Button
```

---

# Output Artifact

Create:

```text
operational/use-cases/
```

One file per use-case.

---

# Use-Case Template

```text
# Use Case

## Goal

Business outcome achieved.

## Primary Actor

Actor initiating the use-case.

## Supporting Actors

Optional participants.

## Trigger

Event starting the use-case.

## Preconditions

Required state before execution.

## Main Flow

Normal interaction flow.

## Alternative Flows

Optional variations.

## Exception Flows

Failure situations.

## Success Result

State after completion.

## Related Domains

Affected domains.

## Related Scenarios

Originating scenarios.
```

---

# Expected Result

The resulting use-case catalog must represent all meaningful actor goals required to execute operational scenarios.

The catalog becomes the authoritative input for:

```text
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

Use-cases are therefore the bridge between operational reality and system design.
