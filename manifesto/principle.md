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

DOMAIN defines business knowledge.

DOMAIN owns:

* Ubiquitous language
* Domain capabilities
* Actors and roles
* Domain objects and aggregates
* Business rules
* State machines and lifecycles
* Domain events

DOMAIN is implementation independent.

DOMAIN does not own business outcomes, operational flows, domain orchestration, or technical realization.

---

## FEATURE

FEATURE defines a business outcome delivered to users.

FEATURE describes how one or more DOMAINs collaborate to achieve that outcome.

FEATURE owns:

* Business outcome
* Operational flow
* Domain orchestration
* Participating domains
* Business constraints
* Business exceptions
* Acceptance criteria

---

## ARCHITECTURE

ARCHITECTURE defines the technical realization of a FEATURE or BUG correction.

ARCHITECTURE owns:

* Technical structure
* Component responsibilities
* Integration design
* Technical decisions

An ARCHITECTURE realizes exactly one FEATURE or BUG correction.

---

## Domain–Feature–Architecture Relationship

The core relationship is:

```text
DOMAIN
    = Business Knowledge

FEATURE
    = Business Outcome
      Operational Flow
      Domain Orchestration

ARCHITECTURE
    = Technical Realization
```

Rules:

* A FEATURE may involve one or more DOMAINs.
* A DOMAIN may participate in multiple FEATUREs.
* FEATURE owns orchestration between participating DOMAINs but does not own domain knowledge or technical realization.
* ARCHITECTURE realizes a FEATURE or BUG correction.
* Each piece of knowledge has a single authoritative owner.
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
    → ARCHITECTURE

ISSUE (BUG)
    → BUG-INVESTIGATION
    → ARCHITECTURE
```

ISSUE ownership belongs to the role responsible for issue intake and issue
documentation. It is not owned by the Architect, Implementer, Reviewer, or
Tester.

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

A capability is considered fully delivered when DOMAIN, FEATURE, and ARCHITECTURE accurately describe the implemented solution.

For delivery purposes, IMPLEMENTATION-PLAN is COMPLETED only when every slice
has implementation status IMPLEMENTED and review status GO. Testing cannot
begin before the plan reaches COMPLETED.
