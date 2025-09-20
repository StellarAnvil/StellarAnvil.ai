using Serilog;
using System.Diagnostics;

namespace StellarAnvil.Api.Middleware;

/// <summary>
/// Middleware for detailed request/response logging
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = Activity.Current?.Id ?? context.TraceIdentifier;

        // Log request
        _logger.LogInformation("HTTP {Method} {Path} started - RequestId: {RequestId}",
            context.Request.Method,
            context.Request.Path,
            requestId);

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP {Method} {Path} failed - RequestId: {RequestId}",
                context.Request.Method,
                context.Request.Path,
                requestId);
            throw;
        }
        finally
        {
            stopwatch.Stop();

            // Log response
            _logger.LogInformation("HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms - RequestId: {RequestId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                requestId);
        }
    }
}
