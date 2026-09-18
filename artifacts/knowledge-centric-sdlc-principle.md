# Knowledge-Centric SDLC Principles

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

* Business concepts
* Responsibilities
* Business rules
* Lifecycles
* Domain events

DOMAIN is implementation independent.

---

## FEATURE

FEATURE defines a business capability delivered to users.

FEATURE owns:

* Business outcome
* Operational flow
* Participating domains
* Business constraints
* Business exceptions

A FEATURE may involve one or more DOMAINs.

A DOMAIN may participate in multiple FEATUREs.

FEATURE owns the orchestration required to achieve the business outcome.

---

## ARCHITECTURE

ARCHITECTURE defines the technical realization of a FEATURE.

ARCHITECTURE owns:

* Technical structure
* Component responsibilities
* Integration design
* Technical decisions

An ARCHITECTURE realizes exactly one FEATURE.

---

## Domain–Feature–Architecture Relationship

DOMAIN provides business knowledge.

FEATURE consumes and orchestrates one or more DOMAINs to deliver a business outcome.

ARCHITECTURE realizes a FEATURE through software.

The relationship is:

DOMAIN
→ FEATURE
→ ARCHITECTURE

---

## Knowledge Ownership

Each piece of knowledge must have a single owner.

Knowledge must not be duplicated across artifacts.

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
