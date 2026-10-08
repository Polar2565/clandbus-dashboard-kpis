namespace ClandbusERPIntegration.DTOs;

public sealed record DashboardSummaryDto(
    int ActiveCases,
    int OpenCases,
    int PendingCustomerCases,
    int ClosedCases,
    int ActiveTasks,
    int CompletedTasks,
    int OverdueTasks,
    double AverageTaskProgress,
    DateTimeOffset? LastSyncAt);
