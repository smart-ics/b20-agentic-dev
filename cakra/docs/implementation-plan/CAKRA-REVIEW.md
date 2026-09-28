---
Code: CAKRA
Artifact: REVIEW
Slice: P4-S19
ReviewIteration: 3
Decision: GO
---

# Template Purpose

This template is used for NO-GO decisions and remediation reviews when review
findings, required corrections, remediation history, and re-review evidence
must be preserved. A NO-GO decision requires a REVIEW artifact. GO decisions
normally update IMPLEMENTATION-PLAN and do not use this template.

# Testing Gate

A GO decision applies only to this slice. It does not authorize testing.
Testing may begin only when the IMPLEMENTATION-PLAN is COMPLETED: every slice
has implementation status IMPLEMENTED and review status GO.

# Findings

## RV-001 (Slice P3-S14)

Severity: MAJOR

Description:
`CustomerModuleIntegrationTests` fails with `System.InvalidOperationException : The ConnectionString property has not been initialized` across all three integration tests when executed against SQL Server (`CakraTestDb`).

Evidence:
- In `cakra/src/backend/Cakra.Api/Program.cs` (lines 23 and 49), `var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");` is evaluated immediately during `WebApplication.CreateBuilder(args)` and captured by the `AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(connectionString ?? string.Empty))` registration before `WebHostBuilder.ConfigureAppConfiguration` callbacks run.
- In `cakra/tests/backend/Cakra.Tests.Integration/Customer/CustomerModuleIntegrationTests.cs` (lines 60–71), `_factory` is constructed using `new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { ... builder.ConfigureAppConfiguration(...); })` instead of `builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString)` (as used by `CakraWebApplicationFactory` and `OrganizationApplicationServiceIntegrationTests`).
- Because `ConfigureAppConfiguration` runs after `Program.cs` has already read `DefaultConnection`, `SqlConnectionFactory` is initialized with an empty connection string (`""`), causing `Migration_0004_creates_customer_schema_tables`, `CustomerService_commands_and_CustomerQueryService_queries_work_end_to_end`, and `Foreign_key_constraint_prevents_orphaned_customer_contacts` to throw `System.InvalidOperationException: The ConnectionString property has not been initialized` when `_sqlServerAvailable` is `true`.

Required Correction:
In `cakra/tests/backend/Cakra.Tests.Integration/Customer/CustomerModuleIntegrationTests.cs`, configure the `WebApplicationFactory<Program>` (or `CakraWebApplicationFactory`) using `builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString)` (and `builder.UseSetting("ConnectionStrings:TestConnection", _connectionString)`) so `Program.cs` receives the test connection string when registering `IDbConnectionFactory`.

Status:
RESOLVED

---

## RV-002 (Slice P3-S14)

Severity: MAJOR

Description:
`CustomerModuleIntegrationTests.InitializeAsync()` silently skips all integration tests when the `CakraTestDb` database has not yet been created on the target SQL Server / LocalDB instance.

Evidence:
- In `cakra/tests/backend/Cakra.Tests.Integration/Customer/CustomerModuleIntegrationTests.cs` (lines 44–58):
  ```csharp
  try
  {
      await using var conn = new SqlConnection(_connectionString);
      await conn.OpenAsync();
      _sqlServerAvailable = true;
  }
  catch
  {
      _sqlServerAvailable = false;
      return;
  }

  var runner = new DatabaseMigrationRunner(_connectionString, NullLogger<DatabaseMigrationRunner>.Instance);
  var migrationResult = runner.Run();
  ```
- `DatabaseMigrationRunner.Run()` calls `EnsureDatabase.For.SqlDatabase(_connectionString)` (in `DatabaseMigrationRunner.cs` line 33) to create `CakraTestDb` if it does not exist. However, because `InitializeAsync()` attempts `conn.OpenAsync()` against `Database=CakraTestDb` *before* calling `runner.Run()`, connecting to a fresh SQL Server / LocalDB instance where `CakraTestDb` does not yet exist throws `SqlException` (`Cannot open database "CakraTestDb" requested by the login`), setting `_sqlServerAvailable = false` and returning early in all three `[Fact]` tests without executing any migrations, Respawn resets, service commands, or query assertions.

Required Correction:
In `cakra/tests/backend/Cakra.Tests.Integration/Customer/CustomerModuleIntegrationTests.cs`, ensure the test database is created before opening a connection to `CakraTestDb` (for example, by probing server connectivity against `master` or invoking `DatabaseMigrationRunner.Run()` / `EnsureDatabase.For.SqlDatabase(_connectionString)` inside the availability check before opening `SqlConnection(_connectionString)`), and verify that all three integration tests execute and pass against SQL Server.

Status:
RESOLVED

---

## RV-003 (Slice P3-S15)

Severity: MAJOR

Description:
`ProductModuleIntegrationTests` fails with `System.InvalidOperationException : The ConnectionString property has not been initialized` across all three integration tests when executed via `dotnet test cakra\Cakra.sln` against SQL Server (`CakraTestDb`).

Evidence:
- In `cakra/src/backend/Cakra.Api/Program.cs` (lines 23 and 49), `var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");` is evaluated immediately during `WebApplication.CreateBuilder(args)` and captured by the `AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(connectionString ?? string.Empty))` registration before `WebHostBuilder.ConfigureAppConfiguration` callbacks run.
- In `cakra/tests/backend/Cakra.Tests.Integration/Product/ProductModuleIntegrationTests.cs` (lines 62–73), `_factory` is constructed using `new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration(...); })` instead of `builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString)` (as used by `CakraWebApplicationFactory` and `OrganizationApplicationServiceIntegrationTests`).
- Consequently, `SqlConnectionFactory` is initialized with an empty connection string (`""`), causing all three integration tests (`Migration_0005_creates_product_schema_table`, `ProductService_commands_and_ProductQueryService_queries_work_end_to_end`, and `CreateProduct_and_AssignProductOwner_reject_nonexistent_or_inactive_owner_in_Organization`) to fail with `System.InvalidOperationException: The ConnectionString property has not been initialized`.

Required Correction:
In `cakra/tests/backend/Cakra.Tests.Integration/Product/ProductModuleIntegrationTests.cs`, configure the `WebApplicationFactory<Program>` (or `CakraWebApplicationFactory`) using `builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString)` (and `builder.UseSetting("ConnectionStrings:TestConnection", _connectionString)`) so `Program.cs` receives the test connection string when registering `IDbConnectionFactory`.

Status:
RESOLVED

---

## RV-004 (Slice P3-S15)

Severity: MAJOR

Description:
`ProductModuleIntegrationTests.InitializeAsync()` silently skips all integration tests when the `CakraTestDb` database has not yet been created on the target SQL Server / LocalDB instance.

Evidence:
- In `cakra/tests/backend/Cakra.Tests.Integration/Product/ProductModuleIntegrationTests.cs` (lines 46–60):
  ```csharp
  try
  {
      await using var conn = new SqlConnection(_connectionString);
      await conn.OpenAsync();
      _sqlServerAvailable = true;
  }
  catch
  {
      _sqlServerAvailable = false;
      return;
  }

  var runner = new DatabaseMigrationRunner(_connectionString, NullLogger<DatabaseMigrationRunner>.Instance);
  var migrationResult = runner.Run();
  ```
- `DatabaseMigrationRunner.Run()` calls `EnsureDatabase.For.SqlDatabase(_connectionString)` (in `DatabaseMigrationRunner.cs` line 33) to create `CakraTestDb` if it does not exist. Because `InitializeAsync()` attempts `conn.OpenAsync()` against `Database=CakraTestDb` *before* calling `runner.Run()`, connecting to a fresh SQL Server / LocalDB instance where `CakraTestDb` does not yet exist throws `SqlException` (`Cannot open database "CakraTestDb" requested by the login`), setting `_sqlServerAvailable = false` and silently skipping all three `[Fact]` tests.

Required Correction:
In `cakra/tests/backend/Cakra.Tests.Integration/Product/ProductModuleIntegrationTests.cs`, probe server availability against `InitialCatalog = "master"` (or invoke `DatabaseMigrationRunner.Run()` inside the availability check) before opening a connection to `CakraTestDb`, ensuring `CakraTestDb` is created by DbUp and all three integration tests execute and pass against SQL Server.

Status:
RESOLVED

---

## RV-005 (Slice P4-S19)

Severity: MAJOR

Description:
`RequestCompletionAndQueriesIntegrationTests.Full_request_lifecycle_Record_Assign_Evaluate_Accept_Complete_and_queries_succeed_against_SqlServer` fails intermittently against SQL Server (`Expected history[2].PreviousStatus to be "EVALUATING" with a length of 10, but "ACCEPTED" has a length of 8`) because consecutive state-transition audit timestamps (`AssignedAtUtc` and `CreatedAt`) recorded during `AcceptRequestResponsibilityAsync` collide in SQL Server after Dapper `DbType.DateTime` parameter rounding, causing `GetRequestStateHistory` to return state history entries in non-chronological GUID order.

Evidence:
- In `cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs` (lines 286–290), `AcceptRequestResponsibilityAsync` executes two state transitions back-to-back in a single command (`EVALUATING -> ACCEPTED` via `request.Accept` followed immediately by `ACCEPTED -> IN_PROGRESS` via `request.StartProgress`):
  ```csharp
  var acceptTime = GetNextMonotonicTimestamp(request);
  request.Accept(resolvedActorId, notes, acceptTime);
  var startTime = GetNextMonotonicTimestamp(request);
  request.StartProgress(resolvedActorId, notes, startTime);
  ```
- In `RequestService.GetNextMonotonicTimestamp` (lines 427–438):
  ```csharp
  var maxAssignedAt = request.Assignments.Max(a => a.AssignedAtUtc);
  return now > maxAssignedAt ? now : maxAssignedAt.AddMilliseconds(1);
  ```
  `startTime` is only incremented by `1` millisecond (`maxAssignedAt.AddMilliseconds(1)`) when `now <= maxAssignedAt` (or by sub-millisecond ticks when `now > maxAssignedAt`).
- By default, Dapper binds `.NET DateTime` properties in anonymous parameter objects (`RequestRepository.AddAssignmentAsync`, lines 298–311) as `DbType.DateTime` (`SqlDbType.DateTime`), which SQL Server / `Microsoft.Data.SqlClient` rounds to 1/300th of a second (`3.33ms` increments: `.000`, `.003`, `.007` ms) even though `[AssignedAtUtc]` and `[CreatedAt]` in `request.RequestAssignments` are `DATETIME2`. Consequently, two timestamps separated by `1ms` (e.g. `.002` and `.003`, or `.005` and `.006`) round to the exact same value in SQL Server.
- When `RequestQueryService.GetRequestStateHistoryAsync` (lines 115–131) queries `request.RequestAssignments` with `ORDER BY [AssignedAtUtc] ASC, [CreatedAt] ASC, [Id] ASC` (and `RequestRepository.GetByIdAsync` queries with `ORDER BY [AssignedAtUtc] ASC, [CreatedAt] ASC`), identical `[AssignedAtUtc]` and `[CreatedAt]` values tie-break on `[Id] ASC` (`Guid.NewGuid()`), reversing the `EVALUATING -> ACCEPTED` and `ACCEPTED -> IN_PROGRESS` transitions whenever the second random GUID sorts before the first.
- Executing `dotnet test cakra\Cakra.sln --filter FullyQualifiedName~RequestCompletionAndQueries --no-build` reproduced the failure at `RequestCompletionAndQueriesIntegrationTests.cs:line 233`:
  `Expected history[2].PreviousStatus to be "EVALUATING" with a length of 10, but "ACCEPTED" has a length of 8, differs near "ACC" (index 0).`

Required Correction:
1. Ensure Dapper preserves full `DATETIME2` precision when binding `DateTime` and `DateTime?` parameters (e.g., by registering `SqlMapper.AddTypeMap(typeof(DateTime), DbType.DateTime2)` and `SqlMapper.AddTypeMap(typeof(DateTime?), DbType.DateTime2)` in module/persistence initialization or binding `DateTime2` explicitly in `RequestRepository`), and
2. Update `RequestService.GetNextMonotonicTimestamp` in `cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs` so that every subsequent state transition on a request receives a strictly increasing timestamp with sufficient separation (e.g., `var minNext = maxAssignedAt.AddMilliseconds(10); return now > minNext ? now : minNext;`) so rapid back-to-back transitions (`EVALUATING -> ACCEPTED -> IN_PROGRESS` and sequential lifecycle commands) are deterministically ordered in SQL Server.

Status:
RESOLVED

# Current Decision

GO

# Re-Review History

## Iteration 1 (Slice P3-S14 — 2026-09-28)

- **Decision**: GO
- **Findings Verified**:
  - `RV-001` (`RESOLVED`): Verified in `cakra/tests/backend/Cakra.Tests.Integration/Customer/CustomerModuleIntegrationTests.cs` (lines 69–75) that `_factory` is constructed using `CakraWebApplicationFactory().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString); builder.UseSetting("ConnectionStrings:TestConnection", _connectionString); })`, ensuring `Program.cs` receives the test connection string when registering `IDbConnectionFactory`.
  - `RV-002` (`RESOLVED`): Verified in `cakra/tests/backend/Cakra.Tests.Integration/Customer/CustomerModuleIntegrationTests.cs` (lines 43–67) that `InitializeAsync()` probes SQL Server connectivity against `InitialCatalog = "master"` before executing `DatabaseMigrationRunner.Run()` (which ensures `CakraTestDb` exists and runs all DbUp migrations including `0004_customer_tables.sql`) and before opening a connection to `CakraTestDb`.
- **Test Execution Evidence**:
  - Executed `dotnet test cakra\Cakra.sln --filter FullyQualifiedName~Customer` against SQL Server LocalDB (`CakraTestDb`).
  - `Cakra.Tests.Unit`: 6 passed, 0 failed, 0 skipped (`CustomerDomainAndServiceTests`).
  - `Cakra.Tests.Integration`: 3 passed, 0 failed, 0 skipped (`Migration_0004_creates_customer_schema_tables`, `CustomerService_commands_and_CustomerQueryService_queries_work_end_to_end`, `Foreign_key_constraint_prevents_orphaned_customer_contacts`).
  - All P3-S14 completion criteria and architecture constraints are satisfied with 0 BLOCKER and 0 MAJOR findings.

## Iteration 1 (Slice P3-S15 — 2026-09-28)

- **Decision**: GO
- **Findings Verified**:
  - `RV-003` (`RESOLVED`): Verified in `cakra/tests/backend/Cakra.Tests.Integration/Product/ProductModuleIntegrationTests.cs` (lines 71–77) that `_factory` is constructed using `CakraWebApplicationFactory().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString); builder.UseSetting("ConnectionStrings:TestConnection", _connectionString); })`, ensuring `Program.cs` receives the test connection string when registering `IDbConnectionFactory`.
  - `RV-004` (`RESOLVED`): Verified in `cakra/tests/backend/Cakra.Tests.Integration/Product/ProductModuleIntegrationTests.cs` (lines 45–69) that `InitializeAsync()` probes SQL Server connectivity against `InitialCatalog = "master"` before executing `DatabaseMigrationRunner.Run()` (which ensures `CakraTestDb` exists and runs all DbUp migrations including `0005_product_tables.sql`) and before opening a connection to `CakraTestDb`.
- **Test Execution Evidence**:
  - Executed `dotnet test cakra\Cakra.sln --filter FullyQualifiedName~ProductModuleIntegrationTests` and `dotnet test cakra\tests\backend\Cakra.Tests.Unit\Cakra.Tests.Unit.csproj --filter FullyQualifiedName~ProductDomainAndServiceTests` against SQL Server LocalDB (`CakraTestDb`).
  - `Cakra.Tests.Unit`: 6 passed, 0 failed, 0 skipped (`ProductDomainAndServiceTests`).
  - `Cakra.Tests.Integration`: 3 passed, 0 failed, 0 skipped (`Migration_0005_creates_product_schema_table`, `ProductService_commands_and_ProductQueryService_queries_work_end_to_end`, `CreateProduct_and_AssignProductOwner_reject_nonexistent_or_inactive_owner_in_Organization`).
  - All P3-S15 completion criteria and architecture constraints are satisfied with 0 BLOCKER and 0 MAJOR findings.

## Iteration 3 (Slice P4-S19 — 2026-09-28)

- **Decision**: GO
- **Findings Verified**:
  - `RV-005` (`RESOLVED`):
    1. Verified in `cakra/src/backend/Cakra.Modules.Request/Persistence/RequestRepository.cs` (lines 17–21) that the static constructor registers `SqlMapper.AddTypeMap(typeof(DateTime), DbType.DateTime2)` and `SqlMapper.AddTypeMap(typeof(DateTime?), DbType.DateTime2)` so Dapper preserves full `DATETIME2` precision when binding `.NET DateTime` parameters to SQL Server.
    2. Verified in `cakra/src/backend/Cakra.Modules.Request/Services/RequestService.cs` (lines 286–290 and 427–439) that `GetNextMonotonicTimestamp` enforces `>= 10ms` separation (`var minNext = maxAssignedAt.AddMilliseconds(10); return now > minNext ? now : minNext;`) and `AcceptRequestResponsibilityAsync` separates `acceptTime` (`EVALUATING -> ACCEPTED`) and `startTime` (`ACCEPTED -> IN_PROGRESS`) by `acceptTime.AddMilliseconds(10)`, guaranteeing deterministic chronological ordering of state-transition audit records in `request.RequestAssignments`.
    3. Verified in `cakra/tests/backend/Cakra.Tests.Integration/Request/RequestCompletionAndQueriesIntegrationTests.cs` (lines 35, 43–85) that the integration suite runs against isolated SQL Server test database `CakraTestDb_RequestCompletion` with `UseSetting("ConnectionStrings:DefaultConnection", _connectionString)` and Respawn isolation.
- **Test Execution Evidence**:
  - Executed `dotnet test cakra\Cakra.sln --filter FullyQualifiedName~RequestCompletionAndQueries` against SQL Server LocalDB (`CakraTestDb_RequestCompletion`).
  - `Cakra.Tests.Unit`: 5 passed, 0 failed, 0 skipped (`RequestCompletionAndQueriesTests`).
  - `Cakra.Tests.Integration`: 3 passed, 0 failed, 0 skipped (`Full_request_lifecycle_Record_Assign_Evaluate_Accept_Complete_and_queries_succeed_against_SqlServer`, `RejectRequest_flow_persists_rejected_resolution_and_state_history_against_SqlServer`, `ListMyAssignedRequests_and_GetFilteredRequestGrid_return_accurate_filtered_and_paginated_results`).
  - All P4-S19 completion criteria and architecture constraints are satisfied with 0 BLOCKER and 0 MAJOR findings.
