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
- Maintain implementation progress

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

- Scope
- Phases
- Slices
- Dependencies
- Progress

## Planning Principles

Each slice must:

- Have a clear objective
- Be independently executable
- Be reviewable
- Produce observable progress
- Target exactly one repository

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
- Preserve Slice IDs
- Never renumber existing phases
- Never renumber existing slices
- Update progress in place
- Do not recreate completed slices
- Do not split completed slices
- Preserve document structure
- Preserve section ordering
- Preserve repository ownership of existing slices
- Do not merge slices from different repositories
- Do not create cross-repository slices

## Progress Rules

Progress must be tracked within the same document.

Allowed statuses:

- NOT-STARTED
- IN-PROGRESS
- BLOCKED
- COMPLETED

## Output

Produce:

<CODE>-IMPLEMENTATION-PLAN.md

Use:

assets/implementation-plan-template.md