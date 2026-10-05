using Cakra.Core;
using Cakra.Modules.Identity.Domain;
using Cakra.Modules.Identity.Services;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Cakra.Modules.Identity.Commands;

/// <summary>
/// MediatR command handler for <see cref="RegisterUserAccountCommand"/> (CR-009; FEAT-USR-002; Architecture §4 TD-001).
/// Verifies username and email uniqueness, hashes password, and persists account with Pending status.
/// </summary>
public sealed class RegisterUserAccountCommandHandler : IRequestHandler<RegisterUserAccountCommand, UserAccountDto>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IPasswordHasher<UserAccount> _passwordHasher;
    private readonly ISystemClock? _clock;
    private readonly ILogger<RegisterUserAccountCommandHandler>? _logger;

    public RegisterUserAccountCommandHandler(
        IUserAccountRepository userAccountRepository,
        IPasswordHasher<UserAccount> passwordHasher,
        ISystemClock? clock = null,
        ILogger<RegisterUserAccountCommandHandler>? logger = null)
    {
        _userAccountRepository = userAccountRepository ?? throw new ArgumentNullException(nameof(userAccountRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _clock = clock;
        _logger = logger;
    }

    private DateTime UtcNow => _clock?.UtcNow ?? DateTime.UtcNow;

    public async Task<UserAccountDto> Handle(RegisterUserAccountCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedUsername = request.Username?.Trim() ?? string.Empty;
        var normalizedEmail = request.Email?.Trim() ?? string.Empty;

        // Check username uniqueness
        var existingByUsername = await _userAccountRepository.GetByUsernameOrEmailAsync(normalizedUsername, cancellationToken);
        if (existingByUsername != null &&
            string.Equals(existingByUsername.Username, normalizedUsername, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Username is already taken.");
        }

        // Check email uniqueness
        var existingByEmail = await _userAccountRepository.GetByUsernameOrEmailAsync(normalizedEmail, cancellationToken);
        if (existingByEmail != null &&
            string.Equals(existingByEmail.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Email is already registered.");
        }

        var now = UtcNow;
        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            PersonId = Guid.Empty,
            Username = normalizedUsername,
            Email = normalizedEmail,
            Status = UserAccountStatus.Pending,
            FailedLoginAttempts = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        await _userAccountRepository.AddAsync(user, cancellationToken);
        _logger?.LogInformation("User account {UserId} ({Username}) self-registered with Status {Status}.", user.UserId, user.Username, user.Status);

        return new UserAccountDto
        {
            UserId = user.UserId,
            PersonId = user.PersonId,
            PersonName = string.Empty,
            Username = user.Username,
            Email = user.Email,
            Status = user.Status,
            FailedLoginAttempts = user.FailedLoginAttempts,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
