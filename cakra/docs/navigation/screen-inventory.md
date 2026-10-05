# Screen Inventory

Derived from approved **User Journeys**, **Use Cases**, **Operational Scenarios**, **Actor Model**, and **Domain Models** in accordance with the **Navigation Creation Skill** (`operational/navigation/navigation-creation-skill.md`).

---

## Inventory Summary

| Screen ID | Screen Name | Navigation Area | Primary Actors | Supported Journeys | Supported Use Cases |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `SCR-REQ-001` | Request List | Requests | Implementator, Request Owner | UJ-REQ-002, UJ-REQ-008, UJ-COL-003 | UC-REQ-002, UC-REQ-008, UC-COL-003 |
| `SCR-REQ-002` | Create Request | Requests | Implementator | UJ-REQ-001 | UC-REQ-001 |
| `SCR-REQ-003` | Request Detail | Requests | Implementator, Request Owner, Management | UJ-REQ-002, UJ-REQ-003, UJ-REQ-004, UJ-REQ-005, UJ-REQ-006, UJ-REQ-007, UJ-REQ-008, UJ-COL-001, UJ-COL-003, UJ-MGT-001 | UC-REQ-002, UC-REQ-003, UC-REQ-004, UC-REQ-005, UC-REQ-006, UC-REQ-007, UC-REQ-008, UC-COL-001, UC-COL-003, UC-MGT-001 |
| `SCR-REQ-004` | Request Search & History | Requests | Implementator | UJ-COL-002 | UC-COL-002 |
| `SCR-REQ-005` | My Assigned Requests | Requests | Implementator, Request Owner | UJ-COL-004 | UC-COL-004 |
| `SCR-MGT-001` | Customer Progress Review | Management Oversight | Management | UJ-MGT-002 | UC-MGT-002 |
| `SCR-MGT-002` | Programmer Performance Review | Management Oversight | Management | UJ-MGT-003 | UC-MGT-003 |
| `SCR-MGT-003` | Programmer Workload Review | Management Oversight | Management | UJ-MGT-004 | UC-MGT-004 |
| `SCR-FEED-001` | Operational Feed | Operational Feed | Implementator, Management, Request Owner | UJ-AWR-001, UJ-AWR-002, UJ-AWR-003, UJ-FCOL-001, UJ-FCOL-002, UJ-FCOL-005 | UC-AWR-001, UC-AWR-002, UC-AWR-003, UC-FCOL-001, UC-FCOL-002, UC-FCOL-005 |
| `CreateRequestModal` | Create Request Modal | Operational Feed / Requests | Implementator, Management, Request Owner | UJ-REQ-001, UJ-AWR-001 | UC-REQ-001, UC-AWR-001 |
| `SCR-POST-001` | Post Detail | Operational Feed | Implementator, Management, Request Owner | UJ-FCOL-001, UJ-FCOL-002, UJ-FCOL-004 | UC-FCOL-001, UC-FCOL-002, UC-FCOL-004 |
| `SCR-POST-002` | Create Post | Operational Feed | Implementator | UJ-FCOL-003 | UC-FCOL-003 |
| `SCR-CUST-001` | Customer Management | Administration | Administrator, Admin | UJ-CUST-001 | UC-CUST-001 |
| `SCR-CUST-002` | Customer Modal | Administration | Administrator, Admin | UJ-CUST-001 | UC-CUST-001 |
| `SCR-CUST-003` | Customer Contact Modal | Administration | Administrator, Admin | UJ-CUST-001 | UC-CUST-001 |
| `SCR-USR-001` | User Management View | Administration | Administrator, Admin | UJ-USR-001 | UC-USR-001 |
| `SCR-USR-002` | Add / Edit User Modal | Administration | Administrator, Admin | UJ-USR-001 | UC-USR-001 |
| `SCR-ORG-001` | Person Management View | Administration | Administrator, Admin | UJ-ORG-001 | UC-ORG-001 |
| `SCR-ORG-002` | Add / Edit Person Modal | Administration | Administrator, Admin | UJ-ORG-001 | UC-ORG-001 |

---

## Screen Definitions

### SCR-REQ-001

**Screen Name:**
Request List

**Purpose:**
View and locate Requests within the system.

**Primary Actors:**
* Implementator
* Request Owner

**Supported Use Cases:**
* UC-REQ-002: Assign Request Owner
* UC-REQ-008: Review Request Completion
* UC-COL-003: Track Request Progress

**Supported User Journeys:**
* UJ-REQ-002: Assign Request Owner
* UJ-REQ-008: Review Request Completion
* UJ-COL-003: Track Request Progress

**Entry Points:**
* Global Navigation (`Requests > All Requests`)

**Exit / Destination:**
* `SCR-REQ-002: Create Request`
* `SCR-REQ-003: Request Detail`

---

### SCR-REQ-002

**Screen Name:**
Create Request

**Purpose:**
Record and submit a new Request.

**Primary Actors:**
* Implementator

**Supported Use Cases:**
* UC-REQ-001: Record Customer Request

**Supported User Journeys:**
* UJ-REQ-001: Record Customer Request

**Entry Points:**
* Request List (`SCR-REQ-001`)
* Global Navigation Quick Action (`+ New Request`)

**Exit / Destination:**
* `SCR-REQ-001: Request List`
* `SCR-REQ-003: Request Detail`

---

### SCR-REQ-003

**Screen Name:**
Request Detail

**Purpose:**
View and interact with a specific Request record.

**Primary Actors:**
* Implementator
* Request Owner
* Management

**Supported Use Cases:**
* UC-REQ-002: Assign Request Owner
* UC-REQ-003: Evaluate Request
* UC-REQ-004: Accept Request Responsibility
* UC-REQ-005: Reject Request
* UC-REQ-006: Escalate Request
* UC-REQ-007: Request Management Decision
* UC-REQ-008: Review Request Completion
* UC-COL-001: Record Supporting Information
* UC-COL-003: Track Request Progress
* UC-MGT-001: Reassign Request Ownership

**Supported User Journeys:**
* UJ-REQ-002: Assign Request Owner
* UJ-REQ-003: Evaluate Request
* UJ-REQ-004: Accept Request Responsibility
* UJ-REQ-005: Reject Request
* UJ-REQ-006: Escalate Request
* UJ-REQ-007: Request Management Decision
* UJ-REQ-008: Review Request Completion
* UJ-COL-001: Record Supporting Information
* UJ-COL-003: Track Request Progress
* UJ-MGT-001: Reassign Request Ownership

**Entry Points:**
* Request List (`SCR-REQ-001`)
* Create Request (`SCR-REQ-002`)
* Request Search & History (`SCR-REQ-004`)
* My Assigned Requests (`SCR-REQ-005`)
* Customer Progress Review (`SCR-MGT-001`)
* Programmer Performance Review (`SCR-MGT-002`)
* Programmer Workload Review (`SCR-MGT-003`)
* Direct URL / Permanent Link

**Exit / Destination:**
* `SCR-REQ-001: Request List`
* `SCR-REQ-005: My Assigned Requests`
* Originating screen (Back navigation)

---

### SCR-REQ-004

**Screen Name:**
Request Search & History

**Purpose:**
Search and retrieve historical Request records.

**Primary Actors:**
* Implementator

**Supported Use Cases:**
* UC-COL-002: Search Request History

**Supported User Journeys:**
* UJ-COL-002: Search Request History

**Entry Points:**
* Global Navigation (`Requests > Search & History`)
* Request List (`SCR-REQ-001`)

**Exit / Destination:**
* `SCR-REQ-003: Request Detail`

---

### SCR-REQ-005

**Screen Name:**
My Assigned Requests

**Purpose:**
View Requests assigned to the current user.

**Primary Actors:**
* Implementator
* Request Owner

**Supported Use Cases:**
* UC-COL-004: Review Assigned Requests

**Supported User Journeys:**
* UJ-COL-004: Review Assigned Requests

**Entry Points:**
* Global Navigation (`Requests > My Assigned`)

**Exit / Destination:**
* `SCR-REQ-003: Request Detail`

---

### SCR-MGT-001

**Screen Name:**
Customer Progress Review

**Purpose:**
View Request progress associated with a Customer.

**Primary Actors:**
* Management

**Supported Use Cases:**
* UC-MGT-002: Review Customer Request Progress

**Supported User Journeys:**
* UJ-MGT-002: Review Customer Request Progress

**Entry Points:**
* Global Navigation (`Management > Customer Progress`)

**Exit / Destination:**
* `SCR-REQ-003: Request Detail`

---

### SCR-MGT-002

**Screen Name:**
Programmer Performance Review

**Purpose:**
View historical Request outcomes for a Programmer.

**Primary Actors:**
* Management

**Supported Use Cases:**
* UC-MGT-003: Review Programmer Request Performance

**Supported User Journeys:**
* UJ-MGT-003: Review Programmer Request Performance

**Entry Points:**
* Global Navigation (`Management > Programmer Performance`)

**Exit / Destination:**
* `SCR-REQ-003: Request Detail`

---

### SCR-MGT-003

**Screen Name:**
Programmer Workload Review

**Purpose:**
View active Request assignments for a Programmer.

**Primary Actors:**
* Management

**Supported Use Cases:**
* UC-MGT-004: Review Programmer Workload

**Supported User Journeys:**
* UJ-MGT-004: Review Programmer Workload

**Entry Points:**
* Global Navigation (`Management > Programmer Workload`)

**Exit / Destination:**
* `SCR-REQ-003: Request Detail`

---

### SCR-FEED-001

**Screen Name:** Operational Feed

**Purpose:** Primary workspace for observing and participating in operational activity.

**Primary Actors:**
* Implementator
* Management
* Request Owner

**Supported Use Cases:**
* UC-AWR-001: Observe Operational Feed
* UC-AWR-002: Discover Request via Feed
* UC-AWR-003: Monitor Operational Exceptions via Feed
* UC-FCOL-001: Comment on Post
* UC-FCOL-002: React to Post
* UC-FCOL-005: Filter Operational Feed

**Supported User Journeys:**
* UJ-AWR-001: Observe Operational Feed
* UJ-AWR-002: Discover Request via Feed
* UJ-AWR-003: Monitor Operational Exceptions via Feed
* UJ-FCOL-001: Comment on Post
* UJ-FCOL-002: React to Post
* UJ-FCOL-005: Filter Feed by Context

**Entry Points:**
* Global Navigation (`Operational Feed`) — Primary
* System Default Landing Page

**Exit / Destination:**
* `SCR-POST-001: Post Detail`
* `SCR-POST-002: Create Post`
* `SCR-REQ-003: Request Detail` (via Post Reference or Success Banner link)
* `CreateRequestModal: Create Request Modal` (Modal Affordance)

---

### CreateRequestModal

**Screen Name:**
Create Request Modal

**Purpose:**
Modal dialog interface launched from the Operational Feed (`SCR-FEED-001`) header for recording a new Request directly into the system without leaving the feed.

**Primary Actors:**
* Implementator
* Management
* Request Owner

**Supported Use Cases:**
* UC-REQ-001: Record Customer Request
* UC-AWR-001: Observe Operational Feed

**Supported User Journeys:**
* UJ-REQ-001: Record Customer Request
* UJ-AWR-001: Observe Operational Feed

**Entry Points:**
* Operational Feed (`SCR-FEED-001`) — "+ Create Request" header action button

**Exit / Destination:**
* `SCR-FEED-001: Operational Feed` — closes modal, triggers immediate feed stream reload to render newly generated system post at index 0, and displays dismissible success alert banner
* `SCR-REQ-003: Request Detail` — navigable directly via link within post-creation success alert banner

---

### SCR-POST-001

**Screen Name:** Post Detail

**Purpose:** Focused view of a single Post and its complete discussion thread.

**Primary Actors:**
* Implementator
* Management
* Request Owner

**Supported Use Cases:**
* UC-FCOL-001: Comment on Post
* UC-FCOL-002: React to Post
* UC-FCOL-004: Navigate from Post to Request

**Supported User Journeys:**
* UJ-FCOL-001: Comment on Post
* UJ-FCOL-002: React to Post
* UJ-FCOL-004: Navigate from Post to Request

**Entry Points:**
* Operational Feed (`SCR-FEED-001`)
* Direct URL / Permanent Link

**Exit / Destination:**
* `SCR-FEED-001: Operational Feed`
* `SCR-REQ-003: Request Detail` (via Post Reference)

---

### SCR-POST-002

**Screen Name:** Create Post

**Purpose:** Author a new operational Post.

**Primary Actors:**
* Implementator

**Supported Use Cases:**
* UC-FCOL-003: Create Operational Post

**Supported User Journeys:**
* UJ-FCOL-003: Create Operational Post

**Entry Points:**
* Operational Feed (`SCR-FEED-001`)
* Request Detail (`SCR-REQ-003`) — Create Post referencing current Request

**Exit / Destination:**
* `SCR-FEED-001: Operational Feed`
* `SCR-POST-001: Post Detail`

---

### SCR-CUST-001

**Screen Name:**
Customer Management View

**Purpose:**
Administrative customer management console displaying all customer organizations (active and inactive), operational KPI metrics (Total Customers, Active Customers, Inactive Customers, Active Maintenance Contracts), keyword search across customer code and name, status filtering, and action triggers for adding, editing, managing contacts, and activating or deactivating customer records. Access restricted strictly to users possessing the Administrator or Admin role.

**Primary Actors:**
* Administrator
* Admin

**Supported Use Cases:**
* UC-CUST-001: Maintain Customer Master Data

**Supported User Journeys:**
* UJ-CUST-001: Customer Master Maintenance

**Entry Points:**
* Global Navigation Sidebar (`Administration > Customer Management` — route `/admin/customers`)

**Exit / Destination:**
* `SCR-CUST-002: Customer Modal`
* `SCR-CUST-003: Customer Contact Modal`

---

### SCR-CUST-002

**Screen Name:**
Customer Modal

**Purpose:**
Modal dialog interface supporting Create and Edit modes for Customer Master records (capturing/updating Customer Code, Customer Name, and Active Maintenance Contract status flag).

**Primary Actors:**
* Administrator
* Admin

**Supported Use Cases:**
* UC-CUST-001: Maintain Customer Master Data

**Supported User Journeys:**
* UJ-CUST-001: Customer Master Maintenance

**Entry Points:**
* Customer Management View (`SCR-CUST-001`) — "Add Customer" button action (`data-testid="add-customer-btn"`)
* Customer Management View (`SCR-CUST-001`) — Customer table row "Edit" button action (`data-testid="edit-customer-btn"`)

**Exit / Destination:**
* Customer Management View (`SCR-CUST-001`) — closes modal and refreshes customers table upon successful save or cancellation

---

### SCR-CUST-003

**Screen Name:**
Customer Contact Modal

**Purpose:**
Modal dialog interface for managing contact persons (view list, create contact, edit contact, and toggle active/inactive status) associated with a selected customer record.

**Primary Actors:**
* Administrator
* Admin

**Supported Use Cases:**
* UC-CUST-001: Maintain Customer Master Data

**Supported User Journeys:**
* UJ-CUST-001: Customer Master Maintenance

**Entry Points:**
* Customer Management View (`SCR-CUST-001`) — Customer table row "Contacts" button action (`data-testid="manage-contacts-btn"`)

**Exit / Destination:**
* Customer Management View (`SCR-CUST-001`) — closes modal upon completion

---

### SCR-USR-001

**Screen Name:**
User Management View

**Purpose:**
Administrative user management console displaying all user accounts, operational metrics (Total Accounts, Active, Locked, Suspended), keyword search, status filtering, and action triggers for creating or editing user accounts. Access restricted strictly to users possessing the Administrator or Admin role.

**Primary Actors:**
* Administrator
* Admin

**Supported Use Cases:**
* UC-USR-001: Manage User Accounts

**Supported User Journeys:**
* UJ-USR-001: Manage User Accounts

**Entry Points:**
* Global Navigation Sidebar (`Administration > User Management` — route `/admin/users`)

**Exit / Destination:**
* `SCR-USR-002: Add / Edit User Modal`

---

### SCR-USR-002

**Screen Name:**
Add / Edit User Modal

**Purpose:**
Modal dialog interface for registering a new user account tied to an unassociated active Person record or updating an existing user's email, account status (ACTIVE, LOCKED, SUSPENDED), and resetting credentials.

**Primary Actors:**
* Administrator
* Admin

**Supported Use Cases:**
* UC-USR-001: Manage User Accounts

**Supported User Journeys:**
* UJ-USR-001: Manage User Accounts

**Entry Points:**
* User Management View (`SCR-USR-001`) — "Add User" button action (`data-testid="add-user-btn"`)
* User Management View (`SCR-USR-001`) — User table row "Edit" button action (`data-testid="edit-user-btn"`)

**Exit / Destination:**
* User Management View (`SCR-USR-001`) — closes modal and refreshes user accounts table upon successful save or cancellation

---

### SCR-ORG-001

**Screen Name:**
Person Management View

**Purpose:**
Administrative person management console displaying all organizational Person records (active and inactive), operational metrics (Total Persons, Active, Inactive), keyword search across name and email, status filtering, and action triggers for adding, editing, activating, or deactivating Person records. Access restricted strictly to users possessing the Administrator or Admin role.

**Primary Actors:**
* Administrator
* Admin

**Supported Use Cases:**
* UC-ORG-001: Manage Organizational Persons

**Supported User Journeys:**
* UJ-ORG-001: Manage Organizational Persons

**Entry Points:**
* Global Navigation Sidebar (`Administration > Person Management` — route `/admin/persons`)

**Exit / Destination:**
* `SCR-ORG-002: Add / Edit Person Modal`

---

### SCR-ORG-002

**Screen Name:**
Add / Edit Person Modal

**Purpose:**
Modal dialog interface for capturing First Name, Last Name, and Email to register a new organizational Person record, or updating identity attributes of an existing Person record, with duplicate email conflict validation.

**Primary Actors:**
* Administrator
* Admin

**Supported Use Cases:**
* UC-ORG-001: Manage Organizational Persons

**Supported User Journeys:**
* UJ-ORG-001: Manage Organizational Persons

**Entry Points:**
* Person Management View (`SCR-ORG-001`) — "Add Person" button action (`data-testid="add-person-btn"`)
* Person Management View (`SCR-ORG-001`) — Person table row "Edit" button action (`data-testid="edit-person-btn"`)

**Exit / Destination:**
* Person Management View (`SCR-ORG-001`) — closes modal and refreshes persons table upon successful save or cancellation


