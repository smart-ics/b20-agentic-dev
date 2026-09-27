A good Navigation Skill should be extremely strict.

The biggest risk is that the agent starts inventing screens, concepts, workflows, or features that never existed in previous artifacts.

I would define Navigation as a **transformation artifact**, not a discovery artifact.

---

# Navigation Creation Skill

## Purpose

Transform approved User Journeys into a complete application navigation structure and screen inventory.

Navigation exists to expose existing business capabilities to users.

Navigation MUST NOT introduce new business concepts, business rules, workflows, features, domains, actors, or use-cases.

Navigation is a structural artifact, not a discovery artifact.

---

# Inputs

The skill requires the following approved artifacts:

```text
Domain
Actor / Role
Operational Scenario
Use Case
User Journey
```

All inputs are authoritative.

Navigation is derived from these artifacts.

---

# Output

The skill produces:

```text
navigation-map.md
screen-inventory.md
```

Optional:

```text
request-navigation.md
collaboration-navigation.md
management-navigation.md
```

---

# Responsibilities

The skill must:

1. Identify destinations required by User Journeys.
2. Group related destinations into coherent navigation areas.
3. Define navigation hierarchy.
4. Define screen inventory.
5. Define screen purpose.
6. Define primary actors for each screen.
7. Define navigation entry points.

The skill must NOT:

1. Invent new domains.
2. Invent new actors.
3. Invent new use-cases.
4. Invent new workflows.
5. Invent new business capabilities.
6. Invent new features.
7. Invent new operational concepts.

---

# Navigation Discovery Rules

## Rule 1 — User Journey Is Primary

Navigation is derived primarily from User Journeys.

The skill must analyze:

```text
Where users start
Where users go
What information they need
What actions they perform
```

Every screen must support at least one User Journey.

---

## Rule 2 — Navigation Must Be Traceable

Every screen must be traceable to:

```text
User Journey
→ Use Case
→ Operational Scenario
```

If no traceability exists:

```text
DO NOT CREATE THE SCREEN
```

---

## Rule 3 — No Orphan Screens

Every screen must support at least one use-case.

If a screen supports no use-case:

```text
REMOVE IT
```

---

## Rule 4 — No Duplicate Screens

If multiple User Journeys require the same destination:

```text
Reuse the screen
```

Do not create duplicate screens.

---

## Rule 5 — Navigation Is Not UI

Navigation defines:

```text
Areas
Pages
Workspaces
Destinations
Hierarchy
```

Navigation does not define:

```text
Layout
Components
Buttons
Colors
Cards
Tables
Forms
```

Those belong to UI Layout.

---

# Screen Identification Process

For every User Journey:

```text
Identify:
    Information needed

Identify:
    Actions performed

Identify:
    Destination required
```

Convert destinations into candidate screens.

Merge duplicates.

Produce final screen inventory.

---

# Navigation Map Structure

The skill must generate a hierarchical navigation tree.

Example:

```text
Home
│
├── Requests
│   ├── Request List
│   ├── Request Detail
│   └── Create Request
│
├── Work Packages
│   ├── List
│   └── Detail
│
├── Customers
│   ├── List
│   └── Detail
│
└── Feed
```

Structure must be based entirely on approved artifacts.

---

# Screen Inventory Structure

Each screen must contain:

```text
Screen ID

Screen Name

Purpose

Primary Actors

Supported Use Cases

Supported User Journeys

Entry Points

Primary Actions
```

Example:

```text
SCR-003

Request Detail

Purpose:
View and manage a request.

Primary Actors:
Trainer
Product Owner
COO

Supported Use Cases:
Request Resolution

Supported Journeys:
Request Lifecycle

Entry Points:
Request List
Customer Detail

Primary Actions:
Assess
Decide
Escalate
Close
```

---

# Validation Checklist

Before completing navigation:

Verify:

✓ Every screen maps to a User Journey

✓ Every screen maps to a Use Case

✓ Every screen maps to an Operational Scenario

✓ No new business concepts introduced

✓ No duplicate screens exist

✓ No orphan screens exist

✓ Navigation contains only structural information

✓ UI decisions are absent

---

# Completion Criteria

The Navigation artifact is complete when:

1. All User Journeys can be executed through the navigation structure.
2. Every screen is traceable to approved artifacts.
3. No navigation element introduces new business concepts.
4. A UI designer can begin UI Layout design using only the navigation outputs.

```
```

This skill fits your methodology because it keeps the flow deterministic:

```text
Domain
    ↓
Actor
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

and prevents Navigation from becoming a second round of feature discovery.
