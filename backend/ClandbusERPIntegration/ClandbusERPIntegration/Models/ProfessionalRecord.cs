namespace ClandbusERPIntegration.Models;

public sealed class ProfessionalRecord
{
    public long Id { get; set; }
    public string UserKey { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
    public string RelatedActivityId { get; set; } = string.Empty;
    public string RelatedActivityName { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Challenge { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public string Learning { get; set; } = string.Empty;
    public string ImprovementArea { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
