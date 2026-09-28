namespace Cakra.Modules.WorkPackage.Domain.Exceptions;

/// <summary>
/// Base exception for domain rule violations and invalid operations in the Work Package module.
/// </summary>
public class WorkPackageDomainException : Exception
{
    public WorkPackageDomainException(string message) : base(message)
    {
    }

    public WorkPackageDomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
