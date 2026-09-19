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
- Assign GO or NO-GO decisions

## Inputs

- IMPLEMENTATION-PLAN
- ARCHITECTURE
- Current Codebase

## Dependency Preflight

Before implementing a slice:

- Read all `Depends On` entries in the IMPLEMENTATION-PLAN.
- Verify that every referenced Slice ID exists in the same IMPLEMENTATION-PLAN.
- Verify that every referenced slice has implementation status IMPLEMENTED.
- Verify that the implementation output required by every dependency exists and
  is verifiable in the target repository.
- Treat the dependency as satisfied only when all of these conditions hold.
- Do not infer dependency satisfaction solely from similar code being present.
- A slice with no dependencies passes the preflight.

Dependency satisfaction is determined solely by implementation status and
required implementation output. Review status (NOT-REVIEWED, GO, or NO-GO) does
not participate: IMPLEMENTED with NOT-REVIEWED, IMPLEMENTED with GO, and
IMPLEMENTED with NO-GO each satisfy a dependency. NO-GO affects review acceptance and plan
completion only; it does not invalidate dependency satisfaction.

If any dependency is not satisfied:

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
IMPLEMENTED

or

IN-PROGRESS
    ↓
BLOCKED

Remediation after NO-GO:

IMPLEMENTED
    ↓
IN-PROGRESS
    ↓
IMPLEMENTED

IMPLEMENTED means implementation work is finished and the slice is ready for review.

Implementation does not assign GO or NO-GO. It only moves a slice to
IMPLEMENTED or BLOCKED.

The review status is separate from implementation status. A slice starts with
review status NOT-REVIEWED. Reviewers independently assign GO or NO-GO and do
not modify the slice implementation status.

## Update Rules

- Preserve Phase IDs
- Preserve Slice IDs
- Preserve slice ordering
- Update assigned slices only
- Mark assigned slices IMPLEMENTED when implementation work is finished
- Do not assign GO or NO-GO
- Do not renumber slices
- Do not modify plan structure
- Do not create new slices
- Do not split slices
- Record blockers explicitly

If the approved plan cannot be executed as written:

- Do not alter the plan.
- Do not split the slice.
- Do not reinterpret the slice into a different scope.
- Mark the slice BLOCKED when execution is blocked by the plan.
- Record the reason for the blockage.
- Escalate the plan problem to the Architect.

The Architect must address a structurally insufficient plan through a
new Planning cycle.

The existing plan is never patched during implementation.

If the approved plan is structurally insufficient:

- Stop execution of the affected work.
- Report the issue.
- Escalate to the Architect.

Do not attempt to repair planning defects through implementation.

Implementation corrects code.
Planning corrects plans.

## Output

No new artifact is created.

Updates:

- IMPLEMENTATION-PLAN
- Codebase
