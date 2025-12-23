# StellarAnvil

StellarAnvil is an AI-powered SDLC (Software Development Life Cycle) orchestration platform built as a .NET 9 modular monolith. It provides OpenAI-compatible APIs for seamless integration with development tools like Cursor, Void, Cline, and Continue, while orchestrating full software development workflows using AI agents.

## 🚀 Features

- **OpenAI-Compatible API**: Full compatibility with OpenAI API endpoints (`/v1/chat/completions`, `/v1/models`)
- **AI-Powered Workflows**: Automated SDLC orchestration using Semantic Kernel and AutoGen
- **Multi-Team Support**: Role-based team member management with AI and human agents
- **Workflow State Machine**: Configurable workflows for different project complexities
- **MCP Integration**: Model Context Protocol support for external tools (Jira, etc.)
- **Observability**: Full OpenTelemetry integration with Grafana stack
- **Modular Architecture**: Clean Architecture with DDD principles

## 🏗️ Architecture

- **Domain Layer**: Core business logic, entities, and domain services
- **Application Layer**: Use cases, DTOs, and application services
- **Infrastructure Layer**: Data access, external integrations, and cross-cutting concerns
- **API Layer**: REST controllers for Admin and OpenAI-compatible endpoints

## 🛠️ Technology Stack

- **.NET 10**: Latest .NET framework
- **PostgreSQL**: Primary database
- **Entity Framework Core**: ORM and migrations
- **Microsoft Agent Framework**: Agent framework
- **Serilog + OpenTelemetry**: Logging and observability
- **Aspire**: Local development orchestration
- **Docker**: Containerization and local services

## 📋 Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Colima](https://github.com/abiosoft/colima)
- [Git](https://git-scm.com/)

## 🚀 Quick Start

### 1. Clone the Repository

```bash
git clone https://github.com/yourusername/StellarAnvil.ai.git
cd StellarAnvil.ai
```

### 2. Start Infrastructure Services

```bash
# Start PostgreSQL and monitoring stack
docker-compose up -d
```

This will start:
- PostgreSQL (port 5432)
- Prometheus (port 9090)
- Grafana (port 3000, admin/admin)
- Loki (port 3100)
- Jaeger (port 16686)

### 3. Configure AI Services (Optional)

Add your AI API keys to `src/StellarAnvil.Api/appsettings.Development.json`:

```json
{
  "AI": {
    "OpenAI": {
      "ApiKey": "your-openai-api-key"
    },
    "Claude": {
      "ApiKey": "your-claude-api-key"
    },
    "Gemini": {
      "ApiKey": "your-gemini-api-key"
    }
  }
}
```

### 4. Run with Aspire (Recommended)

```bash
# Run the complete application with Aspire orchestration
dotnet run --project src/StellarAnvil.AppHost
```

### 5. Alternative: Run API Directly

```bash
# Navigate to API project
cd src/StellarAnvil.Api

# Run database migrations
dotnet ef database update

# Start the API
dotnet run
```

## 🔑 API Keys

The system uses two types of API keys:

1. **Admin API Key**: For team member management (`/api/admin/*`)
2. **OpenAPI Key**: For chat completions (`/v1/*`)

Default keys are automatically generated and can be found in the database after first run.

## 📚 API Documentation

### Admin Endpoints

- `GET /api/admin/teammembers` - List all team members
- `POST /api/admin/teammembers` - Create new team member
- `PUT /api/admin/teammembers/{id}` - Update team member
- `DELETE /api/admin/teammembers/{id}` - Delete team member

### OpenAI-Compatible Endpoints

- `POST /v1/chat/completions` - Create chat completion
- `GET /v1/models` - List available models

### Example: Create Team Member

```bash
curl -X POST "https://localhost:5001/api/admin/teammembers" \
  -H "X-API-Key: your-admin-key" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Alice Developer",
    "email": "alice@company.com",
    "type": 2,
    "role": 5,
    "grade": 1,
    "model": "gpt-4"
  }'
```

### Example: Chat Completion

```bash
curl -X POST "https://localhost:5001/v1/chat/completions" \
  -H "X-API-Key: your-openapi-key" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "gpt-4",
    "messages": [
      {
        "role": "user",
        "content": "I am Alice. Can you help me implement a login feature?"
      }
    ]
  }'
```

## 🔄 Workflows

StellarAnvil includes three default workflows:

1. **Simple SDLC**: PO → BA → Dev → QA → Done
2. **Standard SDLC**: PO → BA → Architect → Dev → QA → Done  
3. **Full SDLC**: PO → BA → Architect → UX → Dev → QA → Done

The AI Planner automatically selects the appropriate workflow based on task complexity.

## 👥 Team Member Roles

- **Product Owner (PO)**: Feature prioritization and business decisions
- **Business Analyst (BA)**: Requirements analysis and Jira integration
- **Architect**: Technical design and system architecture
- **UX Designer**: User interface and experience design
- **Developer**: Code implementation and testing
- **Quality Assurance (QA)**: Testing and quality validation
- **Security Reviewer**: Security analysis and compliance

## 🔧 Development

### Database Migrations

```bash
# Add new migration
dotnet ef migrations add MigrationName --project src/StellarAnvil.Infrastructure --startup-project src/StellarAnvil.Api

# Update database
dotnet ef database update --project src/StellarAnvil.Infrastructure --startup-project src/StellarAnvil.Api
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## 📊 Monitoring

Access the monitoring stack:

- **Grafana**: http://localhost:3000 (admin/admin)
- **Prometheus**: http://localhost:9090
- **Jaeger**: http://localhost:16686

## 🐛 Troubleshooting

### Database Connection Issues

1. Ensure PostgreSQL is running: `docker ps`
2. Check connection string in `appsettings.json`
3. Verify database exists: `docker exec -it stellaranvil-postgres psql -U stellaranvil -d stellaranvil`

### API Key Issues

1. Check API key in database: `SELECT * FROM "ApiKeys"`
2. Ensure correct header: `X-API-Key: your-key`
3. Verify key type matches endpoint (Admin vs OpenAPI)

### AI Integration Issues

1. Verify API keys are configured in `appsettings.json`
2. Check network connectivity to AI providers
3. Review logs for detailed error messages

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`
3. Commit your changes: `git commit -m 'Add amazing feature'`
4. Push to the branch: `git push origin feature/amazing-feature`
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel)
- [Microsoft.Extensions.AI](https://github.com/dotnet/extensions)
- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework)
- [Continue.dev](https://continue.dev/) for inspiration on AI development tools
- [OpenAI](https://openai.com/) for API compatibility standards
