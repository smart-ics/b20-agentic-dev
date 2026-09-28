namespace Cakra.Modules.Post.Domain.Exceptions;

/// <summary>
/// Base exception for domain rule violations in the Post module.
/// </summary>
public class PostDomainException : Exception
{
    public PostDomainException(string message) : base(message)
    {
    }

    public PostDomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
