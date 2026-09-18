---
name: implementation-plan
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
- Define repository ownership
- Define plan structure

## What I do not do

IMPLEMENTATION-PLAN does not:

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
- Repository Ownership
- Plan Structure

## Ownership Boundary

The IMPLEMENTATION-PLAN artifact is created by the Planning skill.

During Planning, the Architect may revise its structure.

After the plan is approved for execution, its structure is immutable to
Implementer and Reviewer.

If structural correction is required after execution approval, the
Architect must create a replacement IMPLEMENTATION-PLAN through a new
Planning cycle. The approved plan is not modified.

Execution progress, implementation status, review status, and delivery state transitions are owned by the Implementer and Reviewer skills.

The Planning skill defines the structure of the plan, not the execution state or review outcome of the plan.

## Planning Principles

Each slice must:

- Have a clear objective
- Be independently executable
- Be reviewable
- Produce observable progress
- Target exactly one repository

Slice IDs must be globally unique within an IMPLEMENTATION-PLAN.

Slice numbering is continuous across all phases.

## Status Model

Implementation status and review status are separate fields.

### Slice Implementation Status

Allowed slice implementation statuses are:

- NOT-STARTED
- IN-PROGRESS
- IMPLEMENTED
- BLOCKED

IMPLEMENTED means implementation work is finished and the slice is ready for review. IMPLEMENTED is an implementation status only and does not indicate that the slice has passed review.

The implementation lifecycle is:

```text
NOT-STARTED
    ↓
IN-PROGRESS
    ↓
IMPLEMENTED
```

or:

```text
IN-PROGRESS
    ↓
BLOCKED
```

### Slice Review Status

Allowed slice review statuses are:

- NOT-REVIEWED
- GO
- NO-GO

Review owns the review status. Implementers must not assign GO or NO-GO. Reviewers assign GO or NO-GO independently and must not modify the slice implementation status.

### Plan Status

COMPLETED is a plan-level status only. A plan is COMPLETED only when every slice has implementation status IMPLEMENTED and review status GO.

COMPLETED must not be used as a slice implementation status.

Testing and test-package creation must not begin until the IMPLEMENTATION-PLAN is COMPLETED. An individual slice with review status GO is not a testing entry condition.

## Versioning

Use:

`Major.Minor`

Update:

- Version
- LastUpdated

Increment Minor for:

- Non-structural planning updates, such as dependency updates or small
  planning adjustments made before execution approval

Increment Major for:

- Significant replanning
- Major scope changes
- Phase restructuring

Version increments apply only to planning work performed before
execution approval.

Once execution approval is granted:

- Structural changes are not performed through version increments.
- Structural correction requires a new Planning cycle.
- The result is a replacement IMPLEMENTATION-PLAN.

Git maintains history.

Do not maintain version history inside the artifact.

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

Once an IMPLEMENTATION-PLAN has been approved for execution, its phase
and slice structure is fixed.

Planning, Implementation, and Review must never split an existing slice
during execution or review.

If a slice is discovered to be too large, incorrectly scoped, missing
necessary work, or otherwise unsuitable for execution, the existing plan
must not be decomposed or patched into child slices.

The Architect must create a replacement IMPLEMENTATION-PLAN through a
new Planning cycle.

The replacement plan becomes the new authoritative execution plan.

No child Slice IDs or slice-decomposition suffixes are used.

## Replacement Plan Rule

If an approved IMPLEMENTATION-PLAN is found to be structurally
insufficient:

- Do not structurally modify the approved plan.
- Do not patch the approved plan.
- Do not insert, remove, split, merge, or reorder slices.
- Do not alter dependencies to accommodate execution findings.

Instead:

- Start a new Planning cycle.
- Produce a new authoritative IMPLEMENTATION-PLAN.
- Treat the previous plan as historical record.

The new plan replaces the previous plan for future execution.

The previous plan remains preserved through repository history.

A slice should be executable by a low-reasoning implementation agent.

## Dependency Declaration

Every slice must have a `Depends On` field.

Use `Depends On: None` when the slice has no dependency.

When dependencies exist, list the required Slice IDs explicitly.

Example:

`Depends On: P1-S01, P1-S03`

## Dependency-Driven Parallelism

- Analyze dependencies across all slices.
- Arrange slices with no dependency on each other so they can be implemented in parallel.
- A slice must depend only on slices whose output is genuinely required before its implementation can begin.
- Do not introduce dependencies merely because slices are in the same phase or because one slice has a lower ID.

## Execution Order

- Derive execution order from the dependency graph.
- Independent slices may execute concurrently.
- Position a dependent slice after all of its dependencies.
- Avoid unnecessary serialization.

## Dependency Integrity

- Every referenced dependency must be a valid Slice ID in the same IMPLEMENTATION-PLAN.
- Do not create circular dependencies.
- Do not use phase IDs as dependencies.
- Dependencies must represent implementation prerequisites, not conceptual relationships.

## Planning Objective

- Minimize unnecessary sequential work while preserving correctness.
- Prefer the smallest dependency set sufficient for safe implementation.

## Repository Boundary Principle

A slice must target exactly one repository.

A slice must not require changes across multiple repositories.

When a FEATURE spans multiple repositories, create separate slices for each repository.

Example:

P2-S01  BTrade API
P2-S02  Android App
P2-S03  Web Admin

instead of:

P2-S01  API + Android + Web

## Update Rules

During Planning:

- The Architect may revise plan structure.

After execution approval:

- Preserve Phase IDs.
- Preserve Slice IDs.
- Do not add slices.
- Do not split slices.
- Do not create child Slice IDs.
- Do not merge slices.
- Do not reorder slices.
- Do not reinterpret slice scope.

Structural correction requires a replacement IMPLEMENTATION-PLAN created
through a new Planning cycle. The approved plan remains unchanged.

## Output

Produce:

<CODE>-IMPLEMENTATION-PLAN.md

Use:

assets/implementation-plan-template.md
