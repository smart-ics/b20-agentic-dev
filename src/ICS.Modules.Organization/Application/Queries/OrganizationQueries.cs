namespace ICS.Modules.Organization.Application.Queries;

using ICS.Modules.Organization.Application.DTOs;
using MediatR;

#region GetPersonById

public sealed record GetPersonByIdQuery(Guid PersonId) : IRequest<PersonDto?>;

internal sealed class GetPersonByIdQueryHandler : IRequestHandler<GetPersonByIdQuery, PersonDto?>
{
    private readonly IOrganizationQueryService _queryService;

    public GetPersonByIdQueryHandler(IOrganizationQueryService queryService)
    {
        _queryService = queryService;
    }

    public Task<PersonDto?> Handle(GetPersonByIdQuery request, CancellationToken cancellationToken)
    {
        return _queryService.GetPersonByIdAsync(request.PersonId, cancellationToken);
    }
}

#endregion

#region ListActivePersons

public sealed record ListActivePersonsQuery : IRequest<IReadOnlyList<PersonDto>>;

internal sealed class ListActivePersonsQueryHandler : IRequestHandler<ListActivePersonsQuery, IReadOnlyList<PersonDto>>
{
    private readonly IOrganizationQueryService _queryService;

    public ListActivePersonsQueryHandler(IOrganizationQueryService queryService)
    {
        _queryService = queryService;
    }

    public Task<IReadOnlyList<PersonDto>> Handle(ListActivePersonsQuery request, CancellationToken cancellationToken)
    {
        return _queryService.ListActivePersonsAsync(cancellationToken);
    }
}

#endregion

#region GetTeamRoster

public sealed record GetTeamRosterQuery(Guid TeamId) : IRequest<IReadOnlyList<TeamMemberDto>>;

internal sealed class GetTeamRosterQueryHandler : IRequestHandler<GetTeamRosterQuery, IReadOnlyList<TeamMemberDto>>
{
    private readonly IOrganizationQueryService _queryService;

    public GetTeamRosterQueryHandler(IOrganizationQueryService queryService)
    {
        _queryService = queryService;
    }

    public Task<IReadOnlyList<TeamMemberDto>> Handle(GetTeamRosterQuery request, CancellationToken cancellationToken)
    {
        return _queryService.GetTeamRosterAsync(request.TeamId, cancellationToken);
    }
}

#endregion

#region GetPersonRoles

public sealed record GetPersonRolesQuery(Guid PersonId) : IRequest<IReadOnlyList<string>>;

internal sealed class GetPersonRolesQueryHandler : IRequestHandler<GetPersonRolesQuery, IReadOnlyList<string>>
{
    private readonly IOrganizationQueryService _queryService;

    public GetPersonRolesQueryHandler(IOrganizationQueryService queryService)
    {
        _queryService = queryService;
    }

    public Task<IReadOnlyList<string>> Handle(GetPersonRolesQuery request, CancellationToken cancellationToken)
    {
        return _queryService.GetPersonRolesAsync(request.PersonId, cancellationToken);
    }
}

#endregion

#region GetPersonResponsibilities

public sealed record GetPersonResponsibilitiesQuery(Guid PersonId) : IRequest<IReadOnlyList<string>>;

internal sealed class GetPersonResponsibilitiesQueryHandler : IRequestHandler<GetPersonResponsibilitiesQuery, IReadOnlyList<string>>
{
    private readonly IOrganizationQueryService _queryService;

    public GetPersonResponsibilitiesQueryHandler(IOrganizationQueryService queryService)
    {
        _queryService = queryService;
    }

    public Task<IReadOnlyList<string>> Handle(GetPersonResponsibilitiesQuery request, CancellationToken cancellationToken)
    {
        return _queryService.GetPersonResponsibilitiesAsync(request.PersonId, cancellationToken);
    }
}

#endregion
