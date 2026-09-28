# CAKRA Database Schema Conventions

Source of truth: `docs/architecture/CAKRA-ARCHITECTURE.md` §17 (Database Design —
Schema Segregation) and §19.3 (Persistence & Data Access). This document records
the schema-per-module naming convention established by slice **P1-S03 —
Database Infrastructure & Migration Framework**.

## Schema-per-module segregation

Each module owns exactly one SQL Server schema. All tables owned by a module live
in that module's schema, and **no module may read from or write to another
module's schema directly** (Architecture §20).

| Schema | Owning module | Owned tables (provisioned by later slices) |
|---|---|---|
| `identity` | Identity & Access | `UserAccounts`, `UserSessions` |
| `organization` | Organization | `Persons`, `Teams`, `Roles`, `Responsibilities`, `TeamMemberships`, `RoleAssignments`, `ResponsibilityAssignments` |
| `customer` | Customer | `Customers`, `CustomerContacts` |
| `product` | Product | `Products` |
| `workpackage` | Work Package | `WorkPackages`, `WorkPackageRequests` |
| `request` | Request | `Requests`, `RequestResolutions`, `RequestAssignments` |
| `post` | Post & Feed | `Posts`, `Comments`, `Reactions`, `PostReferences`, `FeedItems` |
| `analytics` | Management Analytics | `DailyWorkloadSnapshots`, `MonthlyCustomerPerformanceSnapshots` |

These eight names are canonical and are mirrored in code by
`Cakra.Core.Infrastructure.Persistence.DatabaseSchemas` (constants
`DatabaseSchemas.Identity` … `DatabaseSchemas.Analytics`). Application code must
reference schema names through that type rather than using string literals.

## Naming rules

- Schema names are lowercase, single-word, with no prefixes or separators:
  `identity`, `organization`, `customer`, `product`, `workpackage`, `request`,
  `post`, `analytics`.
- Table names are PascalCase plural nouns (e.g. `UserSessions`, `RequestAssignments`).
- Cross-module references are stored as raw `UNIQUEIDENTIFIER` identifier values.
  **No physical cross-schema foreign keys are permitted** (Architecture §20).
- All Dapper SQL must be explicit, parameterized, and target the owning schema only.

## Migration conventions

- Migrations are sequential, idempotent raw SQL scripts managed by **DbUp**
  (Architecture §19.3). **EF Core is strictly prohibited.**
- Scripts live in `src/backend/Cakra.Api/Migrations/Scripts/` and are embedded
  into the `Cakra.Api` assembly.
- Script file names are prefixed with a zero-padded, ascending sequence number
  (`0001_`, `0002_`, …) so DbUp applies them in strict dependency order. Ordering
  follows the architecture's migration dependency order: Identity & Foundation →
  Organization → Customer → Product → Work Package → Request → Post → Analytics.
- Applied scripts are journaled in `dbo.SchemaVersions`; reruns skip already
  applied scripts.
- `0001_baseline.sql` creates only the eight module schemas and deliberately
  creates **no business tables**; business tables are added by each module's
  own migration slice.
- Migrations execute at application startup and via the CLI switch
  `dotnet Cakra.Api.dll --migrate` (Architecture §19.10).

## Connectivity configuration

The application connection string is resolved from
`ConnectionStrings:DefaultConnection` in configuration, overridable by the
`ConnectionStrings__DefaultConnection` environment variable. When it is not
configured, startup migration is skipped with a warning.
