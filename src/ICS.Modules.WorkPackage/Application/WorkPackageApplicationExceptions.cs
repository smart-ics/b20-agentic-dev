namespace ICS.Modules.WorkPackage.Application;

/// <summary>
/// Raised when a Work Package referenced by an identifier does not exist in the workpackage schema.
/// Application-layer concern; distinct from the domain invariant exceptions in the Domain layer.
/// Architecture §11, §19.2.
/// </summary>
public class WorkPackageNotFoundException : InvalidOperationException
{
    public Guid WorkPackageId { get; }

    public WorkPackageNotFoundException(Guid workPackageId, string message) : base(message)
    {
        WorkPackageId = workPackageId;
    }

    public WorkPackageNotFoundException(Guid workPackageId)
        : this(workPackageId, $"Work Package '{workPackageId}' does not exist.")
    {
    }
}

/// <summary>
/// Raised when a cross-module reference validation fails (Organization, Customer, Product, or Request).
/// Architecture §11, §15, §19.2.
/// </summary>
public class WorkPackageReferenceValidationException : InvalidOperationException
{
    public string ReferenceKind { get; }

    public WorkPackageReferenceValidationException(string referenceKind, string message) : base(message)
    {
        ReferenceKind = referenceKind;
    }
}
