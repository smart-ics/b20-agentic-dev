---
name: bug-investigation
description: Analyze a BUG ISSUE and document the technical understanding required before architecture work begins
license: Proprietary
compatibility: opencode
metadata:
  audience: architect
  artifact: BUG-INVESTIGATION
---

## What I do

- Analyze a BUG ISSUE
- Understand the reported problem context
- Document current behavior
- Trace relevant execution flow and evidence
- Identify affected components
- Assess business, operational, and technical impact
- Record assumptions and open questions
- Recommend an implementation-neutral correction direction
- Produce a BUG-INVESTIGATION artifact

## What I do not do

BUG-INVESTIGATION does not:

- Accept or analyze CHANGE-REQUEST issues
- Define architecture
- Create implementation plans
- Modify implementation
- Create test strategies
- Approve solutions
- Perform coding activities

Technical realization belongs to ARCHITECTURE.

Implementation planning belongs to IMPLEMENTATION-PLAN.

Testing strategy belongs to the testing artifacts.

## Inputs

### Primary Input

- ISSUE with `Type = BUG`

The BUG ISSUE is required and is the primary source for the investigation. A
CHANGE-REQUEST ISSUE must not be used as a substitute.

### Additional Evidence

- Current implementation
- Existing architecture
- Existing documentation
- Related artifacts
- Available evidence

Additional inputs provide context and evidence. They must not replace the
primary BUG ISSUE.

## Artifact Ownership

BUG-INVESTIGATION owns:

- Current State
- Problem Analysis
- Affected Components
- Impact Assessment
- Assumptions
- Open Questions
- Recommended Direction
- Decision Summary

BUG-INVESTIGATION does not own the target architecture or implementation
decisions. It provides analysis and decision inputs directly to ARCHITECTURE.

## Analysis Boundary

The investigation focuses on understanding the defect:

- What is happening
- Where it is happening
- What components appear affected
- What evidence supports the findings
- What assumptions currently exist
- What information remains unknown
- What correction direction appears reasonable

Do not require definitive root-cause identification when the available evidence
does not support it. Keep recommended directions high-level and
implementation-neutral.

## Workflow Position

```text
ISSUE (BUG)
    ↓
BUG-INVESTIGATION
    ↓
ARCHITECTURE
```

BUG-INVESTIGATION is a first-class Working Knowledge analysis artifact. It is
not a FEASIBILITY-ASSESSMENT and it does not produce one.

## Output

Produce:

`<CODE>-BUG-INVESTIGATION.md`

The artifact must follow:

- assets/bug-investigation-template.md

Knowledge classification:

- Working Knowledge

## Update Rules

When updating an existing BUG-INVESTIGATION:

- Preserve the existing section structure and ordering.
- Preserve supported findings, assumptions, and open-question identifiers.
- Update findings in place when new evidence changes their understanding.
- Do not convert unknown causes into definitive root causes without evidence.
- Keep the Recommended Direction implementation-neutral.
- Update the Decision Summary when investigation readiness changes.

## When to use me

Use this skill when a BUG ISSUE requires technical investigation before
architecture work begins.

Ask clarifying questions when the issue type, observed behavior, evidence,
affected context, or investigation boundary is unclear. Do not infer a
solution or accept a CHANGE-REQUEST as the primary input.
