# Mock Services Documentation

## Overview

The application includes comprehensive mock implementations of all external dependencies for testing and development without requiring Azure OpenAI API access or live web services.

## Benefits

- **Zero Cost**: No Azure OpenAI API charges during development
- **No API Keys Required**: Test without configuring external services
- **Consistent Behavior**: Deterministic responses for reliable testing
- **Fast Iteration**: Instant responses without network latency
- **Offline Development**: Work without internet connectivity
- **Demo Mode**: Perfect for presentations and demos

## Enabling Mock Mode

### Method 1: Configuration File

Edit `appsettings.json` or `appsettings.Development.json`:

```json
{
  "UseMockServices": true
}
```

### Method 2: Environment Variable

**Windows (PowerShell):**

```powershell
$env:UseMockServices="true"
dotnet run
```

**Linux/macOS:**

```bash
export UseMockServices=true
dotnet run
```

### Method 3: Command Line

```bash
dotnet run --UseMockServices=true
```

## Mock Service Implementations

### MockLLMService

- Streams realistic text responses word-by-word
- Generates contextual answers based on query keywords
- Returns structured JSON for plans, reflections, and clarifications
- Produces deterministic embeddings (same input = same output)
- Simulates realistic API latency (100-500ms)

### MockSearchService

- Returns 3-5 mock search results per query
- Generates relevant titles and snippets based on search terms
- No actual web requests or external API calls
- Instant response times

### MockWebContentFetcher

- Generates realistic article content in multiple formats
- Content adapts to the search query topic
- Simulates network delays (100-500ms)
- Returns 2000-5000 characters per URL

### Mock Agents

All five agents use mock implementations:

- **MockClarificationAgent**: Generates 2-4 relevant questions
- **MockPlannerAgent**: Creates research plans with 2-5 subtasks
- **MockSearchAgent**: Orchestrates mock searches with realistic timing
- **MockSynthesisAgent**: Streams comprehensive answers with citations
- **MockReflectionAgent**: Returns confidence scores (varying for iteration testing)

## Testing Workflow

1. **Enable mock mode** using any method above

2. **Start the application:**

   ```bash
   dotnet run
   ```

3. **Look for confirmation** in console:

   ```text
   === MOCK MODE ENABLED - Using simulated services for testing ===
   ```

4. **Test Deep Research Mode:**
   - Enter: "Compare neural networks and decision trees"
   - Watch multi-phase workflow with realistic delays

5. **Test Simple Chat Mode:**
   - Switch mode in UI
   - Ask: "Explain transformer architecture"

## Mock vs. Real Services

**Mocked (Simulated):**

- ✅ Azure OpenAI API calls (LLM completions, embeddings)
- ✅ Web search (DuckDuckGo)
- ✅ Web content fetching (HTTP requests)
- ✅ All agent reasoning

**Real (Actual Implementation):**

- ✅ SQLite database storage
- ✅ Vector embeddings and similarity search
- ✅ Memory service (conversation history)
- ✅ SSE streaming infrastructure
- ✅ Frontend UI

## Derpification Level Support

Mock services respect the `derpificationLevel` parameter:

- **Level 0-30** (Simple): 1-2 subtasks, 3 sources
- **Level 31-70** (Balanced): 2-4 subtasks, 5 sources  
- **Level 71-100** (Deep): 4-5 subtasks, 10 sources

## Realistic Timing

- Search queries: 200-400ms delay
- Content fetching: 100-500ms per URL
- LLM responses: 20-80ms per word
- Reflection: 300-600ms analysis

## Use Cases

### Frontend Development

```bash
$env:UseMockServices="true"
dotnet run
# Make UI changes with instant feedback
```

### CI/CD Integration

```yaml
- name: Run Integration Tests
  env:
    UseMockServices: true
  run: dotnet test
```

### Offline Development

Work without internet access or when APIs are down.

## Switching Back to Real Services

1. **Remove or set to false** in `appsettings.json`:

   ```json
   {
     "UseMockServices": false
   }
   ```

2. **Unset environment variable:**

   ```powershell
   Remove-Item Env:UseMockServices
   ```

3. **Ensure Azure OpenAI is configured**

4. **Restart application**

## Troubleshooting

**Mock mode not activating:**

- Check spelling: `UseMockServices` (case-sensitive)
- Verify configuration source order
- Look for warning in console output

**Unexpected real API calls:**

- Ensure environment variable is set
- Check appsettings.json doesn't override
- Restart application after config changes
