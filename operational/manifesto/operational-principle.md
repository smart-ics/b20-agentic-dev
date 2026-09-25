---
name: ICS Operational Principle
version: 3
last-update: 2026-09-23
updated by: Drury Yudis Lumenta
---

# Operational Principle

## 1. Purpose

The purpose of the Operational System is to provide sufficient visibility, analysis, and control for company leadership to make timely and informed decisions.

The system exists to answer:

* What is happening?
* What changed?
* What requires attention?
* What decision is needed?
* What is the expected impact?

---

## 2. Operational State is the Source of Truth

Reports are not the source of truth.

The source of truth is the current operational state recorded in the system.

All reports, dashboards, analytics, and summaries are derived from operational state.

---

## 3. Every Object Has One Authoritative Current State

Every operational object in a domain has exactly one authoritative current state.

The operational domains are Organization, Customer, Product, Work Package, Request, and Post.

Derived concepts such as risks, actions, capacity plans, and commitments are not domains. They are derived from domain facts and do not own an independent authoritative state.

Decisions, dashboards, reports, KPIs, and analytics must derive from this current state.

Reports are views of operational state, not the source of operational truth.

Historical changes must be preserved, but only one current state is considered authoritative at any given time.

---

## 4. Decisions Over Reporting

The goal of the system is not to produce reports.

The goal of the system is to support decisions.

Every operational artifact should help answer at least one decision.

If an artifact supports no decision, it should not exist.

---

## 5. Exception-Based Management

Leadership attention is limited.

The system should highlight exceptions, risks, blockers, and abnormal conditions.

Leadership should not be required to inspect every work package, request, team, or activity.

---

## 6. Facts Before Opinions

Operational decisions should be based on observable facts whenever possible.

Opinions, assumptions, and interpretations must be explicitly distinguished from factual observations.

---

## 7. Capacity is Finite

Operational capacity is finite.

A Capacity Plan is a projection / planning view derived from operational state. It is not an operational domain.

A Commitment is a projection derived from Requests, ownership, decisions, and operational state. It is not an operational domain.

Work cannot be committed without considering available capacity.

Demand exceeding capacity must result in one or more of:

* Reprioritization
* Deferral
* Scope reduction
* Additional resources
* Rejection

---

## 8. Effort and Duration are Different

Effort represents the amount of work required.

Duration represents the elapsed calendar time required.

Operational planning must not assume they are equivalent.

---

## 9. Commitments Must Be Explicit

A Commitment is a projection derived from Requests, ownership, decisions, and operational state. It is not an operational domain.

A request is not a commitment.

A discussion is not a commitment.

An idea is not a commitment.

Work becomes a commitment only after explicit approval and allocation of capacity, and the commitment must remain derivable from the authoritative operational state.

---

## 10. Visibility Must Be Continuous

Operational visibility should not depend on meetings, reports, or manual status collection.

The current operational state should be continuously available.

Meetings exist to discuss decisions, not to discover status.

---

## 11. Trends Matter More Than Snapshots

A single measurement has limited value.

Operational health should be evaluated through trends over time.

The system should preserve historical operational snapshots for comparison and analysis.

---

## 12. Every Problem Has an Owner

Every Work Package and Request must have a clearly identified owner.

Derived operational knowledge, such as risks, blockers, and commitments, inherits its owner from the domain facts it is derived from. It does not have an independent owner of its own.

Ownership may be delegated.

Accountability may not.

---

## 13. Attention is a Managed Resource

Leadership attention is one of the company's most limited resources.

The system should actively identify derived operational signals such as:

* Risks
* Blockers
* Capacity shortages
* Delays
* Escalations
* Decisions awaiting activity

and present them in a prioritized manner.

These signals are derived knowledge and projections over operational state. They are not operational domains.

---

## 14. Operational Knowledge Must Be Explicit

Operational knowledge should be stored in structured artifacts whenever practical.

Critical operational knowledge should not depend on memory, verbal communication, chat history, or individual availability.

---

## 15. Simplicity Over Process

Processes exist to improve operational effectiveness.

If a process creates more overhead than value, it should be simplified or removed.

The operational system should be as simple as possible while still providing sufficient control and visibility.

---

## 16. Domains Own Facts, Projections Derive From Facts

The Operational System has exactly six operational domains:

```text
Organization
Customer
Product
Work Package
Request
Post
```

Post is the authoritative domain for operational communication, discussion, organizational awareness, and knowledge sharing.

A Post has its own authoritative current state, lifecycle, ownership, comments, reactions, references, visibility rules, and history.

Posts may reference objects from other domains, including Requests, Work Packages, Products, Customers, and Organization entities, but Posts remain independent operational objects and do not derive their lifecycle from the referenced object.

Domains own operational facts. Only domains have an authoritative current state and a lifecycle.

Derived concepts are not domains:

```text
Risk           = derived operational knowledge
Action         = operational activity or workflow behavior
Capacity Plan  = projection / planning view derived from operational state
Commitment     = projection derived from Requests, ownership, decisions, and operational state
```

Derived knowledge and projections are computed from domain facts.

They do not own facts, do not have an independent authoritative state, and do not have their own lifecycle.

Operational State is authoritative. Derived knowledge is projection.

---

# The Most Important Principle

> **The purpose of the Operational System is to surface the decisions that require attention, not to collect status information.**