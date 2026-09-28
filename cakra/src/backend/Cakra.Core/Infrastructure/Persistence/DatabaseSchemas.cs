namespace Cakra.Core.Infrastructure.Persistence;

/// <summary>
/// Canonical schema names for the CAKRA database. Each module owns exactly one
/// SQL Server schema, and modules must never read from or write to another
/// module's schema directly — see Architecture §17 (Schema Segregation) and
/// §20 (Zero Cross-Schema Foreign Keys &amp; Cross-Schema Direct Writes).
/// </summary>
public static class DatabaseSchemas
{
    /// <summary>IAM tables (<c>UserAccounts</c>, <c>UserSessions</c>).</summary>
    public const string Identity = "identity";

    /// <summary>Organizational master tables (<c>Persons</c>, <c>Teams</c>, <c>Roles</c>, <c>Responsibilities</c>, assignments).</summary>
    public const string Organization = "organization";

    /// <summary>Customer master tables (<c>Customers</c>, <c>CustomerContacts</c>).</summary>
    public const string Customer = "customer";

    /// <summary>Product catalog tables (<c>Products</c>).</summary>
    public const string Product = "product";

    /// <summary>Work package grouping tables (<c>WorkPackages</c>, <c>WorkPackageRequests</c>).</summary>
    public const string WorkPackage = "workpackage";

    /// <summary>Request lifecycle tables (<c>Requests</c>, <c>RequestResolutions</c>, <c>RequestAssignments</c>).</summary>
    public const string Request = "request";

    /// <summary>Communication and feed read model tables (<c>Posts</c>, <c>Comments</c>, <c>Reactions</c>, <c>PostReferences</c>, <c>FeedItems</c>).</summary>
    public const string Post = "post";

    /// <summary>Analytics snapshot tables (<c>DailyWorkloadSnapshots</c>, <c>MonthlyCustomerPerformanceSnapshots</c>).</summary>
    public const string Analytics = "analytics";

    /// <summary>
    /// All module schemas in migration dependency order
    /// (Identity &amp; Foundation → Organization → Customer → Product →
    /// Work Package → Request → Post → Analytics), per Architecture §17.
    /// </summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Identity,
        Organization,
        Customer,
        Product,
        WorkPackage,
        Request,
        Post,
        Analytics,
    };
}
