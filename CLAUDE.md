# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

### Build and Run
```bash
# Full setup (infrastructure + application)
./setup.sh

# Start with Aspire orchestration (recommended)
dotnet run --project src/StellarAnvil.AppHost

# Start API directly
cd src/StellarAnvil.Api && dotnet run

# Build solution
dotnet build

# Restore packages
dotnet restore
```

### Testing
```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Database Operations
```bash
# Add migration
dotnet ef migrations add MigrationName --project src/StellarAnvil.Infrastructure --startup-project src/StellarAnvil.Api

# Update database
dotnet ef database update --project src/StellarAnvil.Infrastructure --startup-project src/StellarAnvil.Api

# Check database status
docker exec -it stellaranvil-postgres pg_isready -U stellaranvil -d stellaranvil
```

### Infrastructure Services
```bash
# Start monitoring stack and PostgreSQL
docker-compose up -d

# Stop services
docker-compose down
```

## Architecture Overview

StellarAnvil is a .NET 9 modular monolith using Clean Architecture with Domain-Driven Design principles.

### Project Structure
- **StellarAnvil.AppHost**: Aspire orchestration host with PostgreSQL and monitoring stack configuration
- **StellarAnvil.Api**: Web API layer with OpenAI-compatible endpoints and admin APIs
- **StellarAnvil.Application**: Business logic, use cases, DTOs, and AI orchestration services
- **StellarAnvil.Domain**: Core entities, enums, and domain services
- **StellarAnvil.Infrastructure**: Data access, external integrations, and cross-cutting concerns

### Key Technologies
- **.NET 9** with Aspire for orchestration
- **Entity Framework Core** with PostgreSQL
- **Semantic Kernel** for AI orchestration
- **Microsoft.Extensions.AI** for model connectors
- **AutoGen** for multi-agent collaboration
- **OpenTelemetry** with Grafana stack (Prometheus, Grafana, Loki, Jaeger)
- **Serilog** for structured logging

### AI Integration
The system provides OpenAI-compatible APIs (`/v1/chat/completions`, `/v1/models`) while orchestrating AI workflows internally using:
- **Semantic Kernel** for AI planning and execution
- **AutoGen** for multi-agent collaboration
- **MCP (Model Context Protocol)** for external tool integration (Jira, etc.)

### Authentication
Two API key types:
- **Admin API Key**: For team member management (`/api/admin/*`)
- **OpenAPI Key**: For chat completions (`/v1/*`)

### Workflow System
Configurable SDLC workflows with state machine:
1. **Simple SDLC**: PO → BA → Dev → QA → Done
2. **Standard SDLC**: PO → BA → Architect → Dev → QA → Done
3. **Full SDLC**: PO → BA → Architect → UX → Dev → QA → Done

AI Planner automatically selects workflow based on task complexity.

### Team Member Roles
- Product Owner, Business Analyst, Architect, UX Designer, Developer, QA, Security Reviewer
- Each role can be AI agent or human with configurable models and capabilities

### Observability
Full OpenTelemetry integration with custom activity sources:
- `StellarAnvil.Api` - API operations
- `StellarAnvil.Workflow` - Workflow state transitions
- `StellarAnvil.AI` - AI operations and model calls
- `StellarAnvil.Database` - Database operations

### Development Environment
- **Aspire Dashboard**: https://localhost:5000 (when using AppHost)
- **API**: https://localhost:5001
- **Swagger UI**: https://localhost:5001 (served at root in development)
- **Grafana**: http://localhost:3000 (admin/admin)
- **Prometheus**: http://localhost:9090
- **Jaeger**: http://localhost:16686

### Database Context
Uses `StellarAnvilDbContext` with automatic migrations on startup. PostgreSQL connection configured through Aspire or connection strings.

### Skills and MCP Integration
- **ContinueDevSkills**: Development tool integration
- **JiraMcpSkills**: Jira integration via MCP
- Skills registered as Semantic Kernel plugins for AI agent use

### Configuration
AI provider keys configured in `appsettings.Development.json`:
```json
{
  "AI": {
    "OpenAI": { "ApiKey": "your-key" },
    "Claude": { "ApiKey": "your-key" },
    "Gemini": { "ApiKey": "your-key" }
  }
}
```