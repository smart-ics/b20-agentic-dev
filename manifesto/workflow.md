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
IMPLEMENTATION-PLAN = COMPLETED
    ↓
Test Package Creation
    ↓
Testing
    ↓
Test Execution
    ↓
Issue Creation (when defects are found)
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

FEASIBILITY-ASSESSMENT owns:

* Current State
* Gap Analysis
* Open Questions
* Risks
* Assumptions
* Decisions

Rules:

* No implementation planning occurs during this stage.
* Target State is not defined during this stage.

---

## 3. Gap Closure

Objective:

Resolve all gaps and open questions identified during feasibility assessment.

Outputs:

* Updated FEASIBILITY-ASSESSMENT
* Closed gaps and open questions
* Recorded decisions

Rules:

* All blocking gaps must be resolved.
* All required decisions must be recorded.
* FEASIBILITY-ASSESSMENT must be marked READY-FOR-PLANNING before proceeding.

---

## 4. Architecture Update

Objective:

Update the target architecture based on approved decisions.

Inputs:

* FEATURE
* FEASIBILITY-ASSESSMENT
* Approved Decisions

Outputs:

* ARCHITECTURE

Rules:

* ARCHITECTURE owns and represents the approved target state.
* All approved decisions must be reflected in ARCHITECTURE.
* Planning must not begin until ARCHITECTURE has been updated.

---

## 5. Planning

Objective:

Create an implementation plan that transforms the current system into the target architecture.

Inputs:

* FEATURE
* ARCHITECTURE
* Current Codebase

Outputs:

* IMPLEMENTATION-PLAN

Planner responsibilities:

* Analyze the current codebase.
* Compare the Current Codebase with the Approved Target Architecture.
* Determine the implementation delta.
* Create phased and/or sliced implementation plans.
* Produce slices that can be executed by implementation agents.

Rules:

* Planning must use ARCHITECTURE as the source of target-state truth.
* Planning must use the current codebase as the source of current-state truth.
* Planning must consume the updated ARCHITECTURE, not unresolved feasibility findings.

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

* Review produces exactly one decision: GO or NO-GO.
* A NO-GO slice returns to Implementation for remediation and re-review.
* A GO slice remains part of the implementation plan until every slice is IMPLEMENTED and every slice review status is GO.
* Testing must not start after an individual slice receives GO.
* The IMPLEMENTATION-PLAN becomes COMPLETED only when every slice has implementation status IMPLEMENTED and review status GO.
* Testing may start only after the IMPLEMENTATION-PLAN is COMPLETED.

Implementation and Review operate in a continuous loop:

* Implementation → Review → GO → IMPLEMENTATION-PLAN completion check
* Implementation → Review → NO-GO → Remediation → Re-Review

---

## 8. Test Package Creation

Objective:

Define human-executable tests for the completed implementation as a whole.

Inputs:

* IMPLEMENTATION-PLAN (COMPLETED)
* FEATURE
* ARCHITECTURE

Outputs:

* TEST-PACKAGE

Rules:

* Test package creation must not begin before the IMPLEMENTATION-PLAN is COMPLETED.
* Test scenarios must cover the completed feature/system behavior, not an individual GO slice.
* Testing artifacts are tester-agnostic.
* A TEST-PACKAGE describes what must be tested, not who performs the test.

---

## 9. Testing

Objective:

Validate the completed feature/system behavior after all planned implementation work has passed review.

Inputs:

* IMPLEMENTATION-PLAN (COMPLETED)
* FEATURE
* ARCHITECTURE
* TEST-PACKAGE

Outputs:

* TEST-EXECUTION

Rules:

* Testing must not use an individual slice GO as its trigger.
* Review validates individual slices.
* Testing validates the completed implementation as a whole.
* Testing validates functionality, integration, and regression impact.
* Testing must verify acceptance criteria defined by FEATURE.

---

## 10. Test Execution and Issue Creation

Objective:

Execute TEST-PACKAGE and record the results of testing.

Inputs:

* TEST-PACKAGE
* IMPLEMENTATION-PLAN (COMPLETED)
* FEATURE
* ARCHITECTURE

Outputs:

* TEST-EXECUTION
* ISSUE artifacts when defects are found

Rules:

* Test execution may begin only after the IMPLEMENTATION-PLAN is COMPLETED.
* Defects found during test execution must be recorded as ISSUE artifacts.

---

## 11. Deployment

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
       IMPLEMENTATION-PLAN = COMPLETED
                      ↓
          TEST PACKAGE CREATION
                      ↓
                  TESTING
                      ↓
               TEST EXECUTION
                      ↓
       ISSUE CREATION (when defects are found)
                      ↓
                DEPLOYMENT
```

---

## Workflow Completion

A development effort is considered complete when:

* DOMAIN accurately reflects business knowledge.
* FEATURE accurately reflects the delivered outcome.
* ARCHITECTURE accurately reflects the implemented solution.
* IMPLEMENTATION-PLAN is COMPLETED: every slice is IMPLEMENTED and every slice review status is GO.
* Testing has passed.
* Deployment has been completed successfully.
* All required delivery artifacts have been produced.
