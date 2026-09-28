namespace ICS.Core.Data;

/// <summary>
/// Authoritative database schema names per Architecture §17 and §20.
/// Relational database with schema segregation per module.
/// Tables in one schema must not have direct physical foreign key constraints
/// or direct write operations to tables in another schema.
/// </summary>
public static class DatabaseSchemas
{
    /// <summary>
    /// IAM tables (UserAccounts, UserSessions).
    /// </summary>
    public const string Identity = "identity";

    /// <summary>
    /// Organizational master tables (Persons, Teams, Roles, Responsibilities, TeamMemberships, RoleAssignments, ResponsibilityAssignments).
    /// </summary>
    public const string Organization = "organization";

    /// <summary>
    /// Customer master tables (Customers, CustomerContacts).
    /// </summary>
    public const string Customer = "customer";

    /// <summary>
    /// Product catalog tables (Products).
    /// </summary>
    public const string Product = "product";

    /// <summary>
    /// Work package grouping tables (WorkPackages, WorkPackageRequests).
    /// </summary>
    public const string WorkPackage = "workpackage";

    /// <summary>
    /// Request lifecycle tables (Requests, RequestResolutions).
    /// </summary>
    public const string Request = "request";

    /// <summary>
    /// Communication and feed read model tables (Posts, Comments, Reactions, PostReferences, FeedItems).
    /// </summary>
    public const string Post = "post";

    /// <summary>
    /// Snapshot tables (DailyWorkloadSnapshots, MonthlyCustomerPerformanceSnapshots).
    /// </summary>
    public const string Analytics = "analytics";

    /// <summary>
    /// All authoritative schema names in dependency/logical order per Architecture §17.
    /// </summary>
    public static readonly IReadOnlyList<string> All =
    [
        Identity,
        Organization,
        Customer,
        Product,
        WorkPackage,
        Request,
        Post,
        Analytics
    ];
}
