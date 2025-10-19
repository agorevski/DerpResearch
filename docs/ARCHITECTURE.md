# Deep Research Architecture

## Overview

Deep Research is a multi-agent system built on ASP.NET Core 9.0 that orchestrates autonomous research through specialized AI agents, web search, and semantic memory. The architecture follows a monolithic design with clear separation of concerns through service layers and agent specialization.

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Frontend (SPA)                          │
│                   wwwroot/index.html                        │
└────────────────────────┬────────────────────────────────────┘
                         │ SSE (Server-Sent Events)
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                   ChatController                            │
│              (API Endpoints + SSE Streaming)                │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                 OrchestratorService                         │
│           (Workflow Coordination & Agent Management)        │
└──────┬──────────────────┬──────────────────┬───────────────┘
       │                  │                  │
       ▼                  ▼                  ▼
┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│   Agents    │   │  Services   │   │   Memory    │
│             │   │             │   │             │
│• Clarify    │   │• LLM        │   │• SQLite     │
│• Planner    │   │• Search     │   │• Vector     │
│• Search     │   │• WebFetch   │   │  Search     │
│• Synthesis  │   │             │   │             │
│• Reflection │   │             │   │             │
└─────────────┘   └─────────────┘   └─────────────┘
```

## Core Components

### 1. Frontend Layer

**Location:** `wwwroot/index.html`

**Responsibilities:**
- Single-page application (SPA) with real-time streaming
- Server-Sent Events (SSE) client for token streaming
- Markdown rendering with citation support
- Mode switching (Deep Research vs Simple Chat)
- Derpification level control (research depth)

**Key Features:**
- Real-time token streaming for ChatGPT-like experience
- Progress indicators for multi-phase workflows
- Citation rendering [1], [2], [3] with clickable links
- Sticky header with scroll-based compaction
- Responsive design for mobile and desktop

### 2. API Layer

**Location:** `Controllers/ChatController.cs`

**Endpoints:**
- `POST /api/chat` - Stream chat responses via SSE
- `GET /api/chat/history/{conversationId}` - Retrieve conversation history
- `POST /api/chat/new` - Create new conversation

**Key Features:**
- Server-Sent Events (SSE) streaming protocol
- Progress update streaming (planning, searching, synthesis)
- Source discovery streaming
- Token-by-token response streaming
- Conversation management

**SSE Message Types:**
```json
{"token": "text", "conversationId": "uuid", "type": "content"}
{"type": "progress", "message": "Planning research..."}
{"type": "source", "title": "...", "url": "...", "snippet": "..."}
{"type": "search_query", "query": "..."}
{"type": "reflection", "confidence": 0.85, "reasoning": "..."}
{"type": "done"}
```

### 3. Orchestration Layer

**Location:** `Services/OrchestratorService.cs`

**Responsibilities:**
- Coordinates multi-agent workflows
- Manages conversation flow
- Handles mode switching (deep-research vs simple-chat)
- Streams progress updates to frontend
- Error handling and fallback logic

**Deep Research Workflow:**

```
1. User Query
   ↓
2. ClarificationAgent (if needed)
   ↓
3. PlannerAgent → Research Plan
   ↓
4. SearchAgent → Web Search + Content Fetching
   ↓
5. MemoryService → Store with Embeddings
   ↓
6. MemoryService → Retrieve Relevant Context
   ↓
7. SynthesisAgent → Comprehensive Answer
   ↓
8. ReflectionAgent → Evaluate Quality
   ↓
9. [If confidence < 0.7] → Additional Search Iteration
   ↓
10. Stream Final Response
```

**Simple Chat Workflow:**

```
User Query → MemoryService (context) → LLMService → Stream Response
```

### 4. Agent Layer

**Location:** `Agents/`

#### ClarificationAgent
**Purpose:** Generate clarifying questions when user intent is ambiguous

**Input:** User query
**Output:** 2-4 structured questions

**When Used:**
- Ambiguous queries
- Multi-faceted topics requiring focus
- User explicitly requests clarification

#### PlannerAgent
**Purpose:** Decompose complex queries into research subtasks

**Input:** User query, derpification level
**Output:** Research plan with 1-5 subtasks

**Strategy:**
- Simple (0-30): 1-2 focused subtasks
- Balanced (31-70): 2-4 subtasks with moderate depth
- Deep (71-100): 4-5 comprehensive subtasks

**Example Plan:**
```json
{
  "subtasks": [
    "Compare neural network architectures",
    "Analyze decision tree algorithms",
    "Performance benchmarks comparison",
    "Use case recommendations"
  ]
}
```

#### SearchAgent
**Purpose:** Execute web searches and fetch content

**Process:**
1. Execute search query via DuckDuckGo
2. Parse HTML results (title, URL, snippet)
3. Fetch full webpage content
4. Chunk content (3000 tokens, 100 overlap)
5. Generate embeddings
6. Store in database

**Caching:** 24-hour cache for search results

#### SynthesisAgent
**Purpose:** Combine sources into coherent, cited response

**Input:** User query, retrieved sources, conversation context
**Output:** Comprehensive answer with [1], [2] citations

**Key Features:**
- Citation tracking and insertion
- Source attribution
- Token streaming for real-time feedback
- Markdown formatting

#### ReflectionAgent
**Purpose:** Evaluate response quality and suggest improvements

**Input:** User query, synthesized response, sources
**Output:** Confidence score (0-1) and reasoning

**Criteria:**
- Coverage of query aspects
- Source quality and relevance
- Answer completeness
- Factual accuracy (based on sources)

**Confidence Threshold:** 0.7
- Below threshold → Additional research iteration
- Above threshold → Accept response

### 5. Service Layer

**Location:** `Services/`

#### LLMService
**Interface:** `ILLMService`
**Implementation:** `LLMService.cs`

**Responsibilities:**
- Azure OpenAI API integration
- Token streaming with `IAsyncEnumerable<string>`
- Structured output parsing (`GetStructuredOutput<T>`)
- Embedding generation (text-embedding-3-large)

**Models:**
- **gpt-4o**: Primary reasoning model (agents, synthesis)
- **gpt-4o-mini**: Lightweight model (reflection, cost optimization)
- **text-embedding-3-large**: 3072-dimension embeddings

#### MemoryService
**Interface:** `IMemoryService`
**Implementation:** `MemoryService.cs`

**Responsibilities:**
- Conversation history persistence
- Vector search for semantic retrieval
- Search result caching
- Memory compaction (90-day default)

**Database Schema:**
- **Conversations**: Metadata, creation timestamps
- **Messages**: User/assistant messages, embeddings
- **SearchCache**: Cached search results (24-hour TTL)

**Vector Search:**
- In-memory FAISS-inspired similarity search
- Cosine similarity for semantic matching
- Top-K retrieval (default: 5)

#### SearchService
**Interface:** `ISearchService`
**Implementation:** `SearchService.cs`

**Responsibilities:**
- DuckDuckGo HTML scraping
- Search result parsing
- Cache management

**HTML Parsing:**
```csharp
// Extracts: title, URL, snippet
ParseDuckDuckGoResults(string html)
```

**Fallback:** Returns empty results on parse failure

#### WebContentFetcher
**Interface:** `IWebContentFetcher`
**Implementation:** `WebContentFetcher.cs`

**Responsibilities:**
- Fetch full webpage content
- Text extraction from HTML
- Timeout handling (5 seconds)
- Error handling with fallback

**Process:**
1. HTTP GET request
2. Parse HTML
3. Extract text content
4. Remove script/style tags
5. Clean whitespace

### 6. Memory Layer

**Location:** `Memory/`

#### DatabaseInitializer
**Purpose:** SQLite schema management and migrations

**Tables:**
- `Conversations`: (Id, CreatedAt, UpdatedAt, Title)
- `Messages`: (Id, ConversationId, Role, Content, Embedding, Timestamp, Tags)
- `SearchCache`: (Query, Results, Timestamp)

**Indexes:**
- ConversationId (Messages)
- Timestamp (Messages, SearchCache)
- Tags (Messages)

#### SimpleFaissIndex
**Purpose:** In-memory vector similarity search

**Algorithm:**
- Cosine similarity: `dot(v1, v2) / (norm(v1) * norm(v2))`
- Top-K selection via sorted list

**Operations:**
- `Add(vector, metadata)`: Store embedding
- `Search(queryVector, k)`: Find k most similar
- `Clear()`: Reset index

**Limitations:**
- In-memory only (resets on restart)
- Single-threaded access
- No disk persistence

### 7. Data Models

**Location:** `Models/`

#### DTOs (Data Transfer Objects)
**File:** `DTOs.cs`

```csharp
ChatRequest: User input, mode, conversationId, derpificationLevel
ChatResponse: SSE streaming messages
HistoryResponse: Conversation history
```

#### Entities
**File:** `Entities.cs`

```csharp
Conversation: Id, CreatedAt, UpdatedAt, Title
Message: Id, ConversationId, Role, Content, Embedding, Timestamp, Tags
SearchResult: Title, Url, Snippet, Query, Timestamp
```

#### Agent Models
**File:** `AgentModels.cs`

```csharp
ResearchPlan: Subtasks list
ClarificationQuestions: Question list
ReflectionResult: Confidence, reasoning, suggestions
SearchQuery: Query string, max results
```

## Design Patterns

### 1. **Orchestrator Pattern**
`OrchestratorService` coordinates agent workflows without agents knowing about each other.

### 2. **Strategy Pattern**
Different agent implementations for different tasks (planning, search, synthesis).

### 3. **Repository Pattern**
`MemoryService` abstracts data access from business logic.

### 4. **Dependency Injection**
All services and agents injected via ASP.NET Core DI container.

### 5. **Streaming Pattern**
`IAsyncEnumerable<T>` for real-time token streaming.

### 6. **Service Locator (Avoided)**
Direct dependency injection instead of static service locator.

## Data Flow

### Deep Research Request

```
1. Frontend → POST /api/chat
   {prompt: "Compare X and Y", mode: "deep-research", derpificationLevel: 70}

2. ChatController → OrchestratorService.ProcessDeepResearchAsync()

3. OrchestratorService:
   a. Load conversation context from MemoryService
   b. Call PlannerAgent → Get research plan
   c. Stream progress: "Planning research..."
   
4. For each subtask in plan:
   a. Call SearchAgent → Execute search
   b. Stream search_query: "Searching for: X vs Y architectures"
   c. Parse results → Fetch content → Generate embeddings
   d. Store in MemoryService
   e. Stream source: {title, url, snippet}

5. MemoryService:
   a. Retrieve top 5 relevant sources via vector search
   b. Return aggregated context

6. SynthesisAgent:
   a. Combine sources + context
   b. Stream tokens in real-time
   c. Insert citations [1], [2], [3]

7. ReflectionAgent:
   a. Evaluate response quality
   b. Stream reflection: {confidence: 0.85, reasoning: "..."}
   c. If confidence < 0.7 → Additional iteration

8. OrchestratorService:
   a. Save conversation to MemoryService
   b. Stream type: "done"
```

## Configuration

**Location:** `appsettings.json`

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-instance.openai.azure.com/",
    "ApiKey": "your-key",
    "Deployments": {
      "Chat": "gpt-4o",
      "ChatMini": "gpt-4o-mini",
      "Embedding": "text-embedding-3-large"
    }
  },
  "Memory": {
    "DatabasePath": "Data/deepresearch.db",
    "TopKResults": 5,
    "MaxMemoryAge": 90
  },
  "Search": {
    "CacheDuration": 86400,
    "MaxResults": 10
  },
  "Reflection": {
    "ConfidenceThreshold": 0.7,
    "MaxIterations": 2
  }
}
```

## Scalability Considerations

### Current Limitations

1. **SQLite**: Single-threaded, not suitable for high concurrency
2. **In-Memory Vector Search**: Lost on restart, memory-bound
3. **No Distributed Caching**: Each instance has separate cache
4. **Synchronous Agent Execution**: Sequential subtask processing

### Scaling Strategy

**Horizontal Scaling:**
- Migrate to Azure SQL / PostgreSQL
- Redis for distributed caching
- Azure Blob Storage for vector index
- Message queue for async agent execution

**Vertical Scaling:**
- Increase App Service Plan tier
- Optimize vector index size
- Reduce embedding dimensions
- Implement connection pooling

## Security

### Current Implementation

- API keys in configuration (not hardcoded)
- CORS enabled for all origins (development)
- Basic input validation
- No rate limiting

### Production Recommendations

1. **Secrets Management**: Azure Key Vault integration
2. **Authentication**: Azure AD B2C or custom auth
3. **Rate Limiting**: Per-user or per-IP limits
4. **CORS**: Restrict to specific origins
5. **Input Validation**: Comprehensive sanitization
6. **Audit Logging**: Track all API calls

## Monitoring & Observability

### Logging

- Structured logging via `ILogger<T>`
- Log levels: Information, Warning, Error, Critical
- Conversation ID tracking throughout request lifecycle

### Recommended Additions

1. **Application Insights**: Azure monitoring
2. **Health Checks**: `/health` endpoint
3. **Metrics**: Request duration, token counts, API costs
4. **Alerting**: Error rates, high latency, cost thresholds

## Testing Strategy

### Unit Tests
- Agent logic with mocked LLM service
- Memory service with in-memory database
- Search result parsing

### Integration Tests
- Full workflow with mock services
- SSE streaming validation
- Database operations

### Mock Services
- `MockLLMService`: Simulated AI responses
- `MockSearchService`: Fake search results
- `MockWebContentFetcher`: Generated content

See [MOCK_SERVICES.md](MOCK_SERVICES.md) for details.

## Future Enhancements

1. **Multi-Modal Support**: Image analysis, document parsing
2. **Advanced Search**: Multiple search providers, federated search
3. **Collaborative Research**: Multi-user conversations
4. **Export Capabilities**: PDF reports, structured data export
5. **Plugin System**: Extensible agent architecture
6. **Graph RAG**: Knowledge graph for enhanced context
7. **Streaming Embeddings**: Real-time vector updates
8. **Distributed Agents**: Microservices architecture

## References

- [ASP.NET Core Documentation](https://docs.microsoft.com/aspnet/core)
- [Azure OpenAI Service](https://azure.microsoft.com/services/cognitive-services/openai-service/)
- [Server-Sent Events (SSE)](https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events)
- [FAISS Vector Search](https://github.com/facebookresearch/faiss)
