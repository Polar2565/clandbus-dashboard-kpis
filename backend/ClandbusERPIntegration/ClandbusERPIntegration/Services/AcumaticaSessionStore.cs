using System.Collections.Concurrent;
using System.Net;
using ClandbusERPIntegration.Configurations;
using ClandbusERPIntegration.DTOs;
using ClandbusERPIntegration.Interfaces;
using Microsoft.Extensions.Options;

namespace ClandbusERPIntegration.Services;

public sealed class AcumaticaSessionStore(IOptions<AcumaticaSettings> settings) : IAcumaticaSessionStore
{
    public const string CookieName = "clandbus_session";
    private readonly ConcurrentDictionary<string, IAcumaticaService> _sessions = new();

    public async Task<IAcumaticaService?> LoginAsync(HttpContext context, LoginRequestDto request)
    {
        await LogoutAsync(context);
        var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true };
        var service = new AcumaticaService(new HttpClient(handler), settings);
        if (!await service.LoginAsync(request))
        {
            service.Dispose();
            return null;
        }

        var key = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        _sessions[key] = service;
        context.Response.Cookies.Append(CookieName, key, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            IsEssential = true,
            MaxAge = TimeSpan.FromHours(8)
        });
        return service;
    }

    public IAcumaticaService? GetCurrent(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var key)) return null;
        return _sessions.TryGetValue(key, out var service) && service.IsLoggedIn ? service : null;
    }

    public async Task LogoutAsync(HttpContext context)
    {
        if (context.Request.Cookies.TryGetValue(CookieName, out var key)
            && _sessions.TryRemove(key, out var service))
        {
            try { await service.LogoutAsync(); }
            finally { if (service is IDisposable disposable) disposable.Dispose(); }
        }
        context.Response.Cookies.Delete(CookieName, new CookieOptions
        {
            HttpOnly = true, Secure = true, SameSite = SameSiteMode.None, IsEssential = true
        });
    }
}
