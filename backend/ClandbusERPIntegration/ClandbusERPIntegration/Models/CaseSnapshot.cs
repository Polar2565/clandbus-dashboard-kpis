namespace ClandbusERPIntegration.Models;

public sealed class CaseSnapshot
{
    public long Id { get; set; }
    public string UserKey { get; set; } = string.Empty;
    public string CaseNumber { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? LastIncomingAt { get; set; }
    public DateTimeOffset? LastOutgoingAt { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
}
