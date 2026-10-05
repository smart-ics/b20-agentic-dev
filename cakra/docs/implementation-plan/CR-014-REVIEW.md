---
Code: CR-014
Artifact: REVIEW
Slice: P4-S05
ReviewIteration: 1
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

## RV-001 (Slice P4-S05)

Severity: BLOCKER

Description:
When running the automated test suite with `dotnet test`, integration tests in `Cakra.Tests.Integration` fail during database initialization because migration script `0013_feed_search_content_fts.sql` manually executes `COMMIT TRANSACTION;` and `BEGIN TRANSACTION;` inside DbUp's transaction-managed pipeline (`WithTransactionPerScript()`). When SQL Server executes `COMMIT TRANSACTION;`, the ADO.NET `SqlTransaction` managed by DbUp is committed and transitions into a zombie state. Subsequent ADO.NET operations and DbUp script completion call `SqlTransaction.Commit()`, which throws `Microsoft.Data.SqlClient.SqlTransaction.ZombieCheck(): This SqlTransaction has completed; it is no longer usable.`. This halts database schema creation for integration test databases and causes widespread test failures across the integration test suite.

Evidence:
- Executing `dotnet test` results in failing integration tests with:
  `System.InvalidOperationException : Database migration failed during application startup.`
  `---- Microsoft.Data.SqlClient.SqlTransaction.ZombieCheck()`
  `---- at Microsoft.Data.SqlClient.SqlTransaction.Commit()`
  `---- at DbUp.Engine.Transactions.TransactionPerScriptStrategy.Execute(Action`1 action)`
- Migration script `cakra/src/backend/Cakra.Api/Migrations/Scripts/0013_feed_search_content_fts.sql` lines 44-49 and lines 64-67 commit and restart the transaction inside DbUp:
  ```sql
  IF @@TRANCOUNT > 0
  BEGIN
      SET @HadTran = 1;
      COMMIT TRANSACTION;
  END;
  ...
  IF @HadTran = 1
  BEGIN
      BEGIN TRANSACTION;
  END;
  ```
- Completion criteria for P4-S05 requires: "All existing tests in Cakra.Tests.Integration and Cakra.Tests.Unit pass cleanly (`dotnet test`)."

Required Correction:
Remediate the migration execution strategy or script `0013_feed_search_content_fts.sql` / `DatabaseMigrationRunner.cs` so that DbUp database migrations execute successfully without corrupting DbUp transaction lifecycle, and verify that `dotnet test` passes cleanly with 0 failures across all unit and integration tests in `Cakra.Tests.Unit` and `Cakra.Tests.Integration`.

Status:
RESOLVED

# Current Decision

GO

# Re-Review History

## Iteration 0 (Slice P4-S05 — 2026-10-05)

- **Decision**: NO-GO
- **Findings Recorded**:
  - `RV-001` (`OPEN`): Integration test execution failure in `Cakra.Tests.Integration` caused by `SqlTransaction.ZombieCheck` when DbUp applies migration `0013_feed_search_content_fts.sql`.

## Iteration 1 (Slice P4-S05 — 2026-10-05)

- **Decision**: GO
- **Findings Resolved**:
  - `RV-001` (`RESOLVED`): Removed manual transaction commit/restart statements in `0013_feed_search_content_fts.sql` (aligning with Architecture TD-002) and configured `DatabaseMigrationRunner.cs` with `.WithoutTransaction()`. Executed `dotnet test` confirming all 358 unit tests and 171 integration tests pass cleanly with 0 failures. Executed `npm run build` in `cakra/src/frontend/Cakra.Web` confirming frontend builds with 0 errors.
