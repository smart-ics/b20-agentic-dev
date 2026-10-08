---
Code: CR-027
Artifact: REVIEW
Slice: P2-S03
ReviewIteration: 1
Decision: GO
---

# Template Purpose

This document records review findings and verification evidence for Change Request CR-027 (Person Role Assignment in SCR-ORG-002 and Table Roles Display) implementation slices.

# Testing Gate

A GO decision applies only to this slice. It does not authorize testing.
Testing may begin only when the IMPLEMENTATION-PLAN is COMPLETED: every slice has implementation status IMPLEMENTED and review status GO.

# Findings

## Finding F-CR027-P2S03-01

- **ID**: `F-CR027-P2S03-01`
- **Severity**: BLOCKER
- **Status**: RESOLVED
- **Description**: Breaking contract expansion on `IOrganizationService` without default implementations breaks compilation of `Cakra.Tests.Integration` (`CS0535`), resulting in complete solution build failure (`dotnet build cakra/Cakra.sln`).
- **Evidence**:
  Executing `dotnet build cakra/Cakra.sln` fails with exit code 1:
  ```text
  D:\Project.Aktif\b20-agentic-dev\cakra\tests\backend\Cakra.Tests.Integration\Api\OrganizationControllerTests.cs(552,56): error CS0535: 'OrganizationControllerTests.InMemoryOrganizationService' does not implement interface member 'IOrganizationService.CreatePersonAsync(string, string, string, IReadOnlyList<Guid>, CancellationToken)' [D:\Project.Aktif\b20-agentic-dev\cakra\tests\backend\Cakra.Tests.Integration\Cakra.Tests.Integration.csproj]
  D:\Project.Aktif\b20-agentic-dev\cakra\tests\backend\Cakra.Tests.Integration\Api\OrganizationControllerTests.cs(552,56): error CS0535: 'OrganizationControllerTests.InMemoryOrganizationService' does not implement interface member 'IOrganizationService.UpdatePersonAsync(Guid, string, string, string, IReadOnlyList<Guid>, Guid?, CancellationToken)' [D:\Project.Aktif\b20-agentic-dev\cakra\tests\backend\Cakra.Tests.Integration\Cakra.Tests.Integration.csproj]
  Build FAILED.
  ```
  In `cakra/src/backend/Cakra.Modules.Organization/Services/IOrganizationService.cs`, the new methods:
  ```csharp
  Task<Person> CreatePersonAsync(
      string firstName,
      string lastName,
      string email,
      IReadOnlyList<Guid> roleIds,
      CancellationToken cancellationToken = default);

  Task<Person> UpdatePersonAsync(
      Guid personId,
      string firstName,
      string lastName,
      string email,
      IReadOnlyList<Guid> roleIds,
      Guid? actorPersonId = null,
      CancellationToken cancellationToken = default);
  ```
  are declared without default interface method implementations, while the legacy overloads were changed into default interface methods delegating to the new overloads. Consequently, existing test doubles or implementations implementing `IOrganizationService` (specifically `InMemoryOrganizationService` in `OrganizationControllerTests.cs`) fail compilation.
- **Required Correction**:
  Ensure the entire solution builds cleanly (`dotnet build cakra/Cakra.sln`) by either:
  1. Providing default interface method implementations on `IOrganizationService.cs` for the new overloads forwarding to the legacy signatures (preserving non-breaking contract compatibility for implementers of `IOrganizationService`), or
  2. Implementing the new members on `InMemoryOrganizationService` in `cakra/tests/backend/Cakra.Tests.Integration/Api/OrganizationControllerTests.cs` (or both).
- **Remediation Verification**:
  Provided default interface method implementations on `IOrganizationService.cs` delegating to legacy/role overloads and implemented both `CreatePersonAsync` and `UpdatePersonAsync` role overloads in `InMemoryOrganizationService` in `OrganizationControllerTests.cs`. Solution builds cleanly with 0 warnings and 0 errors (`dotnet build cakra/Cakra.sln`).

## Finding F-CR027-P2S03-02

- **ID**: `F-CR027-P2S03-02`
- **Severity**: MINOR
- **Status**: RESOLVED
- **Description**: `UpdatePersonCommandValidator` self-demotion rule unconditionally requires `Administrator` role if `ActorPersonId == PersonId`, without verifying whether the target person currently holds the `Administrator` role.
- **Evidence**:
  In `cakra/src/backend/Cakra.Modules.Organization/Commands/UpdatePersonCommand.cs` lines 69-95:
  ```csharp
  if (cmd.ActorPersonId.HasValue && cmd.ActorPersonId.Value == cmd.PersonId)
  {
      return cmd.RoleIds != null && cmd.RoleIds.Contains(adminRole.Id);
  }
  ```
  In contrast, `OrganizationService.UpdatePersonAsync` (lines 160-165) checks:
  ```csharp
  var isCurrentlyAdmin = activeAssignments.Any(a => a.RoleId == adminRole.Id);
  if (isCurrentlyAdmin && (roleIds is null || !roleIds.Contains(adminRole.Id)))
  {
      throw new InvalidOperationException("Cannot remove the Administrator role from your own person record.");
  }
  ```
  If a non-administrator person ever submits an `UpdatePersonCommand` where `ActorPersonId == PersonId`, the validator fails with "Cannot remove the Administrator role from your own person record" even though they never held the role.
- **Required Correction**:
  Align validator behavior or document that `UpdatePersonCommandValidator` assumes caller is an administrator editing their own record per FEAT-ORG-001 administrative preconditions.
- **Remediation Verification**:
  `UpdatePersonCommandValidator` was enhanced to optionally receive `IRoleAssignmentRepository` and check whether the target person currently has an active Administrator role assignment before enforcing the self-demotion restriction. Fallback documentation was added, and the validator dependency injection was wired in `OrganizationModule.cs`.

# Current Decision

GO

# Review History

## Iteration 0 (Slice P2-S03 — 2026-10-08)

- **Slice**: P2-S03 (Atomic Commands with RoleIds, Service Set Reconciliation & Self-Demotion Invariant)
- **Decision**: NO-GO
- **Findings Recorded**:
  - `F-CR027-P2S03-01` (BLOCKER): Breaking interface change in `IOrganizationService` without default implementations breaks `Cakra.Tests.Integration` compilation (`CS0535`) and fails `dotnet build cakra/Cakra.sln`.
  - `F-CR027-P2S03-02` (MINOR): `UpdatePersonCommandValidator` unconditional Administrator role check on self-update.
- **Verification Evidence**:
  - `CreatePersonCommand` and `UpdatePersonCommand` updated with `IReadOnlyList<Guid> RoleIds` and `Guid? ActorPersonId`.
  - `CreatePersonCommandValidator` enforces `RuleFor(x => x.RoleIds).NotEmpty()`.
  - `OrganizationService.CreatePersonAsync` inserts person and assigns roles atomically, emitting `PersonCreated` and `RoleAssigned`.
  - `OrganizationService.UpdatePersonAsync` implements set reconciliation (additions and revocations) emitting `RoleAssigned` and `RoleRevoked` domain events.
  - `OrganizationService.UpdatePersonAsync` protects against administrator self-demotion.
  - `DeactivatePersonCommand` preserves role assignments untouched.
  - Unit tests in `Cakra.Tests.Unit` (530 tests) pass cleanly.
  - However, `dotnet build cakra/Cakra.sln` fails due to compilation errors in `Cakra.Tests.Integration/Api/OrganizationControllerTests.cs` caused by `IOrganizationService` contract expansion without default implementations.

## Iteration 1 (Slice P2-S03 Re-review — 2026-10-08)

- **Slice**: P2-S03 (Atomic Commands with RoleIds, Service Set Reconciliation & Self-Demotion Invariant)
- **Decision**: GO
- **Findings Resolved**:
  - `F-CR027-P2S03-01` (BLOCKER) -> RESOLVED
  - `F-CR027-P2S03-02` (MINOR) -> RESOLVED
- **Verification Evidence**:
  - `dotnet build cakra/Cakra.sln` completed successfully with 0 warnings and 0 errors.
  - `dotnet test cakra/Cakra.sln --no-build` passed completely:
    - `Cakra.Tests.Unit`: 531 passed, 0 failed.
    - `Cakra.Tests.Integration`: 183 passed, 0 failed.
  - All slice completion criteria, architectural rules, and domain invariants are verified and satisfied.
