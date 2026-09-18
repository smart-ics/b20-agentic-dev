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

## Artifact Ownership

ARCHITECTURE owns:

- Technical Structure
- Component Responsibilities
- Integration Design
- Data Ownership
- Technical Decisions
- Implementation Constraints
- Cross-Cutting Concerns

An ARCHITECTURE realizes exactly one FEATURE.

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