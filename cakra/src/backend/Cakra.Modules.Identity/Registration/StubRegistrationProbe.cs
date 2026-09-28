namespace Cakra.Modules.Identity.Registration;

/// <summary>
/// P1-S04 technical probe registered by <see cref="IdentityModule.RegisterServices"/>.
/// It carries no behaviour; it exists solely so DI verification can prove a
/// module's self-registered service is present after startup.
/// </summary>
public sealed class StubRegistrationProbe
{
    /// <summary>Stable marker identifying this module registration.</summary>
    public string ModuleName => "Identity";
}
