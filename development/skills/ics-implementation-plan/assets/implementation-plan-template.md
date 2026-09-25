---
Title:
Code:
Artifact: IMPLEMENTATION-PLAN
Version:
LastUpdated:
Status: NOT-STARTED | IN-PROGRESS | BLOCKED | COMPLETED
Execution Approval: PENDING | APPROVED
---

# 1. Objective

Feature being implemented.

Referenced artifacts:

- FEATURE
- ARCHITECTURE (if applicable)
- FEASIBILITY-ASSESSMENT

Architecture Applicability: [ARCHITECTURE-REQUIRED | ARCHITECTURE-NOT-REQUIRED]

(If ARCHITECTURE-NOT-REQUIRED, explicitly record: "No architectural target-state artifact was required. Implementation relies on existing technical structure. Approved feasibility decisions are authoritative for the change. The current codebase is the source of current technical truth.")

---

# 2. Planning Scope

Describe the implementation scope.

---

# 3. Dependencies

External dependencies.

For slice dependencies:

- `Depends On` declares implementation prerequisites.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires the referenced slice to have implementation
  status IMPLEMENTED.
- Dependency satisfaction does not require review status GO.
- Dependencies must represent real implementation prerequisites.

---

# 4. Progress Summary

Plan status values are:

- NOT-STARTED
- IN-PROGRESS
- BLOCKED
- COMPLETED

Execution Approval values are:

- PENDING
- APPROVED

Execution Approval is owned by the Architect. It is PENDING during Planning
and set to APPROVED when the plan is released for execution. Execution must
not begin while Execution Approval is PENDING.

COMPLETED is a plan-level status only. Set it only when every slice has
implementation status IMPLEMENTED and review status GO.

Testing and test-package creation must not begin until the plan is COMPLETED.
An individual slice with review status GO is not a testing entry condition.

Slice implementation status values are:

- NOT-STARTED
- IN-PROGRESS
- IMPLEMENTED
- BLOCKED

Slice review status values are:

- NOT-REVIEWED
- GO
- NO-GO

| Phase | Implementation Status | Review Status | Progress |
|---------|---------|---------|---------|
| P1 | IMPLEMENTED | GO | 5/5 |
| P2 | IN-PROGRESS | NOT-REVIEWED | 2/5 |

---

# 5. Phases

## P1 - Foundation

Implementation Status: IMPLEMENTED
Review Status: GO

### P1-S01

Title:

Implementation Status: IMPLEMENTED
Review Status: GO

Objective:

Depends On: None

Repository:

Completion Criteria:

Describe observable implementation outputs that downstream slices may rely
upon. These outputs are evidence used to verify dependency satisfaction.

Notes:

---

### P1-S02

...

---

## P2 - Application Layer

Implementation Status: IN-PROGRESS
Review Status: NOT-REVIEWED

### P2-S03

...

---

# 6. Change Log

Optional planning notes.

Keep concise.
