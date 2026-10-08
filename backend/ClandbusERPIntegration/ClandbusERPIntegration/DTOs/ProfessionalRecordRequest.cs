using System.ComponentModel.DataAnnotations;

namespace ClandbusERPIntegration.DTOs;

public sealed class ProfessionalRecordRequest
{
    [Required, RegularExpression("Migration|Case|Certification|Development|Other")]
    public string ActivityType { get; set; } = "Other";
    [StringLength(200)] public string? RelatedActivityId { get; set; }
    [StringLength(500)] public string? RelatedActivityName { get; set; }
    [StringLength(4000)] public string? Result { get; set; }
    [StringLength(4000)] public string? Challenge { get; set; }
    [StringLength(4000)] public string? Solution { get; set; }
    [StringLength(4000)] public string? Learning { get; set; }
    [StringLength(1000)] public string? ImprovementArea { get; set; }
    [Required] public DateOnly Date { get; set; }
    [StringLength(4000)] public string? Notes { get; set; }
}
