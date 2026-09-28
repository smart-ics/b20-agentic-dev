namespace ICS.Modules.WorkPackage.Domain.Exceptions;

/// <summary>
/// Base exception for domain invariant and rule violations within the Work Package module.
/// </summary>
public class WorkPackageDomainException : InvalidOperationException
{
    public WorkPackageDomainException(string message) : base(message) { }
    public WorkPackageDomainException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Domain exception thrown when an invalid Work Package lifecycle state transition is attempted.
/// Architecture §11; work-package-domain.md §9.
/// </summary>
public class InvalidWorkPackageStateTransitionException : WorkPackageDomainException
{
    public Guid WorkPackageId { get; }
    public string CurrentStatus { get; }
    public string TargetStatus { get; }

    public InvalidWorkPackageStateTransitionException(Guid workPackageId, string currentStatus, string targetStatus, string message)
        : base(message)
    {
        WorkPackageId = workPackageId;
        CurrentStatus = currentStatus;
        TargetStatus = targetStatus;
    }

    public InvalidWorkPackageStateTransitionException(Guid workPackageId, string currentStatus, string targetStatus)
        : this(workPackageId, currentStatus, targetStatus, $"Cannot transition Work Package '{workPackageId}' from status '{currentStatus}' to '{targetStatus}'.")
    {
    }
}

/// <summary>
/// Domain exception thrown when Business Rule 9 is violated:
/// A Request may belong to at most one active Work Package.
/// Architecture §11; work-package-domain.md §8 (Rule 9).
/// </summary>
public class BusinessRule9ViolationException : WorkPackageDomainException
{
    public Guid RequestId { get; }
    public Guid WorkPackageId { get; }

    public BusinessRule9ViolationException(Guid requestId, Guid workPackageId, string message)
        : base(message)
    {
        RequestId = requestId;
        WorkPackageId = workPackageId;
    }

    public BusinessRule9ViolationException(Guid requestId, Guid workPackageId)
        : this(requestId, workPackageId, $"Business Rule 9 violation: Request '{requestId}' already belongs to an active Work Package.")
    {
    }
}
