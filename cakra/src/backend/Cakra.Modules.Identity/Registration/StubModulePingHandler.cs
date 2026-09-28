using MediatR;

namespace Cakra.Modules.Identity.Registration;

/// <summary>
/// P1-S04 technical handler proving MediatR handler auto-discovery from a module
/// assembly (Architecture §19.2). It is not a business handler.
/// </summary>
public sealed class StubModulePingHandler : IRequestHandler<StubModulePingRequest, string>
{
    /// <inheritdoc />
    public Task<string> Handle(StubModulePingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult($"pong:{request.Value}");
    }
}
