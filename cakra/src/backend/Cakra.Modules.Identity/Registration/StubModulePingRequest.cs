using MediatR;

namespace Cakra.Modules.Identity.Registration;

/// <summary>
/// P1-S04 technical request used to verify that MediatR handlers defined in a
/// module assembly are discovered by the host. It is not a business message.
/// </summary>
/// <param name="Value">Arbitrary non-negative value echoed by the handler.</param>
public sealed record StubModulePingRequest(int Value) : IRequest<string>;
