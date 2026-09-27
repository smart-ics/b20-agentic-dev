# Screen-to-Use-Case Matrix

This artifact maps the operational UI screens to the authoritative Use Cases they support, validating that every screen provides business value.

| Screen ID | Screen Name | Supported Use Cases | Primary Actor |
| :--- | :--- | :--- | :--- |
| **SCR-FEED-001** | Operational Feed | UC-AWR-001, UC-AWR-002, UC-AWR-003, UC-FCOL-001, UC-FCOL-002, UC-FCOL-005 | All Actors |
| **SCR-POST-001** | Post Detail | UC-FCOL-001, UC-FCOL-002, UC-FCOL-004 | All Actors |
| **SCR-POST-002** | Create Post | UC-FCOL-003 | Implementator |
| **SCR-REQ-001** | Request List | UC-REQ-002, UC-REQ-008, UC-COL-003 | Implementator, Request Owner |
| **SCR-REQ-002** | Create Request | UC-REQ-001 | Implementator |
| **SCR-REQ-003** | Request Detail | UC-REQ-002, UC-REQ-003, UC-REQ-004, UC-REQ-005, UC-REQ-006, UC-REQ-007, UC-REQ-008, UC-COL-001, UC-COL-003, UC-MGT-001 | Implementator, Request Owner, Management |
| **SCR-REQ-004** | Request Search & History | UC-COL-002 | Implementator |
| **SCR-REQ-005** | My Assigned Requests | UC-COL-004 | Implementator, Request Owner |
| **SCR-MGT-001** | Customer Progress Review | UC-MGT-002 | Management |
| **SCR-MGT-002** | Programmer Performance Review | UC-MGT-003 | Management |
| **SCR-MGT-003** | Programmer Workload Review | UC-MGT-004 | Management |

## Validation

* Every screen maps to at least one Use Case.
* The Operational Feed (SCR-FEED-001) supports multiple high-frequency Operational Awareness and Feed Collaboration use cases.
* Request Detail (SCR-REQ-003) centralizes complex Request Lifecycle and Collaboration use cases that require structured context.
