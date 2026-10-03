namespace Cakra.Modules.Request.Domain.Exceptions;

/// <summary>
/// Base exception for domain rule violations and invalid operations in the Request module.
/// </summary>
public class RequestDomainException : InvalidOperationException
{
    public RequestDomainException(string message) : base(message)
    {
    }

    public RequestDomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
