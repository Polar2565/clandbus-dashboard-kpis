using ClandbusERPIntegration.Configurations;
using ClandbusERPIntegration.Data;
using ClandbusERPIntegration.DTOs;
using ClandbusERPIntegration.Interfaces;
using ClandbusERPIntegration.Models;
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

    [HttpGet("report")]
    public async Task<IActionResult> Report(
        [FromQuery] string period = "week",
        [FromQuery] string? userKey = null,
        CancellationToken cancellationToken = default)
    {
        var current = sessions.GetCurrent(HttpContext);
        if (current is null) return Unauthorized();
        if (!CanViewTeam(current.CurrentUsername, current.CurrentDisplayName))
            return Forbid();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var successfulRuns = await db.SyncRuns.AsNoTracking()
            .Where(x => x.Succeeded)
            .OrderByDescending(x => x.FinishedAt)
            .ToListAsync(cancellationToken);
        var latestRuns = successfulRuns
            .GroupBy(x => x.UserKey, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();

        var selectedRuns = string.IsNullOrWhiteSpace(userKey)
            ? latestRuns
            : latestRuns.Where(x => x.UserKey.Equals(userKey, StringComparison.OrdinalIgnoreCase)).ToArray();

        var tasks = new List<TaskSnapshot>();
        var cases = new List<CaseSnapshot>();
        foreach (var run in selectedRuns)
        {
            tasks.AddRange(await db.Tasks.AsNoTracking()
                .Where(x => x.UserKey == run.UserKey && x.CapturedAt == run.StartedAt)
                .ToListAsync(cancellationToken));
            cases.AddRange(await db.Cases.AsNoTracking()
                .Where(x => x.UserKey == run.UserKey && x.CapturedAt == run.StartedAt)
                .ToListAsync(cancellationToken));
        }

        var allLatestTasks = new List<TaskSnapshot>();
        var allLatestCases = new List<CaseSnapshot>();
        foreach (var run in latestRuns)
        {
            allLatestTasks.AddRange(await db.Tasks.AsNoTracking()
                .Where(x => x.UserKey == run.UserKey && x.CapturedAt == run.StartedAt)
                .ToListAsync(cancellationToken));
            allLatestCases.AddRange(await db.Cases.AsNoTracking()
                .Where(x => x.UserKey == run.UserKey && x.CapturedAt == run.StartedAt)
                .ToListAsync(cancellationToken));
        }

        var members = latestRuns.Select(run => new TeamMemberDto(
            run.UserKey,
            DisplayName(run.UserKey, allLatestTasks, allLatestCases),
            run.FinishedAt)).OrderBy(x => x.DisplayName).ToArray();

        var (from, to, previousFrom, previousTo) = ResolveRange(period);
        var started = tasks.Where(x => InRange(x.StartAt, from, to)).ToArray();
        var completed = tasks.Where(x => IsCompleted(x) && InRange(x.CompletedAt ?? x.DueAt, from, to)).ToArray();
        var periodTasks = tasks.Where(x => InRange(x.StartAt, from, to)
            || InRange(x.CompletedAt ?? x.DueAt, from, to)).ToArray();
        var previousCompleted = tasks.Count(x => IsCompleted(x) && InRange(x.CompletedAt ?? x.DueAt, previousFrom, previousTo));
        var createdCases = cases.Where(x => InRange(x.CreatedAt, from, to)).ToArray();
        var activeTasks = tasks.Where(x => !IsCompleted(x) && !IsCancelled(x.Status)).ToArray();
        var activeCases = cases.Where(x => !IsClosed(x.Status)).ToArray();
        var now = DateTimeOffset.Now;
        var overdue = activeTasks.Where(x => x.DueAt.HasValue && x.DueAt.Value < now).ToArray();
        var denominator = periodTasks.Length;
        var completionRate = denominator == 0 ? 0 : Math.Round(completed.Length * 100m / denominator, 1);
        var variation = previousCompleted == 0
            ? completed.Length == 0 ? 0 : 100
            : Math.Round((completed.Length - previousCompleted) * 100m / previousCompleted, 1);

        var memberResults = selectedRuns.Select(run =>
        {
            var memberTasks = tasks.Where(x => x.UserKey == run.UserKey).ToArray();
            var memberCases = cases.Where(x => x.UserKey == run.UserKey).ToArray();
            var memberStarted = memberTasks.Count(x => InRange(x.StartAt, from, to));
            var memberCompleted = memberTasks.Count(x => IsCompleted(x) && InRange(x.CompletedAt ?? x.DueAt, from, to));
            var memberPeriodTasks = memberTasks.Count(x => InRange(x.StartAt, from, to)
                || InRange(x.CompletedAt ?? x.DueAt, from, to));
            var memberActive = memberTasks.Count(x => !IsCompleted(x) && !IsCancelled(x.Status));
            return new TeamMemberResultDto(
                run.UserKey,
                DisplayName(run.UserKey, allLatestTasks, allLatestCases),
                memberStarted,
                memberCompleted,
                memberActive,
                memberTasks.Count(x => !IsCompleted(x) && !IsCancelled(x.Status) && x.DueAt < now),
                memberCases.Count(x => InRange(x.CreatedAt, from, to)),
                memberCases.Count(x => !IsClosed(x.Status)),
                memberPeriodTasks == 0 ? 0 : Math.Round(memberCompleted * 100m / memberPeriodTasks, 1));
        }).OrderByDescending(x => x.TasksCompleted).ThenBy(x => x.DisplayName).ToArray();

        var categories = started
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Category) ? "Sin categoría" : x.Category.Trim())
            .Select(x => new TeamCategoryDto(x.Key, x.Count()))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name).ToArray();

        return Ok(new TeamDashboardDto(
            NormalizePeriod(period), from, to, string.IsNullOrWhiteSpace(userKey) ? null : userKey,
            started.Length, completed.Length, activeTasks.Length, overdue.Length,
            createdCases.Length, activeCases.Length, completionRate, previousCompleted, variation,
            members, memberResults, BuildTrend(tasks, cases, period, from, to), categories,
            tasks.OrderBy(x => x.DueAt).Select(x => new TeamTaskDetailDto(
                x.UserKey, x.Owner, x.Summary, x.Status, x.Category, x.CompletionPercent,
                x.StartAt, x.DueAt, x.CompletedAt, x.RelatedCase)).ToArray(),
            cases.OrderByDescending(x => x.CreatedAt).Select(x => new TeamCaseDetailDto(
                x.UserKey, x.Owner, x.CaseNumber, x.Subject, x.Customer, x.Status, x.CreatedAt)).ToArray(),
            selectedRuns.Select(x => (DateTimeOffset?)x.FinishedAt).Max()));
    }

    private bool CanViewTeam(string username, string displayName) =>
        _settings.ExecutiveUsers.Any(allowed =>
            allowed.Equals(username, StringComparison.OrdinalIgnoreCase)
            || allowed.Equals(displayName, StringComparison.OrdinalIgnoreCase));

    private static string DisplayName(string userKey, IEnumerable<TaskSnapshot> tasks, IEnumerable<CaseSnapshot> cases) =>
        tasks.FirstOrDefault(x => x.UserKey.Equals(userKey, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.Owner))?.Owner
        ?? cases.FirstOrDefault(x => x.UserKey.Equals(userKey, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.Owner))?.Owner
        ?? userKey.Split('@')[0].Replace('.', ' ');

    private static (DateTimeOffset? From, DateTimeOffset? To, DateTimeOffset? PreviousFrom, DateTimeOffset? PreviousTo) ResolveRange(string period)
    {
        var now = DateTimeOffset.Now;
        var today = new DateTimeOffset(now.Date, now.Offset);
        DateTimeOffset from;
        DateTimeOffset to;
        switch (NormalizePeriod(period))
        {
            case "month": from = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset); to = from.AddMonths(1); break;
            case "year": from = new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, now.Offset); to = from.AddYears(1); break;
            case "history": return (null, null, null, null);
            default:
                var back = ((int)today.DayOfWeek + 6) % 7;
                from = today.AddDays(-back); to = from.AddDays(7); break;
        }
        var duration = to - from;
        return (from, to, from - duration, from);
    }

    private static string NormalizePeriod(string period) => period.ToLowerInvariant() switch
    {
        "month" => "month", "year" => "year", "history" => "history", _ => "week"
    };

    private static bool InRange(DateTimeOffset? value, DateTimeOffset? from, DateTimeOffset? to) =>
        value.HasValue && (!from.HasValue || value.Value >= from.Value) && (!to.HasValue || value.Value < to.Value);

    private static bool IsCompleted(TaskSnapshot task) => task.CompletionPercent >= 100
        || task.Status.Contains("Completed", StringComparison.OrdinalIgnoreCase)
        || task.Status.Contains("Complet", StringComparison.OrdinalIgnoreCase)
        || task.Status.Contains("Terminado", StringComparison.OrdinalIgnoreCase);

    private static bool IsCancelled(string status) => status.Contains("Cancel", StringComparison.OrdinalIgnoreCase);

    private static bool IsClosed(string status) => status.Contains("Closed", StringComparison.OrdinalIgnoreCase)
        || status.Contains("Cerrado", StringComparison.OrdinalIgnoreCase)
        || status.Contains("Released", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<TeamTrendPointDto> BuildTrend(
        IReadOnlyCollection<TaskSnapshot> tasks,
        IReadOnlyCollection<CaseSnapshot> cases,
        string period,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        var points = new List<TeamTrendPointDto>();
        var normalized = NormalizePeriod(period);
        if (normalized == "week" && from.HasValue)
        {
            for (var index = 0; index < 7; index++) AddPoint(from.Value.AddDays(index), from.Value.AddDays(index + 1), "ddd");
        }
        else if (normalized == "month" && from.HasValue && to.HasValue)
        {
            for (var cursor = from.Value; cursor < to.Value; cursor = cursor.AddDays(7))
                AddPoint(cursor, cursor.AddDays(7) < to.Value ? cursor.AddDays(7) : to.Value, "d MMM");
        }
        else
        {
            var now = DateTimeOffset.Now;
            var first = normalized == "history"
                ? new DateTimeOffset(tasks.Select(x => x.StartAt).Where(x => x.HasValue).Select(x => x!.Value.Year).DefaultIfEmpty(now.Year).Min(), 1, 1, 0, 0, 0, now.Offset)
                : from!.Value;
            for (var cursor = first; cursor < (to ?? now.AddMonths(1)); cursor = cursor.AddMonths(1))
                AddPoint(cursor, cursor.AddMonths(1), "MMM yy");
        }
        return points;

        void AddPoint(DateTimeOffset start, DateTimeOffset end, string format) => points.Add(new TeamTrendPointDto(
            start.ToString(format, System.Globalization.CultureInfo.GetCultureInfo("es-MX")),
            tasks.Count(x => InRange(x.StartAt, start, end)),
            tasks.Count(x => IsCompleted(x) && InRange(x.CompletedAt ?? x.DueAt, start, end)),
            cases.Count(x => InRange(x.CreatedAt, start, end))));
    }
}
