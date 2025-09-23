using AspNetCore.Authentication.ApiKey;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using StellarAnvil.Api.Authentication;
using StellarAnvil.Api.Middleware;
using StellarAnvil.Api.Observability;
using StellarAnvil.Application;
using StellarAnvil.Domain.Enums;
using StellarAnvil.Infrastructure;
using StellarAnvil.Infrastructure.Data;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Serilog configuration
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "StellarAnvil.Api")
    .WriteTo.Console()
    .WriteTo.File("logs/stellar-anvil-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger configuration
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "StellarAnvil API", 
        Version = "v1",
        Description = "AI-powered SDLC orchestration platform"
    });
    
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "API Key needed to access the endpoints. X-API-Key: My_API_Key",
        In = ParameterLocation.Header,
        Name = "X-API-Key",
        Type = SecuritySchemeType.ApiKey
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Name = "ApiKey",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            new string[] {}
        }
    });
});

// Database and Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

// Application services
builder.Services.AddApplication(builder.Configuration);

// Authentication
builder.Services.AddAuthentication(ApiKeyDefaults.AuthenticationScheme)
    .AddApiKeyInHeaderOrQueryParams<ApiKeyAuthenticationHandler>(options =>
    {
        options.Realm = "StellarAnvil";
        options.KeyName = "X-API-Key";
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("ApiKeyType", ApiKeyType.Admin.ToString()));
              
    options.AddPolicy("OpenApiOnly", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("ApiKeyType", ApiKeyType.OpenApi.ToString()));
});

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<StellarAnvilDbContext>();

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("StellarAnvil.Api")
        .AddAttributes(new Dictionary<string, object>
        {
            ["service.version"] = "1.0.0",
            ["deployment.environment"] = builder.Environment.EnvironmentName
        }))
    .WithTracing(tracing => tracing
        .AddSource(ActivitySources.StellarAnvil.Name)
        .AddSource(ActivitySources.Workflow.Name)
        .AddSource(ActivitySources.AI.Name)
        .AddSource(ActivitySources.Database.Name)
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            options.EnrichWithHttpRequest = (activity, request) =>
            {
                activity.SetTag("http.request.method", request.Method);
                activity.SetTag("http.request.path", request.Path);
            };
            options.EnrichWithHttpResponse = (activity, response) =>
            {
                activity.SetTag("http.response.status_code", response.StatusCode);
            };
        })
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation(options =>
        {
            options.SetDbStatementForText = true;
            options.SetDbStatementForStoredProcedure = true;
        })
        .AddConsoleExporter()
        .AddJaegerExporter())
    .WithMetrics(metrics => metrics
        .AddMeter("StellarAnvil.Api")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter()
        .AddPrometheusExporter())
    .WithLogging(logging => logging
        .AddConsoleExporter());

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "StellarAnvil API V1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });
}

// Custom middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseHttpsRedirection();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Prometheus metrics endpoint
app.MapPrometheusScrapingEndpoint();

// Ensure database is created and migrated
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<StellarAnvilDbContext>();
    await context.Database.MigrateAsync();

    // Seed default data
    await DataSeeder.SeedDefaultDataAsync(context);
}

try
{
    Log.Information("Starting StellarAnvil API");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
