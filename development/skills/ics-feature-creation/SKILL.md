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

### Participating Domains

Participating Domains identify the business capabilities involved in achieving the user outcome.

A Participating Domain may be:

- An existing DOMAIN artifact
- An undocumented existing business capability
- A new DOMAIN candidate discovered during Feature creation

Participating Domains represent required business capabilities.

They do not represent a dependency on existing DOMAIN documents.

The FEATURE references capabilities.

The DOMAIN artifact documents capabilities.

## Independence Rule

DOMAIN and FEATURE are independently creatable artifacts.

FEATURE does not require an existing DOMAIN artifact.

DOMAIN does not require an existing FEATURE artifact.

When both exist, they must remain aligned.

Participating Domains do not require existing DOMAIN artifacts.

FEATURE creation must never be blocked by missing DOMAIN documentation.

A FEATURE may be created even when one or more participating DOMAIN
artifacts do not yet exist.

When a required DOMAIN artifact does not exist:

- Do not refuse FEATURE creation.
- Do not fabricate a complete DOMAIN definition.
- Do not automatically create a DOMAIN artifact unless explicitly requested.
- Record the capability as an undocumented domain or new domain candidate.
- Continue creating the FEATURE focused on the user outcome.

Do not require DOMAIN completion before FEATURE creation.

The FEATURE must remain focused on the user outcome.

### Example

FEATURE:
Doctor Can Complete Discharge Summary

Participating Domains:

- Clinical Documentation (existing DOMAIN artifact)
- Medical Record Management (existing business capability, no DOMAIN artifact yet)

FEATURE creation proceeds normally.

Missing DOMAIN documentation is recorded as knowledge debt, not a blocker.

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
