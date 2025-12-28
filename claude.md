# StellarAnvil - Project Context

This file provides context for AI assistants working on this codebase.

## Project Overview

StellarAnvil is an AI-powered SDLC orchestration API that provides OpenAI-compatible endpoints for IDE integration (Cursor, VS Code, Continue, etc.). It orchestrates multi-agent workflows for software development tasks.

## Architecture Summary

### Manager-Controlled GroupChat Pattern

The core architecture uses a **Manager-controlled GroupChat** where:
- 6 specialized agents participate (BA, Dev, QA with Jr/Sr pairs)
- A Manager Agent decides speaker order via LLM calls
- Approval gates enforce user approval between phases

```
User Request → AgentOrchestrator → GroupChat → Manager selects agents → SSE Response
```

### Key Design Decisions

1. **Manager-Controlled GroupChat** - Single workflow with LLM-based speaker selection instead of hardcoded phase transitions
2. **Junior/Senior Deliberation** - Each phase has Jr work + Sr review pattern
3. **Approval Gates** - User must explicitly approve before phase transitions (BA→Dev→QA)
4. **Task ID Markers** - `<!-- task:xxx -->` embedded in responses for conversation continuity
5. **Typed Result Classes** - `DeliberationResult` and `WorkflowBuildResult` instead of tuples

## Key Files Reference

| File | Purpose |
|------|---------|
| `src/StellarAnvil.Api/Program.cs` | DI setup, endpoint definitions, composition root |
| `src/StellarAnvil.Api/Application/UseCases/AgentOrchestrator.cs` | Main workflow coordinator, handles request/response |
| `src/StellarAnvil.Api/Infrastructure/AI/ManagerGroupChatManager.cs` | LLM-based speaker selection, decides next agent |
| `src/StellarAnvil.Api/Infrastructure/AI/DeliberationWorkflow.cs` | Builds GroupChat with all 6 agents + Manager |
| `src/StellarAnvil.Api/Infrastructure/AI/AgentFactory.cs` | Creates agents with system prompts and tools |
| `src/StellarAnvil.Api/Infrastructure/AI/AgentRegistry.cs` | Loads and caches system prompts from files |
| `src/StellarAnvil.Api/Domain/Entities/AgentTask.cs` | Task entity with state, messages, tools |
| `SystemPrompts/` | Agent role definitions (manager.txt, developer.txt, etc.) |

## Folder Structure

```
StellarAnvil.Api/
├── Domain/           → Entities, Interfaces (no external dependencies)
├── Application/      → DTOs, Results, UseCases (orchestration logic)
├── Infrastructure/   → AI agents, Persistence, Helpers (external implementations)
└── Program.cs        → Composition root
```

### Layer Dependencies

```
Domain ← Application ← Infrastructure ← Program.cs
```

- **Domain**: Zero external dependencies, pure business entities
- **Application**: References Domain, contains use cases and DTOs
- **Infrastructure**: References Domain + Application, implements interfaces

## Coding Conventions

- **Clean Architecture** with DDD principles
- **Async streaming** via `IAsyncEnumerable<T>`
- **SSE** for real-time responses to clients
- **Record types** for DTOs and result classes
- **Dependency Injection** via built-in .NET DI container

## Current State (MVP)

- In-memory task storage (`InMemoryTaskRepository`)
- No database/persistence yet
- No admin endpoints implemented
- No authentication/authorization

## Agent Workflow

The Manager Agent controls the workflow:

1. **Initial Routing**: Analyzes user intent, picks starting phase (BA/Dev/QA)
2. **Deliberation**: Junior agent works → Senior agent reviews
3. **User Gate**: Returns `AWAIT_USER` to get feedback/approval
4. **Transition**: On approval, routes to next phase or `COMPLETE`

### Manager Decision Format

```json
{
  "nextAgent": "developer" | "sr-developer" | "AWAIT_USER" | "COMPLETE",
  "reasoning": "brief explanation"
}
```

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/v1/chat/completions` | POST | Streaming chat completion (SSE) |
| `/v1/models` | GET | List available models |

**Important**: Only streaming requests are supported (`stream: true` required).

## Task Continuity

Tasks are tracked across requests via embedded markers:

```html
<!-- task:guid-here -->
```

The `TaskIdHelper` extracts this from conversation history to resume existing tasks.

