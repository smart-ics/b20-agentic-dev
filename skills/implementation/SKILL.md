---
name: implementation
description: Implement approved plan slices by modifying the codebase and updating implementation progress
license: Proprietary
compatibility: opencode
metadata:
  audience: implementer
  artifact: NONE
---

## What I do

- Implement approved slices
- Modify code
- Update slice progress
- Record implementation notes
- Record changed files
- Report blockers

## What I do not do

- Create plans
- Modify plan structure
- Create architecture
- Modify architecture
- Perform reviews
- Approve completion

## Inputs

- IMPLEMENTATION-PLAN
- ARCHITECTURE
- Current Codebase

## Dependency Preflight

Before implementing a slice:

- Inspect its `Depends On` entries in the IMPLEMENTATION-PLAN.
- Verify that every dependency is implemented in the current codebase.
- A slice with no dependencies passes the preflight.

If any dependency is not implemented:

- Stop immediately.
- Do not modify source code.
- Do not partially implement the slice.
- Do not bypass the dependency.
- Do not implement the missing dependency as part of the target slice.
- Keep the target slice `NOT-STARTED`.
- Report the blocked slice and all missing dependencies.

Proceed only when all dependencies are implemented.

## Progress Rules

Allowed transitions:

NOT-STARTED
    ↓
IN-PROGRESS
    ↓
READY-FOR-REVIEW

or

IN-PROGRESS
    ↓
BLOCKED

Implementation cannot mark a slice COMPLETED.

## Update Rules

- Preserve Phase IDs
- Preserve Slice IDs
- Preserve slice ordering
- Update assigned slices only
- Do not renumber slices
- Do not modify plan structure
- Do not create new slices
- Do not split slices
- Record blockers explicitly

## Output

No new artifact is created.

Updates:

- IMPLEMENTATION-PLAN
- Codebase
