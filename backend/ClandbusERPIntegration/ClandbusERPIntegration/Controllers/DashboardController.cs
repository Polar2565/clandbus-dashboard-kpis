using ClandbusERPIntegration.Data;
using ClandbusERPIntegration.Configurations;
using ClandbusERPIntegration.DTOs;
using ClandbusERPIntegration.Interfaces;
using ClandbusERPIntegration.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClandbusERPIntegration.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DashboardController(
    IDbContextFactory<DashboardDbContext> dbFactory,
    ISynchronizationService synchronization,
    IAcumaticaSessionStore sessions,
    IOptions<TeamDashboardSettings> teamSettings) : ControllerBase
{
    private readonly TeamDashboardSettings _teamSettings = teamSettings.Value;
    private IAcumaticaService? Current => sessions.GetCurrent(HttpContext);
    private string UserKey => Current!.CurrentUsername.Trim().ToLowerInvariant();

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken)
    {
        var service = Current;
        if (service is null) return Unauthorized();
        var result = await synchronization.SynchronizeAsync(service, IsExecutive, cancellationToken);
        return Ok(new { result.CasesRead, result.TasksRead, result.FinishedAt });
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> Summary(
        [FromQuery] string? owner = null,
        CancellationToken cancellationToken = default)
    {
        if (Current is null) return Unauthorized();
        if (!CanUseOwnerFilter(owner)) return Forbid();
        var userKey = UserKey;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var lastRun = await db.SyncRuns.Where(x => x.Succeeded && x.UserKey == userKey)
            .OrderByDescending(x => x.FinishedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastRun is null)
            return Ok(new DashboardSummaryDto(0, 0, 0, 0, 0, 0, 0, 0, null));

        var cases = await FilterCasesQuery(
                db.Cases.AsNoTracking().Where(x => x.UserKey == userKey && x.CapturedAt == lastRun.StartedAt), owner)
            .ToListAsync(cancellationToken);
        var tasks = await FilterTasksQuery(
                db.Tasks.AsNoTracking().Where(x => x.UserKey == userKey && x.CapturedAt == lastRun.StartedAt), owner)
            .ToListAsync(cancellationToken);
        var completed = tasks.Count(x => x.CompletionPercent >= 100
            || x.Status.Contains("Completed", StringComparison.OrdinalIgnoreCase)
            || x.Status.Contains("Complet", StringComparison.OrdinalIgnoreCase));
        var now = DateTimeOffset.Now;

        return Ok(new DashboardSummaryDto(
            cases.Count(x => !IsClosed(x.Status)),
            cases.Count(x => x.Status.Contains("Open", StringComparison.OrdinalIgnoreCase)
                || x.Status.Contains("Abierto", StringComparison.OrdinalIgnoreCase)),
            cases.Count(x => x.Status.Contains("Pending", StringComparison.OrdinalIgnoreCase)
                || x.Status.Contains("Pendiente", StringComparison.OrdinalIgnoreCase)),
            cases.Count(x => IsClosed(x.Status)),
            tasks.Count - completed,
            completed,
            tasks.Count(x => x.DueAt < now && x.CompletionPercent < 100),
            tasks.Count == 0 ? 0 : Math.Round(tasks.Average(x => x.CompletionPercent), 1),
            lastRun.FinishedAt));
    }

    [HttpGet("view")]
    public async Task<IActionResult> View(
        [FromQuery] string? owner = null,
        CancellationToken cancellationToken = default)
    {
        if (Current is null) return Unauthorized();
        if (!CanUseOwnerFilter(owner)) return Forbid();

        var userKey = UserKey;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var run = await db.SyncRuns.AsNoTracking()
            .Where(x => x.Succeeded && x.UserKey == userKey)
            .OrderByDescending(x => x.FinishedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (run is null)
            return Ok(new
            {
                summary = new DashboardSummaryDto(0, 0, 0, 0, 0, 0, 0, 0, null),
                cases = Array.Empty<CaseSnapshot>(),
                tasks = Array.Empty<TaskSnapshot>()
            });

        // LocalDB atiende mejor estas lecturas de forma secuencial. El filtro se
        // ejecuta en SQL para no cargar en memoria la información de todo el equipo.
        var cases = await FilterCasesQuery(
                db.Cases.AsNoTracking()
                    .Where(x => x.UserKey == userKey && x.CapturedAt == run.StartedAt), owner)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var tasks = await FilterTasksQuery(
                db.Tasks.AsNoTracking()
                    .Where(x => x.UserKey == userKey && x.CapturedAt == run.StartedAt), owner)
            .OrderBy(x => x.DueAt)
            .ToListAsync(cancellationToken);

        var completed = tasks.Count(x => x.CompletionPercent >= 100
            || x.Status.Contains("Completed", StringComparison.OrdinalIgnoreCase)
            || x.Status.Contains("Complet", StringComparison.OrdinalIgnoreCase));
        var now = DateTimeOffset.Now;
        var summary = new DashboardSummaryDto(
            cases.Count(x => !IsClosed(x.Status)),
            cases.Count(x => x.Status.Contains("Open", StringComparison.OrdinalIgnoreCase)
                || x.Status.Contains("Abierto", StringComparison.OrdinalIgnoreCase)),
            cases.Count(x => x.Status.Contains("Pending", StringComparison.OrdinalIgnoreCase)
                || x.Status.Contains("Pendiente", StringComparison.OrdinalIgnoreCase)),
            cases.Count(x => IsClosed(x.Status)),
            tasks.Count - completed,
            completed,
            tasks.Count(x => x.DueAt < now && x.CompletionPercent < 100),
            tasks.Count == 0 ? 0 : Math.Round(tasks.Average(x => x.CompletionPercent), 1),
            run.FinishedAt);

        return Ok(new { summary, cases, tasks });
    }

    [HttpGet("cases")]
    public async Task<IActionResult> Cases(
        [FromQuery] string? owner = null,
        CancellationToken cancellationToken = default)
    {
        if (Current is null) return Unauthorized();
        if (!CanUseOwnerFilter(owner)) return Forbid();
        var userKey = UserKey;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var run = await db.SyncRuns.Where(x => x.Succeeded && x.UserKey == userKey).OrderByDescending(x => x.FinishedAt).FirstOrDefaultAsync(cancellationToken);
        if (run is null) return Ok(Array.Empty<CaseSnapshot>());
        var items = await FilterCasesQuery(
                db.Cases.AsNoTracking().Where(x => x.UserKey == userKey && x.CapturedAt == run.StartedAt), owner)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("tasks")]
    public async Task<IActionResult> Tasks(
        [FromQuery] string? owner = null,
        CancellationToken cancellationToken = default)
    {
        if (Current is null) return Unauthorized();
        if (!CanUseOwnerFilter(owner)) return Forbid();
        var userKey = UserKey;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var run = await db.SyncRuns.Where(x => x.Succeeded && x.UserKey == userKey).OrderByDescending(x => x.FinishedAt).FirstOrDefaultAsync(cancellationToken);
        if (run is null) return Ok(Array.Empty<TaskSnapshot>());
        var items = await FilterTasksQuery(
                db.Tasks.AsNoTracking().Where(x => x.UserKey == userKey && x.CapturedAt == run.StartedAt), owner)
            .OrderBy(x => x.DueAt).ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("trends")]
    public async Task<ActionResult<IReadOnlyList<TrendPointDto>>> Trends(
        [FromQuery] string period = "week",
        [FromQuery] string? owner = null,
        CancellationToken cancellationToken = default)
    {
        if (Current is null) return Unauthorized();
        if (!CanUseOwnerFilter(owner)) return Forbid();
        var userKey = UserKey;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var run = await db.SyncRuns.Where(x => x.Succeeded && x.UserKey == userKey)
            .OrderByDescending(x => x.FinishedAt).FirstOrDefaultAsync(cancellationToken);
        if (run is null) return Ok(Array.Empty<TrendPointDto>());

        var cases = await FilterCasesQuery(
                db.Cases.AsNoTracking().Where(x => x.UserKey == userKey && x.CapturedAt == run.StartedAt), owner)
            .ToListAsync(cancellationToken);
        var tasks = await FilterTasksQuery(
                db.Tasks.AsNoTracking().Where(x => x.UserKey == userKey && x.CapturedAt == run.StartedAt), owner)
            .ToListAsync(cancellationToken);
        var points = new List<TrendPointDto>();
        var today = DateTimeOffset.Now.Date;

        if (period.Equals("month", StringComparison.OrdinalIgnoreCase))
        {
            for (var offset = 5; offset >= 0; offset--)
            {
                var start = new DateTime(today.Year, today.Month, 1).AddMonths(-offset);
                var end = start.AddMonths(1);
                points.Add(new TrendPointDto(start.ToString("MMM"),
                    cases.Count(x => x.CreatedAt >= start && x.CreatedAt < end),
                    tasks.Count(x => x.CompletedAt >= start && x.CompletedAt < end)));
            }
        }
        else
        {
            for (var offset = 6; offset >= 0; offset--)
            {
                var start = today.AddDays(-offset);
                var end = start.AddDays(1);
                points.Add(new TrendPointDto(start.ToString("ddd"),
                    cases.Count(x => x.CreatedAt >= start && x.CreatedAt < end),
                    tasks.Count(x => x.CompletedAt >= start && x.CompletedAt < end)));
            }
        }

        return Ok(points);
    }

    private static bool IsClosed(string status) =>
        status.Contains("Closed", StringComparison.OrdinalIgnoreCase)
        || status.Contains("Cerrado", StringComparison.OrdinalIgnoreCase)
        || status.Contains("Released", StringComparison.OrdinalIgnoreCase);

    private bool IsExecutive => Current is not null && _teamSettings.ExecutiveUsers.Any(allowed =>
        allowed.Equals(Current.CurrentUsername, StringComparison.OrdinalIgnoreCase)
        || allowed.Equals(Current.CurrentDisplayName, StringComparison.OrdinalIgnoreCase));

    private bool CanUseOwnerFilter(string? owner) => string.IsNullOrWhiteSpace(owner) || IsExecutive;

    private static IQueryable<CaseSnapshot> FilterCasesQuery(IQueryable<CaseSnapshot> query, string? owner)
    {
        if (string.IsNullOrWhiteSpace(owner)) return query;
        var value = owner.Trim();
        return query.Where(x => x.Owner == value || x.Owner.StartsWith(value + " "));
    }

    private static IQueryable<TaskSnapshot> FilterTasksQuery(IQueryable<TaskSnapshot> query, string? owner)
    {
        if (string.IsNullOrWhiteSpace(owner)) return query;
        var value = owner.Trim();
        return query.Where(x => x.Owner == value || x.Owner.StartsWith(value + " "));
    }

}
