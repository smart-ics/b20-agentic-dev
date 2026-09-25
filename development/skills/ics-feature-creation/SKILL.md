---
name: ics-feature-creation
description: Create or update a FEATURE artifact that defines a business outcome, outcome flow, and domain orchestration
license: Proprietary
compatibility: opencode
metadata:
  audience: ica-analyst, ica-architect
  artifact: FEATURE
---

## What I do

- Create a new FEATURE artifact
- Update an existing FEATURE artifact
- Define business outcomes
- Define outcome boundaries
- Define participating domains
- Define outcome flow
- Define domain orchestration
- Define business constraints
- Define business exceptions
- Define acceptance criteria

## Classification Rules

DOMAIN asks:

"What capability does this part of the business provide?"

FEATURE asks:

"What valuable outcome can a user achieve?"

If the knowledge can exist independently of a specific user outcome,
it belongs to DOMAIN.

If the knowledge describes a user achieving a meaningful result,
it belongs to FEATURE.

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
- Outcome Boundary
- Participating Domains
- Outcome Flow
- Domain Orchestration
- Feature Constraints
- Feature Exceptions
- Acceptance Criteria

Outcome Boundary defines:

- Outcome Start
- Outcome End

Outcome Flow
    = Flow required to achieve the user outcome

A FEATURE may involve one or more DOMAINs.

A DOMAIN may participate in multiple FEATUREs.

## Independence Rule

DOMAIN and FEATURE are independently creatable artifacts.

FEATURE does not require an existing DOMAIN artifact.

DOMAIN does not require an existing FEATURE artifact.

When both exist, they must remain aligned.

A FEATURE may be created even when one or more participating DOMAIN
artifacts do not yet exist.

Missing DOMAIN definitions must never block FEATURE creation.

When a required domain is missing:

Identify the missing domain as:

- Existing but undocumented, or
- New domain candidate

Do not fabricate a full DOMAIN definition inside the FEATURE artifact.

Do not refuse to create the FEATURE.

Do not require DOMAIN completion before FEATURE creation.

The FEATURE must remain focused on the user outcome.

## Retrofit Product Reality

Business capabilities may already exist in the product even when
their DOMAIN artifacts have not yet been formally documented.

User outcomes may already exist even when DOMAIN knowledge remains incomplete.

Agents must prioritize accurate knowledge capture and alignment,
not artifact completeness.

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

- A new user outcome must be delivered
- Existing business behavior changes
- Multiple domains must collaborate
- Outcome flow must be formalized

Ask clarifying questions when business outcome, participating domains, responsibilities, orchestration, or acceptance criteria are ambiguous.
