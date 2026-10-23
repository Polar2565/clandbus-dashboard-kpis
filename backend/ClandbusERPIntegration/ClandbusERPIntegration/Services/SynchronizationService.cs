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
    public async Task<SyncRun> SynchronizeAsync(
        IAcumaticaService acumatica,
        bool includeTeamCases = false,
        CancellationToken cancellationToken = default)
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
            var caseRequest = includeTeamCases
                ? acumatica.GetAllCasesAsync()
                : acumatica.GetCasesAsync();
            var taskRequest = includeTeamCases
                ? acumatica.GetAllTasksAsync()
                : acumatica.GetTasksAsync();
            var caseRows = await caseRequest;
            var cases = caseRows.Select(x => MapCase(x, capturedAt)).ToArray();
            var ownerNames = caseRows
                .Select(row => new
                {
                    Id = Value(row, "Owner", "CRCase_ownerID"),
                    Name = Value(row, "OwnerDisplayName", "OwnerEmployeeName", "CRCase_ownerID_description")
                })
                .Where(owner => !string.IsNullOrWhiteSpace(owner.Id)
                    && !string.IsNullOrWhiteSpace(owner.Name)
                    && !owner.Id.Equals(owner.Name, StringComparison.OrdinalIgnoreCase))
                .GroupBy(owner => owner.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);
            foreach (var item in cases) item.UserKey = userKey;
            TaskSnapshot[] tasks;
            try
            {
                var liveTasks = await taskRequest;
                tasks = liveTasks.Select(x => MapTask(x, capturedAt)).ToArray();
                foreach (var task in tasks)
                    if (ownerNames.TryGetValue(task.Owner, out var ownerName)) task.Owner = ownerName;
            }
            catch (HttpRequestException)
            {
                tasks = previousTasks.Select(x => new TaskSnapshot
                {
                    UserKey = userKey,
                    ExternalId = x.ExternalId, Summary = x.Summary, Status = x.Status,
                    CompletionPercent = x.CompletionPercent, Category = x.Category,
                    Owner = x.Owner, RelatedCase = x.RelatedCase, StartAt = x.StartAt,
                    DueAt = x.DueAt, CompletedAt = x.CompletedAt, CapturedAt = capturedAt
                }).ToArray();
            }
            foreach (var item in tasks) item.UserKey = userKey;

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
        Customer = Value(row, "BAccount_acctName", "BusinessAccountName", "BusinessAccount", "Customer"),
        Category = Value(row, "CRCase_caseClassID", "ClassID", "CaseClass", "Category"),
        Status = Value(row, "CRCase_status", "Status"),
        Reason = Value(row, "CRCase_resolution", "Reason", "Resolution"),
        Owner = Value(row, "OwnerDisplayName", "CRCase_ownerID_description", "Owner", "OwnerEmployeeName", "CRCase_ownerID"),
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
        Owner = Value(row, "OwnerDisplayName", "OwnerID_description", "Owner"),
        RelatedCase = Value(row, "RelatedEntityDescription", "RelatedEntityNoteID", "RelatedEntity", "Entity", "RefNoteID"),
        StartAt = Date(row, "StartDate"),
        DueAt = Date(row, "DueDate"),
        CompletedAt = Date(row, "CompletedAt", "CompletedOn", "CompletedDate", "EndDate"),
        CapturedAt = capturedAt
    };

    private static string Value(JsonElement row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.TryGetProperty(name, out var property))
            {
                var expected = NormalizeFieldName(name);
                var match = row.EnumerateObject()
                    .Where(candidate => NormalizeFieldName(candidate.Name)
                        .Equals(expected, StringComparison.OrdinalIgnoreCase))
                    .Cast<JsonProperty?>()
                    .FirstOrDefault();
                if (!match.HasValue) continue;
                property = match.Value.Value;
            }
            while (property.ValueKind == JsonValueKind.Object)
            {
                if (property.TryGetProperty("value", out var wrapped))
                {
                    property = wrapped;
                    continue;
                }
                if (property.TryGetProperty("description", out var description))
                    return description.GetString() ?? description.ToString();
                if (property.TryGetProperty("id", out var id))
                    return id.GetString() ?? id.ToString();
                return string.Empty;
            }
            return property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty : property.ToString();
        }
        return string.Empty;
    }

    private static string NormalizeFieldName(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

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
