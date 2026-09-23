---
name: architecture-creation
description: Create or update an ARCHITECTURE artifact that defines the technical realization of a FEATURE or BUG correction
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

Realize approved analysis decisions as technical architecture.

## What I do not do

ARCHITECTURE does not own:

- Business concepts
- Business rules
- Business outcomes
- Operational flows
- Domain orchestration
- Problem investigation
- Root cause analysis
- Gap analysis
- Alternative evaluation already completed by upstream analysis
- Decision resolution already completed by upstream analysis
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

FEASIBILITY-ASSESSMENT provides approved gap-closure decisions.
BUG-INVESTIGATION provides approved investigation decisions. ARCHITECTURE
consumes the approved decisions contained within either analysis artifact.

## Architecture Update Responsibility

ARCHITECTURE is created or updated by:

```text
Read DOMAIN and FEATURE when applicable
        ↓
Read FEASIBILITY-ASSESSMENT or BUG-INVESTIGATION
        ↓
Realize approved analysis decisions as target technical state
        ↓
Create / Update ARCHITECTURE
```

## Decision Ownership

FEASIBILITY-ASSESSMENT owns gap-closure decisions.

BUG-INVESTIGATION owns investigation decisions.

ARCHITECTURE owns the technical realization of those decisions.

ARCHITECTURE must not repeat problem investigation, root cause analysis, gap
analysis, alternative evaluation, or decision resolution already completed by
upstream analysis. It does not re-evaluate business decisions, gap-closure
decisions, or investigation decisions unless explicitly requested.

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

An ARCHITECTURE realizes exactly one FEATURE or BUG correction.

## Workflow Position

```text
CHANGE-REQUEST
    ↓
FEASIBILITY-ASSESSMENT
    ↓
ARCHITECTURE

BUG
    ↓
BUG-INVESTIGATION
    ↓
Decision
    ↓
ARCHITECTURE
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
