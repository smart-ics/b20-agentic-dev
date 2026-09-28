namespace ICS.Modules.Customer.Application.Commands;

using FluentValidation;
using ICS.Core.Domain;
using ICS.Core.Time;
using ICS.Modules.Customer.Application.DTOs;
using ICS.Modules.Customer.Domain;
using ICS.Modules.Customer.Persistence;
using MediatR;

#region CreateCustomer

public sealed record CreateCustomerCommand(
    string CustomerCode,
    string CustomerName,
    bool HasActiveMaintenanceContract = false,
    Guid? CustomerId = null) : IRequest<CustomerDto>;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerCode)
            .NotEmpty().WithMessage("Customer code is required.")
            .MaximumLength(50).WithMessage("Customer code must not exceed 50 characters.");

        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Customer name is required.")
            .MaximumLength(150).WithMessage("Customer name must not exceed 150 characters.");
    }
}

internal sealed class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, CustomerDto>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public CreateCustomerCommandHandler(
        ICustomerRepository customerRepository,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _customerRepository = customerRepository;
        _clock = clock;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<CustomerDto> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var existing = await _customerRepository.GetByCodeAsync(request.CustomerCode, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"A customer with code '{request.CustomerCode}' already exists.");
        }

        var customerId = request.CustomerId ?? Guid.NewGuid();
        var customer = Customer.Create(
            customerId,
            request.CustomerCode,
            request.CustomerName,
            request.HasActiveMaintenanceContract,
            _clock.UtcNow);

        await _customerRepository.AddAsync(customer, cancellationToken);
        await _eventDispatcher.DispatchAndClearEventsAsync(customer, cancellationToken);

        return new CustomerDto(
            customer.Id,
            customer.CustomerCode,
            customer.CustomerName,
            customer.Status,
            customer.HasActiveMaintenanceContract,
            customer.CreatedAt,
            customer.UpdatedAt);
    }
}

#endregion

#region UpdateCustomerMasterData

public sealed record UpdateCustomerMasterDataCommand(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    bool HasActiveMaintenanceContract) : IRequest<CustomerDto>;

public sealed class UpdateCustomerMasterDataCommandValidator : AbstractValidator<UpdateCustomerMasterDataCommand>
{
    public UpdateCustomerMasterDataCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId is required.");

        RuleFor(x => x.CustomerCode)
            .NotEmpty().WithMessage("Customer code is required.")
            .MaximumLength(50).WithMessage("Customer code must not exceed 50 characters.");

        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Customer name is required.")
            .MaximumLength(150).WithMessage("Customer name must not exceed 150 characters.");
    }
}

internal sealed class UpdateCustomerMasterDataCommandHandler : IRequestHandler<UpdateCustomerMasterDataCommand, CustomerDto>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public UpdateCustomerMasterDataCommandHandler(
        ICustomerRepository customerRepository,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _customerRepository = customerRepository;
        _clock = clock;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<CustomerDto> Handle(UpdateCustomerMasterDataCommand request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new InvalidOperationException($"Customer with ID '{request.CustomerId}' was not found.");

        if (!string.Equals(customer.CustomerCode, request.CustomerCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var existingWithCode = await _customerRepository.GetByCodeAsync(request.CustomerCode, cancellationToken);
            if (existingWithCode != null && existingWithCode.Id != customer.Id)
            {
                throw new InvalidOperationException($"A customer with code '{request.CustomerCode}' already exists.");
            }
        }

        customer.Update(
            request.CustomerCode,
            request.CustomerName,
            request.HasActiveMaintenanceContract,
            _clock.UtcNow);

        await _customerRepository.UpdateAsync(customer, cancellationToken);
        await _eventDispatcher.DispatchAndClearEventsAsync(customer, cancellationToken);

        return new CustomerDto(
            customer.Id,
            customer.CustomerCode,
            customer.CustomerName,
            customer.Status,
            customer.HasActiveMaintenanceContract,
            customer.CreatedAt,
            customer.UpdatedAt);
    }
}

#endregion

#region DeactivateCustomer

public sealed record DeactivateCustomerCommand(Guid CustomerId) : IRequest<bool>;

public sealed class DeactivateCustomerCommandValidator : AbstractValidator<DeactivateCustomerCommand>
{
    public DeactivateCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId is required.");
    }
}

internal sealed class DeactivateCustomerCommandHandler : IRequestHandler<DeactivateCustomerCommand, bool>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public DeactivateCustomerCommandHandler(
        ICustomerRepository customerRepository,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _customerRepository = customerRepository;
        _clock = clock;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<bool> Handle(DeactivateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new InvalidOperationException($"Customer with ID '{request.CustomerId}' was not found.");

        customer.Deactivate(_clock.UtcNow);

        await _customerRepository.UpdateAsync(customer, cancellationToken);
        await _eventDispatcher.DispatchAndClearEventsAsync(customer, cancellationToken);

        return true;
    }
}

#endregion

#region ActivateCustomer

public sealed record ActivateCustomerCommand(Guid CustomerId) : IRequest<bool>;

public sealed class ActivateCustomerCommandValidator : AbstractValidator<ActivateCustomerCommand>
{
    public ActivateCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId is required.");
    }
}

internal sealed class ActivateCustomerCommandHandler : IRequestHandler<ActivateCustomerCommand, bool>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public ActivateCustomerCommandHandler(
        ICustomerRepository customerRepository,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _customerRepository = customerRepository;
        _clock = clock;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<bool> Handle(ActivateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new InvalidOperationException($"Customer with ID '{request.CustomerId}' was not found.");

        customer.Activate(_clock.UtcNow);

        await _customerRepository.UpdateAsync(customer, cancellationToken);
        await _eventDispatcher.DispatchAndClearEventsAsync(customer, cancellationToken);

        return true;
    }
}

#endregion

#region CreateCustomerContact

public sealed record CreateCustomerContactCommand(
    Guid CustomerId,
    string Name,
    string? Position = null,
    string? PhoneNumber = null,
    string? Email = null,
    Guid? ContactId = null) : IRequest<CustomerContactDto>;

public sealed class CreateCustomerContactCommandValidator : AbstractValidator<CreateCustomerContactCommand>
{
    public CreateCustomerContactCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Contact name is required.")
            .MaximumLength(100).WithMessage("Contact name must not exceed 100 characters.");

        RuleFor(x => x.Position)
            .MaximumLength(100).WithMessage("Position must not exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.Position));

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(50).WithMessage("Phone number must not exceed 50 characters.")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(150).WithMessage("Email must not exceed 150 characters.")
            .When(x => !string.IsNullOrEmpty(x.Email));
    }
}

internal sealed class CreateCustomerContactCommandHandler : IRequestHandler<CreateCustomerContactCommand, CustomerContactDto>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerContactRepository _contactRepository;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public CreateCustomerContactCommandHandler(
        ICustomerRepository customerRepository,
        ICustomerContactRepository contactRepository,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _customerRepository = customerRepository;
        _contactRepository = contactRepository;
        _clock = clock;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<CustomerContactDto> Handle(CreateCustomerContactCommand request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new InvalidOperationException($"Customer with ID '{request.CustomerId}' does not exist.");

        var contactId = request.ContactId ?? Guid.NewGuid();
        var contact = CustomerContact.Create(
            contactId,
            request.CustomerId,
            request.Name,
            request.Position,
            request.PhoneNumber,
            request.Email,
            _clock.UtcNow);

        await _contactRepository.AddAsync(contact, cancellationToken);
        await _eventDispatcher.DispatchAndClearEventsAsync(contact, cancellationToken);

        return new CustomerContactDto(
            contact.Id,
            contact.CustomerId,
            contact.Name,
            contact.Position,
            contact.PhoneNumber,
            contact.Email,
            contact.Status,
            contact.CreatedAt,
            contact.UpdatedAt);
    }
}

#endregion

#region UpdateCustomerContact

public sealed record UpdateCustomerContactCommand(
    Guid ContactId,
    string Name,
    string? Position = null,
    string? PhoneNumber = null,
    string? Email = null) : IRequest<CustomerContactDto>;

public sealed class UpdateCustomerContactCommandValidator : AbstractValidator<UpdateCustomerContactCommand>
{
    public UpdateCustomerContactCommandValidator()
    {
        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("ContactId is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Contact name is required.")
            .MaximumLength(100).WithMessage("Contact name must not exceed 100 characters.");

        RuleFor(x => x.Position)
            .MaximumLength(100).WithMessage("Position must not exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.Position));

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(50).WithMessage("Phone number must not exceed 50 characters.")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(150).WithMessage("Email must not exceed 150 characters.")
            .When(x => !string.IsNullOrEmpty(x.Email));
    }
}

internal sealed class UpdateCustomerContactCommandHandler : IRequestHandler<UpdateCustomerContactCommand, CustomerContactDto>
{
    private readonly ICustomerContactRepository _contactRepository;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public UpdateCustomerContactCommandHandler(
        ICustomerContactRepository contactRepository,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _contactRepository = contactRepository;
        _clock = clock;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<CustomerContactDto> Handle(UpdateCustomerContactCommand request, CancellationToken cancellationToken)
    {
        var contact = await _contactRepository.GetByIdAsync(request.ContactId, cancellationToken)
            ?? throw new InvalidOperationException($"Customer contact with ID '{request.ContactId}' was not found.");

        contact.Update(
            request.Name,
            request.Position,
            request.PhoneNumber,
            request.Email,
            _clock.UtcNow);

        await _contactRepository.UpdateAsync(contact, cancellationToken);
        await _eventDispatcher.DispatchAndClearEventsAsync(contact, cancellationToken);

        return new CustomerContactDto(
            contact.Id,
            contact.CustomerId,
            contact.Name,
            contact.Position,
            contact.PhoneNumber,
            contact.Email,
            contact.Status,
            contact.CreatedAt,
            contact.UpdatedAt);
    }
}

#endregion

#region DeactivateCustomerContact

public sealed record DeactivateCustomerContactCommand(Guid ContactId) : IRequest<bool>;

public sealed class DeactivateCustomerContactCommandValidator : AbstractValidator<DeactivateCustomerContactCommand>
{
    public DeactivateCustomerContactCommandValidator()
    {
        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("ContactId is required.");
    }
}

internal sealed class DeactivateCustomerContactCommandHandler : IRequestHandler<DeactivateCustomerContactCommand, bool>
{
    private readonly ICustomerContactRepository _contactRepository;
    private readonly ISystemClock _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public DeactivateCustomerContactCommandHandler(
        ICustomerContactRepository contactRepository,
        ISystemClock clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _contactRepository = contactRepository;
        _clock = clock;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<bool> Handle(DeactivateCustomerContactCommand request, CancellationToken cancellationToken)
    {
        var contact = await _contactRepository.GetByIdAsync(request.ContactId, cancellationToken)
            ?? throw new InvalidOperationException($"Customer contact with ID '{request.ContactId}' was not found.");

        contact.Deactivate(_clock.UtcNow);

        await _contactRepository.UpdateAsync(contact, cancellationToken);
        await _eventDispatcher.DispatchAndClearEventsAsync(contact, cancellationToken);

        return true;
    }
}

#endregion
