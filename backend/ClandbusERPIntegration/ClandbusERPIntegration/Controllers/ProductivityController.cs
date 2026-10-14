using ClandbusERPIntegration.Data;
using ClandbusERPIntegration.DTOs;
using ClandbusERPIntegration.Interfaces;
using ClandbusERPIntegration.Models;
using ClandbusERPIntegration.Configurations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;

namespace ClandbusERPIntegration.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ProductivityController(
    IDbContextFactory<DashboardDbContext> dbFactory,
    IAcumaticaSessionStore sessions,
    IOptions<TeamDashboardSettings> teamSettings) : ControllerBase
{
    private IAcumaticaService? Current => sessions.GetCurrent(HttpContext);
    private readonly TeamDashboardSettings _teamSettings = teamSettings.Value;

    [HttpGet("records")]
    public async Task<IActionResult> GetRecords([FromQuery] string? owner, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var userKey = ResolveUserKey(owner);
        if (userKey is null) return Current is null ? Unauthorized() : Forbid();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.ProfessionalRecords.AsNoTracking().Where(x => x.UserKey == userKey);
        if (from.HasValue) query = query.Where(x => x.Date >= from.Value);
        if (to.HasValue) query = query.Where(x => x.Date <= to.Value);
        return Ok(await query.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).ToListAsync(cancellationToken));
    }

    [HttpPost("records")]
    public async Task<IActionResult> Create([FromQuery] string? owner, ProfessionalRecordRequest request, CancellationToken cancellationToken)
    {
        var userKey = ResolveUserKey(owner);
        if (userKey is null) return Current is null ? Unauthorized() : Forbid();
        var now = DateTimeOffset.UtcNow;
        var record = Map(request, new ProfessionalRecord { UserKey = userKey, CreatedAt = now, UpdatedAt = now });
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.ProfessionalRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetRecords), new { id = record.Id }, record);
    }

    [HttpPut("records/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromQuery] string? owner, ProfessionalRecordRequest request, CancellationToken cancellationToken)
    {
        var userKey = ResolveUserKey(owner);
        if (userKey is null) return Current is null ? Unauthorized() : Forbid();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var record = await db.ProfessionalRecords.FirstOrDefaultAsync(x => x.Id == id && x.UserKey == userKey, cancellationToken);
        if (record is null) return NotFound();
        Map(request, record).UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(record);
    }

    [HttpDelete("records/{id:long}")]
    public async Task<IActionResult> Delete(long id, [FromQuery] string? owner, CancellationToken cancellationToken)
    {
        var userKey = ResolveUserKey(owner);
        if (userKey is null) return Current is null ? Unauthorized() : Forbid();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var record = await db.ProfessionalRecords.FirstOrDefaultAsync(x => x.Id == id && x.UserKey == userKey, cancellationToken);
        if (record is null) return NotFound();
        db.ProfessionalRecords.Remove(record);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private string? ResolveUserKey(string? owner)
    {
        var current = Current;
        if (current is null) return null;
        if (string.IsNullOrWhiteSpace(owner) || SamePerson(owner, current.CurrentDisplayName))
            return current.CurrentUsername.Trim().ToLowerInvariant();
        if (!_teamSettings.ExecutiveUsers.Any(value =>
            value.Equals(current.CurrentUsername, StringComparison.OrdinalIgnoreCase)
            || value.Equals(current.CurrentDisplayName, StringComparison.OrdinalIgnoreCase))) return null;
        return $"owner:{Normalize(owner)}";
    }

    private static bool SamePerson(string left, string right)
    {
        var a = Normalize(left); var b = Normalize(right);
        return a == b || a.StartsWith(b + " ", StringComparison.Ordinal) || b.StartsWith(a + " ", StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC);
    }

    private static ProfessionalRecord Map(ProfessionalRecordRequest source, ProfessionalRecord target)
    {
        target.ActivityType = source.ActivityType.Trim();
        target.RelatedActivityId = source.RelatedActivityId?.Trim() ?? string.Empty;
        target.RelatedActivityName = source.RelatedActivityName?.Trim() ?? string.Empty;
        target.Result = source.Result?.Trim() ?? string.Empty;
        target.Challenge = source.Challenge?.Trim() ?? string.Empty;
        target.Solution = source.Solution?.Trim() ?? string.Empty;
        target.Learning = source.Learning?.Trim() ?? string.Empty;
        target.ImprovementArea = source.ImprovementArea?.Trim() ?? string.Empty;
        target.Date = source.Date;
        target.Notes = source.Notes?.Trim() ?? string.Empty;
        return target;
    }
}
