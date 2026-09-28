namespace ICS.Core.Auth;

/// <summary>
/// Mutator interface used by authentication middleware and test harnesses to populate ambient request context.
/// Paired with <see cref="ICurrentContextProvider"/> per Architecture §7, §14, and §18.
/// </summary>
public interface ICurrentContextAccessor
{
    /// <summary>
    /// Sets the ambient authenticated identity context.
    /// </summary>
    /// <param name="userId">The authenticated UserAccount identifier, or null.</param>
    /// <param name="personId">The linked organizational Person identifier, or null.</param>
    /// <param name="roles">The active organizational roles assigned to the person.</param>
    void SetContext(Guid? userId, Guid? personId, IEnumerable<string>? roles);

    /// <summary>
    /// Clears the ambient context to an unauthenticated state.
    /// </summary>
    void Clear();
}
