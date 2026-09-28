using MediatR;

namespace Cakra.Modules.Customer.Services;

/// <summary>
/// MediatR query to retrieve a customer by its unique identifier (Architecture §7).
/// </summary>
public sealed record GetCustomerByIdQuery(Guid CustomerId) : IRequest<CustomerDto?>;

/// <summary>
/// MediatR query to retrieve all active customers (Architecture §7).
/// </summary>
public sealed record ListActiveCustomersQuery : IRequest<IReadOnlyList<CustomerDto>>;

/// <summary>
/// MediatR query to retrieve all contacts belonging to a specific customer (Architecture §7).
/// </summary>
public sealed record GetCustomerContactsQuery(Guid CustomerId) : IRequest<IReadOnlyList<CustomerContactDto>>;

/// <summary>
/// MediatR query to retrieve a customer along with its maintenance contract status (Architecture §7).
/// </summary>
public sealed record GetCustomerWithContractStatusQuery(Guid CustomerId) : IRequest<CustomerWithContractStatusDto?>;
