namespace Cakra.Modules.Request.Domain.Exceptions;

/// <summary>
/// Domain exception thrown when a requested Request aggregate cannot be found.
/// </summary>
public class RequestNotFoundException : KeyNotFoundException
{
    public Guid RequestId { get; }

    public RequestNotFoundException(Guid requestId)
        : base($"Request '{requestId}' was not found.")
    {
        RequestId = requestId;
    }

    public RequestNotFoundException(Guid requestId, string message)
        : base(message)
    {
        RequestId = requestId;
    }
}
