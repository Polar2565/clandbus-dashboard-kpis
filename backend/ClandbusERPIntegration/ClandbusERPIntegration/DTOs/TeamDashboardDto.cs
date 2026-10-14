namespace ClandbusERPIntegration.DTOs;

public sealed record TeamMemberDto(string UserKey, string DisplayName, DateTimeOffset? LastSyncAt);

public sealed record TeamTrendPointDto(string Label, int TasksStarted, int TasksCompleted, int CasesCreated);

public sealed record TeamCategoryDto(string Name, int Count);

public sealed record TeamMemberResultDto(
    string UserKey,
    string DisplayName,
    int TasksStarted,
    int TasksCompleted,
    int ActiveTasks,
    int OverdueTasks,
    int CasesCreated,
    int ActiveCases,
    decimal CompletionRate);

public sealed record TeamTaskDetailDto(
    string UserKey,
    string Owner,
    string Summary,
    string Status,
    string Category,
    int CompletionPercent,
    DateTimeOffset? StartAt,
    DateTimeOffset? DueAt,
    DateTimeOffset? CompletedAt,
    string RelatedCase);

public sealed record TeamCaseDetailDto(
    string UserKey,
    string Owner,
    string CaseNumber,
    string Subject,
    string Customer,
    string Status,
    DateTimeOffset? CreatedAt);

public sealed record TeamDashboardDto(
    string Period,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? SelectedUserKey,
    int TasksStarted,
    int TasksCompleted,
    int ActiveTasks,
    int OverdueTasks,
    int CasesCreated,
    int ActiveCases,
    decimal CompletionRate,
    int PreviousTasksCompleted,
    decimal CompletedVariationPercent,
    IReadOnlyList<TeamMemberDto> Members,
    IReadOnlyList<TeamMemberResultDto> MemberResults,
    IReadOnlyList<TeamTrendPointDto> Trend,
    IReadOnlyList<TeamCategoryDto> Categories,
    IReadOnlyList<TeamTaskDetailDto> Tasks,
    IReadOnlyList<TeamCaseDetailDto> Cases,
    DateTimeOffset? LastSyncAt);
