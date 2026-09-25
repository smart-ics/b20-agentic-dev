---
name: ics-issue-creation
description: Create an ISSUE artifact that formally captures a request for change or an observed system problem
license: Proprietary
compatibility: opencode
metadata:
  audience: issue intake
  artifact: ISSUE
---

## What I do

- Create a new ISSUE artifact
- Determine whether the ISSUE is a CHANGE-REQUEST or BUG
- Normalize the issue description
- Capture the desired outcome
- Capture the current situation
- Capture available evidence
- Produce a valid ISSUE document

ISSUE is a Working Knowledge artifact and an intake artifact. ISSUE remains
solution-neutral: it captures what change is requested or what problem exists,
not how the solution will be realized.

## What I do not do

- Perform feasibility analysis
- Perform bug investigation
- Perform root-cause analysis
- Define architecture
- Define implementation
- Define testing strategy
- Create implementation plans
- Decide solutions

Detailed analysis belongs to downstream artifacts. This skill does not replace
FEASIBILITY-ASSESSMENT or BUG-INVESTIGATION.

## Artifact Ownership

ISSUE ownership belongs to the role responsible for issue intake and issue
documentation.

ISSUE owns:

- The problem or request
- The desired outcome
- The currently observed behavior or situation
- Supporting evidence, when available

ISSUE does not own business requirements, domain knowledge, architecture
decisions, implementation decisions, implementation planning, or testing
strategy.

## Inputs

Source information may include:

- User request
- Customer request
- Support report
- Operational report
- Review finding
- Test finding
- TEST-EXECUTION failure information
- Problem report
- Enhancement request

The source information may be unstructured. Reorganize, clarify, and normalize
wording and terminology without adding decisions or solutions.

## Test Failure Intake

Issue Creation may be invoked from TEST-EXECUTION.

When TEST-EXECUTION contains a confirmed FAIL requiring corrective action:

* Defect information from TEST-EXECUTION becomes input.
* Issue Creation produces an ISSUE with Type = BUG.
* The ISSUE references the originating TEST-EXECUTION.
* Issue Creation does not modify TEST-EXECUTION.

## Downstream Relationships

```text
ISSUE (CHANGE-REQUEST)
    -> FEASIBILITY-ASSESSMENT

ISSUE (BUG)
    -> BUG-INVESTIGATION
```

This skill creates the ISSUE intake artifact but does not perform either
downstream activity.

## Output

Produce:

`<CODE>-ISSUE.md`

The artifact must follow:

- assets/issue-template.md

Knowledge classification:

- Working Knowledge

ISSUE may be archived according to Working Knowledge lifecycle rules. It is
not Permanent Knowledge.

## When to use me

Use this skill when a request for change or an observed system problem must be
formally captured as an ISSUE.

Ask clarifying questions when the ISSUE type, problem or request, desired
outcome, current situation, or available evidence is ambiguous. Do not infer
how the solution must be implemented.
