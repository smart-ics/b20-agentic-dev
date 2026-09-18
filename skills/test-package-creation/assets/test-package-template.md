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
- Analyze IMPLEMENTATION-PLAN
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

- FEATURE
- ARCHITECTURE

Optional:

- IMPLEMENTATION-PLAN

# Testing Principles

The test package must:

- Be understandable by non-programmers
- Be executable without architecture knowledge
- Use business language whenever possible
- Minimize technical jargon
- Focus on observable behavior

# Test Coverage

The package should cover:

- Happy Path
- Business Rules
- Validation Rules
- Error Handling
- Regression Risks

# Output

TEST-PACKAGE.md
