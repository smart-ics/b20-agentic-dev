-- =============================================================================
-- CAKRA — 0018_backfill_workpackage_requests.sql
--
-- Purpose:
--   Backfills missing [workpackage].[WorkPackageRequests] records for any
--   requests that were recorded with a [WorkPackageId] (such as via bulk quick
--   capture in SCR-WP-001) but were not linked in [workpackage].[WorkPackageRequests].
--
-- Rules:
--   - Idempotent: Only inserts if an active membership pair does not exist.
--   - Preserves sort order: Uses existing max sort order plus sequential rank.
--   - Enforces referential consistency: Only joins against valid existing WorkPackages.
-- =============================================================================

INSERT INTO [workpackage].[WorkPackageRequests] (
    [Id],
    [WorkPackageId],
    [RequestId],
    [SortOrder],
    [AddedAt],
    [RemovedAt],
    [CreatedAt],
    [UpdatedAt]
)
SELECT
    NEWID() AS [Id],
    r.[WorkPackageId],
    r.[Id] AS [RequestId],
    ISNULL(
        (SELECT MAX(wpr.[SortOrder]) FROM [workpackage].[WorkPackageRequests] wpr WHERE wpr.[WorkPackageId] = r.[WorkPackageId] AND wpr.[RemovedAt] IS NULL),
        -1
    ) + ROW_NUMBER() OVER (PARTITION BY r.[WorkPackageId] ORDER BY r.[CreatedAt] ASC) AS [SortOrder],
    r.[CreatedAt] AS [AddedAt],
    NULL AS [RemovedAt],
    r.[CreatedAt] AS [CreatedAt],
    NULL AS [UpdatedAt]
FROM [request].[Requests] r
INNER JOIN [workpackage].[WorkPackages] wp ON wp.[Id] = r.[WorkPackageId]
WHERE r.[WorkPackageId] IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM [workpackage].[WorkPackageRequests] existing
      WHERE existing.[WorkPackageId] = r.[WorkPackageId]
        AND existing.[RequestId] = r.[Id]
        AND existing.[RemovedAt] IS NULL
  );
