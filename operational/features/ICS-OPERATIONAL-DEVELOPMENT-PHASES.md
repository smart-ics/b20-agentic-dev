# ICS Operational System — Development Phases

## Recommended Implementation Sequence

Features should be built in dependency order, not numerical order. The sequence below follows the dependency chain:

```
ORGANIZATION
    ↓
CUSTOMER + PRODUCT
    ↓
REQUEST
    ↓
POST
    ↓
GLOBAL FEED
    ↓
WORK PACKAGE
    ↓
ATTENTION
    ↓
EXECUTIVE
    ↓
ANALYTICS
```

---

## Phase 1 — Operational Foundation

### 1. Organization — People
Everything else needs a Person.

### 2. Organization — Roles
### 3. Organization — Responsibilities
### 4. Organization — Teams

Establish the organizational reference model before operational work.

---

## Phase 2 — Master Data

### 5. Customer — Customer Management
### 6. Customer — Contact Management
### 7. Product — Product Management
### 8. Product — Product Ownership

At this point you have: People, Customers, Products, Owners — but still no operational workflow.

---

## Phase 3 — Core Operational Engine

### 9. Request — Request Management
The most important implementation after master data. You can now actually record: "Something needs to be done."

### 10. Request — Request Ownership
Very important because the workflow starts with: REQUEST → ASSIGN OWNER.

### 11. Request — Request Context
Allow: Request → Customer, Product, Work Package.

### 12. Request — Request Lifecycle
### 13. Request — Request Resolution

Now the basic operational loop works: Request → Owner → Work → Resolve → Close.

---

## Phase 4 — Communication

This is where the system starts becoming different from Jira.

### 14. Post — Post Creation
When a Request is created: Request Created → Post Automatically Created. This is a very important integration boundary.

### 15. Post — Post Discussion
Comments.

### 16. Post — Operational Reaction
Your operational reactions.

### 17. Post — Post Reference
Links to Request / Product / Customer / Work Package.

### 18. Post — Permanent Link
Now people can share: /post/1234

### 19. Global Feed — Global Feed
Build this immediately after Post basics. Now you get the behavior you actually wanted:

```
Request Created
       ↓
Post
       ↓
Global Feed
       ↓
Everyone sees it
```

This is arguably the first real "ICS Operational System" moment.

### 20. Global Feed — Filters
Only after the global stream works. Don't start with filtered views. Your default should remain organizational awareness.

### 21. Global Feed — Feed Actions
Share / copy link / open source.

### 22. Global Feed — Pinned Posts
### 23. Global Feed — Seen Tracking
Seen tracking can come relatively late; it doesn't need to block the core feed.

---

## Phase 5 — Work Package

### 24. Work Package — Work Package Management
### 25. Work Package — Work Package Ownership
### 26. Work Package — Request Grouping
### 27. Work Package — Work Package Lifecycle

Now multiple Requests can be grouped when the operational situation warrants it. This is exactly consistent with the rule that a Request can exist independently of a Work Package.

---

## Phase 6 — Management Control

Only after the underlying operational data exists should you build the management layer.

### 28. Operational Control — Attention
Now attention can actually derive from real data: Requests, Work Packages, Posts, Ownership, Aging, Escalations.

### 29. Operational Control — Escalation
### 30. Operational Control — Operational Radar
### 31. Operational Control — Decision Support

At this stage you're no longer guessing what the COO needs; you're deriving it from actual operational state.

---

## Phase 7 — Management Views

### 32. Executive — Executive Summary
### 33. Executive — Operational Trends
### 34. Executive — Entity Analytics
### 35. Executive — Executive Decisions
### 36. Executive — Executive Review

This is deliberately late. **Do not start by building the dashboard.** A dashboard built before operational data exists becomes manually maintained decoration. Your manifesto explicitly says dashboards and analytics are derived from operational state.

---

## Phase 8 — Platform Capabilities

These can be introduced incrementally, but prioritize:

### 37. Authentication & Access Control
This actually needs to exist earlier at a technical level, but the feature can remain infrastructural rather than driving the business sequence.

### 38. Audit Trail
### 39. Search
### 40. Notification

Search can move earlier if it materially improves daily use.

---

## Feature-to-Phase Mapping

| Phase | Feature Code | Items |
|-------|---------|-------|
| 1 | FT-02 Organization | FT-02-01 People, FT-02-03 Roles, FT-02-04 Responsibilities, FT-02-02 Teams |
| 2 | FT-03 Customer, FT-04 Product | FT-03-01 Customer Management, FT-03-02 Contact Management, FT-04-01 Product Management, FT-04-02 Product Ownership |
| 3 | FT-05 Request | FT-05-01 Request Management, FT-05-02 Request Ownership, FT-05-03 Request Context, FT-05-04 Request Lifecycle, FT-05-05 Request Resolution |
| 4 | FT-07 Post, FT-08 Global Feed | FT-07-01 Post Creation, FT-07-02 Post Discussion, FT-07-03 Operational Reaction, FT-07-04 Post Reference, FT-07-06 Permanent Link, FT-08-01 Global Feed, FT-08-02 Feed Filters, FT-08-03 Feed Actions, FT-08-04 Pinned Posts, FT-08-05 Seen Tracking |
| 5 | FT-06 Work Package | FT-06-01 Work Package Management, FT-06-02 Work Package Ownership, FT-06-03 Request Grouping, FT-06-04 Work Package Lifecycle |
| 6 | FT-09 Operational Control | FT-09-01 Attention, FT-09-02 Escalation, FT-09-03 Operational Radar, FT-09-04 Decision Support |
| 7 | FT-01 Executive | FT-01-01 Executive Summary, FT-01-02 Operational Trends, FT-01-05 Entity Analytics, FT-01-03 Executive Decisions, FT-01-04 Executive Review |
| 8 | FT-10 Platform | FT-10-01 Authentication & Access Control, FT-10-04 Audit Trail, FT-10-02 Search, FT-10-03 Notification |