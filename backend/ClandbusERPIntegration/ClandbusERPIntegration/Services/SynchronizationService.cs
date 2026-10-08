using System.Globalization;
using System.Text.Json;
using ClandbusERPIntegration.Configurations;
using ClandbusERPIntegration.Data;
using ClandbusERPIntegration.Interfaces;
using ClandbusERPIntegration.Models;
using Microsoft.EntityFrameworkCore;

namespace ClandbusERPIntegration.Services;

public sealed class SynchronizationService(
    IDbContextFactory<DashboardDbContext> dbFactory) : ISynchronizationService
{
    public async Task<SyncRun> SynchronizeAsync(IAcumaticaService acumatica, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userKey = acumatica.CurrentUsername.Trim().ToLowerInvariant();
        var run = new SyncRun { StartedAt = DateTimeOffset.UtcNow, UserKey = userKey };
        db.SyncRuns.Add(run);

        try
        {
            var capturedAt = run.StartedAt;
            var previousRun = await db.SyncRuns.AsNoTracking().Where(x => x.Succeeded && x.UserKey == userKey)
                .OrderByDescending(x => x.FinishedAt).FirstOrDefaultAsync(cancellationToken);
            var previousTasks = previousRun is null
                ? []
                : await db.Tasks.AsNoTracking().Where(x => x.UserKey == userKey && x.CapturedAt == previousRun.StartedAt)
                    .ToListAsync(cancellationToken);
            var cases = (await acumatica.GetCasesAsync())
                .Select(x => MapCase(x, capturedAt)).ToArray();
            foreach (var item in cases) item.UserKey = userKey;
            var tasks = previousTasks.Select(x => new TaskSnapshot
            {
                UserKey = userKey,
                ExternalId = x.ExternalId, Summary = x.Summary, Status = x.Status,
                CompletionPercent = x.CompletionPercent, Category = x.Category,
                Owner = x.Owner, RelatedCase = x.RelatedCase, StartAt = x.StartAt,
                DueAt = x.DueAt, CompletedAt = x.CompletedAt, CapturedAt = capturedAt
            }).ToArray();

            db.Cases.AddRange(cases);
            db.Tasks.AddRange(tasks);
            run.CasesRead = cases.Length;
            run.TasksRead = tasks.Length;
            run.Succeeded = true;
            run.FinishedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return run;
        }
        catch (Exception ex)
        {
            run.Error = ex.Message;
            run.FinishedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private static CaseSnapshot MapCase(JsonElement row, DateTimeOffset capturedAt) => new()
    {
        CaseNumber = Value(row, "CRCase_caseCD", "CaseID", "CaseCD", "CaseNumber"),
        Subject = Value(row, "CRCase_subject", "Subject"),
        Customer = Value(row, "BAccount_acctName", "BusinessAccount", "Customer"),
        Category = Value(row, "CRCase_caseClassID", "ClassID", "CaseClass", "Category"),
        Status = Value(row, "CRCase_status", "Status"),
        Reason = Value(row, "CRCase_resolution", "Reason", "Resolution"),
        Owner = Value(row, "CRCase_ownerID_description", "Owner", "OwnerEmployeeName"),
        CreatedAt = Date(row, "CRCase_createdDateTime", "DateReported", "CreatedDateTime"),
        LastIncomingAt = Date(row, "CRActivityStatistics_lastIncomingActivityDate", "LastIncomingActivity"),
        LastOutgoingAt = Date(row, "CRActivityStatistics_lastOutgoingActivityDate", "LastOutgoingActivity"),
        CapturedAt = capturedAt
    };

    private static TaskSnapshot MapTask(JsonElement row, DateTimeOffset capturedAt) => new()
    {
        ExternalId = Value(row, "NoteID", "TaskID", "id"),
        Summary = Value(row, "Summary", "Subject"),
        Status = Value(row, "Status"),
        CompletionPercent = Number(row, "CompletionPercentage", "CompletionPercent", "PercentCompletion", "PercentCompleted"),
        Category = Value(row, "Category"),
        Owner = Value(row, "Owner"),
        RelatedCase = Value(row, "RelatedEntityDescription", "RelatedEntityNoteID", "RelatedEntity", "Entity", "RefNoteID"),
        StartAt = Date(row, "StartDate"),
        DueAt = Date(row, "DueDate"),
        CompletedAt = Date(row, "CompletedOn", "CompletedDate", "EndDate"),
        CapturedAt = capturedAt
    };

    private static string Value(JsonElement row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.TryGetProperty(name, out var property)) continue;
            if (property.ValueKind == JsonValueKind.Object
                && property.TryGetProperty("value", out var wrapped)) property = wrapped;
            return property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty : property.ToString();
        }
        return string.Empty;
    }

    private static DateTimeOffset? Date(JsonElement row, params string[] names) =>
        DateTimeOffset.TryParse(Value(row, names), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var value) ? value : null;

    private static int Number(JsonElement row, params string[] names)
    {
        var raw = Value(row, names).Replace("%", string.Empty).Trim();
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? Math.Clamp((int)Math.Round(value), 0, 100)
            : 0;
    }
}
