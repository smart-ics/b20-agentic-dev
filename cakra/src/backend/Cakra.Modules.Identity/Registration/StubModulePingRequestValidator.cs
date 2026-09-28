using FluentValidation;

namespace Cakra.Modules.Identity.Registration;

/// <summary>
/// P1-S04 technical validator proving FluentValidation validators defined in a
/// module assembly are auto-discovered and executed via the MediatR validation
/// pipeline behavior (Architecture §19.2). It is not a business validator.
/// </summary>
public sealed class StubModulePingRequestValidator : AbstractValidator<StubModulePingRequest>
{
    /// <summary>Requires the ping value to be non-negative.</summary>
    public StubModulePingRequestValidator()
    {
        RuleFor(request => request.Value)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Value must be greater than or equal to zero.");
    }
}
