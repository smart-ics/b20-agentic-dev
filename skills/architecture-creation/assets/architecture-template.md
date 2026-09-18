---
Title:
Code:
Artifact: ARCHITECTURE
Version:
LastUpdated:
---

# 1. Overview

Feature realized by this architecture.

Referenced FEATURE.

# 2. Architectural Basis

## Domain–Feature

Referenced DOMAIN and FEATURE that define:

- Business Knowledge
- Business Outcome
- Operational Flow
- Domain Orchestration

## Feasibility Assessment

Referenced FEASIBILITY-ASSESSMENT that provides:

- Current State
- Gap Analysis
- Closed Open Questions
- Approved Decisions
- Relevant Risks and Assumptions

The architecture is derived from:

```text
DOMAIN + FEATURE
        +
FEASIBILITY-ASSESSMENT
        ↓
   ARCHITECTURE
```

# 3. Scope

Included.

Excluded.

# 4. Technical Decisions

Important decisions required for implementation.

Feasibility Decisions are approved during Gap Closure.

Technical Decisions describe how those approved decisions are realized within the target architecture.

ARCHITECTURE must not reinterpret or rediscover feasibility findings.

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

# Database Design

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
