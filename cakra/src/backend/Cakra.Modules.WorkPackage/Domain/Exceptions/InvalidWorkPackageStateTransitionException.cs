namespace Cakra.Modules.WorkPackage.Domain.Exceptions;

/// <summary>
/// Exception thrown when an invalid lifecycle state transition is attempted on a Work Package.
/// </summary>
public sealed class InvalidWorkPackageStateTransitionException : WorkPackageDomainException
{
    public Guid WorkPackageId { get; }
    public WorkPackageStatus FromStatus { get; }
    public WorkPackageStatus ToStatus { get; }

    public InvalidWorkPackageStateTransitionException(
        Guid workPackageId,
        WorkPackageStatus fromStatus,
        WorkPackageStatus toStatus,
        string message)
        : base(message)
    {
        WorkPackageId = workPackageId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
    }
}
