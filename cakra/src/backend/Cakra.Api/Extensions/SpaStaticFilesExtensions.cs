namespace Cakra.Api.Extensions;

/// <summary>
/// SPA static asset hosting for the Vite-built Vue 3 frontend
/// (Architecture §19.10 — "serving both REST API endpoints and static SPA
/// frontend assets from wwwroot/").
/// </summary>
/// <remarks>
/// Wiring note for P1-S06: P1-S06 owns the canonical HTTP middleware pipeline
/// order. This extension is deliberately self-contained so the SPA hosting can
/// be positioned at the <c>UseStaticFiles()</c> step (position 3) of that
/// pipeline without duplicating static-file registration. The
/// <c>MapFallbackToFile</c> endpoint is registered with the fallback priority
/// and therefore never shadows API controller routes.
///
/// Asset flow (Architecture §19.10):
///   src/frontend/Cakra.Web → npm run build → dist/ → Cakra.Api/wwwroot/ (publish).
/// </remarks>
public static class SpaStaticFilesExtensions
{
    /// <summary>
    /// Serves the built SPA from <c>wwwroot/</c> and falls back to
    /// <c>index.html</c> so that client-side Vue Router history routes resolve
    /// on a hard refresh or deep link.
    /// </summary>
    public static WebApplication UseCakraSpa(this WebApplication app)
    {
        app.UseDefaultFiles();
        app.UseStaticFiles();

        // SPA history fallback. Requests that do not match a static file or a
        // registered API endpoint return the SPA entry point.
        app.MapFallbackToFile("index.html");

        return app;
    }
}
