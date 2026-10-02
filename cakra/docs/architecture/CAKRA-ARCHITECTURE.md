---
Title: CAKRA - ICS Operational System Target Architecture
Code: CAKRA
Artifact: ARCHITECTURE
Version: 1.4
LastUpdated: 2026-10-02
---

# 1. Overview

Target architecture for the greenfield CAKRA - ICS Operational System (Cakra).
This architecture realizes the complete product definition encompassing the Customer, Organization, Product, Work Package, Request, and Post domains, cross-cutting Identity and Access Management, and all operational features and use cases.

# 2. Architectural Basis

## Business Context

Reference complete product definition:
- `operational/domains/*` (Customer, Organization, Product, Work Package, Request, Post)
- `operational/actors/actor-model.md`
- `operational/manifesto/*` (Operational Principles, Knowledge Lifecycle, Workflow)
- `operational/scenarios/*`
- `operational/use-cases/*`
- `operational/features/*`
- `operational/ui-layout/*`
- `operational/navigation/*`

## Analysis Input

Greenfield execution based on approved product-definition artifacts.
The architecture is derived from:

```text
Approved Product Definition (Domains, Actor Model, Scenarios, Features)
                            ↓
                       ARCHITECTURE
```

# 3. Scope

## Included
- Technical boundaries for all core modules: Identity & Access, Organization, Customer, Product, Work Package, Request, Post, and Management Analytics
- Data ownership and relational persistence schemas for all domains
- Detailed Feed Architecture using an asynchronous/in-process Materialized Read Model (`FeedItems`)
- Detailed Analytics Architecture using a Dual Model: Real-Time Dynamic Queries for active operational workload and Materialized Snapshot Tables for historical performance and monthly comparisons
- Detailed Identity & Authentication boundary defining login credentials, session management, and the explicit 1-to-1 binding between authenticated `UserAccount` and organizational `Person`
- Application components, CQRS command/query service separation, integration contracts, and module dependency graphs

## Excluded
- Human Resources Management (payroll, attendance, leave management, recruitment)
- External billing/accounting execution engines (contracts and invoices are referenced, not processed)
- Implementation code and framework-specific low-level boilerplate

# 4. Architectural Drivers

- **Strict Single Ownership of Write Models**: Each domain entity has exactly one authoritative owning module with exclusive write authority. No cross-module database writes are permitted.
- **Derived Read Models & Projections**: Presentation projections for cross-domain features (such as the Feed and Management Analytics) derive from authoritative domain state and must never mutate authoritative business entities.
- **Explicit Actor & Identity Distinction**: Clear separation between Security User (`UserAccount`), Organizational Individual (`Person`), Organizational Role (`Role`), Area Accountability (`Responsibility`), and Work Ownership (`Operational Assignment`).
- **Permanent History Preservation**: Operational history, lifecycle state transitions, discussions, reactions, and periodic analytical snapshots are preserved permanently for auditability and institutional learning.
- **Predictable Sub-50ms Feed & Operational Response Times**: Core landing views (Feed, Active Queues) rely on denormalized read models and targeted queries rather than runtime distributed joins.

# 5. System Structure & Architectural Pattern

The system is architected as a **Modular Monolith** organized into distinct, decoupled vertical modules sharing an in-process host, with clean layer separation inside each module:

```text
┌─────────────────────────────────────────────────────────────┐
│                     Presentation Layer                      │
│   (Web UI, Screen Views SCR-*, API Controllers, ViewModels) │
└──────────────────────────────┬──────────────────────────────┘
                               │ Dispatches Commands / Queries
┌──────────────────────────────▼──────────────────────────────┐
│                      Application Layer                      │
│   (Command Services, Query Services, Projection Handlers)   │
└──────────────────────────────┬──────────────────────────────┘
                               │ Invokes Aggregates & Emits Events
┌──────────────────────────────▼──────────────────────────────┐
│                        Domain Layer                         │
│  (Aggregates, Value Objects, Domain Events, State Machines) │
└──────────────────────────────┬──────────────────────────────┘
                               │ Persists via Repositories
┌──────────────────────────────▼──────────────────────────────┐
│                    Infrastructure Layer                     │
│  (Relational DB Segregation, Event Bus, Session Store, IAM) │
└─────────────────────────────────────────────────────────────┘
```

- **Presentation**: Renders UI screens (`SCR-*`), handles HTTP/WebSocket connections, evaluates display view-models, and invokes application services.
- **Application**: Orchestrates use cases, coordinates database transactions, executes query projections, and dispatches in-process domain events.
- **Domain**: Encapsulates pure business logic, invariants, state transitions, and domain event creation without external dependencies.
- **Infrastructure**: Provides database access via relational repositories, in-process event publishing, security token handling, and system clock/audit logging.

# 6. Module Boundaries

| Module | Responsibility | Owned Domains | Owned Data / Tables |
|---|---|---|---|
| **Identity & Access** | Authentication, credentials, sessions, security tokens | Identity | `UserAccounts`, `UserSessions` |
| **Organization** | Authoritative organizational identity, structure, roles, and accountability | Organization | `Persons`, `Teams`, `Roles`, `Responsibilities`, `TeamMemberships`, `RoleAssignments`, `ResponsibilityAssignments` |
| **Customer** | Customer master data and institutional contacts | Customer | `Customers`, `CustomerContacts` |
| **Product** | Product catalog master data, product codes, and product ownership | Product | `Products` |
| **Work Package** | Temporary grouping container for related requests sharing a common objective | Work Package | `WorkPackages`, `WorkPackageRequests` |
| **Request** | Operational request lifecycle, evaluation, assignment, escalation, and resolution | Request | `Requests`, `RequestResolutions`, `RequestAssignments` |
| **Post** | Persistent operational communication, discussions, comments, reactions, and references | Post | `Posts`, `Comments`, `Reactions`, `PostReferences` |
| **Feed (Read Tier)** | Denormalized operational feed stream, search, filtering, and exception badges | Post (Projection) | `FeedItems` (Materialized Read Model) |
| **Management Analytics (Read Tier)** | Real-time workload capacity and periodic historical performance snapshots | Cross-Domain (Read Model) | `DailyWorkloadSnapshots`, `MonthlyCustomerPerformanceSnapshots` |

# 7. Component Responsibilities

| Component | Layer | Responsibility |
|---|---|---|
| `AuthenticationService` | Application | Validates credentials, checks account/person active status, manages sessions, issues tokens |
| `AuthorizationService` | Application | Resolves user's assigned organizational roles and evaluates role-based access policies |
| `CurrentContextProvider` | Infrastructure | Exposes ambient `CurrentUserId`, `CurrentPersonId`, and `CurrentRoles` for request execution |
| `CustomerService` | Application | Customer & Contact creation and master updates |
| `CustomerQueryService` | Application | Queries active customer records, contacts, and contract statuses for UI dropdowns |
| `OrganizationService` | Application | Manages Persons, Teams, Roles, Responsibilities, Memberships, and Assignments |
| `OrganizationQueryService` | Application | Queries active persons, team rosters, and role assignments for routing and UI lookups |
| `ProductService` | Application | Commands to create products, update attributes, assign product owners, and toggle status |
| `ProductQueryService` | Application | Queries active products, catalog listings, and product owner associations |
| `WorkPackageService` | Application | Commands to create work packages, update objectives, assign owners, add/remove requests, and manage lifecycle (DRAFT, ACTIVE, CLOSED) |
| `WorkPackageQueryService` | Application | Queries work package details, scope listings, and request-to-work-package associations |
| `RequestService` | Application | Executes request state transitions: Record, Assign, Evaluate, Accept, Reject, Escalate, Request Decision, Complete |
| `RequestQueryService` | Application | Queries request details, state history, my assigned requests, and filtered request grids |
| `PostService` | Application | Recording system posts (event-driven via `RequestRecorded` etc.), posting comments, adding reactions, toggling visibility, archiving |
| `PostQueryService` | Application | Queries post thread details, full comments, and reaction lists for post modal |
| `FeedProjectionHandler` | Application | Listens to domain events (`PostCreated`, `CommentAdded`, `ReactionAdded`, etc.) and synchronously/asynchronously updates `FeedItems` |
| `FeedQueryService` | Application | Executes high-performance indexed queries over `FeedItems` with filtering by Customer, Product, Team, or Exception |
| `ManagementAnalyticsService` | Application | Computes dynamic real-time workload aggregations (`SCR-MGT-003`, `SCR-MGT-001`) and serves snapshot performance trends (`SCR-MGT-002`) |
| `AnalyticsSnapshotJob` | Infrastructure | Scheduled background worker capturing daily workload snapshots and monthly customer performance records |

# 8. Use Case Mapping

| Use Case | Name | Application Component | Participating Domains |
|---|---|---|---|
| **UC-AUTH-001** | Authenticate User & Establish Session | `AuthenticationService` | Identity, Organization |
| **UC-REQ-001** | Record Customer Request | `RequestService` | Request, Customer, Organization, Product |
| **UC-REQ-002** | Assign Request Owner | `RequestService` | Request, Organization |
| **UC-REQ-003** | Evaluate Request | `RequestService` | Request, Organization |
| **UC-REQ-004** | Accept Request Responsibility | `RequestService` | Request, Organization |
| **UC-REQ-005** | Reject Request | `RequestService` | Request, Organization |
| **UC-REQ-006** | Escalate Request | `RequestService` | Request, Organization |
| **UC-REQ-007** | Request Management Decision | `RequestService` | Request, Organization |
| **UC-REQ-008** | Review Request Completion | `RequestService` | Request, Customer, Organization |
| **UC-COL-001** | Record Supporting Information | `PostService`, `RequestService` | Request, Post |
| **UC-COL-002** | Search Request History | `RequestQueryService` | Request |
| **UC-COL-003** | Track Request Progress | `RequestQueryService` | Request |
| **UC-COL-004** | Review Assigned Requests | `RequestQueryService` | Request, Organization |
| **UC-FCOL-001** | Comment on Post | `PostService` | Post |
| **UC-FCOL-002** | React to Post | `PostService` | Post |
| **UC-FCOL-003** | Create Operational Post *(DECOMMISSIONED - CR-001)* | *(Decommissioned)* | Post |
| **UC-FCOL-004** | Navigate from Post to Request | `FeedQueryService`, `RequestQueryService` | Post, Request |
| **UC-FCOL-005** | Filter Operational Feed | `FeedQueryService` | Post, Customer, Product, Organization |
| **UC-AWR-001** | Observe Operational Feed | `FeedQueryService` | Post |
| **UC-AWR-002** | Discover Request via Feed | `FeedQueryService`, `RequestQueryService` | Post, Request |
| **UC-AWR-003** | Monitor Operational Exceptions via Feed | `FeedQueryService` | Post, Request |
| **UC-MGT-001** | Reassign Request Ownership | `RequestService` | Request, Organization |
| **UC-MGT-002** | Review Customer Request Progress | `ManagementAnalyticsService` | Request, Customer |
| **UC-MGT-003** | Review Programmer Request Performance | `ManagementAnalyticsService` | Request, Organization |
| **UC-MGT-004** | Review Programmer Workload | `ManagementAnalyticsService` | Request, Organization |
| **UC-PRD-001** | Maintain Product Catalog | `ProductService` | Product, Organization |
| **UC-PRD-002** | Query Products for Operational Context | `ProductQueryService` | Product |
| **UC-WP-001** | Manage Work Package Lifecycle | `WorkPackageService` | Work Package, Organization |
| **UC-WP-002** | Manage Work Package Scope | `WorkPackageService` | Work Package, Request |
| **UC-WP-003** | Review Work Package Scope & Progress | `WorkPackageQueryService` | Work Package, Request |

# 9. Feature Mapping

| Feature ID | Feature Name | Technical Module | Application Component | Primary UI Boundary | Persistence |
|---|---|---|---|---|---|
| **FEAT-AUTH-001** | Authenticate & Manage Session | Identity | `AuthenticationService` | `SCR-AUTH-001` | `UserAccounts`, `UserSessions` |
| **FEAT-REQ-001..008** | Request Lifecycle Operations | Request | `RequestService` | `SCR-REQ-001`, `SCR-REQ-002`, `SCR-REQ-003` | `Requests`, `RequestResolutions` |
| **FEAT-COL-001..004** | Request Collaboration & Tracking | Request | `RequestQueryService`, `RequestService` | `SCR-REQ-003`, `SCR-REQ-004`, `SCR-REQ-005` | `Requests`, `PostReferences` |
| **FEAT-FCOL-001..003** | Post Authoring, Comments & Reactions | Post | `PostService` | `SCR-FEED-001`, `SCR-POST-001` | `Posts`, `Comments`, `Reactions` |
| **FEAT-FCOL-005** | Filter Operational Feed | Post / Feed | `FeedQueryService` | `SCR-FEED-001` | `FeedItems` (Read Model) |
| **FEAT-AWR-001** | Observe Operational Feed | Post / Feed | `FeedQueryService` | `SCR-FEED-001` | `FeedItems` (Read Model) |
| **FEAT-MGT-002** | Review Customer Request Progress | Analytics | `ManagementAnalyticsService` | `SCR-MGT-001` | `Requests` (Real-Time Query) |
| **FEAT-MGT-003** | Review Programmer Request Performance | Analytics | `ManagementAnalyticsService` | `SCR-MGT-002` | `Requests`, `DailyWorkloadSnapshots` |
| **FEAT-MGT-004** | Review Programmer Workload | Analytics | `ManagementAnalyticsService` | `SCR-MGT-003` | `Requests` (Real-Time Query) |
| **FEAT-PRD-001** | Product Catalog & Ownership | Product | `ProductService`, `ProductQueryService` | `SCR-PRD-001`, Selectors | `Products` |
| **FEAT-WP-001** | Work Package Lifecycle & Grouping | Work Package | `WorkPackageService`, `WorkPackageQueryService` | `SCR-WP-001`, `SCR-REQ-003` | `WorkPackages`, `WorkPackageRequests` |

# 10. Product Module Architecture

## Domain Context
The Product Domain is the authoritative source of product identity, product code, description, ownership, and lifecycle status (`ACTIVE` vs `INACTIVE`). Products represent business products developed, maintained, or provided by ICS (e.g. MyHospital, PenaEl, BTrade3).

## Components & Responsibilities
- **`ProductService`**:
  - `CreateProduct(code, name, description, ownerPersonId)`: Validates unique code, ensures owner exists in Organization, initializes status to `ACTIVE`, emits `ProductCreated`.
  - `UpdateProduct(productId, name, description)`: Updates descriptive attributes.
  - `AssignProductOwner(productId, newOwnerPersonId)`: Updates owner and emits `ProductOwnerChanged`.
  - `ActivateProduct(productId)` / `DeactivateProduct(productId)`: Transitions lifecycle status, emitting `ProductActivated` or `ProductDeactivated`.
- **`ProductQueryService`**:
  - `GetProductById(productId)`: Returns authoritative product record.
  - `GetProductByCode(code)`: Returns product record by unique business code.
  - `ListActiveProducts()`: Supplies active product list for UI dropdown selectors on `SCR-REQ-002` (Create Request), `SCR-FEED-001` (Feed Filter), and `SCR-WP-001` (Work Package).
  - `ListAllProducts()`: Supplies complete catalog including inactive products for historical audit views.

## Data Ownership & Rules
- Owns the `Products` table.
- Does not own Customer-Product relationships (Customer Domain), Request-Product relationships (Request Domain), or Work Package-Product relationships (Work Package Domain).
- Product status is authoritative; external modules and analytics cannot invent synthetic product states.

# 11. Work Package Module Architecture

## Domain Context
The Work Package Domain defines a temporary container of related operational Requests that share a common objective. It provides operational grouping context without owning or altering Request lifecycles.

## Components & Responsibilities
- **`WorkPackageService`**:
  - `CreateWorkPackage(name, objective, ownerPersonId, customerId?, productId?)`: Creates package in `DRAFT` state, assigns owner, associates optional customer or product, emits `WorkPackageCreated`.
  - `UpdateObjective(workPackageId, name, objective)`: Updates objective and title.
  - `AssignOwner(workPackageId, newOwnerPersonId)`: Reassigns package ownership, emits `WorkPackageOwnerChanged`.
  - `AddRequestToWorkPackage(workPackageId, requestId)`: Validates that the request is not already in another active package (Business Rule 9), associates request in `WorkPackageRequests`, emits `RequestAddedToWorkPackage`.
  - `RemoveRequestFromWorkPackage(workPackageId, requestId)`: Deactivates membership link, records removal timestamp, emits `RequestRemovedFromWorkPackage`.
  - `ActivateWorkPackage(workPackageId)`: Transitions state from `DRAFT` to `ACTIVE`, emits `WorkPackageActivated`.
  - `CloseWorkPackage(workPackageId, reason)`: Transitions state from `ACTIVE` (or `DRAFT`) to `CLOSED`, records close timestamp, emits `WorkPackageClosed`.
- **`WorkPackageQueryService`**:
  - `GetWorkPackageById(workPackageId)`: Returns package details with owner and customer/product references.
  - `ListWorkPackages(statusFilter, customerId?, productId?, ownerPersonId?)`: Returns filtered list of work packages.
  - `GetWorkPackageScope(workPackageId)`: Returns current and historical requests included in the package.
  - `GetRequestWorkPackage(requestId)`: Resolves active work package containing a given request.

## Data Ownership & Rules
- Owns `WorkPackages` and `WorkPackageRequests` tables.
- A Request belongs to zero or one active Work Package.
- Adding or removing a Request does not alter the Request Owner or the Request lifecycle state.
- Closing a Work Package does not close its constituent Requests, and closing all Requests does not automatically close the Work Package.

# 12. Feed Architecture

## Architectural Decision: Materialized Read Model (`FeedItems`)
The Feed is architected as an **in-process Materialized Read Model** backed by a dedicated projection table (`FeedItems`) residing in the Post module schema.

## Justification
1. **Primary Landing Workspace**: `SCR-FEED-001` is the main screen accessed continuously by all actors. High concurrency, sub-50ms render, and immediate pagination responsiveness are critical.
2. **Elimination of Cross-Schema Joins**: Feed cards require data from 5 distinct contexts: Post content, Author identity (Organization), Referenced Request details (Request), Referenced Customer name (Customer), Referenced Product name (Product), and aggregate Reaction counts and Comment previews. Executing dynamic cross-schema joins with sorting and filtering at runtime would create severe database bottlenecks and tight coupling.
3. **Strict Domain Integrity**: The projection table is strictly read-only for queries. Authoritative state remains in `Posts`, `Comments`, `Reactions`, and `PostReferences`.

## Feed Projection Table Schema (`FeedItems`)
```text
FeedItemId (UUID, Primary Key)
PostId (UUID, Unique Index, FK -> Posts.PostId)
AuthorPersonId (UUID)
AuthorName (VARCHAR(100))
PostType (VARCHAR(30))              -- 'SYSTEM_GENERATED' | 'HUMAN_AUTHORED'
Title (VARCHAR(255))
ContentExcerpt (VARCHAR(500))
Status (VARCHAR(20))                -- 'ACTIVE' | 'ARCHIVED'
Visibility (VARCHAR(20))            -- 'VISIBLE' | 'HIDDEN'
IsException (BOOLEAN)               -- TRUE if escalation, rejection, or stalled request
ExceptionType (VARCHAR(50) NULL)    -- 'ESCALATION' | 'STALLED' | 'REJECTION'
ReferenceType (VARCHAR(50) NULL)    -- 'REQUEST' | 'WORK_PACKAGE' | 'CUSTOMER' | 'PRODUCT'
ReferenceId (UUID NULL)
ReferenceDisplay (VARCHAR(200) NULL) -- e.g. 'REQ-2026-0042: Billing Error'
CustomerId (UUID NULL, INDEXED)
CustomerName (VARCHAR(150) NULL)
ProductId (UUID NULL, INDEXED)
ProductName (VARCHAR(150) NULL)
CommentCount (INT DEFAULT 0)
LatestCommentExcerpt (VARCHAR(300) NULL)
ReactionCountsJson (JSON / TEXT)    -- e.g. {"SEEN": 4, "EXPERIENCED": 2, "HAVE_IDEA": 1}
CreatedAt (DATETIME, INDEXED)
UpdatedAt (DATETIME)
```

## Update Triggers & Synchronization
Updates to `FeedItems` are orchestrated by `FeedProjectionHandler` subscribing to in-process domain events:
- **`PostCreated`**: Inserts a new row into `FeedItems`. Enriches customer/product/reference display attributes from event payload or in-memory caches.
- **`CommentAdded`**: Increments `CommentCount`, updates `LatestCommentExcerpt` and `UpdatedAt`.
- **`ReactionAdded` / `ReactionRemoved`**: Updates `ReactionCountsJson` atomically.
- **`PostVisibilityChanged`**: Updates `Visibility` (`VISIBLE` or `HIDDEN`).
- **`PostArchived`**: Updates `Status = 'ARCHIVED'`.

*Synchronization Guarantee*: Events are handled in-process within the same database transaction scope as the command, ensuring zero eventual consistency lag for the user executing the action.

## Query Semantics
`FeedQueryService.GetFeed(filter, pagination)` executes a single-table indexed query:
```sql
SELECT * FROM FeedItems
WHERE Visibility = 'VISIBLE' AND Status = 'ACTIVE'
  AND (:CustomerId IS NULL OR CustomerId = :CustomerId)
  AND (:ProductId IS NULL OR ProductId = :ProductId)
  AND (:ExceptionsOnly IS FALSE OR IsException = TRUE)
ORDER BY CreatedAt DESC
LIMIT :PageSize OFFSET :Offset;
```

## Rebuild Strategy
`FeedProjectionRebuilder.RebuildAll()` is an idempotent administrative routine that can truncate `FeedItems` and completely regenerate it from authoritative `Posts`, `PostReferences`, `Comments`, `Reactions`, and reference lookup tables at any time without data loss.

# 13. Analytics Architecture

## Architectural Decision: Dual Analytics Model
Analytics is architected using a **Dual Model**:
1. **Real-Time Dynamic Aggregations** for operational visibility, active workload distribution, and current customer queue health.
2. **Periodic Materialized Snapshot Tables** for historical performance analysis, turnaround trends, and monthly management comparisons.

## Justification
- **Operational Visibility (`FEAT-MGT-004`, `FEAT-MGT-002`)**: Management needs 100% up-to-the-second accuracy when evaluating programmer workloads and reassigning blocked requests. Because active requests form a compact operational working set (hundreds of rows), dynamic query aggregation on indexed operational tables is fast (< 20ms) and eliminates cache invalidation bugs.
- **Historical Analysis & Trends (`FEAT-MGT-003`, COO Monthly Reports)**: Historical requests accumulate indefinitely over years. Calculating historical throughput, resolution time averages, and month-over-month comparisons on raw transaction logs is inefficient. Materialized snapshot tables freeze periodic operational facts at regular intervals, ensuring immutable historical auditability even when employees change roles or leave the company.

## Analytics Components & Storage

### 1. Real-Time Operational Projections (`ManagementAnalyticsService`)
- `GetProgrammerActiveWorkload(personId?)`: Dynamically aggregates `Requests` where `Status IN ('CAPTURED', 'ACTIVE')` grouped by `OwnerPersonId` and sub-state (`CAPTURED`, `EVALUATING`, `ACCEPTED`, `IN_PROGRESS`, `ESCALATED`).
- `GetCustomerRequestPortfolio(customerId)`: Dynamically queries active requests, open blockers, and recent completions for a customer, joined with customer maintenance contract status.

### 2. Snapshot Storage Tables (`Analytics` Schema)

#### `DailyWorkloadSnapshots`
Records end-of-day workload and throughput for each team member:
```text
SnapshotId (UUID, Primary Key)
SnapshotDate (DATE, Indexed)
PersonId (UUID, Indexed, FK -> Persons.PersonId)
ActiveRequestsCount (INT)
EscalatedRequestsCount (INT)
StalledRequestsCount (INT)
CompletedRequestsToday (INT)
AvgAgeHours (DECIMAL(10,2))
CapturedAt (DATETIME)
-- Constraint: UNIQUE(SnapshotDate, PersonId)
```

#### `MonthlyCustomerPerformanceSnapshots`
Records end-of-month operational summary per customer:
```text
SnapshotId (UUID, Primary Key)
YearMonth (VARCHAR(7), Indexed)     -- '2026-09'
CustomerId (UUID, Indexed, FK -> Customers.CustomerId)
TotalRequests (INT)
ResolvedRequestsCount (INT)
RejectedRequestsCount (INT)
AvgResolutionHours (DECIMAL(10,2))
SlaMetCount (INT)
SlaBreachedCount (INT)
CapturedAt (DATETIME)
-- Constraint: UNIQUE(YearMonth, CustomerId)
```

## Refresh & Execution Model
- **`AnalyticsSnapshotJob`**:
  - **Daily Snapshot**: Runs nightly at 23:59:59. Computes end-of-day metrics for all active persons and inserts into `DailyWorkloadSnapshots`.
  - **Monthly Snapshot**: Runs on the 1st day of each month at 00:05:00. Aggregates preceding calendar month metrics per customer and inserts into `MonthlyCustomerPerformanceSnapshots`.
- **On-Demand Recomputation**: `ManagementAnalyticsService.RecomputeSnapshots(startDate, endDate)` allows idempotent backfilling or recomputing snapshot records if historical corrections occur.
- **Immutability**: Once recorded, snapshot records are treated as immutable historical facts.

# 14. Identity & Authentication Architecture

## Architectural Decision: Explicit IAM Boundary Bound to Organization Person
The system defines an independent **Identity & Access Management (IAM)** technical module answering "Who logs in?" and managing credentials, sessions, and authentication tokens, while delegating business identity and organizational roles to the Organization Domain.

## Separation of Concerns
- **Identity Module**: Owns authentication credentials, password hashing, active sessions, security tokens, and login audit trails.
- **Organization Domain**: Owns the `Person` business entity, organizational `Roles` (e.g. `Management`, `Programmer`, `Implementator`, `Administrator`), and area `Responsibilities` (e.g. `Module PIC`, `Customer Pimpro`).
- **1-to-1 Association**: Every `UserAccount` possesses a unique foreign key `PersonId` referencing an authoritative `Person`. A user cannot log in unless both their `UserAccount` and linked `Person` are in `ACTIVE` status.

## IAM Components
- **`AuthenticationService`**:
  - `Login(usernameOrEmail, password, clientInfo) -> LoginResult`: Verifies password against cryptographic hash (Argon2id / bcrypt), checks `UserAccount.Status == 'ACTIVE'`, checks linked `Person.Status == 'ACTIVE'`, creates record in `UserSessions`, issues session token/cookie.
  - `Logout(sessionToken)`: Invalidates session in `UserSessions`.
  - `ValidateSession(sessionToken) -> SecurityContext`: Validates token, checks expiry, returns authenticated `UserId` and `PersonId`.
- **`AuthorizationService`**:
  - Resolves active `Roles` assigned to `PersonId` from the Organization module's `RoleAssignments`.
  - Enforces role-based permissions (e.g. checking `Management` role for `SCR-MGT-*`, or `Programmer`/`Implementator` role for request evaluations).
- **`CurrentContextProvider`**:
  - Ambient request context populated on every incoming request, exposing `CurrentUserId`, `CurrentPersonId`, and `CurrentRoles`.

## IAM Persistence Tables

### `UserAccounts`
```text
UserId (UUID, Primary Key)
PersonId (UUID, Unique Index, FK -> Persons.PersonId)
Username (VARCHAR(50), Unique Index)
Email (VARCHAR(150), Unique Index)
PasswordHash (VARCHAR(255))
Status (VARCHAR(20))                -- 'ACTIVE' | 'LOCKED' | 'SUSPENDED'
FailedLoginAttempts (INT DEFAULT 0)
LastLoginAt (DATETIME NULL)
CreatedAt (DATETIME)
UpdatedAt (DATETIME)
```

### `UserSessions`
```text
SessionId (UUID, Primary Key)
UserId (UUID, FK -> UserAccounts.UserId)
PersonId (UUID, FK -> Persons.PersonId)
SessionToken (VARCHAR(255), Unique Index)
ExpiresAt (DATETIME, Indexed)
CreatedAt (DATETIME)
ClientIp (VARCHAR(45))
UserAgent (VARCHAR(255))
IsRevoked (BOOLEAN DEFAULT FALSE)
```

## UI Boundary
- `SCR-AUTH-001: Login Screen`: Accepts username/password, handles login failures, sets authentication cookie/token, and redirects user to `SCR-FEED-001`.

# 15. Integration Design

| Source Component | Target Component | Integration Style | Purpose | Ownership |
|---|---|---|---|---|
| `AuthenticationService` | `OrganizationQueryService` | In-Process Query | Validate `Person` status and resolve `PersonId` | Identity |
| `AuthorizationService` | `OrganizationQueryService` | In-Process Query | Fetch active `Roles` assigned to `PersonId` | Identity |
| `RequestService` | `OrganizationQueryService` | In-Process Query | Validate assignee exists and holds valid role | Request |
| `RequestService` | `CustomerQueryService` | In-Process Query | Validate customer identity and maintenance contract | Request |
| `RequestService` | `ProductQueryService` | In-Process Query | Validate product reference | Request |
| `WorkPackageService` | `OrganizationQueryService` | In-Process Query | Validate work package owner exists | Work Package |
| `WorkPackageService` | `RequestQueryService` | In-Process Query | Validate request exists and check active membership | Work Package |
| `PostService` | Request, Customer, Product, Org, WP | Domain Events & Query | Attach contextual references to posts | Post |
| `FeedProjectionHandler` | Domain Events | In-Process Event Bus | Update `FeedItems` projection on post/comment/reaction | Post (Feed) |
| `ManagementAnalyticsService` | `RequestQueryService`, `CustomerQueryService` | Read Projection / Query | Compute real-time workload and customer portfolio | Analytics |
| `AnalyticsSnapshotJob` | Request, Organization, Customer | Scheduled Query Batch | Capture daily and monthly frozen snapshot metrics | Analytics |

# 16. Data Ownership

| Data Concept | Owning Module | Write Authority | Read Consumers |
|---|---|---|---|
| User Credentials & Sessions | Identity & Access | Identity Module | Web Presentation Layer, Security Middleware |
| Person, Team, Role, Responsibility, Assignments | Organization | Organization Module | Identity, Request, Work Package, Post, Analytics |
| Customer, Customer Contact | Customer | Customer Module | Request, Work Package, Post, Analytics |
| Product | Product | Product Module | Request, Work Package, Post, Analytics |
| Work Package, Work Package Membership | Work Package | Work Package Module | Request, Post, Analytics |
| Request, Request Resolution, Request Assignment | Request | Request Module | Work Package, Post, Analytics |
| Post, Comment, Reaction, Post Reference | Post | Post Module | Feed UI, Post UI, Request UI |
| Feed Projection Items | Post (Feed Tier) | `FeedProjectionHandler` | Feed UI (`SCR-FEED-001`) |
| Workload & Performance Snapshots | Analytics | `AnalyticsSnapshotJob` | Management UI (`SCR-MGT-*`) |

# 17. Database Design

## Schema Segregation
Relational database with schema segregation per module:
- `identity.*`: IAM tables
- `organization.*`: Organizational master tables
- `customer.*`: Customer master tables
- `product.*`: Product catalog tables
- `workpackage.*`: Work package grouping tables
- `request.*`: Request lifecycle tables
- `post.*`: Communication and feed read model tables
- `analytics.*`: Snapshot tables

## New Tables Summary

| Table | Schema | Owner Module | Purpose |
|---|---|---|---|
| `UserAccounts` | `identity` | Identity | User credentials and 1-to-1 linkage to Person |
| `UserSessions` | `identity` | Identity | Active login sessions and token expiration |
| `Persons` | `organization` | Organization | Authoritative person records |
| `Teams`, `Roles`, `Responsibilities` | `organization` | Organization | Organizational structure and accountability definitions |
| `TeamMemberships`, `RoleAssignments`, `ResponsibilityAssignments` | `organization` | Organization | Person-to-team/role/responsibility relationships |
| `Customers` | `customer` | Customer | Customer master records and contract status |
| `CustomerContacts` | `customer` | Customer | Customer contact persons |
| `Products` | `product` | Product | Product catalog, codes, and product owners |
| `WorkPackages` | `workpackage` | Work Package | Work package identity, objective, and owner |
| `WorkPackageRequests` | `workpackage` | Work Package | Membership link between work packages and requests |
| `Requests` | `request` | Request | Request lifecycle state, owner, priority, customer, product |
| `RequestResolutions` | `request` | Request | Resolution outcome, summary, and resolution timestamp |
| `Posts` | `post` | Post | Human and system operational communication records |
| `Comments` | `post` | Post | Thread discussion comments |
| `Reactions` | `post` | Post | Structured operational reactions on posts |
| `PostReferences` | `post` | Post | Contextual links from posts to external domain objects |
| `FeedItems` | `post` | Post (Feed) | Materialized read model for high-performance feed queries |
| `DailyWorkloadSnapshots` | `analytics` | Analytics | Daily frozen snapshots of programmer workloads |
| `MonthlyCustomerPerformanceSnapshots` | `analytics` | Analytics | Monthly frozen snapshots of customer delivery metrics |

## Migration Considerations
Greenfield system. Initial schema migration scripts will execute in dependency order (Identity & Foundation -> Organization -> Customer -> Product -> Work Package -> Request -> Post -> Analytics).

# 18. Cross-Cutting Concerns

- **Authentication & Security Context**: Every incoming HTTP/API request passes through authentication middleware that validates the session token against `UserSessions` and populates `CurrentContextProvider`.
- **Role-Based Access Control (RBAC)**: Authorization is enforced at application service and presentation boundaries using `[Authorize(Roles = "...")]` mapped to the authenticated user's active `Roles` resolved from Organization.
- **Audit Logging**: Every state change in `Requests`, `WorkPackages`, and `Posts` records actor `PersonId`, timestamp, and previous state.
- **Domain Event Bus**: In-process synchronous event dispatcher with transactional consistency. Events trigger projection updates (`FeedItems`) and automated system post generation without distributed queue complexity.
- **Exception Handling & Validation**: Centralized application validation layer verifying preconditions and business rules before mutating aggregate state.

# 19. Technology Decisions

> [!IMPORTANT]
> **Architecture Authority Rule**: Technology Decisions defined in this section are authoritative and binding for all implementation phases. Implementation agents must not substitute, alter, or introduce alternative technologies, frameworks, libraries, or data access paradigms unless this Architecture document is formally updated and approved.

## 19.1 Backend Stack
- **Runtime**: .NET 8 (LTS)
- **Web Framework**: ASP.NET Core 8.0
- **Programming Language**: C# 12 (nullable reference types enabled, implicit usings enabled)

## 19.2 Application Architecture
- **Pattern**: Vertical Slice Architecture (feature-oriented vertical slices where each slice encapsulates its own request, handler, domain operations, Dapper SQL queries, and response model).
- **In-Process Mediator**: MediatR (Mediator pattern for CQRS Command and Query dispatching, pipeline behaviors, and domain event notifications).
- **Validation**: FluentValidation (strongly typed request validation executed automatically via MediatR pipeline behaviors prior to handler execution).

## 19.3 Persistence & Data Access
- **Database Engine**: Microsoft SQL Server 2019.
- **Data Access**: Dapper (Lightweight high-performance micro-ORM).
- **SQL Strategy**: Explicit SQL. All database operations must execute explicit, handcrafted, optimized, parameterized SQL queries and commands against SQL Server schemas (`identity`, `organization`, `customer`, `product`, `workpackage`, `request`, `post`, `analytics`).
- **ORM Prohibition**: Entity Framework (EF Core) or any other full ORM is **not used and strictly prohibited**. Implementation agents must not reference EF Core packages (`Microsoft.EntityFrameworkCore*`) or introduce automated ORM change tracking.
- **Database Migrations & Schema Provisioning**: Sequential idempotent raw SQL scripts managed and executed in strict dependency order via DbUp upon application startup or migration CLI tool execution.

## 19.4 Frontend Stack
- **Framework**: Vue 3 (Composition API with `<script setup lang="ts">`).
- **Language**: TypeScript.
- **UI Framework & Design System**: Bootstrap 5 (with Bootstrap Icons and standard responsive grid layout).
- **Build Tooling**: Vite.
- **Routing & State Management**: Vue Router 4 for client-side navigation; Pinia for shared client-side application state.
- **HTTP Client**: Axios or native `fetch` with standard authentication interceptors.

## 19.5 Authentication & Authorization
- **Authentication Mechanism**: Cookie Authentication using secure, HttpOnly, SameSite=Strict session cookies.
- **Session Strategy**: Server-side session verification backed by `identity.UserSessions`. Each successful login creates an active `SessionToken` tied to `UserId` and `PersonId` with configurable expiration and absolute/sliding timeout. Logout explicitly revokes the session in `identity.UserSessions`.
- **Authorization Mechanism**: Role-Based Access Control (RBAC). User roles are dynamically resolved from `organization.RoleAssignments` for the authenticated `PersonId` and mapped to ASP.NET Core claims and authorization policies (`[Authorize(Roles = "...")]`).
- **Credential Security**: Cryptographic password hashing using Argon2id (or ASP.NET Core `IPasswordHasher` using PBKDF2 with HMAC-SHA512).

## 19.6 API Style & Communication
- **API Style**: REST API (JSON over HTTP/HTTPS).
- **Endpoints**: ASP.NET Core Controllers (`[ApiController]`) or Minimal API route endpoints organizing REST routes (`/api/v1/{module}/{resource}`) and delegating execution to MediatR commands and queries.
- **Serialization**: `System.Text.Json` with camelCase naming policy.
- **Error Handling**: Centralized exception handling middleware producing standard RFC 7807 Problem Details (`ProblemDetails`) JSON responses with consistent error codes.

## 19.7 Background Processing
- **Mechanism**: ASP.NET Core Hosted Services (`IHostedService` / `BackgroundService`).
- **Worker Responsibilities**:
  - `AnalyticsSnapshotJob`: Periodic background timer worker triggering daily workload snapshot aggregation (at 23:59:59) into `analytics.DailyWorkloadSnapshots` and monthly customer performance snapshot aggregation (on the 1st of each month at 00:05:00) into `analytics.MonthlyCustomerPerformanceSnapshots`.
  - **Asynchronous Projection Processing**: In-process background worker queue (`System.Threading.Channels`) for background tasks and administrative feed rebuilds (`FeedProjectionRebuilder`).

## 19.8 Testing Strategy
- **Strategy**: Integration-first testing for vertical slices and HTTP endpoints; unit testing for pure domain models, business logic invariants, and state machine transition rules.
- **Test Framework**: xUnit.
- **Assertions**: FluentAssertions.
- **Integration Test Infrastructure**: `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`) executing against an isolated SQL Server test instance, using Respawn or transactional isolation to ensure clean test state between test runs.

## 19.9 Logging & Observability
- **Logging Framework**: Serilog configured with `Microsoft.Extensions.Logging`.
- **Log Format**: Structured JSON logging enriched with `TraceId`, `SpanId`, `UserId`, `PersonId`, and `SourceContext`.
- **Sinks**: Console (stdout / system log) and rolling file logs.
- **Health Checks**: ASP.NET Core Health Checks (`/health/live`, `/health/ready`) validating database connectivity and subsystem readiness.
- **Diagnostics**: Built-in .NET 8 `System.Diagnostics.Activity` and OpenTelemetry-compatible tracing identifiers.

## 19.10 Deployment & Runtime Strategy
- **Deployment Model**: Modular Monolith hosted as a single ASP.NET Core executable process (`Cakra.Api`) serving both REST API endpoints and static SPA frontend assets from `wwwroot/`.
- **Authoritative Production Target**: **IIS (Internet Information Services) on Windows Server** using In-Process hosting via the ASP.NET Core Module (`AspNetCoreHostingModel = InProcess` in `web.config`).
- **IIS Process Lifecycle & Supervision**: The dedicated IIS Application Pool (`CakraAppPool`) manages worker process execution (`w3wp.exe`), automatic process recycling, idle timeout management, and automatic crash restarts.
- **Unified Build & Packaging Process**:
  1. **Frontend Compilation**: Vue 3 SPA is compiled via Vite (`npm run build`) in `src/frontend/Cakra.Web/`, emitting production static assets into `src/frontend/Cakra.Web/dist/` (which are ingested into `src/backend/Cakra.Api/wwwroot/` during release packaging).
  2. **Backend Publication**: .NET 8 CLI executes `dotnet publish src/backend/Cakra.Api/Cakra.Api.csproj -c Release -o ./publish` producing the release package containing compiled binaries, dependencies, static web assets (in `./publish/wwwroot/`), and the IIS `web.config`.
  3. **Release Packaging**: Automated PowerShell deployment script (`deploy/publish.ps1`) packages the publication directory into a versioned deployment artifact ready for extraction into the IIS website physical directory.
- **Frontend Asset Flow & Ingestion**:
  ```text
  Frontend (src/frontend/Cakra.Web)
      ↓
  Vite Build (npm run build)
      ↓
  Static Assets (dist/)
      ↓
  Consumed by Backend Host (src/backend/Cakra.Api/wwwroot/ → publish/wwwroot/)
  ```
- **Database Migrations on Deployment**:
  DbUp-SqlServer automated migration runner executes at application startup within `Program.cs` or via a standalone CLI migration switch (`dotnet Cakra.Api.dll --migrate`) to apply idempotent SQL migrations in strict dependency order against SQL Server 2019 before HTTP traffic is served.
- **Runtime Assumptions & Configuration**:
  - Configuration supplied via `appsettings.Production.json` or Windows environment variables (`ConnectionStrings__DefaultConnection`, `ASPNETCORE_ENVIRONMENT=Production`).
  - Stateless application tier (session state maintained in SQL Server `identity.UserSessions`).
  - IIS HTTPS site binding terminates TLS/HTTPS (port 443) and routes traffic directly in-process to the ASP.NET Core application pipeline (`Cakra.Api`), monitoring health via `/health/live` and `/health/ready`.

## 19.11 Solution & Project Layout (Planning Compatibility)
The repository structure and project breakdown are strictly standardized with repository-level separation between backend and frontend to ensure unambiguous implementation planning, independent build tooling, clearer ownership, and easier onboarding:

```text
cakra/
├── Cakra.sln
├── src/
│   ├── backend/
│   │   ├── Cakra.Core/                       # Common domain abstractions, MediatR pipeline behaviors, Dapper helpers
│   │   ├── Cakra.Modules.Identity/           # IAM vertical slices, UserAccount & UserSession commands/queries
│   │   ├── Cakra.Modules.Organization/       # Organization slices: Persons, Teams, Roles, Responsibilities
│   │   ├── Cakra.Modules.Customer/           # Customer slices: Customers, Contacts
│   │   ├── Cakra.Modules.Product/            # Product slices: Products, Catalog queries
│   │   ├── Cakra.Modules.WorkPackage/        # Work Package slices: WorkPackages, Scope management
│   │   ├── Cakra.Modules.Request/            # Request slices: Lifecycle state machine, assignments, resolutions
│   │   ├── Cakra.Modules.Post/               # Post & Feed slices: Posts, Comments, Reactions, Feed projection
│   │   ├── Cakra.Modules.Analytics/          # Analytics queries and AnalyticsSnapshotJob hosted service
│   │   └── Cakra.Api/                        # ASP.NET Core Host, API Controllers, Middleware, DbUp migrations, Static Asset Host
│   │
│   └── frontend/
│       └── Cakra.Web/                        # Vue 3 + TypeScript + Bootstrap 5 + Vite Single Page Application
│
├── tests/
│   └── backend/
│       ├── Cakra.Tests.Unit/                 # Unit tests for domain logic, rules, and state machines
│       └── Cakra.Tests.Integration/          # Integration tests using WebApplicationFactory and SQL Server
│
└── docs/
```

### Testing Structure & Rationale
Backend test projects reside strictly within `tests/backend/` (`Cakra.Tests.Unit` and `Cakra.Tests.Integration`). Placing .NET test suites under `tests/backend/` mirrors the repository-level `src/backend/` separation and ensures future frontend automated testing suites (e.g. `tests/frontend/` using Vitest, Cypress, or Playwright) can be introduced cleanly without mixing .NET test runners and Node.js testing tooling.

### Core Package Dependencies:
- `MediatR`
- `Dapper`
- `Microsoft.Data.SqlClient`
- `FluentValidation.AspNetCore`
- `Serilog.AspNetCore`
- `DbUp-SqlServer`
- `Microsoft.AspNetCore.Mvc.Testing` (Test)
- `xunit` & `xunit.runner.visualstudio` (Test)
- `FluentAssertions` (Test)
- `Respawn` (Test)

# 20. Technical Constraints

- **SQL Server Relational Persistence**: All application data must reside in Microsoft SQL Server segregated by schema (`identity`, `organization`, `customer`, `product`, `workpackage`, `request`, `post`, `analytics`).
- **Strict EF Core Prohibition**: Entity Framework is not used. All persistence must use Dapper with explicit parameterized SQL.
- **Zero Cross-Schema Foreign Keys & Cross-Schema Direct Writes**: Tables in one schema must not have direct physical foreign key constraints or direct write operations to tables in another schema (except within documented same-module projections). References across modules are stored as raw identifier values (`UNIQUEIDENTIFIER`) and validated through application query interfaces.
- **Parameterization Requirement**: All Dapper queries must use SQL parameters. String interpolation or dynamic SQL concatenation of user input is strictly prohibited to prevent SQL injection vulnerabilities.
- **In-Process Communication**: Cross-module communication must use MediatR requests/notifications or in-process direct query interfaces. No distributed message brokers or HTTP calls between internal modules.
- **Sub-50ms Feed & Query Latency**: Landing screen queries (such as `SCR-FEED-001` over `post.FeedItems`) must execute as single-table indexed queries with sub-50ms target execution times.

# 21. Implementation Constraints

- **Strict Vertical Slice Boundary**: Each feature or use case must be implemented as a self-contained vertical slice (Request, Handler, Response, Validator, Dapper Query/Command). Modules must not expose internal domain entities or direct database commands across module boundaries.
- **Zero Shared Write Ownership**: Under no circumstances may a module perform `INSERT`, `UPDATE`, or `DELETE` on a table owned by another module.
- **Non-Mutating Projections**: Projections (`post.FeedItems`, `analytics.DailyWorkloadSnapshots`, `analytics.MonthlyCustomerPerformanceSnapshots`) are strictly read models and must never be treated as authoritative write state.
- **Permanent Data Retention**: Soft-delete or archiving only; operational history, posts, comments, and requests must never be physically purged from the database (`IsArchived = 1` / `Status = 'ARCHIVED'`).
- **Frontend Component Architecture**: Frontend screens (`SCR-*`) must be built as Vue 3 Single-File Components using Bootstrap 5 semantic classes, strictly adhering to the approved UI layouts in `operational/ui-layout/*` and navigation paths in `operational/navigation/*`.

# 22. Implementation Boundaries

| Boundary | Responsibility | Repository / Assembly | Depends On | Implementation Notes |
|---|---|---|---|---|
| **Foundation** | Core interfaces, base entities, domain event dispatchers, clock | `Cakra.Core` | None | Shared technical contracts |
| **Identity & Access** | Authentication, credentials, sessions, security tokens | `Cakra.Modules.Identity` | `Cakra.Core`, `Organization` (Read) | Owns `UserAccounts`, `UserSessions` |
| **Organization** | Person, team, role, and responsibility master data | `Cakra.Modules.Organization` | `Cakra.Core` | Foundational organizational master |
| **Customer** | Customer and contact master data | `Cakra.Modules.Customer` | `Cakra.Core` | Foundational customer master |
| **Product** | Product catalog master data and product ownership | `Cakra.Modules.Product` | `Cakra.Core`, `Organization` (Read) | Foundational product master |
| **Work Package** | Work package lifecycle and request grouping | `Cakra.Modules.WorkPackage` | `Cakra.Core`, `Organization`, `Customer`, `Product`, `Request` | Operational grouping container |
| **Request** | Request lifecycle, evaluation, assignment, resolution | `Cakra.Modules.Request` | `Cakra.Core`, `Organization`, `Customer`, `Product` | Core operational transactional engine |
| **Post & Feed** | Communication, comments, reactions, and feed read model | `Cakra.Modules.Post` | `Cakra.Core`, Domain Events from all modules | Feed materialized projection tier |
| **Management Analytics** | Workload capacity queries and historical snapshot batch jobs | `Cakra.Modules.Analytics` | `Cakra.Core`, `Request`, `Organization`, `Customer` | Management oversight read tier |
| **Backend Host** | ASP.NET Core process host, API endpoints, middleware, DbUp migrations | `Cakra.Api` | All Backend Modules | In-process composition root & static asset server |
| **Frontend Web** | Vue 3 Single Page Application (UI screens `SCR-*`, navigation, Pinia stores) | `Cakra.Web` | Backend REST API (`/api/v1/*`) | Client-side SPA built with Vite |

# 23. Implementation Dependency Graph

```text
       ┌──────────────┐
       │  Foundation  │
       └──────┬───────┘
              │
       ┌──────▼───────────────────────────┐
       │ Organization / Customer / Product│
       └──────┬───────────────────────────┘
              │
       ┌──────▼───────┐
       │   Identity   │ (depends on Organization for Person link)
       └──────┬───────┘
              │
       ┌──────▼───────┐
       │   Request    │ (depends on Org, Customer, Product)
       └──────┬───────┘
              │
       ┌──────▼───────┐
       │ Work Package │ (depends on Org, Customer, Product, Request)
       └──────┬───────┘
              │
       ┌──────▼───────┐
       │  Post & Feed │ (subscribes to domain events across all modules)
       └──────┬───────┘
              │
       ┌──────▼────────────────┐
       │  Management Analytics │ (reads Request, Org, Customer; snapshots)
       └───────────────────────┘
```

# 24. Acceptance Conditions

1. Every approved domain (Customer, Organization, Product, Work Package, Request, Post) has an explicit owning module and technical boundary.
2. The Work Package module is fully defined with `WorkPackageService`, `WorkPackageQueryService`, `WorkPackages` and `WorkPackageRequests` tables, data ownership, and use case/feature mappings.
3. The Product module is fully defined with `ProductService`, `ProductQueryService`, `Products` table, data ownership, and catalog use cases.
4. The Feed Architecture is explicitly defined as a Materialized Read Model (`FeedItems`) with documented projection schema, update triggers, in-process synchronization, and rebuild strategy.
5. The Analytics Architecture is explicitly defined using a Dual Model: real-time dynamic query aggregation for active workload visibility and scheduled snapshot tables (`DailyWorkloadSnapshots`, `MonthlyCustomerPerformanceSnapshots`) for historical trends and monthly comparisons.
6. The Identity and Authentication boundary is explicitly defined, detailing credential management, sessions, login flow on `SCR-AUTH-001`, and the 1-to-1 linkage between `UserAccount` and Organization `Person`.
7. Role-based access control (RBAC) maps authenticated users to `Roles` in the Organization domain without conflating identity with organizational accountability.
8. Every approved use case and feature has an explicit technical mapping to an application component, UI boundary, and persistence table.
9. Every database table has a single owning module with exclusive write authority.
10. Read projections (Feed, Analytics) do not mutate authoritative domain state.
11. Integration interfaces between modules are documented and respect domain boundaries.
12. Historical operational state, discussions, and analytics snapshots are permanently preserved.
13. The implementation-plan skill can generate clean phases and slices directly from this architecture without making unresolved architectural decisions.
14. The backend technology stack (.NET 8, ASP.NET Core, C# 12, MediatR, FluentValidation) is explicitly defined and authoritative.
15. The persistence stack (SQL Server, Dapper, explicit SQL) is explicitly defined, and Entity Framework (EF Core) is explicitly prohibited.
16. The frontend stack (Vue 3, TypeScript, Bootstrap 5, Vite, Pinia, Vue Router) is explicitly defined.
17. Authentication (Cookie Authentication, session backing in `UserSessions`), Authorization (RBAC), Testing (Integration-first, xUnit, FluentAssertions, WebApplicationFactory), Logging (Serilog structured logging), and Deployment (Modular Monolith single process, environment-configured runtime) are authoritative and unambiguous.

