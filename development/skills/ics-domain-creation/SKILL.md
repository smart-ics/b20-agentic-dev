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
- Define Domain Capabilities (domain responsibilities and services)
- Define Actors & Roles
- Define Domain Objects and Aggregates
- Define Business Rules
- Define State Machines and Lifecycles
- Define Domain Events

## What I do not do

DOMAIN owns business knowledge only. DOMAIN does not own:

- Business Outcomes
- Operational Flows
- Domain Orchestration
- Technical Realization
- SOPs
- Design software architecture
- Define APIs
- Define database schema
- Define UI or screens
- Define operational procedures

Business Outcomes, Operational Flows, and Domain Orchestration belong to FEATURE.

Technical Realization belongs to ARCHITECTURE.

SOPs belong to Generated Knowledge.

## Artifact Ownership

DOMAIN owns:

- Ubiquitous Language
- Domain Capabilities (domain responsibilities and services)
- Actors & Roles
- Domain Objects
- Aggregates
- Business Rules
- State Machines & Lifecycles
- Domain Events

A FEATURE may involve one or more DOMAINs, and a DOMAIN may participate in multiple FEATUREs. FEATURE owns the orchestration between domains. DOMAIN must not define workflows, operational flows, business outcomes, or orchestration.

Domain Capabilities describe the responsibilities and services provided by the domain (for example: Admission Management, Bed Management, Inventory Management). They are not user-facing business capabilities. End-to-end capabilities such as Process Admission, Transfer Bed, or Discharge Patient belong to FEATURE and must not be modeled in DOMAIN.

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
