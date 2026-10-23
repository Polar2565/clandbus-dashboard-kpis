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
    private static readonly TimeSpan InactivityTimeout = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new();

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
        _sessions[key] = new SessionEntry(service, DateTimeOffset.UtcNow);
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
        if (!_sessions.TryGetValue(key, out var entry)) return null;
        if (DateTimeOffset.UtcNow - entry.LastActivity >= InactivityTimeout || !entry.Service.IsLoggedIn)
        {
            if (_sessions.TryRemove(key, out var expired)) expired.Service.Dispose();
            return null;
        }
        entry.LastActivity = DateTimeOffset.UtcNow;
        return entry.Service;
    }

    public async Task LogoutAsync(HttpContext context)
    {
        if (context.Request.Cookies.TryGetValue(CookieName, out var key)
            && _sessions.TryRemove(key, out var entry))
        {
            try { await entry.Service.LogoutAsync(); }
            finally { entry.Service.Dispose(); }
        }
        context.Response.Cookies.Delete(CookieName, new CookieOptions
        {
            HttpOnly = true, Secure = true, SameSite = SameSiteMode.None, IsEssential = true
        });
    }

    private sealed class SessionEntry(IAcumaticaService service, DateTimeOffset lastActivity)
    {
        public IAcumaticaService Service { get; } = service;
        public DateTimeOffset LastActivity { get; set; } = lastActivity;
    }
}
