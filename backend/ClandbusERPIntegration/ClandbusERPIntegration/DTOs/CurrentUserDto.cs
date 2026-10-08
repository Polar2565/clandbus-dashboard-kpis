namespace ClandbusERPIntegration.DTOs;

public sealed record CurrentUserDto(
    string Username,
    string DisplayName,
    string OwnerId,
    bool IsAuthenticated);
