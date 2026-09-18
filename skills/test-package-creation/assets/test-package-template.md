---
name: test-package-creation
description: Create a human-readable test package that guides testers through verification of a feature implementation
license: Proprietary
compatibility: opencode
metadata:
  audience: tester
  artifact: TEST-PACKAGE
---

# Purpose

Create a TEST-PACKAGE that can be executed by a human tester.

The package must provide clear testing instructions, expected outcomes, and execution guidance.

Human readability takes precedence over agent readability.

# What I do

- Analyze FEATURE
- Analyze ARCHITECTURE
- Analyze IMPLEMENTATION-PLAN (COMPLETED)
- Identify test scenarios
- Create test cases
- Define expected outcomes
- Define required test data
- Define execution sequence

# What I do not do

- Execute tests
- Update test results
- Create issues
- Modify code
- Perform review

# Inputs

Required:

- IMPLEMENTATION-PLAN (COMPLETED)
- FEATURE
- ARCHITECTURE

# Testing Principles

The test package must:

- Be understandable by non-programmers
- Be executable without architecture knowledge
- Use business language whenever possible
- Minimize technical jargon
- Focus on observable behavior

Test-package creation must not begin before the IMPLEMENTATION-PLAN is COMPLETED.
COMPLETED means every slice has implementation status IMPLEMENTED and review status GO.
An individual slice with review status GO is not sufficient.

The package validates the completed feature/system behavior as a whole. Review
validates individual slices; testing does not use an individual slice GO as its
trigger.

TEST-PACKAGE is tester-agnostic. It describes what must be tested, not who
performs the test. The test may be performed by a Programmer, Trainer, Reviewer,
Customer, or another qualified person.

# Test Coverage

The package should cover:

- Happy Path
- Business Rules
- Validation Rules
- Error Handling
- Regression Risks

# Output

TEST-PACKAGE.md
