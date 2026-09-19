# SDLC Workflow

## Purpose

This workflow defines how work progresses through the Knowledge-Centric SDLC.

The objective is to ensure that business knowledge, technical decisions, and implementation activities remain aligned and traceable throughout the development lifecycle.

---

## Workflow

### Workflow Mechanisms

The SDLC contains three distinct mechanisms. They must never be conflated.

```text
NO-GO        Review rejection during implementation execution.
             Stays inside the Implementation ↔ Review loop.
             Does not create an ISSUE. Does not invoke BUG-INVESTIGATION.
             Is not testing.

TEST FAIL    Human testing discovers a system defect.
             Produces evidence and a defect report.
             Becomes an ISSUE (BUG) through Issue Creation.

BUG ISSUE    Formal intake of a defect into a new SDLC cycle.
             Re-enters via BUG-INVESTIGATION → ARCHITECTURE UPDATE → PLANNING.
```

### Master Workflow

```text
ISSUE (CHANGE-REQUEST)
        ↓
DISCOVERY
        ↓
FEASIBILITY-ASSESSMENT
        ↓
GAP CLOSURE
        ↓
ARCHITECTURE UPDATE
        ↓
PLANNING
        ↓
IMPLEMENTATION
        ↓
REVIEW
        ↓
GO
        ↓
IMPLEMENTATION-PLAN = COMPLETED
        ↓
TEST PACKAGE CREATION
        ↓
TESTING
        ↓
TEST EXECUTION

PASS ──────────────────────────────→ DEPLOYMENT

FAIL
        ↓
ISSUE CREATION
        ↓
ISSUE (BUG)
        ↓
BUG-INVESTIGATION
        ↓
ARCHITECTURE UPDATE
        ↓
PLANNING
        ↓
IMPLEMENTATION
        ↓
REVIEW
```

REVIEW also has a NO-GO exit, shown in the Implementation Execution Loop
below. The FAIL exit of TEST EXECUTION is shown in the Human Testing Workflow
below. The ISSUE (BUG) path is shown in the BUG Issue Workflow below.

### Implementation Execution Loop (NO-GO)

```text
IMPLEMENTATION
        ↓
REVIEW

GO
        ↓
IMPLEMENTATION-PLAN completion check

NO-GO
        ↓
IMPLEMENTATION
```

Rules:

* NO-GO stays inside implementation execution.
* NO-GO does not create ISSUE artifacts.
* NO-GO does not invoke BUG-INVESTIGATION.
* NO-GO is not testing.

### Human Testing Workflow (TEST FAIL)

```text
IMPLEMENTATION-PLAN = COMPLETED
        ↓
TEST-PACKAGE
        ↓
TEST EXECUTION

PASS
        ↓
DEPLOYMENT

FAIL
        ↓
ISSUE CREATION
        ↓
ISSUE (BUG)
```

Rules:

* Testing is a human validation activity.
* Testing does not return work directly to Implementation.
* Testing does not participate in remediation loops.
* Testing produces evidence and defect reports.

### BUG Issue Workflow (ISSUE (BUG))

```text
ISSUE (BUG)
        ↓
BUG-INVESTIGATION
        ↓
ARCHITECTURE UPDATE
        ↓
PLANNING
        ↓
IMPLEMENTATION
        ↓
REVIEW
```

The BUG issue re-enters the SDLC at PLANNING. From REVIEW onward the BUG
correction follows the same sequence as any other plan: GO →
IMPLEMENTATION-PLAN = COMPLETED → TEST PACKAGE CREATION → TESTING → TEST
EXECUTION → PASS → DEPLOYMENT.

ISSUE is a first-class intake artifact. It may be a CHANGE-REQUEST or BUG and
may originate from customer, business, operational, support, review, testing,
or other valid findings. Testing is one possible source of BUG issues, not the
exclusive source of ISSUE artifacts.

---

## Implementation Plan Immutability

Once an IMPLEMENTATION-PLAN is approved for execution, its structure is
immutable for the remainder of that execution cycle.

Implementation and Review operate against the approved plan as written.

No agent may:

- split a slice,
- create child slices,
- add execution-time slices,
- merge slices,
- reorder slices,
- reinterpret slice scope.

If the plan is found to be structurally insufficient, the issue is
escalated to the Architect.

The Architect must create a replacement IMPLEMENTATION-PLAN through a
new Planning cycle. The approved plan is not modified and remains a
historical record.

The existing plan is never structurally patched during Implementation
or Review.

---

## Authority Model

The Authority Model defines which role owns which knowledge, which role may modify which artifacts and fields, and which role may advance workflow gates.

It exists to keep multi-agent operation deterministic: every agent knows what it may change, what it must not change, and what to do when a required change is outside its scope.

### Roles

```text
Analyst      → DOMAIN, FEATURE
Architect    → ARCHITECTURE, IMPLEMENTATION-PLAN
Issue Intake → ISSUE
Implementer  → Source Code, slice implementation status
Reviewer     → REVIEW, slice review status, plan COMPLETED
Tester       → TEST-PACKAGE, TEST-EXECUTION
Deployer     → DEPLOYMENT artifacts
```

FEASIBILITY-ASSESSMENT and BUG-INVESTIGATION are analysis activities, not
staffing roles. The same agent may execute either or both activities without
changing artifact ownership.

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
Issue Request or Problem    → ISSUE
Current State and Analysis  → FEASIBILITY-ASSESSMENT or BUG-INVESTIGATION
Technical Realization       → ARCHITECTURE
Planning Structure          → IMPLEMENTATION-PLAN
Implementation Progress     → IMPLEMENTATION-PLAN slice implementation status
Review Evidence and Findings → REVIEW, when preservation is required
Current Review Decisions    → IMPLEMENTATION-PLAN slice review status
Test Definitions            → TEST-PACKAGE
Test Results                → TEST-EXECUTION
Defect FAIL records         → TEST-EXECUTION
Formal defect issues        → ISSUE
Deployment Results          → DEPLOYMENT artifacts
```

Testing records defects as FAIL results in TEST-EXECUTION.

Issue Creation records defects as ISSUE artifacts.

TEST-EXECUTION and ISSUE are separate artifacts with separate ownership.

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

The analysis artifacts own their respective decisions:

```text
FEASIBILITY-ASSESSMENT
    → gap-closure decisions

BUG-INVESTIGATION
    → investigation decisions

ARCHITECTURE
    → technical realization of those decisions
```

BUG-INVESTIGATION must resolve investigation decisions before ARCHITECTURE
work begins. ARCHITECTURE consumes those approved decisions and must not repeat
the completed investigation unless explicitly requested.

### Review State and Evidence

Review status is authoritative in IMPLEMENTATION-PLAN. The Reviewer updates only
the slice review status and, when applicable, the plan-level COMPLETED status;
these fields are the current review state used by the workflow gates.

REVIEW is Working Knowledge. It records review evidence, findings, required
corrections, remediation history, and review iterations when that evidence must
be preserved. REVIEW does not replace or supersede the review status in
IMPLEMENTATION-PLAN.

A GO decision does not require a REVIEW artifact. REVIEW artifacts are created
only when review findings or remediation history must be preserved. The absence
of a REVIEW artifact for a GO decision is intentional, not an omission.

Review evidence must remain verifiable from implementation outputs,
ARCHITECTURE, IMPLEMENTATION-PLAN, and available execution records.

### Feasibility Decisions

FEASIBILITY decides WHAT constraints,
choices, and resolutions are approved.

BUG-INVESTIGATION decides WHAT defect correction direction is selected.

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
FEASIBILITY-ASSESSMENT        → analysis activity
BUG-INVESTIGATION             → analysis activity
ARCHITECTURE                  → Architect
IMPLEMENTATION-PLAN structure → Architect
Slice implementation status   → Implementer
Slice review status           → Reviewer
Plan status COMPLETED         → Reviewer
REVIEW findings               → Reviewer
Source Code                   → Implementer
TEST-PACKAGE                  → Tester
TEST-EXECUTION results        → Tester
ISSUE artifacts               → Issue Intake
DEPLOYMENT results            → Deployer
```

IMPLEMENTATION-PLAN contains multiple field owners.

Architect owns:

* Phase structure
* Slice structure
* Dependencies
* Planning decisions

Implementer owns:

* Implementation status fields

Reviewer owns:

* Review status fields

After execution approval, only execution-state fields may change.
Plan structure is immutable.

Permitted execution-state changes are implementation status, implementation
notes, review status, and plan status COMPLETED. These changes do not alter
phase structure, slice structure, dependencies, or planning decisions.

IMPLEMENTATION-PLAN is the only artifact with multiple writers. Its writers own disjoint fields:

* Architect owns structure during Planning: phases, slice IDs, objectives, dependencies, execution order, and repository assignment. After execution approval, the structure is immutable. A structural correction creates a replacement plan through a new Planning cycle; it does not modify the approved plan.
* Implementer owns slice implementation status (NOT-STARTED, IN-PROGRESS, IMPLEMENTED, BLOCKED) and implementation notes.
* Reviewer owns slice review status (NOT-REVIEWED, GO, NO-GO) and the plan-level COMPLETED status.
* The Architect must not advance implementation or review status.
* The Implementer must not set review status.
* The Reviewer must not set implementation status and must not modify plan structure.

Implementation corrects implementation defects.

Review identifies implementation defects.

Planning corrects planning defects.

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
* Condition: slice work is complete against the slice objective, every declared dependency slice has implementation status IMPLEMENTED, and the required implementation output exists and is verifiable in the target repository
* Dependency satisfaction requires the referenced Slice ID to exist, its implementation status to be IMPLEMENTED, and its required implementation output to exist and be verifiable in the target repository. The implementation status in IMPLEMENTATION-PLAN is authoritative; the codebase is evidence used to verify it. Similar code alone is not sufficient evidence.
* Dependency satisfaction is determined solely by implementation status and required implementation output. Review status (NOT-REVIEWED, GO, or NO-GO) does not participate: IMPLEMENTED + NOT-REVIEWED, IMPLEMENTED + GO, and IMPLEMENTED + NO-GO each satisfy a dependency. NO-GO affects review acceptance and plan completion only; it does not invalidate dependency satisfaction.
* Unlocks: Review of the slice and implementation of dependent slices

GO

* Granted by: Reviewer
* Condition: no BLOCKER or MAJOR findings, and slice scope, architecture compliance, and dependencies are satisfied
* Unlocks: the slice counts toward plan completion

NO-GO

* Granted by: Reviewer
* Condition: at least one BLOCKER or MAJOR finding exists
* Effect: the slice returns to Implementation for remediation and re-review. NO-GO stays inside the Implementation ↔ Review execution loop: it does not create an ISSUE, does not invoke BUG-INVESTIGATION, and is not testing.

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
* Gate conditions must be verifiable by any agent from the authoritative artifacts and required implementation evidence, without consulting other agents.

### Multi-Agent Safety Rules

* Implementers must not modify DOMAIN, FEATURE, FEASIBILITY-ASSESSMENT, ARCHITECTURE, plan structure, review status, TEST-PACKAGE, or TEST-EXECUTION. Implementers must never modify ARCHITECTURE to match the implementation. When the implementation cannot satisfy ARCHITECTURE, the Implementer marks the slice BLOCKED and raises a request to the Architect.
* Reviewers must not modify DOMAIN, FEATURE, ARCHITECTURE, plan structure, implementation status, or source code. Reviewers must never modify artifacts to justify the implementation. Architecture or plan disagreements are recorded as findings and escalated to the Architect.
* Testers must not modify implementation artifacts: source code, IMPLEMENTATION-PLAN, ARCHITECTURE, FEATURE, or DOMAIN. Testing records defects as FAIL results in TEST-EXECUTION; Issue Creation records formal defects as ISSUE artifacts. Testers never fix defects directly.
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

## 2. Analysis

Objective:

Assess a CHANGE-REQUEST or investigate a BUG against the current system.

Inputs:

* DOMAIN and FEATURE for a CHANGE-REQUEST
* ISSUE with Type = CHANGE-REQUEST for a CHANGE-REQUEST
* ISSUE with Type = BUG for a BUG
* Current Artifacts
* Current Codebase

Outputs:

* FEASIBILITY-ASSESSMENT for a CHANGE-REQUEST
* BUG-INVESTIGATION for a BUG

FEASIBILITY-ASSESSMENT owns for a CHANGE-REQUEST:

* Current State
* Gap Analysis
* Open Questions
* Risks
* Assumptions
* Decisions

BUG-INVESTIGATION owns for a BUG:

* Current State
* Problem Analysis
* Affected Components
* Impact Assessment
* Assumptions
* Open Questions
* Alternative Evaluation
* Recommended Decision
* Decision
* Decision Rationale

Rules:

* No implementation planning occurs during this stage.
* Target State is not defined during this stage.
* BUG-INVESTIGATION does not produce a FEASIBILITY-ASSESSMENT.
* FEASIBILITY-ASSESSMENT and BUG-INVESTIGATION are analysis activities and
  are not restricted to the Architect role.

---

## 3. Gap Closure

Objective:

Resolve all gaps and open questions identified during feasibility assessment.
BUG investigations proceed directly to Architecture after investigation.

Outputs:

* Updated FEASIBILITY-ASSESSMENT when applicable
* Closed gaps and open questions
* Recorded decisions

Rules:

* All blocking gaps must be resolved.
* All required decisions must be recorded.
* FEASIBILITY-ASSESSMENT must be marked READY-FOR-PLANNING before proceeding
  when a CHANGE-REQUEST is being assessed.

---

## 4. Architecture Update

Objective:

Update the target architecture based on approved decisions.

Inputs:

* FEATURE for a CHANGE-REQUEST
* ISSUE with Type = BUG for a BUG
* FEASIBILITY-ASSESSMENT or BUG-INVESTIGATION
* Approved analysis decisions

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

* Updated IMPLEMENTATION-PLAN review status
* REVIEW artifact only when review findings, required corrections, remediation
  history, or re-review evidence must be preserved

Rules:

* Review produces exactly one decision: GO or NO-GO.
* Review status is authoritative in IMPLEMENTATION-PLAN.
* A GO decision updates only the IMPLEMENTATION-PLAN review status. The
  separate plan-level COMPLETED status is set only by its existing completion
  check when its condition is satisfied.
* A GO decision does not require a REVIEW artifact.
* REVIEW artifacts are created only when review findings or remediation history
  must be preserved; their absence for a GO decision is intentional.
* A NO-GO slice returns to Implementation for remediation and re-review.
* A GO slice remains part of the implementation plan until every slice is IMPLEMENTED and every slice review status is GO.
* An individual slice GO is not a trigger for Testing.
* The IMPLEMENTATION-PLAN becomes COMPLETED only when every slice has implementation status IMPLEMENTED and review status GO.
* Testing may start only after the IMPLEMENTATION-PLAN is COMPLETED.
* Review evidence must remain verifiable from implementation outputs,
  ARCHITECTURE, IMPLEMENTATION-PLAN, and available execution records.

Implementation and Review operate in a continuous loop:

* Implementation → Review → GO → IMPLEMENTATION-PLAN completion check
* Implementation → Review → NO-GO → Remediation → Re-Review

NO-GO remediation stays inside this loop. It does not create an ISSUE, does not
invoke BUG-INVESTIGATION, and is not testing.

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

## 10. Test Execution

Objective:

Execute TEST-PACKAGE and record the results of testing.

Inputs:

* TEST-PACKAGE
* IMPLEMENTATION-PLAN (COMPLETED)
* FEATURE
* ARCHITECTURE

Outputs:

* TEST-EXECUTION

Rules:

* Test execution may begin only after the IMPLEMENTATION-PLAN is COMPLETED.

---

## Test Failure Handling

Objective:

Convert failed test results into formal BUG issues.

Inputs:

* TEST-EXECUTION containing one or more FAIL results

Outputs:

* ISSUE (Type = BUG)

Rules:

* TEST-EXECUTION remains the authoritative owner of test results.
* Testing activities record failures in TEST-EXECUTION.
* Testing activities do not create ISSUE artifacts directly.
* Every confirmed FAIL requiring corrective action must result in an ISSUE with Type = BUG.
* ISSUE creation is performed through the Issue Creation skill.
* Defect information recorded in TEST-EXECUTION becomes input to ISSUE creation.
* ISSUE ownership remains with Issue Intake.
* Testing does not return work directly to Implementation. A FAIL defect
  re-enters the SDLC only through ISSUE CREATION → ISSUE (BUG) →
  BUG-INVESTIGATION → ARCHITECTURE UPDATE → PLANNING.
* After ISSUE creation, the BUG issue enters the BUG Issue Workflow and
  re-enters the SDLC at PLANNING:

```text
ISSUE (BUG)
    ↓
BUG-INVESTIGATION
    ↓
ARCHITECTURE UPDATE
    ↓
PLANNING
    ↓
IMPLEMENTATION
    ↓
REVIEW
```

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
ISSUE (CHANGE-REQUEST) ----/
                       ↓
                 Gap Closure
                       ↓
                 ARCHITECTURE
          \
           \
ISSUE (BUG) --> BUG-INVESTIGATION
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
