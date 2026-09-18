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