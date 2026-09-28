# Review Artifact: P2-S09

- Code: ICS
- Slice: P2-S09
- Title: Identity Module — Domain, Application Services & Persistence
- ReviewIteration: 1
- Decision: GO

## Summary
In Re-Review Iteration 1, all prior findings (FINDING-P2-S09-01 and FINDING-P2-S09-02) have been verified as fully resolved. The integration test infrastructure properly detects the `Testing` environment, directs DbUp migrations and `IDbConnectionFactory` to `ICS_Test`, and executes Respawn database resets cleanly between test executions. The migration script naming was aligned to `Script0002_CreateIdentitySchema.sql`. Non-incremental build `dotnet build ICS.sln` succeeded with 0 warnings/errors, and `dotnet test ICS.sln` executed cleanly with 100% pass rate across all 29 tests (13 unit, 16 integration) with zero errors on repeated runs.

## Findings

### FINDING-P2-S09-01
- **Severity**: BLOCKER
- **Status**: RESOLVED
- **Description**: `dotnet test ICS.sln` fails with 7 failures in `ICS.Tests.Integration` due to test database connection mismatch and failure of database reset isolation.
- **Evidence**:
  1. `dotnet test ICS.sln` exits with code 1:
     ```text
     Failed! - Failed: 7, Passed: 9, Skipped: 0, Total: 16, Duration: 692 ms - ICS.Tests.Integration.dll (net8.0)
     ```
  2. Test failure output shows duplicate key constraint violations when attempting to insert user accounts during integration tests:
     ```text
     Microsoft.Data.SqlClient.SqlException : Cannot insert duplicate key row in object 'identity.UserAccounts' with unique index 'UQ_UserAccounts_Username'. The duplicate key value is (active.user).
     Microsoft.Data.SqlClient.SqlException : Cannot insert duplicate key row in object 'identity.UserAccounts' with unique index 'UQ_UserAccounts_Username'. The duplicate key value is (retry.user).
     Microsoft.Data.SqlClient.SqlException : Cannot insert duplicate key row in object 'identity.UserAccounts' with unique index 'UQ_UserAccounts_Username'. The duplicate key value is (expired.user).
     Microsoft.Data.SqlClient.SqlException : Cannot insert duplicate key row in object 'identity.UserAccounts' with unique index 'UQ_UserAccounts_Username'. The duplicate key value is (logout.user).
     Microsoft.Data.SqlClient.SqlException : Cannot insert duplicate key row in object 'identity.UserAccounts' with unique index 'UQ_UserAccounts_Username'. The duplicate key value is (sync.user).
     ```
  3. Direct SQL inspection reveals:
     - `ICS_Test` contains 0 tables (`identity.UserAccounts` was never provisioned in `ICS_Test`).
     - `ICS` (default/production database) contains `[identity].[UserAccounts]` with all 8 test accounts persisted: `active.user`, `admin`, `expired.user`, `lockout.user`, `logout.user`, `retry.user`, `session.user`, `sync.user`.
  4. Root cause analysis:
     - In `src/ICS.Web/Program.cs`, the database connection string and DbUp migration runner are initialized from `builder.Configuration` prior to host build using the default connection string from `appsettings.json` (`Server=localhost;Database=ICS;...`).
     - In `tests/ICS.Tests.Integration/IcsWebApplicationFactory.cs`, the web host configuration override (`builder.ConfigureAppConfiguration`) does not take effect for the application startup connection string read in `Program.cs`, causing both migrations and `IDbConnectionFactory` to operate against `ICS` instead of `ICS_Test`.
     - `DatabaseResetHelper` is configured with `_testConnectionString` (`ICS_Test`). Because no tables were migrated to `ICS_Test`, Respawn handles the empty database exception and performs no resets. The database actually receiving test writes (`ICS`) is never cleaned, causing state leakage and immediate test failures across test runs.
- **Required Correction**:
  1. Fix `IcsWebApplicationFactory` and/or `Program.cs` configuration resolution so that when running under the `Testing` environment, the application's connection string, `IDbConnectionFactory`, and startup `DbUpMigrationRunner` target the isolated test database (`ICS_Test` or `TEST_CONNECTION_STRING`) rather than the default `ICS` database.
  2. Ensure `DbUpMigrationRunner` runs against the test database so all bounded context tables (including `identity.*`) exist in `ICS_Test`.
  3. Ensure `DatabaseResetHelper` with Respawn successfully clears all bounded context tables in the test database between test runs.
  4. Clean up any test records polluted in the default `ICS` database.
  5. Verify that `dotnet test ICS.sln` executes cleanly and passes 100% of tests repeatedly with zero errors.
- **Resolution Verification (Iteration 1)**:
  - `src/ICS.Web/appsettings.Testing.json` and `src/ICS.Web/Program.cs` updated to route testing environment connections to `ICS_Test`.
  - `DbUpMigrationRunner` applied migrations `Script0001_CreateModuleSchemas.sql` and `Script0002_CreateIdentitySchema.sql` to `ICS_Test`.
  - Integration tests executed against `ICS_Test` and `DatabaseResetHelper` cleaned all business tables between runs.
  - Direct database queries confirmed 0 residual rows in `ICS` and `ICS_Test`.
  - `dotnet test ICS.sln` passed all 29 tests (13 unit, 16 integration) with 0 failures on repeated executions.

### FINDING-P2-S09-02
- **Severity**: MINOR
- **Status**: RESOLVED
- **Description**: Inconsistent SQL migration script naming pattern in `src/ICS.Web/Migrations/`.
- **Evidence**:
  - Baseline migration: `src/ICS.Web/Migrations/Script0001_CreateModuleSchemas.sql`
  - Identity migration: `src/ICS.Web/Migrations/001_CreateIdentitySchema.sql`
  DbUp orders embedded migration scripts alphabetically. `001_CreateIdentitySchema.sql` sorts before `Script0001_CreateModuleSchemas.sql`. While currently idempotent, this alphabetical sorting inconsistency risks script execution out-of-order in future module migrations.
- **Required Correction**:
  Align migration script filenames to a consistent numerical prefix convention (e.g., `0001_...` and `0002_...` or `Script0001_...` and `Script0002_...`) across all module migration scripts.
- **Resolution Verification (Iteration 1)**:
  - File renamed to `src/ICS.Web/Migrations/Script0002_CreateIdentitySchema.sql`, maintaining consistent `Script####_...` convention. Verified in `dbo.SchemaVersions` on both databases.

## Remediation History

| Iteration | Date | Decision | Findings | Summary |
|---|---|---|---|---|
| 0 | 2026-09-28 | NO-GO | FINDING-P2-S09-01 (BLOCKER), FINDING-P2-S09-02 (MINOR) | Initial review. `dotnet test ICS.sln` fails with 7 errors due to test database connection mismatch and Respawn test isolation failure. |
| 1 | 2026-09-28 | GO | None (All Resolved) | Re-review. Test database isolation fixed, migrations applied to `ICS_Test`, script naming aligned, all 29 unit and integration tests passing cleanly. |
