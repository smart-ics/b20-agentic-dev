---
name: feature-creation
description: Create or update a FEATURE artifact that defines a business outcome, operational flow, and domain orchestration
license: Proprietary
compatibility: opencode
metadata:
  audience: analyst, architect
  artifact: FEATURE
---

## What I do

- Create a new FEATURE artifact
- Update an existing FEATURE artifact
- Define business outcomes
- Define participating domains
- Define operational flow
- Define domain orchestration
- Define business constraints
- Define business exceptions
- Define acceptance criteria

## What I do not do

FEATURE does not own:

- Business concepts
- Business rules that belong to a domain
- Domain lifecycles
- Domain events
- Technical design
- APIs
- Database schema
- UI implementation
- SOP documents

Business knowledge belongs to DOMAIN.

Technical realization belongs to ARCHITECTURE.

SOPs belong to Generated Knowledge.

## Artifact Ownership

FEATURE owns:

- Business Purpose
- Business Outcome
- Participating Domains
- Operational Flow
- Domain Orchestration
- Feature Constraints
- Feature Exceptions
- Acceptance Criteria

A FEATURE may involve one or more DOMAINs.

A DOMAIN may participate in multiple FEATUREs.

## Output

Produce:

`<CODE>-FEATURE.md`

The artifact must follow:

- assets/feature-template.md

## Versioning

Use:

`Major.Minor`

Update:

- Version
- LastUpdated

Do not maintain history inside the document.

## When to use me

Use this skill when:

- A new business outcome must be delivered
- Existing business behavior changes
- Multiple domains must collaborate
- Operational flow must be formalized

Ask clarifying questions when business outcome, participating domains, responsibilities, orchestration, or acceptance criteria are ambiguous.
