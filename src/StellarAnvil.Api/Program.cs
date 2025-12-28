using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http.HttpResults;
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

// Register OpenAI Agent Service
builder.Services.AddSingleton<IOpenAIAgentService, OpenAIAgentService>();

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

// POST /v1/chat/completions - SSE streaming only via Microsoft Agent Framework
app.MapPost("/v1/chat/completions", (
    ChatCompletionRequest request,
    IOpenAIAgentService agentService,
    CancellationToken cancellationToken) =>
{
    if (request.Stream != true)
    {
        return Results.BadRequest(new { error = "Only streaming requests are supported. Set stream: true" });
    }

    return TypedResults.ServerSentEvents(
        StreamChatCompletionAsync(request, agentService, cancellationToken));
})
.WithName("CreateChatCompletion");

app.Run();

static async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
    ChatCompletionRequest request,
    IOpenAIAgentService agentService,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    await foreach (var chunk in agentService.StreamAsync(request, cancellationToken))
    {
        yield return chunk;
    }
}
