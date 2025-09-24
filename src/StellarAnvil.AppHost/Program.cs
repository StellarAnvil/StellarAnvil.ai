
var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL database
var postgres = builder.AddPostgres("postgres")
    .WithImage("postgres", "17")
    .WithEnvironment("POSTGRES_DB", "stellaranvil")
    .WithEnvironment("POSTGRES_USER", "stellaranvil")
    .WithEnvironment("POSTGRES_PASSWORD", "stellaranvil123")
    .WithEnvironment("POSTGRES_PORT", "5433");

var stellaranvildb = postgres.AddDatabase("stellaranvildb");

// Grafana stack for observability
var prometheus = builder.AddContainer("prometheus", "prom/prometheus", "latest")
    .WithBindMount("./monitoring/prometheus", "/etc/prometheus")
    .WithHttpEndpoint(port: 9090, targetPort: 9090, name: "prometheus-http");

var grafana = builder.AddContainer("grafana", "grafana/grafana", "latest")
    .WithEnvironment("GF_SECURITY_ADMIN_PASSWORD", "admin")
    .WithBindMount("./monitoring/grafana/provisioning", "/etc/grafana/provisioning")
    .WithBindMount("./monitoring/grafana/dashboards", "/var/lib/grafana/dashboards")
    .WithHttpEndpoint(port: 3000, targetPort: 3000, name: "grafana-http");

var loki = builder.AddContainer("loki", "grafana/loki", "latest")
    .WithBindMount("./monitoring/loki", "/etc/loki")
    .WithHttpEndpoint(port: 3100, targetPort: 3100, name: "loki-http");

var jaeger = builder.AddContainer("jaeger", "jaegertracing/all-in-one", "latest")
    .WithEnvironment("COLLECTOR_OTLP_ENABLED", "true")
    .WithHttpEndpoint(port: 16686, targetPort: 16686, name: "jaeger-ui")
    .WithHttpEndpoint(port: 14268, targetPort: 14268, name: "jaeger-collector");

// StellarAnvil API
var api = builder.AddProject<Projects.StellarAnvil_Api>("stellaranvil-api")
    .WithReference(stellaranvildb)
    .WithEnvironment("ConnectionStrings__DefaultConnection", stellaranvildb)
    .WithEnvironment("ASPNETCORE_URLS", "https://localhost:15888;http://localhost:15889")
    .WithEnvironment("DOTNET_DASHBOARD_OTLP_ENDPOINT_URL", "https://localhost:16090")
    .WithEnvironment("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true")
    .WithHttpEndpoint(port: 5000, targetPort: 8080, name: "stellaranvil-http")
    .WithHttpsEndpoint(port: 5001, targetPort: 8081, name: "stellaranvil-https");

var app = builder.Build();

app.Run();
