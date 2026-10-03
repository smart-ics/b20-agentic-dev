---
Title: Request Complexity Rating (1 to 5) Technical Architecture
Code: CR-005
Artifact: ARCHITECTURE
Version: 1.0
LastUpdated: 2026-10-03
---

# 1. Overview

This architecture defines the authoritative technical realization for Change Request `CR-005`: Request Complexity Rating (1 to 5) for Operational Demands.

It realizes [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md), [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md), [FEAT-REQ-003-evaluate-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-003-evaluate-request.md), and the approved gap-closure decisions from [CR-005-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-005-FEASIBILITY-ASSESSMENT.md). It establishes an authoritative numerical complexity attribute (`1` to `5`) on the `Request` aggregate root, persists it in the `[request].[Requests]` table with relational constraints, enforces role-based authorization for modifications across active lifecycle states, publishes domain events for change auditing, exposes new application commands and HTTP endpoints, and presents the rating across user interfaces.

---

# 2. Architectural Basis

## Business Context

- DOMAIN: [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/request-domain.md) (v1.2)
- DOMAIN: [organization-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/domains/organization-domain.md)
- FEATURE: [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md)
- FEATURE: [FEAT-REQ-003-evaluate-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-003-evaluate-request.md)
- ISSUE: [CR-005-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-005-ISSUE.md)
- SYSTEM ARCHITECTURE: [CAKRA-ARCHITECTURE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/architecture/CAKRA-ARCHITECTURE.md)

## Analysis Input

- FEASIBILITY ASSESSMENT: [CR-005-FEASIBILITY-ASSESSMENT.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/analysis/CR-005-FEASIBILITY-ASSESSMENT.md) (Status: `READY-FOR-PLANNING`)

This architecture consumes and realizes the approved decisions:
- `GAP-001`: Add `Complexity` (int, 1–5, default 1) to `Request` aggregate root with domain validation invariants and `SetComplexity(...)` mutation method.
- `GAP-002`: Add `[Complexity]` column with `DEFAULT 1` and `CHECK (Complexity BETWEEN 1 AND 5)` to `[request].[Requests]`, backfilling historical records and updating repository/query mappings.
- `GAP-003`: Introduce `UpdateRequestComplexityCommand` for standalone updates during any active state; add optional `Complexity` parameter to `RecordRequestCommand` and `EvaluateRequestCommand`.
- `GAP-004`: Introduce domain event `RequestComplexityUpdated` capturing old/new values, actor ID, timestamp, and optional reason.
- `GAP-005`: Enforce role verification checking for `Programmer`, `Administrator`, `Team Lead`, or `Manager` prior to allowing complexity mutations.
- `GAP-006`: Map `Complexity` into `RequestDto` and display it in Request views.
- `GAP-007`: Synchronize domain and feature documentation.
- `OQ-001`: Closed requests (`Completed`, `Rejected`) reject complexity modifications.
- `OQ-002`: Historical database records are backfilled to `1`.
- `OQ-003`: Justification/reason note is optional (max 500 characters).
- `OQ-004`: Dedicated grid filtering/sorting is deferred; field is surfaced on read models and UI detail/list components.

---

# 3. Scope

## Included

1. **Domain Layer (`Cakra.Modules.Request.Domain`)**:
   - `Complexity` property on aggregate root [`Request`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs) with encapsulation (`public int Complexity { get; private set; } = 1;`).
   - Domain invariants in `Request.Record(...)` and `Request.SetComplexity(...)` enforcing integer range `1 <= Complexity <= 5`.
   - Lifecycle invariant: throw `InvalidRequestStateTransitionException` if `SetComplexity` is invoked when `Status` is `RequestStatus.Completed` or `RequestStatus.Rejected`.
   - Domain event `RequestComplexityUpdated` in `Cakra.Modules.Request.Domain.Events`.

2. **Application & Service Layer (`Cakra.Modules.Request.Services`)**:
   - `RecordRequestCommand` and validator updated with optional `int? Complexity = null`.
   - `EvaluateRequestCommand` and validator updated with optional `int? Complexity = null`.
   - New `UpdateRequestComplexityCommand` and FluentValidation validator `UpdateRequestComplexityCommandValidator`.
   - Role authorization check in `RequestService` verifying that the actor possesses at least one authorized role (`Programmer`, `Administrator`, `Team Lead`, `Manager`).
   - Updated `RequestDto` containing `public int Complexity { get; init; }`.

3. **Persistence & Database Migration (`Cakra.Modules.Request.Persistence`, `Cakra.Api`)**:
   - Idempotent migration script `0013_add_request_complexity.sql` adding `[Complexity] INT NOT NULL CONSTRAINT [DF_Requests_Complexity] DEFAULT 1` and `CONSTRAINT [CK_Requests_Complexity_Range] CHECK ([Complexity] BETWEEN 1 AND 5)`.
   - Updated Dapper parameterized SQL queries in [`RequestRepository.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Persistence/RequestRepository.cs) and [`RequestQueryService.cs`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestQueryService.cs).

4. **API Gateway & Controllers (`Cakra.Api.Controllers`)**:
   - Updated `POST /api/v1/requests` body contract with optional `complexity`.
   - Updated `POST /api/v1/requests/{id}/evaluate` body contract with optional `complexity`.
   - New endpoint `PATCH /api/v1/requests/{id}/complexity` invoking `UpdateRequestComplexityCommand`.

5. **Frontend Application (`Cakra.Web`)**:
   - Surfacing `complexity` badge/indicator in `RequestDetailView.vue` and `RequestListView.vue`.
   - Optional complexity selector (1–5) in `CreateRequestModal.vue` and `CreateRequestView.vue`.
   - Complexity adjustment affordance on `RequestDetailView.vue` for authorized roles.

## Excluded

- Modifying existing external or non-request modules (`Customer`, `Product`, `Post`, `Analytics`).
- Custom algorithmic complexity calculators or automatic time estimation models.
- Dedicated grid multi-range filtering or backend index alterations for complexity (deferred per OQ-004).

---

# 4. Technical Decisions

## TD-001: Authoritative Domain Modeling & Invariants
`Complexity` is a core aggregate property of `Request`, not an untyped metadata dictionary.
- Initial factory method `Request.Record(...)` defaults `complexity` to `1` when `null`.
- Invariants are checked immediately: if value `< 1` or `> 5`, throws `RequestDomainValidationException`.
- Dapper parameterless constructor instantiates `Complexity = 1` as baseline.

```csharp
public int Complexity { get; private set; } = 1;
```

## TD-002: Lifecycle Immobility & Mutation Method
To ensure domain consistency, mutating complexity requires calling a dedicated domain method:
```csharp
public void SetComplexity(
    int newComplexity,
    Guid actorPersonId,
    string? reason = null,
    DateTime? utcNow = null)
{
    if (newComplexity < 1 || newComplexity > 5)
        throw new RequestDomainValidationException("Complexity must be between 1 and 5.", nameof(newComplexity));

    if (actorPersonId == Guid.Empty)
        throw new RequestDomainValidationException("ActorPersonId cannot be empty.", nameof(actorPersonId));

    if (Status == RequestStatus.Completed || Status == RequestStatus.Rejected)
    {
        throw new InvalidRequestStateTransitionException(
            Id, Status, nameof(SetComplexity), reason: "Cannot change complexity on a closed request.");
    }

    if (Complexity == newComplexity)
        return;

    var previousComplexity = Complexity;
    var now = utcNow ?? DateTime.UtcNow;

    Complexity = newComplexity;
    UpdatedAt = now;

    _domainEvents.Add(new RequestComplexityUpdated(
        requestId: Id,
        previousComplexity: previousComplexity,
        newComplexity: newComplexity,
        actorPersonId: actorPersonId,
        reason: reason?.Trim(),
        occurredAtUtc: now));
}
```

## TD-003: In-Process Domain Event Auditability
`RequestComplexityUpdated` implements `IDomainEvent` and records:
- `RequestId` (Guid)
- `PreviousComplexity` (int)
- `NewComplexity` (int)
- `ActorPersonId` (Guid)
- `Reason` (string?, trimmed, max 500 chars)
- `OccurredAtUtc` (DateTime)

Dispatched via existing `IDomainEventDispatcher` upon aggregate commit in `RequestService`.

## TD-004: Dual-Path Command API Design
1. **Creation & Triage Evaluation**:
   - `RecordRequestCommand`: optional `int? Complexity = null` (defaults to `1` if omitted).
   - `EvaluateRequestCommand`: optional `int? Complexity = null` (if provided, invokes `request.SetComplexity(...)`).
2. **Dedicated Mutation Command**:
   - `UpdateRequestComplexityCommand(Guid RequestId, int Complexity, string? Reason, Guid? ActorPersonId)`:
     Allows authorized actors to adjust complexity at any time during `Captured`, `Evaluating`, `Accepted`, `InProgress`, or `Escalated` status without requiring triage notes.

## TD-005: Role-Based Authorization Enforcement
Authorization is checked inside `RequestService` before executing complexity mutations:
- Authorized role names: `Programmer`, `Administrator`, `Admin`, `Developer`, `Team Lead`, `Manager`.
- Resolution: `RequestService` queries the actor's roles via `IAuthorizationService.ResolveRolesAsync(actorPersonId, cancellationToken)` or `IOrganizationQueryService`.
- If the actor does not possess any authorized role, the service throws an `UnauthorizedAccessException` or returns a 403 Forbidden problem details.

## TD-006: Schema Migration & Check Constraints
Database migration is written as an idempotent SQL script `0013_add_request_complexity.sql`:
```sql
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[request].[Requests]') AND name = N'Complexity'
)
BEGIN
    ALTER TABLE [request].[Requests]
    ADD [Complexity] INT NOT NULL CONSTRAINT [DF_Requests_Complexity] DEFAULT (1);

    ALTER TABLE [request].[Requests]
    ADD CONSTRAINT [CK_Requests_Complexity_Range] CHECK ([Complexity] BETWEEN 1 AND 5);
END;
```
Adding `INT NOT NULL CONSTRAINT [DF_Requests_Complexity] DEFAULT (1)` guarantees that all existing historical records are automatically backfilled to `1` without requiring manual multi-statement UPDATE scripts, and subsequent inserts default to `1`.

## TD-007: DTO Mapping & UI Badging
`RequestDto` includes `public int Complexity { get; init; }`.
In `Cakra.Web`, complexity is presented visually using standard badge styles:
- `1`: Badge / Label "Complexity: 1 (Very Low)"
- `2`: Badge / Label "Complexity: 2 (Low)"
- `3`: Badge / Label "Complexity: 3 (Medium)"
- `4`: Badge / Label "Complexity: 4 (High)"
- `5`: Badge / Label "Complexity: 5 (Very High)"

---

# 5. Component Responsibilities

| Component | Responsibility |
|---|---|
| [`Request`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs) | Enforces aggregate invariants, validates 1–5 range, manages `Complexity` state and emits `RequestComplexityUpdated`. |
| `RequestComplexityUpdated` | Domain event representing an authoritative change in request complexity. |
| `UpdateRequestComplexityCommand` | MediatR command and FluentValidation rules for complexity modification. |
| [`RequestService`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs) | Verifies role authorization, executes domain methods, persists changes via repository, and dispatches events. |
| [`RequestRepository`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Persistence/RequestRepository.cs) | Maps `Complexity` column in Dapper SQL queries for `[request].[Requests]`. |
| [`RequestQueryService`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestQueryService.cs) | Projections of `Complexity` into `RequestDto` and grid results. |
| [`RequestsController`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/RequestsController.cs) | Exposes `PATCH /api/v1/requests/{id}/complexity`, updates `POST` and `/evaluate` payload bindings. |
| `Cakra.Web` (`RequestDetailView.vue`, `RequestListView.vue`, `CreateRequestModal.vue`) | Collects optional complexity on create, displays badge in lists/details, and provides complexity edit action for authorized roles. |

---

# 6. Integration Design

| Source | Target | Purpose |
|---|---|---|
| `RequestsController` | `IMediator` | Dispatches `UpdateRequestComplexityCommand`, `RecordRequestCommand`, and `EvaluateRequestCommand`. |
| `RequestService` | `IAuthorizationService` / `IOrganizationQueryService` | Resolves active roles for `ActorPersonId` to authorize complexity modifications. |
| `RequestService` | `IRequestRepository` | Loads and persists `Request` aggregate with updated `Complexity`. |
| `Request` (Domain) | `IDomainEventDispatcher` | Publishes `RequestComplexityUpdated` event after transaction commit. |
| `Cakra.Web` | `RequestsController` (HTTP API) | Submits `PATCH /api/v1/requests/{id}/complexity` and reads `complexity` on `RequestDto`. |

---

# 7. Data Ownership

| Data Element | Owner |
|---|---|
| `Request.Complexity` | `Cakra.Modules.Request` (`[request].[Requests]`) |
| `Complexity` history / audit events | `Cakra.Modules.Request` (`RequestComplexityUpdated` domain event) |
| Actor Role Definitions | `Cakra.Modules.Organization` / `Cakra.Modules.Identity` |

No cross-schema direct writes are permitted.

---

# 8. Database Design

## New Tables
*None.*

## Modified Tables

| Table | Change |
|---|---|
| `[request].[Requests]` | Add column `[Complexity] INT NOT NULL DEFAULT (1)` with constraint `CHECK ([Complexity] BETWEEN 1 AND 5)`. |

## Relationships
No new foreign key relationships are introduced. `ActorPersonId` in events remains a logical cross-module reference without foreign key constraints.

## Migration Considerations
- Migration script: `0013_add_request_complexity.sql` placed in `cakra/src/backend/Cakra.Api/Migrations/Scripts/`.
- Executed automatically on API startup via existing migration runner (`MigrationRunner.cs`).
- Default constraint ensures all historical rows instantly become valid (`Complexity = 1`) without null reference exceptions or data patching scripts.

---

# 9. Cross-Cutting Concerns

## Security & Authorization
- Only authenticated users with `Programmer`, `Administrator`, `Admin`, `Developer`, `Team Lead`, or `Manager` roles can modify complexity.
- Requesters can only submit initial complexity if they have permission or when recording internal requests; otherwise, it defaults to 1.
- Unauthorized attempts return HTTP 403 Forbidden with standard `ProblemDetails`.

## Audit & Observability
- Every change to complexity emits `RequestComplexityUpdated` containing `PreviousComplexity`, `NewComplexity`, `ActorPersonId`, `Reason`, and `OccurredAtUtc`.
- Structured logging records the modification in application logs.

## Data Integrity & Robustness
- Triple-layer validation:
  1. Frontend form input boundaries (1 to 5).
  2. FluentValidation command validators (`InclusiveBetween(1, 5)`).
  3. Domain entity validation (`RequestDomainValidationException`).
  4. Relational database check constraint (`CK_Requests_Complexity_Range`).

---

# 10. Implementation Constraints

1. **Framework & Architecture**: Must use .NET 8, C# 12, MediatR command pipeline, and FluentValidation.
2. **Persistence**: Must use Dapper with parameterized SQL; no ORMs or EF Core.
3. **Module Boundaries**: All persistence changes must remain strictly inside `[request]` schema.
4. **State Invariance**: Closed requests (`Completed`, `Rejected`) cannot have their complexity modified under any circumstance.
5. **Backwards Compatibility**: Existing callers of `POST /api/v1/requests` that omit `complexity` must continue to function seamlessly, defaulting to 1.

---

# 11. Acceptance Conditions

1. `Request` aggregate root rejects complexity values `< 1` or `> 5`.
2. Newly created requests without an explicit complexity are initialized with `Complexity = 1`.
3. Newly created requests with explicit valid complexity (1–5) are initialized with that value.
4. `SetComplexity` transitions `Complexity` and emits `RequestComplexityUpdated` when called on active requests (`Captured`, `Evaluating`, `Accepted`, `InProgress`, `Escalated`).
5. Calling `SetComplexity` on a request with status `Completed` or `Rejected` throws `InvalidRequestStateTransitionException`.
6. `UpdateRequestComplexityCommand` executes successfully for authorized roles (`Programmer`, `Administrator`, `Team Lead`, `Manager`) and fails with 403 for unauthorized users.
7. Database migration `0013_add_request_complexity.sql` executes idempotently, adds column and constraint, and leaves all existing records with `Complexity = 1`.
8. `RequestDto` surfaces `Complexity`, which is rendered in `RequestDetailView.vue` and `RequestListView.vue`.
9. All unit, service, and integration tests for the Request module pass with 100% success.
