namespace Cakra.Modules.Post.Domain.Exceptions;

/// <summary>
/// Domain exception thrown when invariant validation fails on Post domain operations.
/// Inherits <see cref="ArgumentException"/> for consistent HTTP 400 Bad Request ProblemDetails mapping.
/// </summary>
public class PostDomainValidationException : ArgumentException
{
    public PostDomainValidationException(string message) : base(message)
    {
    }

    public PostDomainValidationException(string message, string paramName) : base(message, paramName)
    {
    }
}
