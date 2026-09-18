---
name: domain-creation
description: Create or update a DOMAIN artifact that defines business knowledge for a bounded context
license: Proprietary
compatibility: opencode
metadata:
  audience: analyst, architect
  artifact: DOMAIN
---

## What I do

- Create a new DOMAIN artifact
- Update an existing DOMAIN artifact
- Define Ubiquitous Language
- Define Business Capabilities
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
- Business Capabilities
- Actors & Roles
- Domain Objects
- Aggregates
- Business Rules
- State Machines & Lifecycles
- Domain Events

A FEATURE may involve one or more DOMAINs, and a DOMAIN may participate in multiple FEATUREs. FEATURE owns the orchestration between domains. DOMAIN must not define workflows, operational flows, business outcomes, or orchestration.

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

- A new business capability is introduced
- A bounded context must be defined
- Existing business rules change
- Existing domain knowledge must be formalized

Ask clarifying questions when business terminology, ownership, lifecycle, or responsibilities are ambiguous.
