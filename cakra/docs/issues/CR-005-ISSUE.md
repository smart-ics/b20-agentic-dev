# ISSUE

## Metadata

ID: CR-005
Type: CHANGE-REQUEST
Status: OPEN
Title: Request Complexity Rating (1 to 5) for Operational Demands

## Source

Reported By: User
Reported Date: 2026-10-03

## Description

The user requested that every operational Request must have a Complexity rating starting from 1 to 5, which can be set by a programmer or administrator.

All necessary functional and operational specifications for this change request:
1. **Scale and Range**: The complexity metric is an integer rating bounded between 1 (lowest/baseline) and 5 (highest). The rating is a pure numeric score based on team convention without system-enforced duration windows.
2. **Default Value and Creation**: Every newly recorded Request defaults to a complexity of 1. Requesters/intake actors may optionally provide an initial complexity value (1 to 5) when recording a Request if already assessed.
3. **Role Authorization**: Setting or updating the complexity rating is restricted to technical and operational management roles, specifically: Programmer, Administrator, Team Lead, and Manager.
4. **Lifecycle Constraints**: Complexity can be modified at any point while a Request is in an active lifecycle state (`Captured`, `Evaluating`, `Accepted`, `InProgress`, `Escalated`). Once a Request reaches a closed state (`Completed` or `Rejected`), the complexity value is immutable and cannot be updated.
5. **Auditability and Domain Events**: Any change to a Request's complexity value must emit a domain event (`RequestComplexityUpdated`) capturing the previous complexity, new complexity, modifying actor, timestamp, and an optional justification/reason note.
6. **Application and Command Interface**:
   - Provide a dedicated update command (`UpdateRequestComplexityCommand`) allowing authorized actors to adjust complexity across active lifecycle states.
   - Allow an optional complexity parameter in triage evaluation (`EvaluateRequestCommand`) so evaluation notes and complexity adjustments can be captured together.
7. **Query and Representation**: The complexity value must be exposed in `RequestDto` to support display on Request detail and grid views.
8. **Data Persistence and Historical Integrity**: The database schema must store complexity as an integer column enforcing a range constraint between 1 and 5 with a default of 1. All existing historical request records must be backfilled to 1.

## Desired Outcome

1. Every operational Request aggregate and record explicitly holds a `Complexity` rating (integer 1 to 5).
2. Requests recorded without an explicit complexity are automatically initialized with `Complexity = 1`.
3. Authorized roles (Programmer, Administrator, Team Lead, Manager) can update the complexity rating at any point during active lifecycle states (`Captured`, `Evaluating`, `Accepted`, `InProgress`, `Escalated`).
4. Requests in closed states (`Completed`, `Rejected`) reject any attempts to modify complexity.
5. Complexity modifications emit `RequestComplexityUpdated` events and support capturing an optional change reason.
6. Query endpoints and DTOs surface `Complexity` for user interface presentation.
7. Existing database records are safely migrated and backfilled with `1`, guarded by a check constraint (1 to 5).

## Current Situation

1. The `Request` aggregate root ([Request.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs)) contains operational properties such as `Title`, `Description`, `RequestType`, `Status`, and `Priority` (LOW, NORMAL, HIGH, URGENT), but has no attribute for sizing or technical complexity.
2. Request creation ([`Request.Record`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs#L70-L135) and [`RecordRequestCommand`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs#L9-L18)) does not accept or initialize a complexity score.
3. Triage evaluation ([`Request.Evaluate`](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs#L224-L249)) only records textual `EvaluationNotes` and cannot assign or modify an operational complexity metric.
4. No domain event, command validator, or authorization check exists for managing complexity transitions or audits.
5. In persistence ([RequestRepository.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Persistence/RequestRepository.cs#L30-L50)), table `[request].[Requests]` does not contain a `Complexity` column.

## Evidence

- Request Aggregate Root: [Request.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Domain/Request.cs)
- Request Commands: [RequestCommands.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestCommands.cs)
- Request Data Transfer Object: [RequestDto.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Models/RequestDto.cs)
- Request Repository & SQL Mapping: [RequestRepository.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Persistence/RequestRepository.cs)
- Request Service Implementation: [RequestService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs)
- Identity & Authorization Contracts: [IAuthorizationService.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Modules.Identity/Services/IAuthorizationService.cs)

## Notes

This artifact formally captures the change request in an intake format adhering to Knowledge-Centric SDLC standards without linking to external draft notes. Feasibility analysis, domain modeling updates, downstream feature modifications, architecture updates, and implementation planning will be executed by downstream roles.
