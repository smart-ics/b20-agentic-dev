using Cakra.Core;
using Cakra.Modules.Identity.Domain;
using Cakra.Modules.Organization;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Cakra.Modules.Identity.Services;

/// <summary>
/// Core implementation of <see cref="IUserAccountService"/> (Architecture §14; CR-007).
/// Coordinates user account operations, password hashing via <see cref="IPasswordHasher{TUser}"/>,
/// in-memory Person enrichment via <see cref="IOrganizationQueryService"/>, and persistence via <see cref="IUserAccountRepository"/>.
/// </summary>
public sealed class UserAccountService : IUserAccountService
{
    private const string UnknownPersonName = "Unknown / Archived";

    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IPasswordHasher<UserAccount> _passwordHasher;
    private readonly IOrganizationQueryService? _organizationQueryService;
    private readonly ISystemClock? _clock;
    private readonly ILogger<UserAccountService>? _logger;
    private readonly IValidator<CreateUserAccountCommand> _createValidator;
    private readonly IValidator<UpdateUserAccountCommand> _updateValidator;

    public UserAccountService(
        IUserAccountRepository userAccountRepository,
        IPasswordHasher<UserAccount> passwordHasher,
        IOrganizationQueryService? organizationQueryService = null,
        ISystemClock? clock = null,
        ILogger<UserAccountService>? logger = null,
        IValidator<CreateUserAccountCommand>? createValidator = null,
        IValidator<UpdateUserAccountCommand>? updateValidator = null)
    {
        _userAccountRepository = userAccountRepository ?? throw new ArgumentNullException(nameof(userAccountRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _organizationQueryService = organizationQueryService;
        _clock = clock;
        _logger = logger;
        _createValidator = createValidator ?? new CreateUserAccountCommandValidator();
        _updateValidator = updateValidator ?? new UpdateUserAccountCommandValidator();
    }

    private DateTime UtcNow => _clock?.UtcNow ?? DateTime.UtcNow;

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserAccountSummaryDto>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userAccountRepository.GetAllAsync(cancellationToken);
        if (users.Count == 0)
        {
            return Array.Empty<UserAccountSummaryDto>();
        }

        var personNames = new Dictionary<Guid, string>();
        if (_organizationQueryService != null)
        {
            try
            {
                var activePersons = await _organizationQueryService.ListActivePersonsAsync(cancellationToken);
                foreach (var person in activePersons)
                {
                    personNames[person.PersonId] = person.FullName;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to retrieve active persons for in-memory user enrichment.");
            }
        }

        var summaries = new List<UserAccountSummaryDto>(users.Count);
        foreach (var user in users)
        {
            if (!personNames.TryGetValue(user.PersonId, out var personName) || string.IsNullOrWhiteSpace(personName))
            {
                personName = UnknownPersonName;
            }

            summaries.Add(new UserAccountSummaryDto
            {
                UserId = user.UserId,
                PersonId = user.PersonId,
                PersonName = personName,
                Username = user.Username,
                Email = user.Email,
                Status = user.Status,
                FailedLoginAttempts = user.FailedLoginAttempts,
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            });
        }

        return summaries;
    }

    /// <inheritdoc />
    public async Task<UserAccountDto?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userAccountRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            return null;
        }

        var personName = await ResolvePersonNameAsync(user.PersonId, cancellationToken);

        return new UserAccountDto
        {
            UserId = user.UserId,
            PersonId = user.PersonId,
            PersonName = personName,
            Username = user.Username,
            Email = user.Email,
            Status = user.Status,
            FailedLoginAttempts = user.FailedLoginAttempts,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    /// <inheritdoc />
    public async Task<UserAccountDto> CreateUserAsync(CreateUserAccountCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validationResult = await _createValidator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var normalizedUsername = command.Username.Trim();
        var normalizedEmail = command.Email.Trim();

        // Check 1-to-1 association with Person
        var existingByPerson = await _userAccountRepository.GetByPersonIdAsync(command.PersonId, cancellationToken);
        if (existingByPerson != null)
        {
            throw new InvalidOperationException($"Person '{command.PersonId}' is already associated with another user account.");
        }

        // Check username uniqueness (case-insensitive)
        var existingByUsername = await _userAccountRepository.GetByUsernameOrEmailAsync(normalizedUsername, cancellationToken);
        if (existingByUsername != null &&
            string.Equals(existingByUsername.Username, normalizedUsername, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Username '{normalizedUsername}' is already in use.");
        }

        // Check email uniqueness (case-insensitive)
        var existingByEmail = await _userAccountRepository.GetByUsernameOrEmailAsync(normalizedEmail, cancellationToken);
        if (existingByEmail != null &&
            string.Equals(existingByEmail.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Email '{normalizedEmail}' is already in use.");
        }

        var status = string.IsNullOrWhiteSpace(command.Status)
            ? UserAccountStatus.Active
            : command.Status.Trim().ToUpperInvariant();

        var now = UtcNow;
        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = command.PersonId,
            Username = normalizedUsername,
            Email = normalizedEmail,
            Status = status,
            FailedLoginAttempts = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, command.Password);

        await _userAccountRepository.AddAsync(user, cancellationToken);
        _logger?.LogInformation("Created user account {UserId} ({Username}) for Person {PersonId}.", user.UserId, user.Username, user.PersonId);

        var personName = await ResolvePersonNameAsync(user.PersonId, cancellationToken);

        return new UserAccountDto
        {
            UserId = user.UserId,
            PersonId = user.PersonId,
            PersonName = personName,
            Username = user.Username,
            Email = user.Email,
            Status = user.Status,
            FailedLoginAttempts = user.FailedLoginAttempts,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    /// <inheritdoc />
    public async Task<UserAccountDto> UpdateUserAsync(Guid id, UpdateUserAccountCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validationResult = await _updateValidator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var user = await _userAccountRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            throw new KeyNotFoundException($"User account '{id}' was not found.");
        }

        var now = UtcNow;

        // Verify email uniqueness excluding current user
        if (!string.IsNullOrWhiteSpace(command.Email))
        {
            var normalizedEmail = command.Email.Trim();
            if (!string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            {
                var existing = await _userAccountRepository.GetByUsernameOrEmailAsync(normalizedEmail, cancellationToken);
                if (existing != null && existing.UserId != user.UserId &&
                    (string.Equals(existing.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(existing.Username, normalizedEmail, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException($"Email '{normalizedEmail}' is already in use by another user account.");
                }
                user.Email = normalizedEmail;
            }
        }

        // Validate status transition and reset failed attempts if unlocking
        if (!string.IsNullOrWhiteSpace(command.Status))
        {
            var targetStatus = command.Status.Trim().ToUpperInvariant();
            if (targetStatus != UserAccountStatus.Active &&
                targetStatus != UserAccountStatus.Locked &&
                targetStatus != UserAccountStatus.Suspended)
            {
                throw new ArgumentException($"Invalid status '{command.Status}'. Allowed statuses are ACTIVE, LOCKED, and SUSPENDED.", nameof(command));
            }

            if (string.Equals(user.Status, UserAccountStatus.Locked, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(targetStatus, UserAccountStatus.Active, StringComparison.OrdinalIgnoreCase))
            {
                user.Unlock(now);
            }
            else
            {
                user.Status = targetStatus;
            }
        }

        // Update password if provided
        if (!string.IsNullOrWhiteSpace(command.NewPassword))
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, command.NewPassword);
        }

        user.UpdatedAt = now;

        await _userAccountRepository.UpdateAsync(user, cancellationToken);
        _logger?.LogInformation("Updated user account {UserId} ({Username}).", user.UserId, user.Username);

        var personName = await ResolvePersonNameAsync(user.PersonId, cancellationToken);

        return new UserAccountDto
        {
            UserId = user.UserId,
            PersonId = user.PersonId,
            PersonName = personName,
            Username = user.Username,
            Email = user.Email,
            Status = user.Status,
            FailedLoginAttempts = user.FailedLoginAttempts,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    private async Task<string> ResolvePersonNameAsync(Guid personId, CancellationToken cancellationToken)
    {
        if (_organizationQueryService == null)
        {
            return UnknownPersonName;
        }

        try
        {
            var person = await _organizationQueryService.GetPersonByIdAsync(personId, cancellationToken);
            if (person != null && !string.IsNullOrWhiteSpace(person.FullName))
            {
                return person.FullName;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to resolve person name for PersonId {PersonId}.", personId);
        }

        return UnknownPersonName;
    }
}
