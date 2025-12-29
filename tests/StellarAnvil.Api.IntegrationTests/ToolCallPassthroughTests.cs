using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace StellarAnvil.Api.IntegrationTests;

/// <summary>
/// Integration tests for tool call passthrough functionality.
/// Tests the flow: Client -> Agent requests tool -> Client executes -> Agent continues
/// </summary>
public class ToolCallPassthroughTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public ToolCallPassthroughTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    [Fact]
    public async Task ToolCallFlow_WhenAgentRequestsTool_ReturnsToolCallAndContinuesAfterResult()
    {
        // Arrange - A question that should trigger a tool call (read_file for package.json or similar)
        var request = new ChatCompletionRequest
        {
            Model = "gpt-5-nano",
            Stream = true,
            Messages =
            [
                new ChatMessage { Role = "user", Content = "Which library are we using for OpenAPI docs? Check the project files." }
            ],
            Tools =
            [
                new Tool
                {
                    Type = "function",
                    Function = new FunctionDefinition
                    {
                        Name = "read_file",
                        Description = "Read a file from the filesystem",
                        Parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                target_file = new { type = "string", description = "Path to the file to read" }
                            },
                            required = new[] { "target_file" }
                        }
                    }
                }
            ]
        };

        // Act - Step 1: Send initial request and collect tool calls
        var (taskId, toolCalls) = await SendRequestAndCollectToolCalls(request);

        // Assert - Should have received tool calls
        toolCalls.Should().NotBeEmpty("Agent should request tool calls to read project files");
        taskId.Should().NotBeNullOrEmpty("Response should include task ID for continuation");

        Console.WriteLine($"Task ID: {taskId}");
        Console.WriteLine($"Tool calls received: {toolCalls.Count}");
        foreach (var tc in toolCalls)
        {
            Console.WriteLine($"  - {tc.Function?.Name}: {tc.Function?.Arguments}");
        }

        // Act - Step 2: Send tool results back (for ALL tool calls)
        var toolResultMessages = new List<ChatMessage>
        {
            // Include original user message
            new ChatMessage { Role = "user", Content = "Which library are we using for OpenAPI docs? Check the project files." },
            // Include task ID marker
            new ChatMessage { Role = "assistant", Content = $"<!-- task:{taskId} -->" },
            // Assistant message with tool calls
            new ChatMessage 
            { 
                Role = "assistant", 
                Content = null,
                ToolCalls = toolCalls
            }
        };
        
        // Add tool results for ALL tool calls
        foreach (var tc in toolCalls)
        {
            var fileName = tc.Function?.Arguments?.Contains("package.json") == true ? "package.json" :
                           tc.Function?.Arguments?.Contains("README.md") == true ? "README.md" :
                           tc.Function?.Arguments?.Contains(".csproj") == true ? "project.csproj" :
                           "unknown";
            
            // Simulate realistic responses - most files don't exist, but csproj has the answer
            string content;
            if (tc.Function?.Arguments?.Contains("package.json") == true ||
                tc.Function?.Arguments?.Contains("pyproject.toml") == true ||
                tc.Function?.Arguments?.Contains("requirements.txt") == true ||
                tc.Function?.Arguments?.Contains("go.mod") == true)
            {
                content = "Error: File not found";
            }
            else if (tc.Function?.Arguments?.Contains("README.md") == true)
            {
                content = @"# StellarAnvil API
A multi-agent orchestration API built with .NET 10 and Microsoft Agent Framework.

## API Documentation
OpenAPI documentation is available via Scalar at /scalar/v1";
            }
            else
            {
                content = @"<Project Sdk=""Microsoft.NET.Sdk.Web"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Scalar.AspNetCore"" Version=""2.0.36"" />
    <PackageReference Include=""Microsoft.AspNetCore.OpenApi"" Version=""10.0.0-preview.1.25120.3"" />
  </ItemGroup>
</Project>";
            }
            
            toolResultMessages.Add(new ChatMessage
            {
                Role = "tool",
                ToolCallId = tc.Id,
                Content = content
            });
        }
        
        var toolResultRequest = new ChatCompletionRequest
        {
            Model = "gpt-5-nano",
            Stream = true,
            Messages = toolResultMessages,
            Tools = request.Tools
        };

        // Loop through tool calls until we get a text response (max 5 rounds)
        var currentRequest = toolResultRequest;
        var currentToolCalls = toolCalls;
        var allToolCallsProcessed = new List<string>();
        const int maxRounds = 10;
        
        for (int round = 1; round <= maxRounds; round++)
        {
            Console.WriteLine($"\n=== Round {round} ===");
            
            var (newTaskId, newToolCalls) = await SendRequestAndCollectToolCalls(currentRequest);
            
            if (newToolCalls.Count == 0)
            {
                // No more tool calls - we should have a text response
                Console.WriteLine("No more tool calls - getting final response...");
                break;
            }
            
            Console.WriteLine($"Agent requested {newToolCalls.Count} tool calls:");
            foreach (var tc in newToolCalls)
            {
                var fileName = ExtractFileName(tc.Function?.Arguments);
                Console.WriteLine($"  - {tc.Function?.Name}: {fileName}");
                allToolCallsProcessed.Add(fileName);
            }
            
            // Build next request with all tool results
            var nextMessages = new List<ChatMessage>(currentRequest.Messages!);
            
            // Add the assistant message with new tool calls
            nextMessages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = null,
                ToolCalls = newToolCalls
            });
            
            // Add tool results for all new tool calls
            foreach (var tc in newToolCalls)
            {
                var content = GetSimulatedToolResult(tc.Function?.Arguments);
                nextMessages.Add(new ChatMessage
                {
                    Role = "tool",
                    ToolCallId = tc.Id,
                    Content = content
                });
            }
            
            currentRequest = new ChatCompletionRequest
            {
                Model = "gpt-5-nano",
                Stream = true,
                Messages = nextMessages,
                Tools = request.Tools
            };
            currentToolCalls = newToolCalls;
        }
        
        // Get the final text response
        var finalResponse = await SendRequestAndCollectResponse(currentRequest);
        Console.WriteLine($"\n=== Final Response ===\n{finalResponse}");
        
        // Verify we got responses from Jr and Sr agents
        var hasJrResponse = finalResponse.Contains("Business Analyst", StringComparison.OrdinalIgnoreCase) ||
                           finalResponse.Contains("BA", StringComparison.OrdinalIgnoreCase);
        var hasSrResponse = finalResponse.Contains("Sr", StringComparison.OrdinalIgnoreCase) ||
                           finalResponse.Contains("Senior", StringComparison.OrdinalIgnoreCase);
        
        Console.WriteLine($"\nValidation:");
        Console.WriteLine($"  - Total tool calls processed: {allToolCallsProcessed.Count}");
        Console.WriteLine($"  - Has Jr response: {hasJrResponse}");
        Console.WriteLine($"  - Has Sr response: {hasSrResponse}");
        Console.WriteLine($"  - Response length: {finalResponse.Length} chars");
        
        // Assertions
        finalResponse.Should().NotBeNullOrEmpty("Should have a final response");
        (finalResponse.Length > 50).Should().BeTrue("Response should be substantial");
    }
    
    private static string ExtractFileName(string? arguments)
    {
        if (string.IsNullOrEmpty(arguments)) return "unknown";
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(arguments);
            if (json.TryGetProperty("target_file", out var file))
                return file.GetString() ?? "unknown";
        }
        catch { }
        return arguments;
    }
    
    private static string GetSimulatedToolResult(string? arguments)
    {
        if (string.IsNullOrEmpty(arguments)) return "Error: No arguments provided";
        
        var fileName = ExtractFileName(arguments);
        
        // Simulate realistic file contents
        if (fileName.EndsWith(".csproj"))
        {
            return @"<Project Sdk=""Microsoft.NET.Sdk.Web"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Scalar.AspNetCore"" Version=""2.0.36"" />
    <PackageReference Include=""Microsoft.AspNetCore.OpenApi"" Version=""10.0.0-preview.1.25120.3"" />
    <PackageReference Include=""Microsoft.Agents.AI.Workflows"" Version=""1.0.0-preview.251219.1"" />
  </ItemGroup>
</Project>";
        }
        
        if (fileName == "README.md")
        {
            return @"# StellarAnvil API
A multi-agent orchestration API built with .NET 10 and Microsoft Agent Framework.

## API Documentation
OpenAPI documentation is available via **Scalar** at `/scalar/v1`

## Tech Stack
- .NET 10
- Microsoft Agent Framework for multi-agent orchestration
- Scalar.AspNetCore for OpenAPI documentation";
        }
        
        if (fileName.Contains("openapi") || fileName.Contains("swagger"))
        {
            return "Error: File not found";
        }
        
        if (fileName == "package.json" || fileName == "pyproject.toml" || 
            fileName == "requirements.txt" || fileName == "go.mod" ||
            fileName == "build.gradle" || fileName == "pom.xml")
        {
            return "Error: File not found";
        }
        
        // Default - try to help the agent find the right file
        return "Error: File not found. Try looking for *.csproj files or README.md";
    }

    private async Task<(string? TaskId, List<ToolCall> ToolCalls)> SendRequestAndCollectToolCalls(ChatCompletionRequest request)
    {
        var response = await _client.PostAsJsonAsync("/v1/chat/completions", request, _jsonOptions);
        response.EnsureSuccessStatusCode();

        var toolCalls = new List<ToolCall>();
        var toolCallsById = new Dictionary<int, ToolCall>();
        string? taskId = null;
        var contentBuilder = new StringBuilder();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            try
            {
                var chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(data, _jsonOptions);
                if (chunk?.Choices == null || chunk.Choices.Count == 0) continue;

                var delta = chunk.Choices[0].Delta;
                
                // Collect content (may contain task ID)
                if (!string.IsNullOrEmpty(delta?.Content))
                {
                    contentBuilder.Append(delta.Content);
                }

                // Collect tool calls
                if (delta?.ToolCalls != null)
                {
                    foreach (var tc in delta.ToolCalls)
                    {
                        var index = tc.Index ?? 0;
                        if (!toolCallsById.TryGetValue(index, out var existing))
                        {
                            existing = new ToolCall { Id = tc.Id, Type = tc.Type, Function = new FunctionCall() };
                            toolCallsById[index] = existing;
                        }

                        if (!string.IsNullOrEmpty(tc.Id)) existing.Id = tc.Id;
                        if (!string.IsNullOrEmpty(tc.Type)) existing.Type = tc.Type;
                        if (tc.Function != null)
                        {
                            if (!string.IsNullOrEmpty(tc.Function.Name))
                                existing.Function!.Name = tc.Function.Name;
                            if (!string.IsNullOrEmpty(tc.Function.Arguments))
                                existing.Function!.Arguments = (existing.Function.Arguments ?? "") + tc.Function.Arguments;
                        }
                    }
                }

                // Check for finish_reason: tool_calls
                if (chunk.Choices[0].FinishReason == "tool_calls")
                {
                    break;
                }
            }
            catch (JsonException)
            {
                // Skip malformed chunks
            }
        }

        // Extract task ID from content
        var content = contentBuilder.ToString();
        var taskIdMatch = System.Text.RegularExpressions.Regex.Match(content, @"<!-- task:(\w+) -->");
        if (taskIdMatch.Success)
        {
            taskId = taskIdMatch.Groups[1].Value;
        }

        toolCalls.AddRange(toolCallsById.Values);
        return (taskId, toolCalls);
    }

    private async Task<string> SendRequestAndCollectResponse(ChatCompletionRequest request)
    {
        var response = await _client.PostAsJsonAsync("/v1/chat/completions", request, _jsonOptions);
        response.EnsureSuccessStatusCode();

        var contentBuilder = new StringBuilder();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            try
            {
                var chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(data, _jsonOptions);
                if (chunk?.Choices == null || chunk.Choices.Count == 0) continue;

                var delta = chunk.Choices[0].Delta;
                if (!string.IsNullOrEmpty(delta?.Content))
                {
                    contentBuilder.Append(delta.Content);
                }
            }
            catch (JsonException)
            {
                // Skip malformed chunks
            }
        }

        return contentBuilder.ToString();
    }

    #region DTOs for test

    private class ChatCompletionRequest
    {
        public string? Model { get; set; }
        public bool Stream { get; set; }
        public List<ChatMessage>? Messages { get; set; }
        public List<Tool>? Tools { get; set; }
    }

    private class ChatMessage
    {
        public string? Role { get; set; }
        public string? Content { get; set; }
        public List<ToolCall>? ToolCalls { get; set; }
        public string? ToolCallId { get; set; }
    }

    private class Tool
    {
        public string? Type { get; set; }
        public FunctionDefinition? Function { get; set; }
    }

    private class FunctionDefinition
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public object? Parameters { get; set; }
    }

    private class ToolCall
    {
        public int? Index { get; set; }
        public string? Id { get; set; }
        public string? Type { get; set; }
        public FunctionCall? Function { get; set; }
    }

    private class FunctionCall
    {
        public string? Name { get; set; }
        public string? Arguments { get; set; }
    }

    private class ChatCompletionChunk
    {
        public string? Id { get; set; }
        public string? Object { get; set; }
        public List<ChunkChoice>? Choices { get; set; }
    }

    private class ChunkChoice
    {
        public int Index { get; set; }
        public ChunkDelta? Delta { get; set; }
        public string? FinishReason { get; set; }
    }

    private class ChunkDelta
    {
        public string? Role { get; set; }
        public string? Content { get; set; }
        public List<ToolCall>? ToolCalls { get; set; }
    }

    #endregion
}

