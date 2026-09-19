---
name: review
description: Independently verify implemented slices and assign GO or NO-GO
license: Proprietary
compatibility: opencode
metadata:
  audience: reviewer
  artifact: REVIEW
---

## What I do

- Review implemented slices
- Verify against IMPLEMENTATION-PLAN
- Verify against ARCHITECTURE
- Verify FEATURE acceptance criteria when applicable
- Identify findings
- Assign GO or NO-GO

## What I do not do

- Implement code
- Modify source code
- Modify DOMAIN, FEATURE, ARCHITECTURE, or IMPLEMENTATION-PLAN structure,
  implementation status, or planning fields
- Create or split slices
- Assign implementation progress status
- Make business or architecture decisions

## Inputs

Required:

- IMPLEMENTATION-PLAN
- ARCHITECTURE
- Current Codebase

Optional:

- FEATURE
- Implementation Notes

## Review Scope

Verify:

- Slice objective
- Planned scope
- Architecture compliance
- Dependencies
- Acceptance criteria
- Repository ownership
- Error handling and edge cases when applicable

## Review Decision

### GO

Use when:

- Scope is satisfied
- Architecture is satisfied
- Dependencies are satisfied
- No BLOCKER or MAJOR findings exist

### NO-GO

Use when remediation is required.

A NO-GO finding must contain:

- ID
- Severity
- Description
- Evidence
- Required Correction

The Required Correction applies to the implementation.

It must not instruct the implementer to:

- split slices
- add slices
- remove slices
- reorder slices
- merge slices
- restructure the plan

If the approved plan is structurally insufficient:

- Record the issue as a finding.
- Escalate it to the Architect.

Review does not modify plan structure.

Review identifies planning defects but does not correct them.

When a planning defect is discovered:

- Record the finding.
- Escalate to the Architect.

Review does not restructure the approved plan.

## Severity

- BLOCKER
- MAJOR
- MINOR
- NOTE

BLOCKER or MAJOR requires NO-GO.

## Review Artifact Policy

Review status is authoritative in IMPLEMENTATION-PLAN. The Reviewer updates the
slice review status there; REVIEW is Working Knowledge used to preserve review
evidence rather than the current review state.

Review evidence must remain verifiable from implementation outputs,
ARCHITECTURE, IMPLEMENTATION-PLAN, and available execution records.

### GO

- Update IMPLEMENTATION-PLAN review status to GO.
- Do not create a REVIEW artifact solely to record the GO decision.
- A GO decision does not require a REVIEW artifact; its absence is intentional,
  not an omission.
- A GO decision completes only the reviewed slice. It does not authorize testing.
- Testing is authorized only when the IMPLEMENTATION-PLAN is COMPLETED: every slice is IMPLEMENTED and every slice review status is GO.

### NO-GO

Create or update:

`<CODE>-REVIEW.md`

The REVIEW artifact records:

- Findings
- Evidence
- Required Corrections
- Resolution Status
- Remediation History
- Review Iterations

Create a REVIEW artifact only when review findings, required corrections,
remediation history, or re-review evidence must be preserved.

## Re-Review

- Reuse the existing REVIEW artifact.
- Preserve previous findings.
- Update resolution status.
- Record remediation evidence.
- Review against the same approved slice definition.
- Do not change slice structure during re-review.

If remediation requires changing the approved slice structure or scope,
stop remediation and escalate for a new Planning cycle.

## Review Integrity

- Review actual implementation, not claims.
- Verify observable behavior.
- Do not assign GO based on intent.
- Do not assign NO-GO based on personal preference.

## Output

GO:
- Updated IMPLEMENTATION-PLAN

NO-GO:
- Updated REVIEW artifact
