using AspNetCore.Authentication.ApiKey;
using Microsoft.EntityFrameworkCore;
using StellarAnvil.Domain.Enums;
using StellarAnvil.Infrastructure.Data;
using System.Security.Claims;

namespace StellarAnvil.Api.Authentication;

public class ApiKeyAuthenticationHandler : IApiKeyProvider
{
    private readonly StellarAnvilDbContext _context;

    public ApiKeyAuthenticationHandler(StellarAnvilDbContext context)
    {
        _context = context;
    }

    public async Task<IApiKey?> ProvideAsync(string key)
    {
        var apiKey = await _context.ApiKeys
            .FirstOrDefaultAsync(ak => ak.Key == key && ak.IsActive);

        if (apiKey == null)
            return null;

        // Update last used timestamp
        apiKey.LastUsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new ApiKeyModel(apiKey.Key, apiKey.Name, new List<Claim>
        {
            new(ClaimTypes.Name, apiKey.Name),
            new("ApiKeyType", apiKey.Type.ToString()),
            new("ApiKeyId", apiKey.Id.ToString())
        });
    }
}

public class ApiKeyModel : IApiKey
{
    public ApiKeyModel(string key, string ownerName, IReadOnlyCollection<Claim> claims)
    {
        Key = key;
        OwnerName = ownerName;
        Claims = claims;
    }

    public string Key { get; }
    public string OwnerName { get; }
    public IReadOnlyCollection<Claim> Claims { get; }
}
