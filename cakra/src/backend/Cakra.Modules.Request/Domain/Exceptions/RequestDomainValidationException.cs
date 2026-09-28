namespace Cakra.Modules.Request.Domain.Exceptions;

/// <summary>
/// Domain exception thrown when invariant validation fails on Request domain operations.
/// </summary>
public class RequestDomainValidationException : ArgumentException
{
    public RequestDomainValidationException(string message) : base(message) { }
    public RequestDomainValidationException(string message, string paramName) : base(message, paramName) { }
}
