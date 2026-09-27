# Screen Inventory & Screen-to-Use-Case Matrix

Derived from the authoritative **Navigation Map** (`operational/navigation/navigation-map.md`) and **Screen Inventory** (`operational/navigation/screen-inventory.md`).

---

## 1. Screen Inventory

| Screen ID | Screen Name | Navigation Area | Primary Actors | Layout File |
| :--- | :--- | :--- | :--- | :--- |
| `SCR-REQ-001` | Request List | Requests | Implementator, Request Owner | `01-request-list.md` |
| `SCR-REQ-002` | Create Request | Requests | Implementator | `02-create-request.md` |
| `SCR-REQ-003` | Request Detail | Requests | Implementator, Request Owner, Management | `03-request-detail.md` |
| `SCR-REQ-004` | Request Search & History | Requests | Implementator | `04-request-search-history.md` |
| `SCR-REQ-005` | My Assigned Requests | Requests | Implementator, Request Owner | `05-my-assigned-requests.md` |
| `SCR-MGT-001` | Customer Progress Review | Management Oversight | Management | `06-customer-progress-review.md` |
| `SCR-MGT-002` | Programmer Performance Review | Management Oversight | Management | `07-programmer-performance-review.md` |
| `SCR-MGT-003` | Programmer Workload Review | Management Oversight | Management | `08-programmer-workload-review.md` |

**Total Screens: 8**

---

## 2. Screen-to-Use-Case Matrix

| Screen ID | UC-REQ-001 | UC-REQ-002 | UC-REQ-003 | UC-REQ-004 | UC-REQ-005 | UC-REQ-006 | UC-REQ-007 | UC-REQ-008 | UC-COL-001 | UC-COL-002 | UC-COL-003 | UC-COL-004 | UC-MGT-001 | UC-MGT-002 | UC-MGT-003 | UC-MGT-004 |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| SCR-REQ-001 | | ✓ | | | | | | ✓ | | | ✓ | | | | | |
| SCR-REQ-002 | ✓ | | | | | | | | | | | | | | | |
| SCR-REQ-003 | | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | | ✓ | | ✓ | | | |
| SCR-REQ-004 | | | | | | | | | | ✓ | | | | | | |
| SCR-REQ-005 | | | | | | | | | | | | ✓ | | | | |
| SCR-MGT-001 | | | | | | | | | | | | | | ✓ | | |
| SCR-MGT-002 | | | | | | | | | | | | | | | ✓ | |
| SCR-MGT-003 | | | | | | | | | | | | | | | | ✓ |

---

## 3. Screen-to-User-Journey Matrix

| Screen ID | UJ-REQ-001 | UJ-REQ-002 | UJ-REQ-003 | UJ-REQ-004 | UJ-REQ-005 | UJ-REQ-006 | UJ-REQ-007 | UJ-REQ-008 | UJ-COL-001 | UJ-COL-002 | UJ-COL-003 | UJ-COL-004 | UJ-MGT-001 | UJ-MGT-002 | UJ-MGT-003 | UJ-MGT-004 |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| SCR-REQ-001 | | ✓ | | | | | | ✓ | | | ✓ | | | | | |
| SCR-REQ-002 | ✓ | | | | | | | | | | | | | | | |
| SCR-REQ-003 | | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | | ✓ | | ✓ | | | |
| SCR-REQ-004 | | | | | | | | | | ✓ | | | | | | |
| SCR-REQ-005 | | | | | | | | | | | | ✓ | | | | |
| SCR-MGT-001 | | | | | | | | | | | | | | ✓ | | |
| SCR-MGT-002 | | | | | | | | | | | | | | | ✓ | |
| SCR-MGT-003 | | | | | | | | | | | | | | | | ✓ |

---

## 4. Use Case Coverage Summary

| Use Case | Use Case Name | Screens |
| :--- | :--- | :--- |
| UC-REQ-001 | Record Customer Request | SCR-REQ-002 |
| UC-REQ-002 | Assign Request Owner | SCR-REQ-001, SCR-REQ-003 |
| UC-REQ-003 | Evaluate Request | SCR-REQ-003 |
| UC-REQ-004 | Accept Request Responsibility | SCR-REQ-003 |
| UC-REQ-005 | Reject Request | SCR-REQ-003 |
| UC-REQ-006 | Escalate Request | SCR-REQ-003 |
| UC-REQ-007 | Request Management Decision | SCR-REQ-003 |
| UC-REQ-008 | Review Request Completion | SCR-REQ-001, SCR-REQ-003 |
| UC-COL-001 | Record Supporting Information | SCR-REQ-003 |
| UC-COL-002 | Search Request History | SCR-REQ-004 |
| UC-COL-003 | Track Request Progress | SCR-REQ-001, SCR-REQ-003 |
| UC-COL-004 | Review Assigned Requests | SCR-REQ-005 |
| UC-MGT-001 | Reassign Request Ownership | SCR-REQ-003 |
| UC-MGT-002 | Review Customer Request Progress | SCR-MGT-001 |
| UC-MGT-003 | Review Programmer Request Performance | SCR-MGT-002 |
| UC-MGT-004 | Review Programmer Workload | SCR-MGT-003 |

**All 16 use cases are covered. No orphan use case.**

---

## 5. Scenario Coverage Summary

All 16 operational scenarios (SC-REQ-001 through SC-REQ-008, SC-COL-001 through SC-COL-004, SC-MGT-001 through SC-MGT-004) are covered through their corresponding use cases and user journeys. No orphan scenario exists.
