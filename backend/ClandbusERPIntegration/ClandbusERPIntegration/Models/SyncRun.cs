namespace ClandbusERPIntegration.Models;

public sealed class SyncRun
{
    public long Id { get; set; }
    public string UserKey { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public bool Succeeded { get; set; }
    public int CasesRead { get; set; }
    public int TasksRead { get; set; }
    public string Error { get; set; } = string.Empty;
}
