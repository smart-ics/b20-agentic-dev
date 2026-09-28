namespace ICS.Modules.Product.Application.Queries;

using Dapper;
using FluentValidation;
using ICS.Core.Data;
using ICS.Modules.Organization;
using ICS.Modules.Product.Application.DTOs;
using MediatR;

#region GetProductById

public sealed record GetProductByIdQuery(Guid ProductId) : IRequest<ProductDto?>;

public sealed class GetProductByIdQueryValidator : AbstractValidator<GetProductByIdQuery>
{
    public GetProductByIdQueryValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("ProductId is required.");
    }
}

internal sealed class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDto?>
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IOrganizationQueryService _organizationQueryService;

    public GetProductByIdQueryHandler(
        IDbConnectionFactory connectionFactory,
        IOrganizationQueryService organizationQueryService)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
    }

    public async Task<ProductDto?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                ProductId,
                Code,
                Name,
                Description,
                OwnerPersonId,
                Status,
                CreatedAt,
                UpdatedAt
            FROM [product].[Products]
            WHERE ProductId = @ProductId;";

        var product = await connection.QuerySingleOrDefaultAsync<ProductDto>(sql, new { ProductId = request.ProductId });
        if (product == null)
        {
            return null;
        }

        var owner = await _organizationQueryService.GetPersonByIdAsync(product.OwnerPersonId, cancellationToken);
        return product with { OwnerName = owner?.Name };
    }
}

#endregion

#region GetProductByCode

public sealed record GetProductByCodeQuery(string Code) : IRequest<ProductDto?>;

public sealed class GetProductByCodeQueryValidator : AbstractValidator<GetProductByCodeQuery>
{
    public GetProductByCodeQueryValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Product code is required.");
    }
}

internal sealed class GetProductByCodeQueryHandler : IRequestHandler<GetProductByCodeQuery, ProductDto?>
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IOrganizationQueryService _organizationQueryService;

    public GetProductByCodeQueryHandler(
        IDbConnectionFactory connectionFactory,
        IOrganizationQueryService organizationQueryService)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
    }

    public async Task<ProductDto?> Handle(GetProductByCodeQuery request, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                ProductId,
                Code,
                Name,
                Description,
                OwnerPersonId,
                Status,
                CreatedAt,
                UpdatedAt
            FROM [product].[Products]
            WHERE UPPER(Code) = UPPER(@Code);";

        var product = await connection.QuerySingleOrDefaultAsync<ProductDto>(sql, new { Code = request.Code.Trim() });
        if (product == null)
        {
            return null;
        }

        var owner = await _organizationQueryService.GetPersonByIdAsync(product.OwnerPersonId, cancellationToken);
        return product with { OwnerName = owner?.Name };
    }
}

#endregion

#region ListActiveProducts

public sealed record ListActiveProductsQuery() : IRequest<IReadOnlyList<ProductDto>>;

internal sealed class ListActiveProductsQueryHandler : IRequestHandler<ListActiveProductsQuery, IReadOnlyList<ProductDto>>
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IOrganizationQueryService _organizationQueryService;

    public ListActiveProductsQueryHandler(
        IDbConnectionFactory connectionFactory,
        IOrganizationQueryService organizationQueryService)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
    }

    public async Task<IReadOnlyList<ProductDto>> Handle(ListActiveProductsQuery request, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                ProductId,
                Code,
                Name,
                Description,
                OwnerPersonId,
                Status,
                CreatedAt,
                UpdatedAt
            FROM [product].[Products]
            WHERE Status = 'ACTIVE'
            ORDER BY Name ASC;";

        var products = (await connection.QueryAsync<ProductDto>(sql)).ToList();
        if (products.Count == 0)
        {
            return products;
        }

        var activePersons = await _organizationQueryService.ListActivePersonsAsync(cancellationToken);
        var personMap = activePersons.ToDictionary(p => p.PersonId, p => p.Name);

        return products.Select(p => personMap.TryGetValue(p.OwnerPersonId, out var ownerName)
            ? p with { OwnerName = ownerName }
            : p).ToList();
    }
}

#endregion

#region ListAllProducts

public sealed record ListAllProductsQuery() : IRequest<IReadOnlyList<ProductDto>>;

internal sealed class ListAllProductsQueryHandler : IRequestHandler<ListAllProductsQuery, IReadOnlyList<ProductDto>>
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IOrganizationQueryService _organizationQueryService;

    public ListAllProductsQueryHandler(
        IDbConnectionFactory connectionFactory,
        IOrganizationQueryService organizationQueryService)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
    }

    public async Task<IReadOnlyList<ProductDto>> Handle(ListAllProductsQuery request, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 
                ProductId,
                Code,
                Name,
                Description,
                OwnerPersonId,
                Status,
                CreatedAt,
                UpdatedAt
            FROM [product].[Products]
            ORDER BY Name ASC;";

        var products = (await connection.QueryAsync<ProductDto>(sql)).ToList();
        if (products.Count == 0)
        {
            return products;
        }

        var activePersons = await _organizationQueryService.ListActivePersonsAsync(cancellationToken);
        var personMap = activePersons.ToDictionary(p => p.PersonId, p => p.Name);

        return products.Select(p => personMap.TryGetValue(p.OwnerPersonId, out var ownerName)
            ? p with { OwnerName = ownerName }
            : p).ToList();
    }
}

#endregion
