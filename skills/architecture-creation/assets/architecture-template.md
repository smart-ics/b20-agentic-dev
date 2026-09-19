---
Title:
Code:
Artifact: ARCHITECTURE
Version:
LastUpdated:
---

# 1. Overview

Feature or bug correction realized by this architecture.

Referenced FEATURE or originating BUG ISSUE.

# 2. Architectural Basis

## Business Context

Reference DOMAIN and FEATURE for a CHANGE-REQUEST, or the originating BUG
ISSUE for a BUG. When present, DOMAIN and FEATURE define:

- Business Knowledge
- Business Outcome
- Operational Flow
- Domain Orchestration

## Analysis Input

Reference one of the following analysis artifacts:

- FEASIBILITY-ASSESSMENT for a CHANGE-REQUEST
- BUG-INVESTIGATION for a BUG

The analysis artifact provides:

- Current State
- Analysis findings
- Impact
- Assumptions
- Open Questions
- Approved Decisions

For a CHANGE-REQUEST, the architecture is derived from:

```text
DOMAIN + FEATURE
        +
        FEASIBILITY-ASSESSMENT
        ↓
    ARCHITECTURE
```

For a BUG, the architecture is derived from:

```text
ISSUE (BUG)
        +
        BUG-INVESTIGATION
        ↓
    ARCHITECTURE
```

# 3. Scope

Included.

Excluded.

# 4. Technical Decisions

Important decisions required for implementation.

Feasibility decisions are approved during Gap Closure. Investigation decisions
are approved during BUG-INVESTIGATION.

Technical Decisions describe how those approved decisions are realized within
the target architecture.

ARCHITECTURE must not repeat investigation, gap analysis, alternative
evaluation, or decision resolution already completed by the upstream analysis
artifact.

Examples:

- Framework
- Storage
- Messaging
- Authentication
- Deployment constraints

# 5. Component Responsibilities

| Component | Responsibility |
|------------|---------------|

Every implementation responsibility must have exactly one owner.

# 6. Integration Design

| Source | Target | Purpose |
|----------|----------|----------|

Describe collaboration between components.

Do not describe business flow.

# 7. Data Ownership

| Data | Owner |
|--------|--------|

Define ownership.

Avoid shared ownership.

# 8. Database Design

## New Tables

| Table | Purpose |
|---------|---------|

## Modified Tables

| Table | Change |
|---------|---------|

## Relationships

Architecturally significant relationships introduced or modified.

## Migration Considerations

Data migration, backfill, compatibility, and rollout considerations.

# 9. Cross-Cutting Concerns

Only if applicable.

Examples:

- Security
- Audit
- Logging
- Observability
- Performance
- Concurrency

# 10. Implementation Constraints

Rules implementers must follow.

Examples:

- Must use Dapper
- SQL Server only
- No Entity Framework
- REST only
- Existing API compatibility

# 11. Acceptance Conditions

Conditions that must be true for implementation to be considered architecturally complete.
