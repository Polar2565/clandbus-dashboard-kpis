using System.Text.Json;
using ClandbusERPIntegration.DTOs;

namespace ClandbusERPIntegration.Interfaces;

public interface IAcumaticaService : IDisposable
{
    bool IsLoggedIn { get; }
    string CurrentUsername { get; }
    string CurrentDisplayName { get; }
    string CurrentOwnerId { get; }
    Task<bool> LoginAsync(LoginRequestDto request);
    Task LogoutAsync();
    Task<IReadOnlyList<JsonElement>> GetCasesAsync();
    Task<IReadOnlyList<JsonElement>> GetTasksAsync();
    Task<List<SalesOrderDto>> GetLastSalesOrdersAsync();
    Task<bool> UpdateOrderAsync(UpdateOrderDto request);
    Task<bool> RemoveHoldAsync(RemoveHoldDto request);
}
