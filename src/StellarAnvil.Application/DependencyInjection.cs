using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using StellarAnvil.Application.Services;
using StellarAnvil.Application.Mappings;
using StellarAnvil.Application.Skills;

namespace StellarAnvil.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        // AutoMapper
        services.AddAutoMapper(typeof(MappingProfile));

        // Application Services
        services.AddScoped<ITeamMemberApplicationService, TeamMemberApplicationService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<ITaskApplicationService, TaskApplicationService>();
        services.AddScoped<AutoGenCollaborationService>();
        services.AddScoped<WorkflowStateMachine>();

        // Skills
        services.AddScoped<ContinueDevSkills>();
        services.AddScoped<JiraMcpSkills>();
        services.AddHttpClient<JiraMcpSkills>();

        // AI Services
        services.AddSingleton<IChatClient>(provider =>
        {
            var openAiApiKey = configuration["AI:OpenAI:ApiKey"];
            if (!string.IsNullOrEmpty(openAiApiKey))
            {
                return new MockChatClient(); // Use mock for now - replace with actual OpenAI client when available
            }
            
            // Fallback to a mock client for development
            return new MockChatClient();
        });

        // Semantic Kernel
        services.AddSingleton<Kernel>(provider =>
        {
            var builder = Kernel.CreateBuilder();
            
            var openAiApiKey = configuration["AI:OpenAI:ApiKey"];
            if (!string.IsNullOrEmpty(openAiApiKey))
            {
                builder.AddOpenAIChatCompletion("gpt-4", openAiApiKey);
            }

            var claudeApiKey = configuration["AI:Claude:ApiKey"];
            if (!string.IsNullOrEmpty(claudeApiKey))
            {
                // Add Claude connector when available
            }

            var geminiApiKey = configuration["AI:Gemini:ApiKey"];
            if (!string.IsNullOrEmpty(geminiApiKey))
            {
                // Add Gemini connector when available
            }

            // Register skills
            var continueDevSkills = provider.GetRequiredService<ContinueDevSkills>();
            var jiraMcpSkills = provider.GetRequiredService<JiraMcpSkills>();
            
            builder.Plugins.AddFromObject(continueDevSkills, "ContinueDevSkills");
            builder.Plugins.AddFromObject(jiraMcpSkills, "JiraMcpSkills");

            return builder.Build();
        });

        return services;
    }
}

// Mock chat client for development/testing
public class MockChatClient : IChatClient
{
    public ChatClientMetadata Metadata => new("mock-client");

    public async Task<ChatCompletion> CompleteAsync(IList<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken); // Simulate API call
        
        var lastMessage = chatMessages.LastOrDefault()?.Text ?? "";
        var response = GenerateMockResponse(lastMessage);
        
        return new ChatCompletion(new ChatMessage(ChatRole.Assistant, response));
    }

    public IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(IList<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Streaming not implemented in mock client");
    }

    public void Dispose()
    {
        // Nothing to dispose
    }

    public TService? GetService<TService>(object? key = null) where TService : class
    {
        return null;
    }

    public object? GetService(Type serviceType, object? key = null)
    {
        return null;
    }

    private static string GenerateMockResponse(string input)
    {
        if (input.Contains("workflow", StringComparison.OrdinalIgnoreCase))
        {
            return "Simple SDLC Workflow";
        }
        
        if (input.Contains("yes", StringComparison.OrdinalIgnoreCase) || 
            input.Contains("confirm", StringComparison.OrdinalIgnoreCase))
        {
            return "YES";
        }
        
        return "I'm a mock AI assistant. This is a placeholder response for development purposes.";
    }
}
