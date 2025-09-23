using StellarAnvil.Domain.Enums;

namespace StellarAnvil.Domain.Services;

public interface ISystemPromptService
{
    string GetDefaultSystemPrompt(TeamMemberRole role);
    Task<string> LoadSystemPromptFromFileAsync(string fileName);
}