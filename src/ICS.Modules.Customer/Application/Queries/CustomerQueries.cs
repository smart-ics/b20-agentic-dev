namespace ICS.Modules.Customer.Application.Queries;

using Dapper;
using FluentValidation;
using ICS.Core.Data;
using ICS.Modules.Customer.Application.DTOs;
using MediatR;

#region GetCustomerById

public sealed record GetCustomerByIdQuery(Guid CustomerId) : IRequest<CustomerDto?>;

public sealed class GetCustomerByIdQueryValidator : AbstractValidator<GetCustomerByIdQuery>
{
    public GetCustomerByIdQueryValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId is required.");
    }
}

internal sealed class GetCustomerByIdQueryHandler : IRequestHandler<GetCustomerByIdQuery, CustomerDto?>
{
    private readonly IDbConnectionFactory _connectionFactory;

    public GetCustomerByIdQueryHandler(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<CustomerDto?> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                CustomerId,
                CustomerCode,
                CustomerName,
                Status,
                HasActiveMaintenanceContract,
                CreatedAt,
                UpdatedAt
            FROM [customer].[Customers]
            WHERE CustomerId = @CustomerId;";

        return await connection.QuerySingleOrDefaultAsync<CustomerDto>(sql, new { CustomerId = request.CustomerId });
    }
}

#endregion

#region ListActiveCustomers

public sealed record ListActiveCustomersQuery() : IRequest<IReadOnlyList<CustomerDto>>;

internal sealed class ListActiveCustomersQueryHandler : IRequestHandler<ListActiveCustomersQuery, IReadOnlyList<CustomerDto>>
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ListActiveCustomersQueryHandler(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IReadOnlyList<CustomerDto>> Handle(ListActiveCustomersQuery request, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                CustomerId,
                CustomerCode,
                CustomerName,
                Status,
                HasActiveMaintenanceContract,
                CreatedAt,
                UpdatedAt
            FROM [customer].[Customers]
            WHERE Status = 'ACTIVE'
            ORDER BY CustomerName ASC;";

        var results = await connection.QueryAsync<CustomerDto>(sql);
        return results.ToList();
    }
}

#endregion

#region GetCustomerContacts

public sealed record GetCustomerContactsQuery(Guid CustomerId) : IRequest<IReadOnlyList<CustomerContactDto>>;

public sealed class GetCustomerContactsQueryValidator : AbstractValidator<GetCustomerContactsQuery>
{
    public GetCustomerContactsQueryValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId is required.");
    }
}

internal sealed class GetCustomerContactsQueryHandler : IRequestHandler<GetCustomerContactsQuery, IReadOnlyList<CustomerContactDto>>
{
    private readonly IDbConnectionFactory _connectionFactory;

    public GetCustomerContactsQueryHandler(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IReadOnlyList<CustomerContactDto>> Handle(GetCustomerContactsQuery request, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                ContactId,
                CustomerId,
                Name,
                Position,
                PhoneNumber,
                Email,
                Status,
                CreatedAt,
                UpdatedAt
            FROM [customer].[CustomerContacts]
            WHERE CustomerId = @CustomerId
            ORDER BY Name ASC;";

        var results = await connection.QueryAsync<CustomerContactDto>(sql, new { CustomerId = request.CustomerId });
        return results.ToList();
    }
}

#endregion

#region GetCustomerWithContractStatus

public sealed record GetCustomerWithContractStatusQuery(Guid CustomerId) : IRequest<CustomerWithContractStatusDto?>;

public sealed class GetCustomerWithContractStatusQueryValidator : AbstractValidator<GetCustomerWithContractStatusQuery>
{
    public GetCustomerWithContractStatusQueryValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId is required.");
    }
}

internal sealed class GetCustomerWithContractStatusQueryHandler : IRequestHandler<GetCustomerWithContractStatusQuery, CustomerWithContractStatusDto?>
{
    private readonly IDbConnectionFactory _connectionFactory;

    public GetCustomerWithContractStatusQueryHandler(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<CustomerWithContractStatusDto?> Handle(GetCustomerWithContractStatusQuery request, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                CustomerId,
                CustomerCode,
                CustomerName,
                Status,
                HasActiveMaintenanceContract
            FROM [customer].[Customers]
            WHERE CustomerId = @CustomerId;";

        return await connection.QuerySingleOrDefaultAsync<CustomerWithContractStatusDto>(sql, new { CustomerId = request.CustomerId });
    }
}

#endregion
