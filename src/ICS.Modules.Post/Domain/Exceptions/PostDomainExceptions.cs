namespace ICS.Modules.Post.Domain.Exceptions;

using ICS.Modules.Post.Domain;

/// <summary>
/// Exception raised when a domain operation on a Post violates a business rule or invariant.
/// Architecture §8; post-domain.md §7.
/// </summary>
public class PostDomainException : Exception
{
    public PostDomainException(string message) : base(message) { }
    public PostDomainException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception raised when a requested Post does not exist.
/// </summary>
public sealed class PostNotFoundException : PostDomainException
{
    public PostNotFoundException(Guid postId)
        : base($"Post with ID '{postId}' does not exist.")
    {
        PostId = postId;
    }

    public Guid PostId { get; }
}

/// <summary>
/// Exception raised when a requested Comment does not exist.
/// </summary>
public sealed class CommentNotFoundException : PostDomainException
{
    public CommentNotFoundException(Guid commentId)
        : base($"Comment with ID '{commentId}' does not exist.")
    {
        CommentId = commentId;
    }

    public Guid CommentId { get; }
}

/// <summary>
/// Exception raised when an invalid Post lifecycle transition is attempted.
/// Architecture §8 (Post Lifecycle); post-domain.md §8.
/// </summary>
public sealed class InvalidPostStateTransitionException : PostDomainException
{
    public InvalidPostStateTransitionException(
        Guid postId,
        string currentStatus,
        string targetStatus)
        : base($"Invalid Post state transition from '{currentStatus}' to '{targetStatus}' for Post '{postId}'.")
    {
        PostId = postId;
        CurrentStatus = currentStatus;
        TargetStatus = targetStatus;
    }

    public Guid PostId { get; }
    public string CurrentStatus { get; }
    public string TargetStatus { get; }
}

/// <summary>
/// Exception raised when a Post lifecycle operation is attempted on an archived Post.
/// </summary>
public sealed class PostArchivedException : PostDomainException
{
    public PostArchivedException(string operation)
        : base($"Cannot perform operation '{operation}' on an archived Post.")
    {
        Operation = operation;
    }

    public string Operation { get; }
}

/// <summary>
/// Exception raised when a Reaction is invalid (duplicate or unsupported type).
/// post-domain.md §7 (Business Rules 22, 23).
/// </summary>
public sealed class InvalidReactionException : PostDomainException
{
    public InvalidReactionException(string message) : base(message) { }
}

/// <summary>
/// Exception raised when a Post Reference type or target is invalid.
/// post-domain.md §7 (Business Rules 9, 10).
/// </summary>
public sealed class InvalidPostReferenceException : PostDomainException
{
    public InvalidPostReferenceException(string message) : base(message) { }
}

/// <summary>
/// Exception raised when a cross-domain reference validation fails.
/// Architecture §15.
/// </summary>
public sealed class PostReferenceValidationException : PostDomainException
{
    public PostReferenceValidationException(string referenceType, string message)
        : base($"Reference validation failed for {referenceType}: {message}")
    {
        ReferenceType = referenceType;
    }

    public string ReferenceType { get; }
}