---
name: architecture-creation
description: Create or update an ARCHITECTURE artifact that defines the technical realization of a FEATURE
license: Proprietary
compatibility: opencode
metadata:
  audience: architect
  artifact: ARCHITECTURE
---

## What I do

- Create a new ARCHITECTURE artifact
- Update an existing ARCHITECTURE artifact
- Define technical structure
- Define component responsibilities
- Define integration design
- Define technical decisions
- Define implementation boundaries

## What I do not do

ARCHITECTURE does not own:

- Business concepts
- Business rules
- Business outcomes
- Operational flows
- Domain orchestration
- Project planning
- Task breakdown
- SOPs

Business knowledge belongs to DOMAIN.

Business outcomes belong to FEATURE.

## Inputs

### Business Context

- DOMAIN and FEATURE, for a CHANGE-REQUEST
- ISSUE with `Type = BUG`, for a BUG

DOMAIN provides business knowledge when applicable.

FEATURE provides business outcome, operational flow, and domain orchestration
when applicable.

### Analysis Input

- FEASIBILITY-ASSESSMENT, for a CHANGE-REQUEST
- BUG-INVESTIGATION, for a BUG

FEASIBILITY-ASSESSMENT provides feasibility findings and approved decisions.
BUG-INVESTIGATION provides defect findings, impact, assumptions, open
questions, and recommended direction. Both provide the analysis and decision
inputs that must be realized by the target architecture.

## Architecture Update Responsibility

ARCHITECTURE is created or updated by:

```text
Read DOMAIN and FEATURE when applicable
        ↓
Read FEASIBILITY-ASSESSMENT or BUG-INVESTIGATION
        ↓
Translate approved decisions into target technical state
        ↓
Create / Update ARCHITECTURE
```

## Decision Ownership

FEASIBILITY-ASSESSMENT owns approved feasibility decisions.

BUG-INVESTIGATION owns investigation findings, assumptions, open questions,
and recommended direction.

ARCHITECTURE owns the technical realization of the analysis inputs.

ARCHITECTURE must not reinterpret or rediscover analysis findings.

## Artifact Ownership

ARCHITECTURE owns:

- Technical Structure
- Component Responsibilities
- Integration Design
- Data Ownership
- Database Design
- Technical Decisions
- Implementation Constraints
- Cross-Cutting Concerns

Database Design is part of the technical realization of a FEATURE.

ARCHITECTURE owns architecturally significant persistence decisions, including:

- New Tables
- Modified Tables
- Relationships
- Data Ownership
- Migration Considerations

ARCHITECTURE does not own detailed physical schema definitions such as every column, datatype, index, or constraint.

An ARCHITECTURE realizes exactly one FEATURE.

## Workflow Position

```text
Discovery
    ↓
Feasibility Assessment
    ↓
Gap Closure
    ↓
Architecture Update
    ↓
Planning
```

## Output

Produce:

`<CODE>-ARCHITECTURE.md`

The artifact must follow:

- assets/architecture-template.md

## Versioning

Use:

`Major.Minor`

Update:

- Version
- LastUpdated

Do not maintain history inside the document.

## When to use me

Use this skill when:

- A new FEATURE has been approved
- Technical realization must be defined
- Implementation ambiguity must be removed
- Agents require deterministic implementation guidance

Ask clarifying questions when responsibilities, ownership, integration boundaries, or technical decisions are unclear.
