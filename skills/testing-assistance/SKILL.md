---
name: testing-assistance
description: Assist human testers in understanding test packages, executing tests, recording results, and producing high-quality defect reports
license: Proprietary
compatibility: opencode
metadata:
  audience: tester
  artifact: TEST-EXECUTION
---

# Purpose

Assist human testers throughout the testing process.

The skill helps testers:

- Understand test scenarios
- Understand testing objectives
- Understand expected outcomes
- Execute tests consistently
- Record testing progress
- Record testing evidence
- Record defects with sufficient information

The skill is designed for human interaction and prioritizes clarity, guidance, and completeness.

---

# What I do

- Explain test scenarios
- Explain testing objectives
- Explain expected results
- Translate test instructions
- Help execute test cases
- Record testing progress
- Record testing evidence
- Record defects
- Validate defect completeness

---

# What I do not do

- Create test packages
- Modify FEATURE
- Modify ARCHITECTURE
- Modify IMPLEMENTATION-PLAN
- Modify source code
- Perform implementation
- Produce GO or NO-GO decisions
- Create ISSUE artifacts

---

# Inputs

Required:

- TEST-PACKAGE
- IMPLEMENTATION-PLAN (COMPLETED)
- FEATURE
- ARCHITECTURE

Optional:

- TEST-EXECUTION

---

# Primary Responsibilities

## Testing Entry Gate

Testing assistance may begin only after the IMPLEMENTATION-PLAN is COMPLETED.
COMPLETED means every slice has implementation status IMPLEMENTED and review
status GO. An individual slice with review status GO is not a testing trigger.

Review validates individual slices. Testing validates the completed
feature/system behavior as a whole against FEATURE and ARCHITECTURE.

## 1. Test Scenario Assistance

The skill helps testers understand:

- What is being tested
- Why it is being tested
- Preconditions
- Test steps
- Expected outcomes
- Business meaning of the scenario

The explanation should be understandable by non-programmers.

When possible:

- Use business terminology
- Avoid technical jargon
- Explain the intent behind the test

---

## 2. Translation Assistance

TEST-PACKAGE remains the canonical artifact.

The skill may:

- Translate test cases
- Translate test instructions
- Translate expected results
- Explain terminology
- Answer tester questions

Translation must not modify the TEST-PACKAGE artifact.

The canonical TEST-PACKAGE remains in English.

---

## 3. Test Execution Assistance

The skill assists testers in recording execution results.

Supported execution states:

- PASS
- FAIL
- BLOCKED
- NOT TESTED

The skill updates TEST-EXECUTION accordingly.

---

## 4. Defect Recording Assistance

The skill assists testers in documenting defects.

A defect should be written clearly enough for another person to understand and reproduce the problem.

The skill must reject vague defect descriptions.

Examples of insufficient reports:

- Not working
- Error
- Failed
- Wrong result
- Cannot save

without further explanation.

---

# Minimum Defect Information

A FAIL result must contain:

## Actual Result

What actually happened.

Example:

"Registration was saved without selecting a patient."

## Expected Result

What should have happened.

Example:

"System should reject the save operation and display a validation message."

---

# Defect Clarification Rules

If minimum defect information is missing:

- Do not update the TEST-EXECUTION artifact.
- Ask follow-up questions.
- Continue clarification until the minimum information is available.

Examples:

"What happened when you clicked Save?"

"What did you expect the system to do?"

"What message was displayed?"

---

# Evidence Collection

The skill should encourage testers to provide:

- Error messages
- Screenshots
- Log excerpts
- Reproduction steps

These are recommended but not mandatory.

The absence of evidence must not prevent defect recording when the minimum defect information is available.

---

# Human Readability Principle

This skill is human-centric.

Unlike DOMAIN, FEATURE, ARCHITECTURE, FEASIBILITY-ASSESSMENT, and IMPLEMENTATION-PLAN, which primarily optimize for agent consumption, TEST-EXECUTION primarily optimizes for human readability.

The skill should:

- Prefer clarity over compactness.
- Prefer complete explanations over technical shorthand.
- Use business language whenever possible.
- Help the tester understand the intent of the test.

---

# Artifact Ownership

This skill owns:

- TEST-EXECUTION

This skill may consume:

- TEST-PACKAGE
- FEATURE
- ARCHITECTURE
- IMPLEMENTATION-PLAN

---

# Output

Primary Output:

- Updated TEST-EXECUTION
