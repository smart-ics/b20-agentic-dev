namespace ICS.Modules.WorkPackage.Domain;

using ICS.Modules.WorkPackage.Domain.Exceptions;

/// <summary>
/// Domain-level contract to verify Business Rule 9:
/// A Request may belong to at most one active Work Package.
/// Architecture §11; work-package-domain.md §8 (Rule 9).
/// </summary>
public interface IWorkPackageMembershipChecker
{
    /// <summary>
    /// Checks whether the specified Request already belongs to any active Work Package.
    /// Optionally excludes <paramref name="excludeWorkPackageId"/> (the current package).
    /// </summary>
    bool IsRequestInActiveWorkPackage(Guid requestId, Guid? excludeWorkPackageId = null);
}

/// <summary>
/// Domain policy helpers for enforcing Business Rule 9.
/// </summary>
public static class BusinessRule9Policy
{
    /// <summary>
    /// Enforces that the Request does not belong to another active Work Package.
    /// Throws <see cref="BusinessRule9ViolationException"/> if violated.
    /// </summary>
    public static void Enforce(Guid requestId, Guid currentWorkPackageId, bool isRequestInAnotherActivePackage)
    {
        if (isRequestInAnotherActivePackage)
        {
            throw new BusinessRule9ViolationException(
                requestId,
                currentWorkPackageId,
                $"Business Rule 9 violation: Request '{requestId}' already belongs to an active Work Package.");
        }
    }
}
