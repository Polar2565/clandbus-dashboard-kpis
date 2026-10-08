using ClandbusERPIntegration.Data;
using ClandbusERPIntegration.DTOs;
using ClandbusERPIntegration.Interfaces;
using ClandbusERPIntegration.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClandbusERPIntegration.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ProductivityController(
    IDbContextFactory<DashboardDbContext> dbFactory,
    IAcumaticaSessionStore sessions) : ControllerBase
{
    private string? UserKey => sessions.GetCurrent(HttpContext)?.CurrentUsername.Trim().ToLowerInvariant();

    [HttpGet("records")]
    public async Task<IActionResult> GetRecords([FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var userKey = UserKey;
        if (userKey is null) return Unauthorized();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.ProfessionalRecords.AsNoTracking().Where(x => x.UserKey == userKey);
        if (from.HasValue) query = query.Where(x => x.Date >= from.Value);
        if (to.HasValue) query = query.Where(x => x.Date <= to.Value);
        return Ok(await query.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).ToListAsync(cancellationToken));
    }

    [HttpPost("records")]
    public async Task<IActionResult> Create(ProfessionalRecordRequest request, CancellationToken cancellationToken)
    {
        var userKey = UserKey;
        if (userKey is null) return Unauthorized();
        var now = DateTimeOffset.UtcNow;
        var record = Map(request, new ProfessionalRecord { UserKey = userKey, CreatedAt = now, UpdatedAt = now });
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.ProfessionalRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetRecords), new { id = record.Id }, record);
    }

    [HttpPut("records/{id:long}")]
    public async Task<IActionResult> Update(long id, ProfessionalRecordRequest request, CancellationToken cancellationToken)
    {
        var userKey = UserKey;
        if (userKey is null) return Unauthorized();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var record = await db.ProfessionalRecords.FirstOrDefaultAsync(x => x.Id == id && x.UserKey == userKey, cancellationToken);
        if (record is null) return NotFound();
        Map(request, record).UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(record);
    }

    [HttpDelete("records/{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var userKey = UserKey;
        if (userKey is null) return Unauthorized();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var record = await db.ProfessionalRecords.FirstOrDefaultAsync(x => x.Id == id && x.UserKey == userKey, cancellationToken);
        if (record is null) return NotFound();
        db.ProfessionalRecords.Remove(record);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
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
