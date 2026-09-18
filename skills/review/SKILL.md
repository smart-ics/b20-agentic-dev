---
name: review
description: Verify implemented plan slices against the implementation plan, architecture, and acceptance criteria
license: Proprietary
compatibility: opencode
metadata:
  audience: reviewer
  artifact: REVIEW
---

## What I do

- Review implemented slices
- Verify implementation against IMPLEMENTATION-PLAN
- Verify implementation against ARCHITECTURE
- Verify adherence to FEATURE acceptance criteria when relevant
- Inspect changed code and affected behavior
- Identify defects, gaps, deviations, and risks
- Record review findings
- Assign a GO or NO-GO decision

## What I do not do

- Implement code
- Modify source code
- Create or modify architecture
- Create or modify implementation plans
- Invent business decisions
- Resolve architecture ambiguity by making decisions
- Assign GO when a slice does not satisfy its required scope
- Assign implementation status transitions

## Inputs

- IMPLEMENTATION-PLAN
- ARCHITECTURE
- FEATURE
- Current Codebase
- Source Code Changes
- Implementation Notes

## Review Principle

Review verifies whether the implemented slice satisfies the approved knowledge and execution plan.

The reviewer must treat:

- DOMAIN as the source of business knowledge
- FEATURE as the source of business outcome and acceptance criteria
- ARCHITECTURE as the source of approved technical realization
- IMPLEMENTATION-PLAN as the source of approved implementation scope

Do not redefine or duplicate knowledge owned by those artifacts.

## Review Scope

For the target slice, verify at minimum:

- Slice objective is satisfied
- All planned scope is implemented
- Complete slice scope and acceptance are verified, not only changed lines
- Implementation follows ARCHITECTURE
- Required dependencies were respected
- No unapproved architectural or business decisions were introduced
- Changed code is consistent with the surrounding codebase
- Relevant error handling and edge cases are addressed
- Behavior is verified; tests are inspected or updated when required by the slice, architecture, acceptance criteria, or risk
- No unrelated or unnecessary changes were introduced
- Implementation does not violate repository boundaries
- Acceptance criteria are satisfied where applicable

## Dependency Verification

Before reviewing the target slice:

- Read its `Depends On` entries from the IMPLEMENTATION-PLAN.
- Verify that each dependency is implemented in the current codebase.
- Verify that declared dependencies are valid Slice IDs in the same plan and form no circular dependency.
- Do not review a slice as independently acceptable when a declared prerequisite is absent.
- A missing or invalid dependency is a review blocker.
- Verify that the implementation did not introduce a prerequisite missing from the plan.
- Report an undeclared prerequisite as a finding; do not modify the plan to resolve it.
- Do not compensate for or implement a missing dependency during review.

## Review Decision

The reviewer must produce exactly one decision:

### GO

Use when the implementation satisfies the slice scope, architecture, and applicable acceptance criteria, with no BLOCKER or MAJOR findings. MINOR and NOTE findings may coexist with GO when they do not prevent the slice from proceeding and are recorded.

### NO-GO

Use when one or more findings require remediation before the slice may proceed.

A NO-GO decision must identify:

- Finding
- Severity
- Evidence
- Required correction

A NO-GO slice returns to Implementation.

## Findings

Findings must be evidence-based and specific.

Each finding should contain:

- ID
- Severity
- Description
- Evidence
- Required correction

Prefer concrete references such as:

- file
- class
- method
- line
- requirement
- architecture section
- plan section
- acceptance criterion

Do not report subjective preferences as defects.

## Severity

Use:

- BLOCKER — prevents acceptance
- MAJOR — significant correctness, architectural, or requirement violation
- MINOR — limited issue that does not fundamentally invalidate the slice
- NOTE — observation with no required correction

Any BLOCKER or MAJOR finding requires NO-GO.

## Review Artifact

Create or update the REVIEW artifact for the reviewed slice.

The REVIEW artifact must record:

- Slice ID
- Review decision
- Reviewed scope
- Findings
- Evidence
- Required corrections
- Review timestamp or iteration identifier when supported

The workflow for an iteration is:

`Implementation` → `Review` → `GO`

or

`Implementation` → `Review` → `NO-GO` → `Remediation` → `Re-Review`

For each review iteration, preserve prior decisions and findings, and record:

- Iteration identifier
- Current decision
- Finding resolution status
- Evidence of remediation when applicable

The REVIEW artifact is working knowledge and does not become a source of permanent business or architectural truth.

## Implementation-Plan State

Review owns the review decision only.

The Implementation skill owns execution progress and status transitions:

`NOT-STARTED` → `IN-PROGRESS` → `READY-FOR-REVIEW`

Rules:

- GO records that the slice passes review; the next workflow transition is performed by its owning workflow actor.
- NO-GO records that remediation is required and returns the slice to Implementation.
- Do not rewrite slice scope, dependencies, ordering, or repository ownership.
- Do not create or split slices.

## Review Integrity

- Review must evaluate the implementation actually present in the current codebase.
- Do not rely only on implementation notes or claimed completion.
- Do not assign GO based on intent; verify observable implementation.
- Do not assign NO-GO solely because implementation differs from personal preference when it remains compliant with the approved architecture and plan.
- Do not silently resolve ambiguity. Record it as a finding or blocker requiring the appropriate decision owner.

## Output

Produce or update:

`<CODE>-REVIEW.md`

Keep the report concise when the slice passes.

For NO-GO, provide sufficient detail for the Implementer to remediate the findings without requiring the reviewer to implement the solution.
