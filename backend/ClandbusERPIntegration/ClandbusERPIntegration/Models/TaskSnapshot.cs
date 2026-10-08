namespace ClandbusERPIntegration.Models;

public sealed class TaskSnapshot
{
    public long Id { get; set; }
    public string UserKey { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CompletionPercent { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string RelatedCase { get; set; } = string.Empty;
    public DateTimeOffset? StartAt { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
}
