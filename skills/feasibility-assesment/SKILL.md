---
name: feasibility-assessment
description: Assess the feasibility of implementing a FEATURE within the current system and identify gaps, questions, risks, and required decisions before architecture can be finalized
license: Proprietary
compatibility: opencode
metadata:
  audience: architect
  artifact: FEASIBILITY-ASSESSMENT
---

## What I do

- Analyze the current system state
- Analyze existing artifacts
- Analyze the current codebase
- Compare the requested FEATURE with the current implementation
- Identify gaps
- Identify open questions
- Identify risks
- Identify assumptions
- Assess implementation impact
- Recommend solution options
- Determine planning readiness

## What I do not do

FEASIBILITY-ASSESSMENT does not own:

- Business knowledge
- Business outcomes
- Operational flows
- Domain orchestration
- Target architecture
- Technical realization
- Implementation planning
- Task breakdown
- SOPs

Business knowledge belongs to DOMAIN.

Business outcomes belong to FEATURE.

Technical realization belongs to ARCHITECTURE.

Implementation planning belongs to PLANNING.

## Inputs

### Business Context

- DOMAIN
- FEATURE

DOMAIN provides business knowledge.

FEATURE provides business outcome, operational flow, and domain orchestration.

### Current State

- Current Artifacts
- Current Codebase

These provide the current implementation state that will be assessed.

## Artifact Ownership

FEASIBILITY-ASSESSMENT owns:

- Current State
- Gap Analysis
- Open Questions
- Assumptions
- Risks
- Recommendations
- Gap Closure Decisions
- Planning Readiness

FEASIBILITY-ASSESSMENT does not define the target state.

The target state belongs to ARCHITECTURE.

## Update Rules

When updating an existing FEASIBILITY-ASSESSMENT:

* Preserve existing GAP, OQ, ASM, and RISK IDs.
* Do not renumber existing entries.
* Do not remove historical findings unless explicitly obsolete.
* Do not create duplicate GAPs or OQs for the same issue.
* Update existing entries in place.
* Record decisions within the corresponding GAP or OQ.
* Preserve document structure and section ordering.
* Preserve previously approved decisions.
* Update Planning Readiness whenever gap status changes.

### Gap Closure Rules

For every GAP and OQ, use a consistent structure:

```text
Status: OPEN | CLOSED

Decision:
...

Rationale:
...

Impact:
...

Architecture Impact:
...

Resolved By:
...

Resolved Date:
...
```

### Multi-Agent Consistency

These rules ensure that different models and agents update the same FEASIBILITY-ASSESSMENT artifact in a consistent and predictable format.

## Workflow Position

```text
Discovery
    ↓
Feasibility Assessment
    ↓
Gap Closure
    ↓
Architecture Update
    ↓
Planning
````

FEASIBILITY-ASSESSMENT bridges Discovery and Architecture.

Its purpose is to determine what must be resolved before the target architecture can be finalized.

## Output

Produce:

`<CODE>-FEASIBILITY-ASSESSMENT.md`

The artifact must follow:

- assets/feasibility-assessment-template.md
    

## Versioning

Use:

`Major.Minor`

Update:

- Version
    
- LastUpdated
    

Do not maintain history inside the document.

## Planning Readiness

A FEASIBILITY-ASSESSMENT is considered ready when:

- All critical gaps have been resolved
    
- All required decisions have been recorded
    
- All blocking open questions have been resolved
    
- Architecture can be updated based on the approved decisions
    

Status should be:

`NOT-READY`

or

`READY-FOR-PLANNING`

## When to use me

Use this skill when:

- A new FEATURE has been proposed
    
- An existing FEATURE requires significant change
    
- The implementation impact is unclear
    
- Architectural decisions cannot yet be made confidently
    
- Planning cannot begin until uncertainties are resolved
    

Ask clarifying questions whenever business intent, existing behavior, integration boundaries, or implementation constraints are unclear.
