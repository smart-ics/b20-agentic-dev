---
Title: ICS Operational System Target Architecture
Code: ICS
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-09-27
---

# 1. Overview

Target architecture for the greenfield ICS Operational System.
This architecture realizes the complete product definition encompassing the Customer, Organization, Post, and Request domains, and all defined operational features and use-cases.

# 2. Architectural Basis

## Business Context

Reference complete product definition:
- `operational/domains/*` (Customer, Organization, Post, Request, Product, Work Package)
- `operational/use-cases/*`
- `operational/features/*`

## Analysis Input

Greenfield execution based on approved product-definition artifacts.
The architecture is derived from:

```text
Approved Product Definition (Domains, Scenarios, Features)
        ↓
    ARCHITECTURE
```

# 3. Scope

Included:
- Technical boundaries for the complete ICS Operational System
- Data ownership for Customer, Organization, Post, Request, Product domains
- UI/API and Database structuring
- Module and Application Component mappings

Excluded:
- Human Resources Management (out of scope for Organization domain)
- Detailed implementation code

# 4. Architectural Drivers

- Clear domain boundaries and strict data ownership (no shared write ownership).
- Presentation projections for cross-domain features (like Feed and Analytics) must not mutate authoritative domain state.
- Permanent preservation of operational and historical history.

# 5. System Structure

```text
Presentation
Application
Domain
Infrastructure
```

- **Presentation**: Renders UI, handles user input, dispatches commands/queries to Application layer.
- **Application**: Coordinates use cases, manages transactions, cross-module orchestration.
- **Domain**: Pure business rules, state machines, domain events.
- **Infrastructure**: Database access, external integrations, event bus.

# 6. Module Boundaries

| Module | Responsibility | Owned Domains | Owned Data |
|---|---|---|---|
| Customer | Customer master data | Customer | Customers, Contacts |
| Organization | Organizational identity & roles | Organization | Persons, Teams, Roles, Responsibilities, Memberships, Assignments |
| Request | Request lifecycle and resolution | Request | Requests, Resolutions, Assignments |
| Post | Operational communication and feed | Post | Posts, Comments, Reactions, References |
| Product | Product catalog reference | Product | Products |
| Work Package | Work collaboration | Work Package | Work Packages |

# 7. Component Responsibilities

| Component | Responsibility |
|------------|---------------|
| `CustomerService` | Customer & Contact management commands/queries |
| `OrganizationService` | Person, Role, Team management commands/queries |
| `RequestService` | Request lifecycle commands (Record, Assign, Evaluate, Accept, Reject, Escalate, Complete) |
| `RequestQueryService` | Request historical search and progress queries |
| `PostService` | Post authoring, commenting, reacting commands |
| `FeedQueryService` | Cross-domain feed projection and filtering queries |
| `ManagementAnalyticsService` | Cross-domain analytical projections (Workload, Performance, Customer Progress) |

# 8. Use Case Mapping

| Use Case | Application Component | Domain Components |
|---|---|---|
| UC-COL-001 | `RequestService`, `PostService` | Request, Post |
| UC-COL-002 | `RequestQueryService` | Request |
| UC-COL-003 | `RequestQueryService` | Request |
| UC-COL-004 | `RequestQueryService` | Request, Organization |
| UC-FCOL-001 | `PostService` | Post |
| UC-FCOL-002 | `PostService` | Post |
| UC-FCOL-003 | `PostService` | Post |
| UC-FCOL-004 | `FeedQueryService` | Post, Request |
| UC-FCOL-005 | `FeedQueryService` | Post, Customer, Product, Organization |
| UC-MGT-001 | `RequestService` | Request, Organization |
| UC-MGT-002 | `ManagementAnalyticsService`| Request, Customer |
| UC-MGT-003 | `ManagementAnalyticsService`| Request, Organization |
| UC-MGT-004 | `ManagementAnalyticsService`| Request, Organization |
| UC-AWR-001..003 | `FeedQueryService` | Post, Request |
| UC-REQ-001..008 | `RequestService` | Request, Organization, Customer |

# 9. Feature Mapping

| Feature | Technical Module | Application Component | UI Boundary | Persistence |
|---|---|---|---|---|
| FEAT-REQ-001..008 | Request | `RequestService` | `SCR-REQ-*` | Request DB |
| FEAT-COL-001..004 | Request | `RequestQueryService`, `RequestService` | `SCR-REQ-*` | Request DB |
| FEAT-FCOL-001..005 | Post | `PostService`, `FeedQueryService` | `SCR-FEED-*`, `SCR-POST-*` | Post DB |
| FEAT-MGT-002..004 | Request | `ManagementAnalyticsService` | `SCR-MGT-*` | Request DB (Read) |
| FEAT-AWR-001 | Post | `FeedQueryService` | `SCR-FEED-001` | Post DB |

# 10. Integration Design

| Source | Target | Interface | Purpose | Ownership |
|---|---|---|---|---|
| Post | Request/Customer/Org | Domain Events | Link context to posts | Post |
| Request | Organization | Read API / DB View | Validate owner assignment | Request |
| ManagementAnalytics | Request, Org, Customer | Read APIs / Projections | Aggregate metrics | Request |

# 11. Data Ownership

| Data | Owner Module | Write Authority | Read Consumers |
|---|---|---|---|
| Customer, Contact | Customer | Customer Module | Request, Post, Analytics |
| Person, Role, Team | Organization | Organization Module | Request, Post, Analytics |
| Request, Resolution | Request | Request Module | Post, Analytics |
| Post, Comment, Reaction| Post | Post Module | Feed UI, Request UI |

# 12. Database Design

## New Tables

| Table | Owner Module | Purpose |
|---------|---------|---------|
| `Customers` | Customer | Customer master |
| `CustomerContacts`| Customer | Contact master |
| `Persons` | Organization | Individual master |
| `Teams`, `Roles` | Organization | Structure master |
| `Requests` | Request | Request state and details |
| `Resolutions` | Request | Request completion details |
| `Posts` | Post | Content and state |
| `Comments` | Post | Thread messages |
| `Reactions` | Post | Structured reactions |
| `PostReferences`| Post | Links to external domains |

## Migration Considerations

Greenfield system. No legacy migration required.

# 13. Cross-Cutting Concerns

- **Security & Authorization**: Role-based access control based on Organization domain Persons.
- **Audit Logging**: Mandatory tracking of state changes (e.g. Request transitions).
- **Domain Events**: Internal event bus for triggering system-generated posts.

# 14. Technical Decisions

- Architecture Pattern: Modular Monolith
- Communication: In-process method calls or internal event bus
- Persistence: Relational DB with schema segregation per module

# 15. Implementation Constraints

- Vertical-slice organization per module.
- Strict data ownership: No cross-module direct database writes.
- Read projections allowed across boundaries for Analytics and Feed.

# 16. Implementation Boundaries

| Boundary | Responsibility | Repository | Depends On | Implementation Notes |
|---|---|---|---|---|
| Foundation | Core abstractions | ICS-Core | None | Shared interfaces |
| Organization | Org master | ICS-Modules | Foundation | No upstream dependencies |
| Customer | Customer master | ICS-Modules | Foundation | No upstream dependencies |
| Request | Request lifecycle | ICS-Modules | Foundation, Org, Cust | Core operational flow |
| Post | Feed and comms | ICS-Modules | Foundation | Event-driven integration |

# 17. Implementation Dependency Graph

```text
Foundation
    ↓
Organization / Customer / Product
    ↓
Request / Work Package
    ↓
Post (Feed)
    ↓
Management Analytics
```

# 18. Acceptance Conditions

1. Every approved domain has a technical home.
2. Every approved use case has an implementing application boundary.
3. Every approved feature has an explicit technical mapping.
4. Every important persistent data concept has an owner.
5. Integration boundaries are explicit.
6. Major cross-cutting concerns are resolved.
7. Technical constraints are explicit.
8. Implementation boundaries are identifiable.
9. Architectural dependencies are identifiable.
10. The implementation-plan skill can create slices without having to redesign the target architecture.
