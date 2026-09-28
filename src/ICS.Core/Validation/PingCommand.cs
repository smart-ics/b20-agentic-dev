using FluentValidation;
using ICS.Core.Time;
using MediatR;

namespace ICS.Core.Validation;

/// <summary>
/// Sample command demonstrating MediatR request dispatch, validation pipeline behavior, and handler execution.
/// </summary>
/// <param name="Message">Echo message payload; must not be empty.</param>
public sealed record PingCommand(string Message) : IRequest<PingResult>;

/// <summary>
/// Result returned by <see cref="PingCommandHandler"/>.
/// </summary>
/// <param name="Echo">The echoed message.</param>
/// <param name="Timestamp">The UTC timestamp when processed.</param>
public sealed record PingResult(string Echo, DateTime Timestamp);

/// <summary>
/// FluentValidation validator for <see cref="PingCommand"/> verifying validation pipeline interception.
/// </summary>
public sealed class PingCommandValidator : AbstractValidator<PingCommand>
{
    public PingCommandValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("Message cannot be empty.");
    }
}

/// <summary>
/// Handler for <see cref="PingCommand"/> demonstrating DI resolution and pipeline flow.
/// </summary>
public sealed class PingCommandHandler : IRequestHandler<PingCommand, PingResult>
{
    private readonly ISystemClock _clock;

    public PingCommandHandler(ISystemClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<PingResult> Handle(PingCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new PingResult(request.Message, _clock.UtcNow));
    }
}
