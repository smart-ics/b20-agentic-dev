using Cakra.Modules.Identity.Services;
using MediatR;

namespace Cakra.Modules.Identity.Commands;

/// <summary>
/// MediatR command for self-service user account registration (CR-009; FEAT-USR-002).
/// Creates a new user account with Pending approval status and unassociated PersonId.
/// </summary>
public sealed record RegisterUserAccountCommand(
    string Username,
    string Email,
    string Password
) : IRequest<UserAccountDto>;
