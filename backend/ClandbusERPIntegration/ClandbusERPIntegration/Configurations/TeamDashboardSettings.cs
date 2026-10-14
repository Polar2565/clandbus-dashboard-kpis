namespace ClandbusERPIntegration.Configurations;

public sealed class TeamDashboardSettings
{
    public const string SectionName = "TeamDashboard";

    public string[] ExecutiveUsers { get; set; } = [];
    public string[] Members { get; set; } = [];
}
