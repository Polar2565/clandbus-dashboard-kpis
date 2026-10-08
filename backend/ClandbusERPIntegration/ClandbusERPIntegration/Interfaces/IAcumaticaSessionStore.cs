using ClandbusERPIntegration.DTOs;

namespace ClandbusERPIntegration.Interfaces;

public interface IAcumaticaSessionStore
{
    Task<IAcumaticaService?> LoginAsync(HttpContext context, LoginRequestDto request);
    IAcumaticaService? GetCurrent(HttpContext context);
    Task LogoutAsync(HttpContext context);
}
