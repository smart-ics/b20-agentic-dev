---
name: implementation-planning
description: Create and maintain an executable implementation plan that transforms the current system into the approved target architecture
license: Proprietary
compatibility: opencode
metadata:
  audience: architect
  artifact: IMPLEMENTATION-PLAN
---

## What I do

- Analyze target architecture
- Analyze current codebase
- Determine implementation delta
- Create implementation phases
- Create implementation slices
- Define execution order
- Define dependencies
- Define repository boundaries
- Define repository ownership
- Define plan structure

## What I do not do

IMPLEMENTATION-PLANNING does not:

- Define business knowledge
- Define business outcomes
- Perform feasibility analysis
- Define target architecture
- Implement code
- Review code

## Inputs

- FEATURE
- ARCHITECTURE
- FEASIBILITY-ASSESSMENT (READY-FOR-PLANNING)
- Current Codebase

## Artifact Ownership

IMPLEMENTATION-PLAN owns:

- Implementation Scope
- Phase Breakdown
- Slice Breakdown
- Execution Order
- Dependencies
- Repository Boundaries
- Slice Design
- Plan Structure

## Ownership Boundary

The IMPLEMENTATION-PLAN artifact is created and maintained by the Planning skill.

Execution progress, implementation status, review status, and delivery state transitions are owned by the Implementer and Reviewer skills.

The Planning skill defines the structure of the plan, not the execution state of the plan.

## Planning Principles

Each slice must:

- Have a clear objective
- Be independently executable
- Be reviewable
- Produce observable progress
- Target exactly one repository

Slice IDs must be globally unique within an IMPLEMENTATION-PLAN.

Slice numbering is continuous across all phases.

Example:

```text
P1-S01
P1-S02
P1-S03
P2-S04
P2-S05
P3-S06
```

Do not reset slice numbering per phase.

A slice may be split during implementation or review.

The original Slice ID remains reserved.

Child slices are created by appending a lowercase suffix.

Examples:

```text
P2-S18
    ↓
P2-S18a
P2-S18b

P3-S24
    ↓
P3-S24a
P3-S24b
P3-S24c
```

Use suffixes only for slice decomposition. Do not create a new sequential Slice ID when the work is a refinement of an existing slice.

Allowed examples:

```text
P1-S01
P1-S02
P2-S18
P2-S18a
P2-S18b
P3-S24
```

Invalid examples:

```text
P2-S18.1
P2-S18-1
P2-S19   (when it is actually a split of P2-S18)
```

A slice should be executable by a low-reasoning implementation agent.

## Repository Boundary Principle

A slice must target exactly one repository.

A slice must not require changes across multiple repositories.

When a FEATURE spans multiple repositories, create separate slices for each repository.

Example:

P2-S1  BTrade API
P2-S2  Android App
P2-S3  Web Admin

instead of:

P2-S1  API + Android + Web

## Update Rules

When updating an existing IMPLEMENTATION-PLAN:

- Preserve Phase IDs
- Preserve existing Slice IDs
- Never renumber existing phases
- Never renumber existing Slice IDs
- New slices must continue the next available Slice Number
- When splitting a slice, preserve the original Slice ID
- Child slices must use suffixes: a, b, c, ...
- Do not create duplicate Slice IDs
- Do not modify completed phases or slices unless explicitly requested
- Preserve repository ownership of existing slices
- Do not merge slices from different repositories
- Do not create cross-repository slices
- Preserve document structure
- Preserve section ordering

## Output

Produce:

<CODE>-IMPLEMENTATION-PLAN.md

Use:

assets/implementation-plan-template.md
