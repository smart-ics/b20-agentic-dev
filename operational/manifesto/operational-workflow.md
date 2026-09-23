# Operational Workflow

## Purpose

The Operational Workflow defines how operational work flows through the organization.

It exists to:

* assign ownership;
* resolve operational matters;
* escalate when necessary;
* surface exceptions requiring attention;
* support operational decisions.

The workflow must remain simple enough to support daily operations without creating unnecessary administrative work.

---

# Domains and Workflow

The Operational System has exactly five operational domains:

```text
Organization
Customer
Product
Work Package
Request
```

Operational workflow operates on Requests and Work Packages.

Risk, Action, Capacity Plan, and Commitment are not operational domains:

```text
Risk           = derived operational knowledge
Action         = operational activity or workflow behavior
Capacity Plan  = projection / planning view derived from operational state
Commitment     = projection derived from Requests, ownership, decisions, and operational state
```

They may appear as workflow activities or derived signals, but they never own facts and never have an independent lifecycle.

---

# Workflow and Lifecycle

The Operational Knowledge Lifecycle defines the lifecycle of operational knowledge.

The Operational Workflow defines how operational work is performed.

```text
Lifecycle = Operational Knowledge State
Workflow  = Operational Work Flow
```

A workflow is not required to pass through every lifecycle state.

The workflow should follow the minimum path necessary to resolve the operational matter correctly.

---

# Core Workflow

```text
REQUEST
    ↓
ASSIGN OWNER
    ↓
OWNER REVIEW
    │
    ├── DIRECT RESOLUTION
    │
    ├── ESCALATE
    │
    └── REJECT
```

Every operational matter must have an owner.

The owner is responsible for maintaining the current state, resolving the matter when possible, and escalating when necessary.

Ownership should be assigned directly whenever ownership is already known.

The system should not require triage when ownership is already clear.

---

# Direct Resolution

Direct Resolution is the default operational workflow.

```text
REQUEST
    ↓
ASSIGN OWNER
    ↓
DIRECT RESOLUTION
    ↓
DOCUMENT
    ↓
CLOSE
```

Most operational work should be resolved through this path.

Examples include:

* support activities;
* maintenance work;
* routine operational issues;
* work already within the owner's authority.

---

# Escalation

Escalation occurs when the owner cannot responsibly resolve the matter within their authority, responsibility, or available capacity.

```text
REQUEST
    ↓
ASSIGN OWNER
    ↓
ESCALATE
    ↓
ASSESS
    ↓
DECIDE
```

Assessment, estimation, commitment, and approval are invoked only when required.

They are not mandatory workflow stages for every request.

---

# Attention Workflow

The Operational System continuously evaluates operational state.

When an exception is detected:

```text
OPERATIONAL STATE
        ↓
    ATTENTION
        ↓
    DECISION
        ↓
  ACTIVITY
```

Here `ACTIVITY` denotes operational activity or workflow behavior. It is not an Action domain.

Management attention should focus on exceptions and decisions, not routine operational activities.

---

# Operational Review Workflow

Operational Review exists to review trends and decisions.

```text
CURRENT STATE
      ↓
   SNAPSHOT
      ↓
 TREND REVIEW
      ↓
  DECISIONS
      ↓
  ACTIVITIES
```

`ACTIVITIES` denotes operational activity or workflow behavior. They are not Action domain objects.

The purpose of Operational Review is not to discover status.

The purpose is to evaluate change, identify exceptions, and make decisions.

---

# Workflow Rules

1. Every operational matter must have an owner.
2. Resolve work at the lowest appropriate authority.
3. Escalate only when necessary.
4. Do not require triage when ownership is already known.
5. Do not require assessment, estimation, or approval for every request.
6. Direct Resolution should be the default path.
7. Urgent operational work may be executed before documentation is completed.
8. Documentation must eventually reflect operational reality.
9. Operational State remains the source of truth.
10. Management attention should focus on exceptions and decisions.

---

# Summary

```text
REQUEST
    ↓
ASSIGN OWNER
    ↓
OWNER REVIEW
    │
    ├── DIRECT RESOLUTION
    │         ↓
    │      DOCUMENT
    │         ↓
    │       CLOSE
    │
    ├── ESCALATE
    │         ↓
    │      ASSESS
    │         ↓
    │      DECIDE
    │         ↓
    │      EXECUTE
    │         ↓
    │      DOCUMENT
    │         ↓
    │       CLOSE
    │
    └── REJECT
```
