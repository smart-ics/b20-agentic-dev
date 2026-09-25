---

name: ICS Operational Knowledge Lifecycle
version: 3
last-update: 2026-09-23
updated by: Drury Yudis Lumenta
---

# Operational Knowledge Lifecycle

## 1. Purpose

The Operational Knowledge Lifecycle defines how operational knowledge is created, validated, maintained, closed, and retained.

It exists to ensure that:

* operational state remains explicit;
* every operational object has one authoritative current state;
* historical changes remain traceable;
* operational knowledge remains useful for decisions;
* the system does not depend on reports, meetings, memory, or verbal communication.

The lifecycle applies to the operational domains:

* Organization
* Customer
* Product
* Work Package
* Request
* Post

Derived concepts are not domains and do not have an independent lifecycle:

* Risk — derived operational knowledge
* Action — operational activity or workflow behavior
* Capacity Plan — projection / planning view derived from operational state
* Commitment — projection derived from Requests, ownership, decisions, and operational state

---

## 2. Core Rule

> **Every operational domain object has one authoritative current state.**

At any point in time, an operational domain object must have exactly one current state that is authoritative.

Derived concepts are projections over this state. They do not have an independent authoritative state.

The current state is the source of truth for:

* decisions;
* dashboards;
* reports;
* KPIs;
* analytics;
* operational summaries.

Historical states and changes may be preserved, but they do not replace the current state as the authoritative operational truth.

---

## 3. Lifecycle Model

The generic lifecycle is:

```text
CAPTURED
    ↓
VALIDATED
    ↓
ACTIVE
    ↓
CLOSED
    ↓
ARCHIVED
```

These are lifecycle states of operational knowledge.

They are not business-specific workflow states.

A domain may define additional states or a different terminal condition when necessary, but such variation must have an explicit reason.

The system should prefer the simplest lifecycle that provides sufficient operational control.

---

## 4. Lifecycle States

### 4.1 CAPTURED

The operational object has been recorded but has not yet been sufficiently validated.

Examples:

* a customer request has been received;
* a work package has been proposed;
* a customer has been identified;
* a product has been proposed;
* an organizational change has been reported.

At this stage:

* the information is recorded;
* the information may be incomplete;
* no commitment should be inferred;
* no decision should be inferred unless explicitly recorded.

`CAPTURED` means:

> "This has been reported or identified."

It does not mean:

> "This is accepted, approved, or committed."

---

### 4.2 VALIDATED

The operational object has been checked sufficiently to be treated as reliable operational knowledge.

Validation should establish, as applicable:

* what the object represents;
* its subject or scope;
* its owner;
* relevant relationships;
* sufficient factual information;
* whether the object is genuine and relevant to operations.

Validation does not necessarily mean approval.

For example:

```text
Customer Request
    ↓
VALIDATED
```

means the request is understood and recognized as a real request.

It does not mean the request has been accepted for implementation.

---

### 4.3 ACTIVE

The operational object currently represents an active operational matter.

Examples:

```text
Organization → ACTIVE
Customer     → ACTIVE
Product      → ACTIVE
Work Package → ACTIVE
Request      → ACTIVE
Post         → ACTIVE
```

An active object:

* has an authoritative current state;
* may change as reality changes;
* may require decisions or action;
* may appear in operational monitoring and dashboards.

Most operational management occurs while an object is `ACTIVE`.

---

### 4.4 CLOSED

The operational matter has reached its meaningful terminal condition.

Examples:

```text
Request      → accepted and completed
Request      → rejected
Work Package → completed
Work Package → cancelled
Product      → retired
```

Closure means:

> "No further operational work is currently required for this object."

Closure does not mean deletion.

The object remains part of the historical record.

A closed object must retain its final authoritative state and relevant history.

---

### 4.5 ARCHIVED

The object is no longer part of active operational management but is retained for historical reference, analysis, audit, or organizational memory.

Archived objects:

* are not part of normal active operational views;
* remain retrievable;
* remain historically attributable;
* may still contribute to historical analytics where appropriate.

Archiving is a retention decision, not a replacement of historical information.

---

## 5. State Transitions

The generic transitions are:

```text
CAPTURED  → VALIDATED
VALIDATED → ACTIVE
ACTIVE    → CLOSED
CLOSED    → ARCHIVED
```

A transition must have a clear reason.

Invalid transitions should not be silently performed.

Examples of acceptable transitions include:

```text
CAPTURED  → CLOSED
```

when an item is validated as unnecessary and immediately closed.

```text
VALIDATED → CLOSED
```

when validation determines that no active operational work is required.

Domain-specific workflows may therefore omit intermediate states when the operational meaning is clear.

The lifecycle is a control model, not a mandatory ceremony.

---

## 6. Update Is Not a Lifecycle State

`UPDATE` is an operation, not a lifecycle state.

An operational object may be updated while it is active.

For example:

```text
Work-Package-001

Current State:
ACTIVE

Progress:
45% → 52%

Health:
WATCH → AT-RISK

Owner:
Person-A

Updated:
2026-09-23
```

The object remains `ACTIVE`.

Its current state changes because reality changed.

This distinction prevents unnecessary lifecycle complexity.

---

## 7. Current State and History

Every operational object has two conceptual layers:

```text
CURRENT STATE
+
HISTORY
```

### Current State

Represents the latest authoritative operational truth.

### History

Represents how the object reached its current state.

History may include:

* previous states;
* changes;
* decisions;
* ownership changes;
* significant events;
* timestamps;
* relevant reasons.

Historical information must not compete with the current state as the source of truth.

The system must always be able to answer:

> "What is the current state now?"

and separately:

> "How did it get here?"

---

## 8. State Changes Must Be Explicit

A change to an authoritative state must be explicit.

For example:

```text
Work Package Health:
WATCH → AT-RISK
```

should identify:

* previous state;
* new state;
* timestamp;
* reason;
* actor or source, when applicable.

This does not require elaborate event-sourcing infrastructure.

A simple change history is sufficient unless the system has a demonstrated need for more complexity.

---

## 9. Facts, Interpretation, and Decision

Operational knowledge may contain three different kinds of information:

```text
FACT
INTERPRETATION
DECISION
```

They must not be confused.

### Fact

Something observable or reported as occurring.

Example:

```text
Two milestones are overdue.
```

### Interpretation

An assessment of what the facts mean.

Example:

```text
The work package may be experiencing insufficient engineering capacity.
```

### Decision

An explicit management choice.

Example:

```text
Reduce Phase-2 scope by two items.
```

The lifecycle preserves all three, but they serve different purposes.

---

## 10. Accountability

Every active operational object must have an explicitly identified accountable authority appropriate to its nature.

The accountable authority is responsible for maintaining the correctness of the operational state.

Examples:

```text
Organization → Organization Owner
Customer     → Customer Owner
Product      → Product Owner
Work Package → Work Package Owner
Request      → Request Owner
Post         → Author and/or moderation authority
```

Accountability may be delegated.

Accountability remains explicit.

An active object without an accountable authority is operationally incomplete.

Each operational domain may define a lifecycle appropriate to its nature; for example, Requests follow the operational work lifecycle, while Posts follow a communication‑visibility lifecycle.

```text
VISIBLE    ↓
HIDDEN     ↓
ARCHIVED
```

---

## 11. Commitment Is a Projection, Not a Lifecycle

A Commitment is not an operational domain and does not have its own lifecycle.

A Commitment is a projection derived from Requests, ownership, decisions, and operational state.

The lifecycle does not define whether work is approved or committed.

These are separate operational facts.

In particular:

```text
Request ≠ Commitment
Discussion ≠ Commitment
Idea ≠ Commitment
```

A request may be:

```text
VALIDATED
```

without being accepted.

A request may be:

```text
ACTIVE
```

while its business decision is still pending.

A commitment exists only when the appropriate decision and capacity allocation have been explicitly made, and it must remain derivable from the authoritative operational state.

---

## 12. Domain Workflow vs Knowledge Lifecycle

The Operational Knowledge Lifecycle must not be confused with a domain's operational workflow.

### Knowledge Lifecycle

Defines:

```text
The lifecycle of operational knowledge
```

Generic model:

```text
CAPTURED
→ VALIDATED
→ ACTIVE
→ CLOSED
→ ARCHIVED
```

### Domain Workflow

Defines:

```text
How operational work is performed
```

There is no single universal domain workflow.

A Domain Workflow is **not** required to pass through every lifecycle state sequentially.

It may skip intermediate lifecycle states, branch, or terminate early whenever the operational meaning is clear.

Multiple workflow patterns are supported, for example:

* **Direct resolution** — the assigned owner reviews and resolves the matter directly;
* **Assessment and commitment** — assess → estimate → decide → commit → deliver;
* **Escalation** — the owner cannot decide within authority and escalates to a higher authority;
* **Rejection** — the matter is reviewed and closed without operational work.

Activities such as assessment, estimation, decision, and commitment are operational activity or workflow behavior. They are not operational domains, and they do not create lifecycle states.

The previously documented request-oriented workflow:

```text
CAPTURE → ASSESS → ESTIMATE → DECIDE → COMMIT → DELIVER → COMPLETE
```

is only one possible workflow example.

It is not the default, the standard, or a required sequence for operational requests.

### Pragmatic Operational Example

The following example reflects ICS operational reality: lightweight ownership, with escalation only when required.

```text
REQUEST
   ↓
ASSIGN OWNER
   ↓
OWNER REVIEW
   ↓
┌─────────────┬──────────────┬─────────────┐
│             │              │             │
DIRECT FIX   ESCALATE       REJECT
│             │
▼             ▼
EXECUTE    HIGHER AUTHORITY
│             │
└──────┬──────┘
       ▼
   DOCUMENT
       ▼
     CLOSE
```

In this pattern:

* most operational requests may be resolved directly by the assigned owner;
* assessment, estimation, commitment, and escalation are invoked only when required;
* operational work may begin before documentation is completed;
* documentation may occur after execution for urgent operational situations;
* the lifecycle governs operational knowledge, not operational bureaucracy.

Not every request needs assessment, estimation, a formal decision, or a commitment ceremony.

Those steps are exceptions invoked by conditions, not mandatory stages.

### Relationship Between the Two Models

The domain workflow may move or update the lifecycle state.

For example:

```text
REQUEST

ASSIGN OWNER
   ↓
CAPTURED

OWNER REVIEW: UNDERSTOOD
   ↓
VALIDATED

DIRECT FIX: ACCEPTED
   ↓
ACTIVE

DOCUMENT + CLOSE
   ↓
CLOSED
```

A rejected request may move:

```text
OWNER REVIEW: REJECT
   ↓
CLOSED
```

without ever passing through `ACTIVE`.

The two models are related but should remain conceptually separate.

Workflow activity is how the work is performed.

The lifecycle state is the authoritative operational truth.

### Alignment With Manifesto Principles

* **Simplicity Over Process** — a workflow includes only the steps that add value; steps that create more overhead than value are omitted.
* **Exception-Based Management** — escalation, assessment, estimation, and commitment are exceptions, not routine stages; leadership attention is required only when an exception occurs.
* **Every Problem Has an Owner** — every request is assigned an owner; the owner resolves the matter directly or escalates it, and accountability is never left unassigned.
* **Operational State Is The Source Of Truth** — regardless of which workflow path is taken, decisions, dashboards, and reports derive from the object's authoritative current state, never from workflow activity, meetings, or documentation alone.

---

## 13. Domain-Specific Lifecycle Variation

Not every operational object needs exactly the same domain workflow.

For example:

```text
WORK PACKAGE
ACTIVE → CLOSED
```

may be sufficient for its operational lifecycle.

A request may require:

```text
CAPTURED
→ VALIDATED
→ ACTIVE
→ CLOSED
```

A product may become:

```text
ACTIVE
→ RETIRED
```

A domain may introduce additional operational states when those states materially affect decisions.

However:

> **Do not create a state merely because the workflow contains an activity.**

For example:

```text
ASSESSING
ESTIMATING
DISCUSSING
REVIEWING
```

should not automatically become lifecycle states.

They are workflow activities unless the distinction has operational meaning that must be visible in the authoritative state.

---

## 14. Operational Snapshots

Current state alone is sufficient for current operational control, but not for historical trend analysis.

Therefore the system should preserve periodic operational snapshots.

A snapshot captures the operational state of the company or a defined operational scope at a point in time.

Examples:

```text
2026-07 Operational Snapshot
2026-08 Operational Snapshot
2026-09 Operational Snapshot
```

Snapshots support:

* month-to-month comparison;
* trend analysis;
* operational performance analysis;
* historical management review.

Snapshots are derived from operational state.

They do not replace current operational state as the source of truth.

---

## 15. Reports and Dashboards

Reports, dashboards, and analytics are derived artifacts.

They must not become alternative sources of operational truth.

The relationship is:

```text
Operational State
       ↓
Metrics / Analysis
       ↓
Dashboard / Report
       ↓
Management Decision
```

Not:

```text
Report
   ↓
Operational State
```

A report may summarize the state.

It must not define the state.

---

## 16. Data Quality Rules

An operational object should not be considered operationally complete when essential information is missing.

For an active object, the minimum practical information is:

```text
Identity
Current State
Owner
Relevant Scope
Last Update
```

Additional fields depend on the object type.

The system should favor:

> **minimum sufficient information**

over:

> **maximum information collection**

Missing information should be visible rather than silently invented.

---

## 17. Stale Operational Knowledge

Operational knowledge becomes stale when its current state has not been confirmed or updated for a period where change is reasonably expected.

Staleness does not automatically mean the state is wrong.

It means:

> "The system has insufficient evidence that the current state is still fresh."

Therefore stale knowledge should be treated as an operational signal.

For example:

```text
Work Package:
Current State = ACTIVE
Last Update = 35 days ago

→ State remains ACTIVE
→ Freshness = STALE
```

The system must not silently change the authoritative state merely because the information is old.

---

## 18. Closure and Reopening

A closed object may need to be reopened when new operational facts make the previous closure invalid.

Example:

```text
Work Package
ACTIVE
  ↓
CLOSED
  ↓
New operational facts appear
  ↓
REOPENED
```

Reopening must create a new explicit state change.

The previous closure remains part of history.

The current state becomes the new authoritative state.

---

## 19. Lifecycle Design Rules

Operational lifecycle design should follow these rules:

1. **One authoritative current state.**
2. **State represents operational truth, not workflow activity.**
3. **Updates are operations, not automatically new states.**
4. **History is preserved.**
5. **Reports and dashboards derive from operational state.**
6. **Commitment is a projection derived from explicit decisions and operational state; it is never a lifecycle state and must not be inferred.**
7. **Ownership is explicit.**
8. **Facts, interpretations, and decisions are distinguishable.**
9. **Domain workflows remain separate from the generic knowledge lifecycle.**
10. **Use the simplest lifecycle that provides sufficient control.**
11. **Workflow steps are invoked only when required, not by default.**
12. **Documentation may follow execution when operational urgency requires it.**

---

# 20. Lifecycle Summary

The ICS Operational Knowledge Lifecycle is:

```text
                 ┌──────────────┐
                 │   CAPTURED   │
                 └──────┬───────┘
                        │ validate
                        ▼
                 ┌──────────────┐
                 │  VALIDATED   │
                 └──────┬───────┘
                        │ activate
                        ▼
                 ┌──────────────┐
          ┌─────►│    ACTIVE    │◄─────┐
          │      └──────┬───────┘      │
          │             │ close        │
          │             ▼              │
          │      ┌──────────────┐      │
          │      │    CLOSED    │      │
          │      └──────┬───────┘      │
          │             │ archive      │
          │             ▼              │
          │      ┌──────────────┐      │
          └──────┤   ARCHIVED   │      │
                 └──────────────┘      │
                                        │
                           reopen when needed
```

Throughout the lifecycle:

```text
┌─────────────────────────────────────────┐
│        ONE AUTHORITATIVE CURRENT STATE  │
│                                         │
│  + change history                       │
│  + explicit ownership                   │
│  + explicit decisions                   │
└─────────────────────────────────────────┘
```

The lifecycle exists to maintain trustworthy operational knowledge so that the Operational System can provide continuous visibility, surface exceptions, and support management decisions.
