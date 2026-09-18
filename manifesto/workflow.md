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

## Authority Model

The Authority Model defines which role owns which knowledge, which role may modify which artifacts and fields, and which role may advance workflow gates.

It exists to keep multi-agent operation deterministic: every agent knows what it may change, what it must not change, and what to do when a required change is outside its scope.

### Roles

```text
Analyst      → DOMAIN, FEATURE
Architect    → FEASIBILITY-ASSESSMENT, ARCHITECTURE, IMPLEMENTATION-PLAN
Implementer  → Source Code, slice implementation status
Reviewer     → REVIEW, slice review status, plan COMPLETED
Tester       → TEST-PACKAGE, TEST-EXECUTION, ISSUE
Deployer     → DEPLOYMENT artifacts
```

Rules:

* Roles are executed by agents through their corresponding skills.
* An agent acts under exactly one role per workflow step.
* Implementation and Review of the same slice must be performed by different agent executions.
* DOMAIN and FEATURE skills may be executed by an analyst or an architect acting as Analyst.
* The Deployer role is defined by the Deployment stage and does not require a dedicated skill.

### Knowledge Ownership

Each class of knowledge has exactly one authoritative artifact:

```text
Business Knowledge          → DOMAIN
Business Outcome            → FEATURE
Current State and Decisions → FEASIBILITY-ASSESSMENT
Technical Realization       → ARCHITECTURE
Planning Structure          → IMPLEMENTATION-PLAN
Implementation Progress     → IMPLEMENTATION-PLAN slice implementation status
Review Findings             → REVIEW
Review Decisions            → IMPLEMENTATION-PLAN slice review status
Test Definitions            → TEST-PACKAGE
Test Results                → TEST-EXECUTION
Defects                     → ISSUE
Deployment Results          → DEPLOYMENT artifacts
```

Rules:

* Knowledge must not be redefined outside its authoritative artifact.
* Other artifacts must reference the owner rather than duplicate the knowledge.

### Change Propagation Rules

A change to a Permanent Knowledge artifact requires review of the artifacts derived from it:

```text
DOMAIN change       → review FEATURE
FEATURE change      → review ARCHITECTURE
ARCHITECTURE change → review Generated Knowledge
```

Rules:

* The review is performed by the role that owns the downstream artifact.
* Propagation is transitive: when a downstream artifact is updated as a result of the review, its own downstream review is triggered.
* If the review finds the downstream artifact inconsistent, the owning role must update it before dependent work continues.
* Generated Knowledge is regenerated from DOMAIN, FEATURE, and ARCHITECTURE rather than patched.

### Decision Ownership

### Feasibility Decisions

FEASIBILITY decides WHAT constraints,
choices, and resolutions are approved.

### Architecture Decisions

ARCHITECTURE decides HOW approved
feasibility decisions are technically realized.

### Decision Flow
```
FEATURE
    ↓
FEASIBILITY
    ↓
ARCHITECTURE
    ↓
IMPLEMENTATION-PLAN
```

### Modification Authority

```text
DOMAIN                        → Analyst
FEATURE                       → Analyst
FEASIBILITY-ASSESSMENT        → Architect
ARCHITECTURE                  → Architect
IMPLEMENTATION-PLAN structure → Architect
Slice implementation status   → Implementer
Slice review status           → Reviewer
Plan status COMPLETED         → Reviewer
REVIEW findings               → Reviewer
Source Code                   → Implementer
TEST-PACKAGE                  → Tester
TEST-EXECUTION results        → Tester
ISSUE artifacts               → Tester
DEPLOYMENT results            → Deployer
```

IMPLEMENTATION-PLAN is the only artifact with multiple writers. Its writers own disjoint fields:

* Architect owns structure: phases, slice IDs, objectives, dependencies, execution order, and repository assignment. New slices are created with implementation status NOT-STARTED and review status NOT-REVIEWED.
* Implementer owns slice implementation status (NOT-STARTED, IN-PROGRESS, IMPLEMENTED, BLOCKED) and implementation notes.
* Reviewer owns slice review status (NOT-REVIEWED, GO, NO-GO) and the plan-level COMPLETED status.
* The Architect must not advance implementation or review status.
* The Implementer must not set review status.
* The Reviewer must not set implementation status and must not modify plan structure.

Core rules:

> An agent may modify only artifacts and fields within its authority scope.

> If an agent identifies a required change outside its authority scope, it must create a finding, issue, recommendation, or request rather than modifying the artifact directly.

### Workflow Gate Authority

Gates are deterministic. A gate condition is evaluated from artifact state alone. Any agent may verify a gate condition; only the authorized role may grant the gate. A gate must be granted when its condition is satisfied and must not be granted when it is not.

READY-FOR-PLANNING

* Granted by: Architect
* Condition: every blocking GAP and OQ is CLOSED with Decision, Rationale, Impact, Architecture Impact, Resolved By, and Resolved Date recorded
* Unlocks: Architecture Update, then Planning

IMPLEMENTED

* Granted by: Implementer
* Condition: slice work is complete against the slice objective and every declared dependency is implemented in the codebase
* Unlocks: Review of the slice

GO

* Granted by: Reviewer
* Condition: no BLOCKER or MAJOR findings, and slice scope, architecture compliance, and dependencies are satisfied
* Unlocks: the slice counts toward plan completion

NO-GO

* Granted by: Reviewer
* Condition: at least one BLOCKER or MAJOR finding exists
* Effect: the slice returns to Implementation for remediation and re-review

COMPLETED

* Granted by: Reviewer
* Condition: every slice has implementation status IMPLEMENTED and review status GO
* Unlocks: Test Package Creation and Testing
* After granting GO to a slice, the Reviewer must evaluate this condition and set COMPLETED when it is satisfied

TEST PASSED

* Granted by: Tester
* Condition: every test case in TEST-EXECUTION is PASS, or every FAIL has been resolved through an ISSUE and retested as PASS
* Unlocks: Deployment

DEPLOYED

* Granted by: Deployer
* Condition: environmental readiness validated and post-release health verified
* Unlocks: Workflow Completion

Gate rules:

* An individual slice GO does not unlock Testing; only a COMPLETED plan does.
* Only the Reviewer may move a slice review status from NO-GO back to GO, through re-review.
* Gate conditions must be verifiable by any agent from the artifacts alone, without consulting other agents.

### Multi-Agent Safety Rules

* Implementers must not modify DOMAIN, FEATURE, FEASIBILITY-ASSESSMENT, ARCHITECTURE, plan structure, review status, TEST-PACKAGE, or TEST-EXECUTION. Implementers must never modify ARCHITECTURE to match the implementation. When the implementation cannot satisfy ARCHITECTURE, the Implementer marks the slice BLOCKED and raises a request to the Architect.
* Reviewers must not modify DOMAIN, FEATURE, ARCHITECTURE, plan structure, implementation status, or source code. Reviewers must never modify artifacts to justify the implementation. Architecture or plan disagreements are recorded as findings and escalated to the Architect.
* Testers must not modify implementation artifacts: source code, IMPLEMENTATION-PLAN, ARCHITECTURE, FEATURE, or DOMAIN. Defects are recorded as FAIL results and ISSUE artifacts, never fixed directly.
* Architects must not implement code, review slices, advance implementation or review status, or record test results.
* Analysts must not define technical realization or planning structure.
* No agent may bypass a workflow gate, grant a gate whose condition is not satisfied, or record a gate state that the artifacts do not support.
* Authority conflicts are resolved by ownership: the owning role decides and updates the artifact. All other agents record findings or requests and continue work within their own scope.
* An out-of-scope change request does not pause the requesting agent's in-scope work unless the change blocks it, in which case the affected slice or test case is marked BLOCKED.

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
* An individual slice GO is not a trigger for Testing.
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
