using Microsoft.Extensions.AI;

namespace StellarAnvil.Application.Services;

public interface IAIClientService
{
    /// <summary>
    /// Gets the AI client for the specified model
    /// </summary>
    Task<IChatClient> GetClientForModelAsync(string? model);
    
    /// <summary>
    /// Gets list of supported models (only those with API keys configured)
    /// </summary>
    Task<List<string>> GetSupportedModelsAsync();
}
