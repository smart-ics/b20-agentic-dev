# Principles

## Name

Knowledge-Centric SDLC

## Purpose

This SDLC is designed to build and evolve software through durable knowledge.

The objective is to preserve the business and technical knowledge required to understand, implement, operate, and maintain the system over time.

---

## Permanent Knowledge

The system is defined by three permanent artifacts:

* DOMAIN
* FEATURE
* ARCHITECTURE

These artifacts form the authoritative knowledge base of the product.

---

## DOMAIN

DOMAIN = Defined Business Capability

DOMAIN answers: "What can this part of the business do?"

DOMAIN owns:

* Ubiquitous language
* Domain capabilities
* Actors and roles
* Domain objects and aggregates
* Business rules
* State machines and lifecycles
* Domain events

DOMAIN is implementation independent.

DOMAIN does not own user outcomes, cross-domain orchestration, operational workflows, or technical realization.

---

## FEATURE

FEATURE = Defined User Outcome

FEATURE answers: "What valuable outcome can a user achieve?"

FEATURE owns:

* User outcome
* User-facing behavior
* Operational flow
* Participating domains
* Domain orchestration
* Feature-specific constraints
* Feature-specific exceptions
* Acceptance criteria

FEATURE is implementation independent.

FEATURE does not own domain capabilities, domain business rules, domain lifecycles, or technical realization. Those remain owned by DOMAIN or ARCHITECTURE.

---

## Feature Boundary

A FEATURE represents a complete user outcome and must have:

* Outcome Start
* Outcome End

A FEATURE is not:

* a screen
* a menu
* a button
* an API endpoint
* a database operation
* a technical component

Those are implementation concerns. A FEATURE remains valid regardless of how many screens, APIs, services, components, or technical operations are required to realize it.

---

## ARCHITECTURE

ARCHITECTURE defines the technical realization of a FEATURE or BUG correction.

ARCHITECTURE owns:

* Technical structure
* Component responsibilities
* Integration design
* Technical decisions

An ARCHITECTURE realizes exactly one FEATURE or BUG correction. ARCHITECTURE realizes a FEATURE outcome; ARCHITECTURE does not realize a DOMAIN.

Missing ARCHITECTURE artifact is a signal to evaluate architecture applicability, not an instruction to create ARCHITECTURE.
An ARCHITECTURE artifact is required only when the change requires formal technical target-state definition.
If an ARCHITECTURE artifact already exists for the affected capability, it remains authoritative.

---

## Domain–Feature–Architecture Relationship

The core conceptual model is:

```text
DOMAIN
    = Defined Business Capability

FEATURE
    = Defined User Outcome

ARCHITECTURE
    = Technical Realization
```

The relationship is:

```text
DOMAIN
    provides capability
        ↓
FEATURE
    produces user outcome
        ↓
ARCHITECTURE
    realizes the outcome technically
```

Rules:

* A DOMAIN provides capability.
* A FEATURE consumes one or more DOMAIN capabilities.
* A FEATURE produces a user outcome.
* A FEATURE may involve one DOMAIN or multiple DOMAINs.
* A DOMAIN may participate in multiple FEATUREs.
* A FEATURE may be realized entirely by a single DOMAIN capability; a FEATURE is not required to span multiple domains.
* FEATURE owns orchestration between participating DOMAINs but does not own domain capabilities, domain knowledge, or technical realization.
* ARCHITECTURE realizes a FEATURE or BUG correction. ARCHITECTURE does not realize a DOMAIN.
* Each piece of knowledge has a single authoritative owner (DOMAIN → Business Capability, FEATURE → User Outcome, ARCHITECTURE → Technical Realization).
* Knowledge must not be duplicated across artifacts.

---

## ISSUE

ISSUE is a Working Knowledge artifact that represents a request for change.

ISSUE may be one of:

* CHANGE-REQUEST
* BUG

ISSUE is the intake artifact used to formally capture a requested system
change or an observed system problem.

ISSUE captures:

* The problem or request
* The desired outcome
* The currently observed behavior or situation
* Supporting evidence, when available

ISSUE is intentionally lightweight and solution-neutral. It describes what
is being requested or what problem exists, but does not prescribe how the
solution must be implemented.

ISSUE is not Permanent Knowledge. It may be archived according to Working
Knowledge lifecycle rules.

ISSUE does not own:

* Business requirements
* Domain knowledge
* Architecture decisions
* Implementation decisions
* Implementation planning
* Testing strategy

Those concerns are owned by downstream artifacts. Detailed analysis belongs
in FEASIBILITY-ASSESSMENT and BUG-INVESTIGATION. ISSUE serves as the entry
point for those analyses rather than replacing them.

CHANGE-REQUEST issues are analyzed through FEASIBILITY-ASSESSMENT. BUG
issues are analyzed through BUG-INVESTIGATION. The outputs of both analyses
are consumed by ARCHITECTURE:

```text
ISSUE (CHANGE-REQUEST)
    → FEASIBILITY-ASSESSMENT
    → ARCHITECTURE APPLICABILITY
    ├── REQUIRED → ARCHITECTURE
    └── NOT REQUIRED → (no architecture artifact)

ISSUE (BUG)
    → BUG-INVESTIGATION
    → ARCHITECTURE APPLICABILITY
    ├── REQUIRED → ARCHITECTURE
    └── NOT REQUIRED → (no architecture artifact)
```

ISSUE ownership belongs to the role responsible for issue intake and issue
documentation. It is not owned by the ica-architect, ica-implementer, ica-reviewer, or
ica-tester.

---

## Knowledge Ownership

When knowledge changes, the owning artifact must be updated.

Other artifacts must reference the owner rather than redefine it.

---

## Working Knowledge

Artifacts created to support analysis, planning, implementation, review, testing, and deployment are considered Working Knowledge.

Working Knowledge exists to support delivery activities and may be archived or removed after its purpose has been fulfilled.

---

## Generated Knowledge

Artifacts such as SOPs, User Guides, Training Materials, and Release Notes are Generated Knowledge.

Generated Knowledge is derived from Permanent Knowledge and must not become an independent source of truth.

Whenever possible, Generated Knowledge should be regenerated from DOMAIN, FEATURE, and ARCHITECTURE.

---

## Completion

A capability is considered fully delivered when DOMAIN, FEATURE, and ARCHITECTURE (if applicable) accurately describe the implemented solution.
If implementation materially changes architectural knowledge, ARCHITECTURE must be created or updated before the delivery is considered complete. If the implementation does not change architectural knowledge, no new ARCHITECTURE artifact is required.

For delivery purposes, IMPLEMENTATION-PLAN is COMPLETED only when every slice
has implementation status IMPLEMENTED and review status GO. Testing cannot
begin before the plan reaches COMPLETED.
