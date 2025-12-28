using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Scalar.AspNetCore;
using StellarAnvil.Api.Models.OpenAI;
using StellarAnvil.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for HTTP/2 support
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
    });
});

// Add services to the container.
builder.Services.AddOpenApi();

// Register Task Store (in-memory for now, can be swapped to database later)
builder.Services.AddSingleton<ITaskStore, InMemoryTaskStore>();

// Register Agent Registry (loads system prompts from SystemPrompts folder)
builder.Services.AddSingleton<IAgentRegistry, AgentRegistry>();

// Register Agent Factory (creates ChatClientAgent instances for Agent Framework)
builder.Services.AddSingleton<IAgentFactory, AgentFactory>();

// Register Deliberation Workflow (builds GroupChat workflows using Agent Framework)
builder.Services.AddSingleton<IDeliberationWorkflow, DeliberationWorkflow>();

// Register Agent Orchestrator (coordinates multi-agent workflow)
builder.Services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

// GET /v1/models - return hardcoded model list
app.MapGet("/v1/models", () =>
{
    var response = new ModelsResponse
    {
        Data =
        [
            new ModelInfo { Id = "gpt-5-nano" }
        ]
    };
    return Results.Ok(response);
})
.WithName("ListModels");

// POST /v1/chat/completions - Multi-agent orchestration with SSE streaming
app.MapPost("/v1/chat/completions", (
    ChatCompletionRequest request,
    IAgentOrchestrator orchestrator,
    CancellationToken cancellationToken) =>
{
    if (request.Stream != true)
    {
        return Results.BadRequest(new { error = "Only streaming requests are supported. Set stream: true" });
    }

    return TypedResults.ServerSentEvents(
        StreamChatCompletionAsync(request, orchestrator, cancellationToken));
})
.WithName("CreateChatCompletion");

app.Run();

static async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
    ChatCompletionRequest request,
    IAgentOrchestrator orchestrator,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    await foreach (var chunk in orchestrator.ProcessAsync(request, cancellationToken))
    {
        yield return chunk;
    }
}
