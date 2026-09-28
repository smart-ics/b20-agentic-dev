namespace Cakra.Modules.Post.Domain.Exceptions;

/// <summary>
/// Domain exception thrown when an operation is attempted on a <see cref="Post"/> or <see cref="Comment"/>
/// whose lifecycle state or visibility condition does not permit the action (Post Domain §7, §8).
/// </summary>
public class InvalidPostStateException : InvalidOperationException
{
    public Guid PostId { get; }
    public string CurrentStatus { get; }
    public string CurrentVisibility { get; }

    public InvalidPostStateException(
        Guid postId,
        string currentStatus,
        string currentVisibility,
        string message)
        : base(message)
    {
        PostId = postId;
        CurrentStatus = currentStatus;
        CurrentVisibility = currentVisibility;
    }

    public InvalidPostStateException(string message) : base(message)
    {
        CurrentStatus = string.Empty;
        CurrentVisibility = string.Empty;
    }
}
