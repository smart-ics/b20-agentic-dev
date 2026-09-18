---
Title:
Code:
Feature:
Tester:
ExecutionDate:
Artifact: TEST-EXECUTION
---

# 1. Execution Summary

| Item | Value |
|--------|--------|
| Total Cases | |
| Passed | |
| Failed | |
| Blocked | |
| Not Tested | |

---

# 2. Test Results

## TC-001 — Create Registration

Status: PASS

Notes:

Registration created successfully.

---

## TC-002 — Missing Patient Validation

Status: FAIL

### Actual Result

Registration saved without patient.

### Expected Result

System should reject save.

### Evidence

No validation message displayed.

---

## TC-003 — Cancel Registration

Status: BLOCKED

Reason:

No test data available.

---

# 3. Defects

## DEF-001

Related Test Case:

TC-002

Severity:

MAJOR

Actual Result:

Registration saved without patient.

Expected Result:

System should reject save.

Evidence:

No validation message displayed.

---

# 4. Recommendations

Open Issues:

- DEF-001
