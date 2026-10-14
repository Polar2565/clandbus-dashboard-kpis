using ClandbusERPIntegration.Configurations;
using ClandbusERPIntegration.Data;
using ClandbusERPIntegration.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClandbusERPIntegration.Controllers;

[ApiController]
[Route("api/team-dashboard")]
public sealed class TeamDashboardController(
    IDbContextFactory<DashboardDbContext> dbFactory,
    IAcumaticaSessionStore sessions,
    IOptions<TeamDashboardSettings> settings) : ControllerBase
{
    private readonly TeamDashboardSettings _settings = settings.Value;

    [HttpGet("access")]
    public IActionResult Access()
    {
        var current = sessions.GetCurrent(HttpContext);
        if (current is null) return Unauthorized();
        return Ok(new { canViewTeam = CanViewTeam(current.CurrentUsername, current.CurrentDisplayName) });
    }

    [HttpGet("owners")]
    public async Task<IActionResult> Owners(CancellationToken cancellationToken)
    {
        var current = sessions.GetCurrent(HttpContext);
        if (current is null) return Unauthorized();
        if (!CanViewTeam(current.CurrentUsername, current.CurrentDisplayName)) return Forbid();

        var userKey = current.CurrentUsername.Trim().ToLowerInvariant();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var run = await db.SyncRuns.AsNoTracking()
            .Where(x => x.Succeeded && x.UserKey == userKey)
            .OrderByDescending(x => x.FinishedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (run is null) return Ok(Array.Empty<string>());

        var taskOwners = await db.Tasks.AsNoTracking()
            .Where(x => x.UserKey == userKey && x.CapturedAt == run.StartedAt && x.Owner != "")
            .Select(x => x.Owner).Distinct().ToListAsync(cancellationToken);
        var caseOwners = await db.Cases.AsNoTracking()
            .Where(x => x.UserKey == userKey && x.CapturedAt == run.StartedAt && x.Owner != "")
            .Select(x => x.Owner).Distinct().ToListAsync(cancellationToken);

        return Ok(taskOwners.Concat(caseOwners)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToArray());
    }

    private bool CanViewTeam(string username, string displayName) =>
        _settings.ExecutiveUsers.Any(allowed =>
            allowed.Equals(username, StringComparison.OrdinalIgnoreCase)
            || allowed.Equals(displayName, StringComparison.OrdinalIgnoreCase));
}
