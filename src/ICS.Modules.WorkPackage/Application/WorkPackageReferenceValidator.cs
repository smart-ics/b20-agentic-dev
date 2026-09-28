namespace ICS.Modules.WorkPackage.Application;

using ICS.Modules.Customer;
using ICS.Modules.Organization;
using ICS.Modules.Product;
using ICS.Modules.Request;

/// <summary>
/// Cross-module reference validation for Work Package commands. Each reference is resolved through
/// the owning module's published query service; no other module's schema is read or written.
/// Architecture §11, §15, §20.
/// </summary>
internal sealed class WorkPackageReferenceValidator
{
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly IProductQueryService _productQueryService;
    private readonly IRequestQueryService _requestQueryService;

    public WorkPackageReferenceValidator(
        IOrganizationQueryService organizationQueryService,
        ICustomerQueryService customerQueryService,
        IProductQueryService productQueryService,
        IRequestQueryService requestQueryService)
    {
        _organizationQueryService = organizationQueryService ?? throw new ArgumentNullException(nameof(organizationQueryService));
        _customerQueryService = customerQueryService ?? throw new ArgumentNullException(nameof(customerQueryService));
        _productQueryService = productQueryService ?? throw new ArgumentNullException(nameof(productQueryService));
        _requestQueryService = requestQueryService ?? throw new ArgumentNullException(nameof(requestQueryService));
    }

    /// <summary>
    /// Validates that the owner is an existing, active Person in the Organization module.
    /// </summary>
    public async Task EnsureActivePersonAsync(Guid personId, string role, CancellationToken cancellationToken)
    {
        if (personId == Guid.Empty)
        {
            throw new WorkPackageReferenceValidationException(
                "Person",
                $"The {role} PersonId cannot be empty.");
        }

        var person = await _organizationQueryService.GetPersonByIdAsync(personId, cancellationToken);
        if (person is null)
        {
            throw new WorkPackageReferenceValidationException(
                "Person",
                $"The {role} with PersonId '{personId}' does not exist in Organization.");
        }

        if (!string.Equals(person.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            throw new WorkPackageReferenceValidationException(
                "Person",
                $"The {role} '{person.Name}' is not active in Organization.");
        }
    }

    /// <summary>
    /// Validates an optional Customer reference against the Customer module.
    /// </summary>
    public async Task EnsureCustomerAsync(Guid? customerId, CancellationToken cancellationToken)
    {
        if (!customerId.HasValue || customerId.Value == Guid.Empty)
        {
            return;
        }

        var customer = await _customerQueryService.GetCustomerByIdAsync(customerId.Value, cancellationToken);
        if (customer is null)
        {
            throw new WorkPackageReferenceValidationException(
                "Customer",
                $"Customer with ID '{customerId.Value}' does not exist.");
        }

        if (!string.Equals(customer.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            throw new WorkPackageReferenceValidationException(
                "Customer",
                $"Customer '{customer.CustomerName}' is not active.");
        }
    }

    /// <summary>
    /// Validates an optional Product reference against the Product module.
    /// </summary>
    public async Task EnsureProductAsync(Guid? productId, CancellationToken cancellationToken)
    {
        if (!productId.HasValue || productId.Value == Guid.Empty)
        {
            return;
        }

        var product = await _productQueryService.GetProductByIdAsync(productId.Value, cancellationToken);
        if (product is null)
        {
            throw new WorkPackageReferenceValidationException(
                "Product",
                $"Product with ID '{productId.Value}' does not exist.");
        }

        if (!string.Equals(product.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            throw new WorkPackageReferenceValidationException(
                "Product",
                $"Product '{product.Name}' is not active.");
        }
    }

    /// <summary>
    /// Validates that a Request exists via the Request module's published query service.
    /// </summary>
    public async Task EnsureRequestExistsAsync(Guid requestId, CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty)
        {
            throw new WorkPackageReferenceValidationException(
                "Request",
                "The RequestId cannot be empty.");
        }

        var request = await _requestQueryService.GetRequestByIdAsync(requestId, cancellationToken);
        if (request is null)
        {
            throw new WorkPackageReferenceValidationException(
                "Request",
                $"Request with ID '{requestId}' does not exist.");
        }
    }
}
