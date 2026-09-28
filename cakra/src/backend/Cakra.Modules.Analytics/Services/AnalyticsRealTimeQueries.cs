using FluentValidation;
using MediatR;

namespace Cakra.Modules.Analytics.Services;

/// <summary>
/// MediatR query to compute real-time active request workload per programmer/person
/// for <c>SCR-MGT-003</c> (Architecture §7, §8, §9, §13 — UC-MGT-004, FEAT-MGT-004).
/// </summary>
/// <param name="PersonId">Optional PersonId to filter workload for a specific person.</param>
public sealed record GetProgrammerActiveWorkloadQuery(
    Guid? PersonId = null) : IRequest<IReadOnlyList<ProgrammerActiveWorkloadDto>>;

/// <summary>
/// FluentValidation validator for <see cref="GetProgrammerActiveWorkloadQuery"/>.
/// </summary>
public sealed class GetProgrammerActiveWorkloadQueryValidator : AbstractValidator<GetProgrammerActiveWorkloadQuery>
{
    public GetProgrammerActiveWorkloadQueryValidator()
    {
        When(x => x.PersonId.HasValue, () =>
        {
            RuleFor(x => x.PersonId!.Value)
                .NotEmpty()
                .WithMessage("PersonId must not be an empty GUID when specified.");
        });
    }
}

/// <summary>
/// MediatR query to compute the real-time request portfolio (active requests, open blockers,
/// and recent completions joined with maintenance contract status) for a customer on
/// <c>SCR-MGT-001</c> (Architecture §7, §8, §9, §13 — UC-MGT-002, FEAT-MGT-002).
/// </summary>
/// <param name="CustomerId">Unique identifier of the Customer organization.</param>
public sealed record GetCustomerRequestPortfolioQuery(
    Guid CustomerId) : IRequest<CustomerRequestPortfolioDto>;

/// <summary>
/// FluentValidation validator for <see cref="GetCustomerRequestPortfolioQuery"/>.
/// </summary>
public sealed class GetCustomerRequestPortfolioQueryValidator : AbstractValidator<GetCustomerRequestPortfolioQuery>
{
    public GetCustomerRequestPortfolioQueryValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("CustomerId is required.");
    }
}
