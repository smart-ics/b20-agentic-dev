---
name: ics-domain-creation
description: Create or update a DOMAIN artifact that defines business knowledge for a bounded context
license: Proprietary
compatibility: opencode
metadata:
  audience: ica-analyst, ica-architect
  artifact: DOMAIN
---

## What I do

- Create a new DOMAIN artifact
- Update an existing DOMAIN artifact
- Define Ubiquitous Language
- Define Domain Capabilities
- Define Actors & Roles
- Define Domain Objects and Aggregates
- Define Business Rules
- Define State Machines and Lifecycles
- Define Domain Events

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

DOMAIN owns business knowledge only. DOMAIN must not own:

- User Outcomes
- Outcome Flows
- Outcome Boundaries
- Domain Orchestration
- Acceptance Criteria
- Technical Realization
- SOPs
- Design software architecture
- Define APIs
- Define database schema
- Define UI or screens
- Define operational procedures

User Outcomes, Outcome Flows, Outcome Boundaries, and Domain Orchestration belong to FEATURE.

Technical Realization belongs to ARCHITECTURE.

SOPs belong to Generated Knowledge.

## Artifact Ownership

DOMAIN owns:

- Ubiquitous Language
- Domain Capabilities
- Actors & Roles
- Domain Objects
- Aggregates
- Business Rules
- State Machines & Lifecycles
- Domain Events

A FEATURE may involve one or more DOMAINs, and a DOMAIN may participate in multiple FEATUREs. FEATURE owns the orchestration between domains. DOMAIN must not define workflows, outcome flows, user outcomes, or orchestration.

Domain Capabilities describe the responsibilities and services provided by the domain (for example: Admission Management, Bed Management, Inventory Management). They are not user outcomes. End-to-end user outcomes such as Process Admission, Transfer Bed, or Discharge Patient belong to FEATURE and must not be modeled in DOMAIN.

## Independence Rule

DOMAIN and FEATURE are independently creatable artifacts.

FEATURE does not require an existing DOMAIN artifact.

DOMAIN does not require an existing FEATURE artifact.

When both exist, they must remain aligned.

A DOMAIN may be created, updated, refined, or formalized
without any existing FEATURE artifact.

Examples:

- Discovery of previously undocumented business knowledge
- Refactoring of domain boundaries
- Formalization of existing business capability
- Knowledge cleanup

## Retrofit Product Reality

Business capabilities may already exist in the product even when
their DOMAIN artifacts have not yet been formally documented.

User outcomes may already exist even when DOMAIN knowledge remains incomplete.

Agents must prioritize accurate knowledge capture and alignment,
not artifact completeness.

## Output

Produce:

`<CODE>-DOMAIN.md`

The artifact must follow:

- assets/domain-template.md

## Versioning

Use:

`Major.Minor`

Update:

- Version
- LastUpdated

Do not maintain history inside the document.

## When to use me

Use this skill when:

- A new domain capability or responsibility is introduced
- A bounded context must be defined
- Existing business rules change
- Existing domain knowledge must be formalized

Ask clarifying questions when business terminology, ownership, lifecycle, or responsibilities are ambiguous.
