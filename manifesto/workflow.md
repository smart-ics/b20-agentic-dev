# SDLC Workflow

## Purpose

This workflow defines how work progresses through the Knowledge-Centric SDLC.

The objective is to ensure that business knowledge, technical decisions, and implementation activities remain aligned and traceable throughout the development lifecycle.

---

## Workflow

```text
Request
    ↓
Discovery
    ↓
Feasibility Assessment
    ↓
Gap Closure
    ↓
Architecture Update
    ↓
Planning
    ↓
Implementation
    ↔
Review
    ↓
Testing
    ↓
Deployment
```

---

## 1. Discovery

Objective:

Define the requested business change.

Outputs:

* DOMAIN (new or updated)
* FEATURE (new or updated)

Rules:

* Discovery focuses on business understanding.
* Technical implementation must not be defined during this stage.
* Discovery is complete when the requested capability is sufficiently described by DOMAIN and FEATURE.

---

## 2. Feasibility Assessment

Objective:

Assess the impact of the requested change against the current system.

Inputs:

* DOMAIN
* FEATURE
* Current Artifacts
* Current Codebase

Outputs:

* FEASIBILITY-ASSESSMENT

The assessment must identify:

* Current State
* Gap Analysis
* Open Questions
* Risks
* Assumptions

Rules:

* No implementation planning occurs during this stage.
* No architecture decisions are finalized during this stage.

---

## 3. Gap Closure

Objective:

Resolve all gaps and open questions identified during feasibility assessment.

Outputs:

* Recorded Decisions
* Updated FEASIBILITY-ASSESSMENT

Rules:

* All open questions must be resolved or explicitly accepted.
* All required decisions must be recorded.
* FEASIBILITY-ASSESSMENT must be marked READY-FOR-PLANNING before proceeding.

---

## 4. Architecture Update

Objective:

Update the target architecture based on approved decisions.

Inputs:

* FEATURE
* FEASIBILITY-ASSESSMENT

Outputs:

* ARCHITECTURE

Rules:

* ARCHITECTURE represents the approved target state.
* All approved decisions must be reflected in ARCHITECTURE.
* Planning must not begin until ARCHITECTURE has been updated.

---

## 5. Planning

Objective:

Create an implementation plan that transforms the current system into the target architecture.

Inputs:

* FEATURE
* ARCHITECTURE
* FEASIBILITY-ASSESSMENT
* Current Codebase

Outputs:

* IMPLEMENTATION-PLAN

Planner responsibilities:

* Analyze the current codebase.
* Compare the current implementation with the target architecture.
* Determine the implementation delta.
* Create phased and/or sliced implementation plans.
* Produce slices that can be executed by implementation agents.

Rules:

* Planning must use ARCHITECTURE as the source of target-state truth.
* Planning must use the current codebase as the source of current-state truth.

---

## 6. Implementation

Objective:

Implement one approved slice.

Inputs:

* IMPLEMENTATION-PLAN
* ARCHITECTURE
* Current Codebase

Outputs:

* Source Code Changes

Rules:

* Implementation must follow ARCHITECTURE.
* Implementers must not invent business or architecture decisions.

---

## 7. Review

Objective:

Verify that implementation satisfies the plan and architecture.

Inputs:

* Source Code Changes
* IMPLEMENTATION-PLAN
* ARCHITECTURE

Outputs:

* REVIEW Report

Rules:

* Review may approve or reject a slice.
* Rejected slices return to Implementation.
* Approved slices may proceed to Testing.

Implementation and Review operate in a continuous loop until the slice is accepted.

---

## 8. Testing

Objective:

Validate the completed solution.

Inputs:

* Approved Implementation

Outputs:

* TEST-PACKAGE
* Test Results

Rules:

* Testing validates functionality, integration, and regression impact.
* Testing must verify acceptance criteria defined by FEATURE.

---

## 9. Deployment

Objective:

Release the validated solution to the target environment.

Inputs:

* Tested Solution
* Deployment Environment

Outputs:

* Deployment Checklist
* Deployment Result
* Serah Terima Documentation (if required)

Rules:

* Deployment must validate environmental readiness.
* Deployment must verify post-release health and operability.

---

## Artifact Flow

```text
DOMAIN
        \
         \
FEATURE -----> FEASIBILITY-ASSESSMENT
                     ↓
               Gap Closure
                     ↓
               ARCHITECTURE
                     ↓
             IMPLEMENTATION-PLAN
                     ↓
             IMPLEMENTATION
                     ↔
                 REVIEW
                     ↓
                 TESTING
                     ↓
               DEPLOYMENT
```

---

## Workflow Completion

A development effort is considered complete when:

* DOMAIN accurately reflects business knowledge.
* FEATURE accurately reflects the delivered outcome.
* ARCHITECTURE accurately reflects the implemented solution.
* All planned slices are completed.
* Testing has passed.
* Deployment has been completed successfully.
* All required delivery artifacts have been produced.
