---

name: ics-greenfield-architecture-creation
description: Create the initial ARCHITECTURE artifact for a greenfield system from the complete approved product definition, producing a target architecture that can be directly consumed by the IMPLEMENTATION-PLAN skill
license: Proprietary
compatibility: opencode
metadata:
audience: ica-architect
artifact: ARCHITECTURE
mode: greenfield
----------------

# What I do

Create the initial `ARCHITECTURE` artifact for a new system from its
complete product definition.

The skill transforms approved product knowledge into a coherent technical
target state that is sufficiently explicit for the `ics-implementation-plan`
skill to determine implementation scope, phases, slices, dependencies,
repository boundaries, and execution order.

The resulting artifact is the authoritative target technical architecture
for the greenfield system.

This skill is used when:

* The system is being created from scratch.
* No authoritative system architecture exists yet.
* Product definition has been completed.
* Implementation planning must begin for the first implementation cycle.

This skill does not implement code.

---

# What I do not do

This skill does not own:

* Business concepts
* Business rules
* Business outcomes
* Operational scenario definitions
* Use-case definitions
* User journeys
* Navigation decisions
* UI layout decisions
* Feature definition
* Product prioritization
* Feature acceptance criteria
* Implementation status
* Review status
* Test results
* Detailed implementation tasks

Those belong to upstream or downstream artifacts.

Business knowledge remains authoritative in `operational/domains`.

Business behavior remains authoritative in `operational/scenarios` and
`operational/use-cases`.

User interaction remains authoritative in `operational/user-journey`,
`operational/navigation`, and `operational/ui-layout`.

Business outcomes remain authoritative in `operational/features`.

Implementation sequencing remains authoritative in
`IMPLEMENTATION-PLAN`.

---

# Product Definition Inputs

The skill must consume the complete approved product definition before
creating the architecture.

Required product-definition inputs:

```text
domains/*
actors/*
scenarios/*
use-cases/*
user-journey/*
navigation/*
ui-layout/*
features/*
```

The Architect must read all applicable artifacts, not only a selected subset
of features.

The architecture must be derived from the complete system model.

The current codebase must be inspected when a repository already exists.

For a true greenfield repository with no implementation code, the absence of
a current codebase is valid and must be recorded explicitly.

---

# Product Definition Authority

The following authority model applies:

```text
DOMAIN
    ↓
defines business knowledge

SCENARIO / USE CASE
    ↓
defines operational behavior

USER JOURNEY / NAVIGATION / UI LAYOUT
    ↓
defines user interaction structure

FEATURE
    ↓
defines user outcome and required capability

ARCHITECTURE
    ↓
defines technical realization
```

The Architect must not redefine upstream business meaning.

When two upstream artifacts appear inconsistent, do not silently choose one.

Record the inconsistency as an architecture input issue and resolve it through
the appropriate upstream owner before finalizing the architecture.

---

# Greenfield Architecture Responsibility

The Architect is responsible for determining the technical target state of the
entire system.

The architecture must establish:

* System boundary
* Application boundaries
* Technical module boundaries
* Domain-to-module mapping
* Application-layer responsibilities
* Domain-layer responsibilities
* Infrastructure responsibilities
* API boundaries
* UI/application boundaries
* Data ownership
* Persistence boundaries
* Integration boundaries
* Cross-cutting technical concerns
* Repository boundaries
* Deployment/runtime structure
* Technical constraints
* Implementation boundaries

The Architect must optimize for a coherent whole.

Do not create one independent architecture per feature.

Features must be realized within the system architecture.

---

# Architecture Creation Flow

Create the architecture using this sequence:

```text
Read Product Definition
        ↓
Identify System Boundary
        ↓
Identify Architectural Drivers
        ↓
Map Domains to Technical Modules
        ↓
Map Use Cases to Application Components
        ↓
Map Features to Technical Capabilities
        ↓
Define Data Ownership
        ↓
Define Integration Boundaries
        ↓
Define Application / API / UI Structure
        ↓
Define Persistence Architecture
        ↓
Define Cross-Cutting Concerns
        ↓
Define Implementation Constraints
        ↓
Define Implementation Units and Dependencies
        ↓
Validate Architecture Completeness
        ↓
Create ARCHITECTURE
```

---

# Architecture Principles

## 1. Architecture realizes the whole system

The output is the target architecture of the system, not a feature-specific
solution.

## 2. Product definition remains authoritative

Do not move business knowledge into ARCHITECTURE unless necessary to explain
technical realization.

Reference upstream artifacts instead of duplicating their content.

## 3. Every technical responsibility has one owner

Every component responsibility must have exactly one technical owner.

Avoid ambiguous shared ownership.

## 4. Every persistent data concept has one owner

Define authoritative data ownership explicitly.

Avoid shared write ownership.

Read access across boundaries is allowed where technically appropriate.

## 5. Feature boundaries must not automatically become technical boundaries

A feature may span multiple technical components.

Multiple features may share one technical component.

Technical boundaries must be based on responsibility, ownership, cohesion,
dependency direction, and implementation safety.

## 6. Architecture must be implementation-oriented

The result must be detailed enough that another Architect can produce an
IMPLEMENTATION-PLAN without re-designing the system.

## 7. Do not create implementation tasks

Architecture defines technical structure and boundaries.

Implementation-plan defines phases and executable slices.

---

# Required Architectural Mappings

The architecture must contain explicit mappings between product definition and
technical structure.

## Domain-to-Architecture Mapping

Every operational domain must map to one or more technical modules.

Example:

| Domain   | Technical Module | Ownership         |
| -------- | ---------------- | ----------------- |
| Request  | Request          | Request lifecycle |
| Customer | Customer         | Customer master   |
| Product  | Product          | Product state     |

No domain may remain architecturally unexplained.

---

## Use-Case-to-Architecture Mapping

Every approved use case must map to the technical components responsible for
realizing it.

Example:

| Use Case             | Application Component | Domain Components     |
| -------------------- | --------------------- | --------------------- |
| Create Request       | CreateRequest         | Request               |
| Assign Request Owner | AssignRequestOwner    | Request, Organization |

The mapping must identify actual implementation boundaries.

---

## Feature-to-Architecture Mapping

Every approved feature must map to the technical realization required to
implement it.

Example:

| Feature      | Technical Module | Application Component | UI Boundary    | Persistence |
| ------------ | ---------------- | --------------------- | -------------- | ----------- |
| FEAT-REQ-001 | Request          | CreateRequest         | Request Entry  | Request     |
| FEAT-REQ-002 | Request          | AssignRequestOwner    | Request Detail | Request     |

This section is mandatory.

The IMPLEMENTATION-PLAN skill uses this mapping to determine the impact of a
feature on the target architecture.

---

# System Structure

Define the overall technical structure.

At minimum identify applicable layers/components such as:

```text
Presentation
Application
Domain
Infrastructure
Persistence
Integration
```

Do not force these layers when the technology or system does not require them.

For each layer/component define:

* Responsibility
* Ownership
* Allowed dependencies
* Prohibited dependencies

---

# Module Boundaries

Define every major technical module.

For each module specify:

* Name
* Responsibility
* Owned domains
* Owned data
* Primary use cases
* Features realized
* Dependencies
* Public interfaces
* Internal responsibilities

Modules must have clear ownership.

Avoid creating modules solely because a feature happens to have a different
name.

---

# Application Components

Define application components required to execute the approved use cases.

For each component specify:

* Name
* Responsibility
* Owning module
* Input
* Output
* Dependencies
* State changes
* External interactions

Do not describe detailed code-level classes unless needed to remove ambiguity.

---

# API and Integration Design

Define externally visible technical boundaries.

For each API or integration define:

| Source | Target | Interface | Purpose | Ownership |
| ------ | ------ | --------- | ------- | --------- |

Include:

* Internal API boundaries
* External integrations
* Authentication boundaries
* Integration direction
* Data exchanged at the boundary
* Failure ownership where architecturally significant

Do not use this section to describe business workflow.

---

# Data Ownership

Define authoritative ownership of all important persistent data.

| Data | Owner Module | Write Authority | Read Consumers |
| ---- | ------------ | --------------- | -------------- |

Avoid shared write ownership.

If data is projected, cached, replicated, or derived, identify:

* Source of truth
* Projection owner
* Rebuild mechanism where applicable

Respect the operational knowledge rule that every operational object has one
authoritative current state.

---

# Database Design

Define the architecturally significant persistence structure.

## New Tables

| Table | Owner Module | Purpose |
| ----- | ------------ | ------- |

## Relationships

Describe important relationships between owned data structures.

## Derived / Projection Data

Identify read models, projections, snapshots, or generated data where
applicable.

## Migration Considerations

For a greenfield system this section should explicitly state:

* No legacy migration required

or define required initial migration strategy.

Do not define every column, datatype, index, or constraint unless required to
resolve an architectural decision.

---

# Cross-Cutting Concerns

Define system-wide technical concerns where applicable:

* Authentication
* Authorization
* Audit
* Logging
* Observability
* Error handling
* Concurrency
* Transactions
* Caching
* Background processing
* Notifications
* File storage
* Security
* Performance

Only include concerns that materially affect architecture.

---

# Technology Decisions

Record system-wide technical decisions that constrain implementation.

Examples:

* Framework
* Runtime
* Database
* ORM / micro-ORM
* API style
* Authentication mechanism
* Frontend framework
* Deployment model
* Messaging technology
* Storage technology

Do not duplicate the development standards when simply inherited.

Reference them where appropriate.

---

# Implementation Constraints

Define explicit rules that implementation agents must follow.

Examples:

* Must use Dapper
* Must use SQL Server
* No Entity Framework
* Explicit SQL
* Vertical-slice organization
* Explicit dependency registration
* No cross-module direct persistence access
* One repository per implementation slice

Only record constraints that are actually authoritative.

---

# Implementation Boundaries

This section is mandatory because the artifact must be consumable by the
IMPLEMENTATION-PLAN skill.

Identify the natural implementation boundaries of the system.

For each boundary define:

| Boundary | Responsibility | Repository | Depends On | Implementation Notes |
| -------- | -------------- | ---------- | ---------- | -------------------- |

A boundary may represent:

* Technical foundation
* Shared infrastructure
* Module
* Application component
* Integration adapter
* UI application
* API
* Persistence component

Do not turn boundaries into task lists.

---

# Implementation Dependency Graph

Define architectural prerequisites.

Example:

```text
Foundation
    ↓
Identity
    ↓
Organization
    ↓
Request
    ↓
Work Package
    ↓
Operational Awareness
    ↓
Analytics
```

Only declare dependencies that are technically required.

Do not serialize independent modules unnecessarily.

This dependency graph is input to implementation planning.

---

# Implementation Readiness

The architecture is implementation-ready only when:

1. Every approved domain has a technical home.
2. Every approved use case has an implementing application boundary.
3. Every approved feature has an explicit technical mapping.
4. Every important persistent data concept has an owner.
5. Integration boundaries are explicit.
6. Major cross-cutting concerns are resolved.
7. Technical constraints are explicit.
8. Implementation boundaries are identifiable.
9. Architectural dependencies are identifiable.
10. The implementation-plan skill can create slices without having to redesign
    the target architecture.

If any condition is not satisfied, the architecture is not complete.

---

# Planning Handoff Contract

The ARCHITECTURE output must be directly consumable by
`ics-implementation-plan`.

The Planning skill must be able to derive:

```text
Implementation Scope
Phase Breakdown
Slice Breakdown
Execution Order
Dependencies
Repository Boundaries
Repository Ownership
```

without redesigning the architecture.

The Architecture must therefore provide:

```text
System Boundary
Module Structure
Component Responsibilities
Feature Mapping
Data Ownership
Integration Boundaries
Implementation Boundaries
Dependency Graph
Technical Constraints
```

The Architecture must not define:

* Slice IDs
* Implementation status
* Review status
* Execution approval
* Final implementation task text

Those belong to `IMPLEMENTATION-PLAN`.

---

# Architecture Validation

Before producing the final artifact, perform these checks.

## Product Coverage

Verify that:

```text
Every Domain → Technical Home
Every Use Case → Application Realization
Every Feature → Technical Realization
Every Navigation/UI area → Technical Support
```

## Ownership

Verify:

```text
Every technical responsibility → one owner
Every authoritative data concept → one owner
Every integration boundary → identified owner
```

## Dependency

Verify:

```text
No circular dependencies
No unexplained cross-module dependency
No unnecessary serialization
```

## Planning Compatibility

Verify:

```text
Every major architecture unit can become implementation scope
Every dependency can become a planning dependency
Every repository boundary is explicit
```

---

# Output

Produce:

`<SYSTEM-CODE>-ARCHITECTURE.md`

Use the existing architecture artifact format:

`development/skills/ics-architecture-creation/assets/architecture-template.md`

The artifact remains:

```text
Artifact: ARCHITECTURE
```

Do not introduce a separate artifact type.

The greenfield architecture is simply the initial system-level
`ARCHITECTURE`.

---

# Output Structure

The resulting ARCHITECTURE must contain at minimum:

```text
1. Overview

2. Architectural Basis

3. Scope

4. Architectural Drivers

5. System Structure

6. Module Boundaries

7. Component Responsibilities

8. Use Case Mapping

9. Feature Mapping

10. Integration Design

11. Data Ownership

12. Database Design

13. Cross-Cutting Concerns

14. Technical Decisions

15. Implementation Constraints

16. Implementation Boundaries

17. Implementation Dependency Graph

18. Acceptance Conditions
```

The existing template may be extended to support these sections.

Do not remove the standard architecture sections required by the existing
architecture contract.

---

# Versioning

Use:

`Major.Minor`

Update:

* Version
* LastUpdated

Initial greenfield architecture starts at:

`1.0`

Do not maintain version history inside the document.

Git maintains history.

---

# Final Rule

The Architect must answer:

> "What technical system must exist so that all approved product-definition
> artifacts can be implemented coherently?"

The Architect must not answer:

> "How should one feature be implemented?"

The first question is owned by this greenfield architecture skill.

The second question is owned by the existing feature architecture workflow.
