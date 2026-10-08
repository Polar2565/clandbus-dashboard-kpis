using ClandbusERPIntegration.Models;

namespace ClandbusERPIntegration.Interfaces;

public interface ISynchronizationService
{
    Task<SyncRun> SynchronizeAsync(IAcumaticaService acumatica, CancellationToken cancellationToken = default);
}
