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
- Perform impact analysis covering business, operational, and technical impact
- Evaluate alternative correction approaches
- Manage assumptions and open questions
- Make and document investigation decisions
- Recommend the selected implementation-neutral correction approach
- Produce a BUG-INVESTIGATION artifact

BUG-INVESTIGATION is an analysis artifact. Its purpose is to understand the
defect, evaluate alternatives, resolve investigation questions, and produce a
recommended correction approach. The resulting decisions become authoritative
inputs to ARCHITECTURE.

## What I do not do

BUG-INVESTIGATION does not:

- Accept or analyze CHANGE-REQUEST issues
- Define architecture
- Create implementation plans
- Modify implementation
- Create test strategies
- Define technical realization
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
- Alternative Evaluation
- Recommended Decision
- Decision
- Decision Rationale

BUG-INVESTIGATION owns investigation decisions. It does not own the target
architecture or technical realization. It provides authoritative decisions
directly to ARCHITECTURE.

## Analysis Boundary

The investigation focuses on understanding the defect:

- What is happening
- Where it is happening
- What components appear affected
- What evidence supports the findings
- What assumptions currently exist
- What information remains unknown
- What alternatives were evaluated
- What investigation decision was selected and why

Do not require definitive root-cause identification when the available evidence
does not support it. Investigation decisions must remain high-level and
implementation-neutral.

## Workflow Position

```text
ISSUE (BUG)
    ↓
BUG-INVESTIGATION
    ↓
Decision
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

A BUG-INVESTIGATION artifact must contain sufficient decisions for
ARCHITECTURE to proceed without re-investigating the defect.

Knowledge classification:

- Working Knowledge

## Update Rules

When updating an existing BUG-INVESTIGATION:

- Preserve the existing section structure and ordering.
- Preserve supported findings, assumptions, and open-question identifiers.
- Update findings in place when new evidence changes their understanding.
- Do not convert unknown causes into definitive root causes without evidence.
- Keep the Recommended Decision implementation-neutral.
- Update the Decision and Decision Rationale when investigation conclusions
  change.

## When to use me

Use this skill when a BUG ISSUE requires technical investigation before
architecture work begins.

Ask clarifying questions when the issue type, observed behavior, evidence,
affected context, or investigation boundary is unclear. Do not infer a
solution or accept a CHANGE-REQUEST as the primary input.
