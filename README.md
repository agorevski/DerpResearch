# 🔬 Deep Research - ChatGPT Deep Research Clone

A multi-agent ASP.NET Core application that replicates ChatGPT's Deep Research with autonomous web search, semantic memory, and iterative reasoning.

## 🎯 Features

- **Deep Research Mode**: Multi-step research with web search and synthesis
- **Simple Chat Mode**: Traditional conversational AI
- **Web Search Integration**: DuckDuckGo with intelligent caching
- **Semantic Memory**: SQLite + vector search for context retention
- **Multi-Agent Architecture**: Specialized agents for planning, search, synthesis, and reflection
- **Real-time Streaming**: Server-Sent Events (SSE) for live responses
- **Citation Support**: Automatic source attribution [1], [2], [3]
- **Self-Reflection**: Quality evaluation and iterative improvement

## 🚀 Quick Start

### Prerequisites

- .NET 9.0 SDK
- Azure OpenAI with deployments: `gpt-4o`, `gpt-4o-mini`, `text-embedding-3-large`

### Installation

**Configure Azure OpenAI** - Edit `appsettings.json`:

  ```json
  {
    "AzureOpenAI": {
      "Endpoint": "https://YOUR-INSTANCE.openai.azure.com/",
      "ApiKey": "YOUR-API-KEY",
      "Deployments": {
        "Chat": "gpt-4o",
        "ChatMini": "gpt-4o-mini",
        "Embedding": "text-embedding-3-large"
      }
    }
  }
  ```
  
**Run the application**:

  ```bash
  dotnet restore
  dotnet run
  ```

**Open browser**: `https://localhost:5001`

### Testing Without API Keys

Enable mock mode for testing without Azure OpenAI:

```bash
# PowerShell
$env:UseMockServices="true"
dotnet run
```

See [docs/MOCK_SERVICES.md](docs/MOCK_SERVICES.md) for details.

## 🏗️ Architecture

```text
User Query → Planner Agent → Search Agent → Memory Storage
                                              ↓
           Synthesis Agent ← Semantic Retrieval
                   ↓
           Reflection Agent → [Iterate if needed]
                   ↓
           Streamed Response
```

**Key Components:**

- **OrchestratorService**: Coordinates multi-agent workflow
- **Agents**: Planner, Search, Synthesis, Reflection, Clarification
- **Memory**: SQLite + in-memory vector search (FAISS-inspired)
- **Services**: LLM, Search (DuckDuckGo), WebContentFetcher

## 📁 Project Structure

```folder
DeepResearch.WebApp/
├── Controllers/          # SSE streaming API
├── Services/            # Core business logic
├── Agents/              # Specialized AI agents
├── Memory/              # Database & vector search
├── Models/              # DTOs and entities
├── Interfaces/          # Service contracts
├── wwwroot/             # Frontend SPA
└── docs/                # Documentation
```

## 🎮 Usage

### Deep Research

```text
"Compare neural networks and decision trees in terms of performance"
```

**Workflow:**

1. Creates research plan with subtasks
2. Executes web searches per subtask
3. Fetches and stores full webpage content
4. Synthesizes comprehensive answer with citations
5. Evaluates confidence and iterates if needed

### Simple Chat

```text
"Explain the transformer architecture"
```

Direct conversation without web research.

## 🔧 Configuration

**Memory Settings:**

```json
{
  "Memory": {
    "DatabasePath": "Data/deepresearch.db",
    "TopKResults": 5
  }
}
```

**Search Settings:**

```json
{
  "Search": {
    "CacheDuration": 86400,
    "MaxResults": 10
  }
}
```

**Reflection Settings:**

```json
{
  "Reflection": {
    "ConfidenceThreshold": 0.7,
    "MaxIterations": 2
  }
}
```

## 📊 API Reference

### POST /api/chat

Stream chat responses (SSE).

**Request:**

```json
{
  "prompt": "Your question",
  "mode": "deep-research",
  "conversationId": "uuid"
}
```

**Response:** SSE stream with progress updates, sources, and tokens.

### GET /api/chat/history/{conversationId}

Retrieve conversation history.

### POST /api/chat/new

Create new conversation.

## 🐳 Deployment

Quick deployment to Azure:

```bash
docker build -t derpresearch:latest .
az acr login --name websitesregistry
docker push websitesregistry.azurecr.io/derpresearch:latest
az webapp restart --name derpresearch --resource-group Websites
```

See [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) for complete guide.

## 🧪 Development

### Running Tests

```bash
dotnet test
```

### Building

```bash
dotnet build
```

### Mock Mode for Development

```bash
$env:UseMockServices="true"
dotnet run
```

## 📝 Extending

### Add New Agent

1. Create interface in `Interfaces/IAgents.cs`
2. Implement in `Agents/YourAgent.cs`
3. Register in `Program.cs`
4. Use in `OrchestratorService`

### Change LLM Provider

Modify `Services/LLMService.cs` to support OpenAI, Anthropic, Ollama, etc.

### Enhance Search

Replace `SearchService.cs` with Brave, Google Custom Search, or Bing API.

## 🐛 Common Issues

**Database locked errors**: SQLite is single-threaded. Consider PostgreSQL for production.

**Empty search results**: DuckDuckGo HTML may change. Check parser in `SearchService.cs`.

**Out of memory**: Reduce vector index size or implement disk-based storage.

**Slow responses**: Reduce `MaxResults`, use `gpt-4o-mini`, or add caching.

## 🔐 Security

- Never commit `appsettings.json` with real credentials
- Use Azure Key Vault for production secrets
- Configure CORS for specific origins
- Add rate limiting for production

## 📚 Documentation

- [Architecture Guide](docs/ARCHITECTURE.md) - Detailed system architecture
- [Mock Services Guide](docs/MOCK_SERVICES.md) - Testing without API keys
- [Deployment Guide](docs/DEPLOYMENT.md) - Azure container deployment

## 📄 License

MIT License

## 🙏 Acknowledgments

Inspired by OpenAI's ChatGPT Deep Research. Built with ASP.NET Core 9.0 and Azure OpenAI.

---

### Built with ❤️ using .NET and Azure OpenAI
