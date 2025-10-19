# 🔬 Deep Research - ChatGPT Deep Research Clone

A monolithic ASP.NET Core application that replicates ChatGPT's Deep Research functionality with autonomous web search, semantic memory, and multi-agent reasoning.

## 🎯 Features

- **Deep Research Mode**: Autonomous multi-step research with web search
- **Simple Chat Mode**: Traditional conversational AI
- **Web Search Integration**: DuckDuckGo HTML scraping with caching
- **Semantic Memory**: SQLite + vector search for context retention
- **Multi-Agent Architecture**: Planner, Search, Synthesis, and Reflection agents
- **Real-time Streaming**: Server-Sent Events (SSE) for ChatGPT-like token streaming
- **Citation Support**: Automatic source attribution with [1], [2] notation
- **Self-Reflection**: Confidence scoring and iterative improvement

## 🏗️ Architecture

```
┌─────────────────────────────────────────┐
│           Chat Controller               │
│         (SSE Streaming API)             │
└──────────────┬──────────────────────────┘
               │
┌──────────────▼──────────────────────────┐
│      Orchestrator Service               │
│   (Coordinates agent workflow)          │
└──────────┬────────────┬─────────────────┘
           │            │
    ┌──────▼──────┐ ┌──▼──────────┐
    │   Agents    │ │  Services   │
    ├─────────────┤ ├─────────────┤
    │ • Planner   │ │ • LLM       │
    │ • Search    │ │ • Memory    │
    │ • Synthesis │ │ • Search    │
    │ • Reflection│ │             │
    └─────────────┘ └─────────────┘
           │              │
    ┌──────▼──────────────▼─────────┐
    │   Memory Layer                │
    │   • SQLite (metadata)         │
    │   • Vector Search (embeddings)│
    └───────────────────────────────┘
```

## 📁 Project Structure

```
DeepResearch.WebApp/
├── Controllers/
│   └── ChatController.cs          # SSE streaming endpoint
├── Services/
│   ├── OrchestratorService.cs     # Main workflow orchestration
│   ├── LLMService.cs               # Azure OpenAI wrapper
│   ├── MemoryService.cs            # SQLite + vector search
│   └── SearchService.cs            # DuckDuckGo web search
├── Agents/
│   ├── PlannerAgent.cs             # Creates research plans
│   ├── SearchAgent.cs              # Executes web searches
│   ├── SynthesisAgent.cs           # Combines findings
│   └── ReflectionAgent.cs          # Evaluates confidence
├── Memory/
│   ├── DatabaseInitializer.cs      # SQLite schema setup
│   └── SimpleFaissIndex.cs         # In-memory vector search
├── Models/
│   ├── DTOs.cs                     # API request/response models
│   ├── Entities.cs                 # Domain entities
│   └── AgentModels.cs              # Agent-specific models
├── Interfaces/                     # All service interfaces
├── wwwroot/
│   └── index.html                  # Single-page frontend
├── Data/
│   └── deepresearch.db             # SQLite database (created at runtime)
├── appsettings.json
└── Program.cs
```

## 🚀 Getting Started

### Prerequisites

- .NET 9.0 SDK
- Azure OpenAI account with:
  - `gpt-4o` deployment (for reasoning)
  - `gpt-4o-mini` deployment (for reflection)
  - `text-embedding-3-large` deployment (for embeddings)

### Installation

1. **Clone the repository**
   ```bash
   cd DerpResearch
   ```

2. **Configure Azure OpenAI**
   
   Edit `appsettings.json`:
   ```json
   {
     "AzureOpenAI": {
       "Endpoint": "https://YOUR-INSTANCE.openai.azure.com/",
       "ApiKey": "YOUR-API-KEY-HERE",
       "Deployments": {
         "Chat": "gpt-4o",
         "ChatMini": "gpt-4o-mini",
         "Embedding": "text-embedding-3-large"
       }
     }
   }
   ```

3. **Restore dependencies**
   ```bash
   dotnet restore
   ```

4. **Run the application**
   ```bash
   dotnet run
   ```

5. **Open in browser**
   ```
   https://localhost:5001
   ```

## 🧪 Testing with Mock Services

To test the UX without hitting live Azure OpenAI or web services, the application includes comprehensive mock implementations of all external dependencies.

### Why Use Mock Services?

- **Zero Cost**: No Azure OpenAI API charges during development
- **No API Keys Required**: Test without configuring external services
- **Consistent Behavior**: Deterministic responses for reliable testing
- **Fast Iteration**: Instant responses without network latency
- **Offline Development**: Work without internet connectivity
- **Demo Mode**: Perfect for presentations and demos

### Enabling Mock Mode

#### Method 1: Configuration File

Edit `appsettings.json` or `appsettings.Development.json`:

```json
{
  "UseMockServices": true
}
```

#### Method 2: Environment Variable

Set the environment variable before running:

**Windows (CMD):**
```cmd
set UseMockServices=true
dotnet run
```

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

#### Method 3: Command Line

Pass as a command-line argument:

```bash
dotnet run --UseMockServices=true
```

### Mock Service Behavior

When mock mode is enabled, the following services are replaced with simulated versions:

#### MockLLMService
- Streams realistic text responses word-by-word
- Generates contextual answers based on query keywords
- Returns structured JSON for plans, reflections, and clarifications
- Produces deterministic embeddings (same input = same output)
- Simulates realistic API latency (100-500ms)

#### MockSearchService
- Returns 3-5 mock search results per query
- Generates relevant titles and snippets based on search terms
- No actual web requests or external API calls
- Instant response times

#### MockWebContentFetcher
- Generates realistic article content in multiple formats:
  - Technical documentation
  - Blog posts
  - Research papers
  - Tutorials
  - General articles
- Content adapts to the search query topic
- Simulates network delays (100-500ms)
- Returns 2000-5000 characters per URL

#### Mock Agents
All five agents (Clarification, Planner, Search, Synthesis, Reflection) use mock implementations:

- **MockClarificationAgent**: Generates 2-4 relevant questions
- **MockPlannerAgent**: Creates research plans with 2-5 subtasks
- **MockSearchAgent**: Orchestrates mock searches with realistic timing
- **MockSynthesisAgent**: Streams comprehensive answers with citations
- **MockReflectionAgent**: Returns confidence scores (varying for iteration testing)

### Testing the Complete Workflow

1. **Enable mock mode** using any method above

2. **Start the application:**
   ```bash
   dotnet run
   ```

3. **Look for confirmation** in the console output:
   ```
   === MOCK MODE ENABLED - Using simulated services for testing ===
   MockLLMService initialized - responses will be simulated
   MockWebContentFetcher initialized - will return simulated content
   MockSearchService: Returning mock results for query
   ```

4. **Open your browser** to `https://localhost:5001`

5. **Test Deep Research Mode:**
   - Enter: "Compare neural networks and decision trees"
   - Watch the multi-phase workflow:
     - Planning phase with subtasks
     - Search queries being executed
     - Sources being discovered (with realistic delays)
     - Synthesis with citations [1], [2], [3]
     - Reflection with confidence score

6. **Test Simple Chat Mode:**
   - Switch mode in the UI
   - Ask: "Explain transformer architecture"
   - Get instant streamed response

### What Gets Mocked vs. What's Real

**Mocked (Simulated):**
- ✅ Azure OpenAI API calls (LLM completions, embeddings)
- ✅ Web search (DuckDuckGo, Google)
- ✅ Web content fetching (HTTP requests)
- ✅ All agent reasoning (plans, synthesis, reflection)

**Real (Actual Implementation):**
- ✅ SQLite database storage
- ✅ Vector embeddings and similarity search
- ✅ Memory service (conversation history)
- ✅ SSE streaming infrastructure
- ✅ Frontend UI and interactions

### Mock Data Characteristics

**Derpification Level Awareness:**
The mock services respect the `derpificationLevel` parameter:

- **Level 0-30** (Simple): 1-2 subtasks, concise responses, 3 sources
- **Level 31-70** (Balanced): 2-4 subtasks, moderate detail, 5 sources  
- **Level 71-100** (Deep): 4-5 subtasks, comprehensive responses, 10 sources

**Realistic Timing:**
- Search queries: 200-400ms delay
- Content fetching: 100-500ms per URL
- LLM responses: 20-80ms per word
- Reflection: 300-600ms analysis

**Confidence Variation:**
MockReflectionAgent randomly varies confidence scores to test the iteration logic. Sometimes it will return low confidence (< 0.7) triggering additional research rounds.

### Use Cases for Mock Mode

#### Frontend Development
Test UI components and streaming behavior without backend dependencies.

```bash
# Terminal 1: Run backend in mock mode
$env:UseMockServices="true"
dotnet run

# Terminal 2: Make frontend changes and see instant feedback
```

#### UX Testing
Evaluate the user experience of the multi-agent research workflow.

#### Demo Presentations
Run live demos without worrying about API rate limits or costs.

#### CI/CD Integration
Run automated tests without external service dependencies:

```yaml
# .github/workflows/test.yml
- name: Run Integration Tests
  env:
    UseMockServices: true
  run: dotnet test
```

#### Offline Development
Continue development without internet access or when APIs are down.

### Switching Back to Real Services

To disable mock mode and use real Azure OpenAI services:

1. **Remove or set to false** in `appsettings.json`:
   ```json
   {
     "UseMockServices": false
   }
   ```

2. **Unset the environment variable:**
   ```bash
   # PowerShell
   Remove-Item Env:UseMockServices
   
   # Linux/macOS
   unset UseMockServices
   ```

3. **Ensure Azure OpenAI is configured:**
   - Valid endpoint URL
   - Valid API key
   - Correct deployment names

4. **Restart the application**

The console will show:
```
Registering LLM Service...
Registering WebContentFetcher...
Registering SearchService...
```

### Troubleshooting Mock Mode

**Mock mode not activating:**
- Check spelling: `UseMockServices` (case-sensitive)
- Verify configuration source order
- Look for warning in console output

**Unexpected real API calls:**
- Ensure environment variable is set
- Check appsettings.json doesn't override
- Restart application after config changes

**Performance too slow/fast:**
- Mock services use random delays for realism
- Adjust delays in mock service source code if needed
- Real services will have different timing characteristics

## 🎮 Usage

### Deep Research Mode

Ask complex questions that require web research:

```
"Compare Mistral 7B and GPT-4 in terms of architecture and performance"
```

The system will:
1. **Plan**: Break down into research subtasks
2. **Search**: Query DuckDuckGo for each subtask
3. **Store**: Save results with embeddings
4. **Synthesize**: Create comprehensive answer with citations
5. **Reflect**: Evaluate confidence and iterate if needed

### Simple Chat Mode

Use for general conversation without web search:

```
"Explain the transformer architecture"
```

## 🔧 Configuration

### Memory Settings

```json
"Memory": {
  "DatabasePath": "Data/deepresearch.db",
  "TopKResults": 5,              // Number of similar memories to retrieve
  "MaxMemoryAge": 90              // Days before memory compaction
}
```

### Search Settings

```json
"Search": {
  "CacheDuration": 86400,         // Cache search results (seconds)
  "MaxResults": 10                // Max search results per query
}
```

### Reflection Settings

```json
"Reflection": {
  "ConfidenceThreshold": 0.7,     // Min confidence to skip iteration
  "MaxIterations": 2              // Max research iterations
}
```

## 🧠 How It Works

### Deep Research Flow

```
User Query
    ↓
PlannerAgent creates research plan
    ↓
SearchAgent executes web searches
    ↓
Results stored with embeddings
    ↓
MemoryService retrieves relevant context
    ↓
SynthesisAgent creates comprehensive answer
    ↓
ReflectionAgent evaluates confidence
    ↓
[Optional] Additional iteration if confidence low
    ↓
Stream final response to user
```

### Agent Responsibilities

- **PlannerAgent**: Uses GPT-4o to decompose queries into subtasks
- **SearchAgent**: Performs web searches and stores results with embeddings
- **SynthesisAgent**: Combines sources into coherent, cited responses
- **ReflectionAgent**: Evaluates response quality and suggests improvements

### Memory System

- **SQLite**: Stores conversation history, search cache, and metadata
- **Vector Search**: In-memory cosine similarity for semantic retrieval
- **Embeddings**: Azure OpenAI `text-embedding-3-large` (3072 dimensions)

## 📊 API Endpoints

### POST /api/chat
Stream chat responses via Server-Sent Events.

**Request:**
```json
{
  "prompt": "Your question here",
  "mode": "deep-research",
  "conversationId": "optional-uuid"
}
```

**Response:** SSE stream
```
data: {"token":"🔍","conversationId":"abc-123","type":"content"}
data: {"token":" ","conversationId":"abc-123","type":"content"}
...
data: {"token":"","conversationId":"abc-123","type":"done"}
```

### GET /api/chat/history/{conversationId}
Retrieve conversation history.

### POST /api/chat/new
Create a new conversation.

## 🎨 Frontend

Simple, ChatGPT-inspired UI with:
- Real-time token streaming
- Markdown rendering with citations
- Mode switching (Deep Research / Simple Chat)
- Responsive design
- Typing indicators

## 🔐 Security Notes

- **API Keys**: Never commit `appsettings.json` with real credentials
- **CORS**: Currently allows all origins (configure for production)
- **Rate Limiting**: Consider adding rate limiting for production use
- **Input Validation**: Basic validation included, enhance for production

## 🐳 Container Deployment to Azure Web Apps

### Prerequisites

- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) installed
- [Docker](https://docs.docker.com/get-docker/) installed
- Azure subscription with:
  - Azure Container Registry: `websitesregistry.azurecr.io`
  - Azure Web App: `derpresearch` (Linux, Container)
  - Resource Group: `Websites`

```bash
docker build -t derpresearch:latest .
docker tag derpresearch:latest websitesregistry.azurecr.io/derpresearch:latest
az acr login --name websitesregistry
docker push websitesregistry.azurecr.io/derpresearch:latest
az webapp restart --name derpresearch --resource-group Websites
az webapp log tail --name derpresearch --resource-group Websites
```

### Step 1: Build the Docker Image

```bash
# Navigate to project directory
cd DerpResearch

# Build the Docker image
docker build -t derpresearch:latest .
```

The Dockerfile uses a multi-stage build:
- **Stage 1 (Build)**: Uses .NET 9.0 SDK to restore, build, and publish the application
- **Stage 2 (Runtime)**: Uses lightweight .NET 9.0 ASP.NET runtime with the published app

### Step 1.5: Test Locally (Optional but Recommended)

Before deploying to Azure, test the container locally:

```bash
# Run the container locally
docker run -d -p 8080:8080 -e AzureOpenAI__Endpoint="https://YOUR-INSTANCE.openai.azure.com/" -e AzureOpenAI__ApiKey="YOUR-API-KEY-HERE" -e AzureOpenAI__Deployments__Chat="gpt-4o" -e AzureOpenAI__Deployments__ChatMini="gpt-4o-mini" -e AzureOpenAI__Deployments__Embedding="text-embedding-3-large" --name derpresearch-test derpresearch:latest

# Check if container is running
docker ps

# View logs
docker logs derpresearch-test

# Test the application
# Open browser to: http://localhost:8080
```

**Verify the application:**
1. Navigate to `http://localhost:8080` in your browser
2. Test a simple chat query
3. Test a deep research query
4. Check browser console for errors

**Cleanup after testing:**
```bash
# Stop and remove the test container
docker stop derpresearch-test
docker rm derpresearch-test
```

**Alternative: Interactive testing with live logs**
```bash
# Run in foreground to see logs immediately
docker run -p 8080:8080 -e AzureOpenAI__Endpoint="https://YOUR-INSTANCE.openai.azure.com/" -e AzureOpenAI__ApiKey="YOUR-API-KEY-HERE" -e AzureOpenAI__Deployments__Chat="gpt-4o" -e AzureOpenAI__Deployments__ChatMini="gpt-4o-mini" -e AzureOpenAI__Deployments__Embedding="text-embedding-3-large" derpresearch:latest

# Press Ctrl+C to stop
```

### Step 2: Tag Image for Azure Container Registry

```bash
# Tag the image for your ACR
docker tag derpresearch:latest websitesregistry.azurecr.io/derpresearch:latest

# Optional: Add version tag
docker tag derpresearch:latest websitesregistry.azurecr.io/derpresearch:v1.0.0
```

### Step 3: Authenticate with Azure Container Registry

```bash
# Login to Azure
az login

# Login to your container registry
az acr login --name websitesregistry
```

**Alternative**: Use admin credentials:
```bash
# Enable admin user (if not already enabled)
az acr update --name websitesregistry --admin-enabled true

# Get credentials
az acr credential show --name websitesregistry

# Docker login with credentials
docker login websitesregistry.azurecr.io -u websitesregistry -p <password>
```

### Step 4: Push Image to ACR

```bash
# Push the latest tag
docker push websitesregistry.azurecr.io/derpresearch:latest

# Push version tag (if created)
docker push websitesregistry.azurecr.io/derpresearch:v1.0.0
```

### Step 5: Configure Azure Web App

```bash
# Set the container image
az webapp config container set --name derpresearch --resource-group Websites --docker-custom-image-name websitesregistry.azurecr.io/derpresearch:latest --docker-registry-server-url https://websitesregistry.azurecr.io

# Enable continuous deployment (optional - auto-pull on new image push)
az webapp deployment container config --name derpresearch --resource-group Websites --enable-cd true
```

### Step 6: Configure Environment Variables

Set Azure OpenAI credentials and other settings:

```bash
# Set Azure OpenAI configuration
az webapp config appsettings set --name derpresearch --resource-group Websites --settings AzureOpenAI__Endpoint="https://YOUR-INSTANCE.openai.azure.com/" AzureOpenAI__ApiKey="YOUR-API-KEY-HERE" AzureOpenAI__Deployments__Chat="gpt-4o" AzureOpenAI__Deployments__ChatMini="gpt-4o-mini" AzureOpenAI__Deployments__Embedding="text-embedding-3-large" ASPNETCORE_ENVIRONMENT="Production"
```

**Alternative**: Configure via Azure Portal:
1. Navigate to Azure Portal → App Services → `derpresearch`
2. Go to **Configuration** → **Application settings**
3. Add the settings above as key-value pairs
4. Click **Save**

### Step 7: Restart and Verify

```bash
# Restart the web app
az webapp restart --name derpresearch --resource-group Websites

# Get the web app URL
az webapp show --name derpresearch --resource-group Websites --query defaultHostName -o tsv
```

Visit: `https://derpresearch.azurewebsites.net`

### Step 8: Monitor Logs

```bash
# Enable logging
az webapp log config --name derpresearch --resource-group Websites --docker-container-logging filesystem

# Stream logs
az webapp log tail --name derpresearch --resource-group Websites
```

### Complete Deployment Script

Save this as `deploy.sh` or `deploy.ps1`:

```bash
#!/bin/bash
# Deploy script for DerpResearch to Azure Web Apps

# Configuration
ACR_NAME="websitesregistry"
ACR_LOGIN_SERVER="websitesregistry.azurecr.io"
IMAGE_NAME="derpresearch"
VERSION="latest"
RESOURCE_GROUP="Websites"
WEBAPP_NAME="derpresearch"

# Build
echo "Building Docker image..."
docker build -t ${IMAGE_NAME}:${VERSION} .

# Tag
echo "Tagging image for ACR..."
docker tag ${IMAGE_NAME}:${VERSION} ${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${VERSION}

# Login to ACR
echo "Logging in to Azure Container Registry..."
az acr login --name ${ACR_NAME}

# Push
echo "Pushing image to ACR..."
docker push ${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${VERSION}

# Configure Web App
echo "Configuring Azure Web App..."
az webapp config container set --name ${WEBAPP_NAME} --resource-group ${RESOURCE_GROUP} --docker-custom-image-name ${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${VERSION} --docker-registry-server-url https://${ACR_LOGIN_SERVER}

# Restart
echo "Restarting web app..."
az webapp restart --name ${WEBAPP_NAME} --resource-group ${RESOURCE_GROUP}

echo "Deployment complete!"
echo "URL: https://${WEBAPP_NAME}.azurewebsites.net"
```

### Important Notes

#### SQLite Database Persistence

✅ **Database now uses `/home` directory for persistence** - Azure App Service persists the `/home` directory across container restarts.

**What's configured:**
- Database path: `/home/Data/deepresearch.db`
- FAISS index: `/home/Data/faiss.index`
- Survives: Container restarts, app restarts
- Persists: Between deployments (with limitations)

**For production scale, consider upgrading to:**
- **Azure SQL Database**: Full relational database with backups
- **Azure Database for PostgreSQL**: Open-source alternative
- **Azure Blob Storage**: Mount as persistent volume for SQLite file
- **Cosmos DB**: NoSQL alternative with global distribution

**Important:** While `/home` persists across most operations, deployment slot swaps and major infrastructure changes may reset it. For critical production data, use a dedicated database service.

#### Environment-Specific Considerations

1. **Port Configuration**: The container listens on port 8080 (Azure Web Apps standard)
2. **HTTPS**: Automatically handled by Azure Web Apps
3. **Scaling**: Use Azure App Service Plan scaling features
4. **Health Checks**: Azure Web Apps pings `/` by default

#### Troubleshooting

**Container won't start:**
```bash
# Check container logs
az webapp log tail --name derpresearch --resource-group Websites

# Check deployment status
az webapp deployment list --name derpresearch --resource-group Websites
```

**502 Bad Gateway:**
- Verify container is listening on port 8080
- Check environment variables are set correctly
- Review application logs for startup errors

**Memory Issues:**
- Upgrade App Service Plan (currently Basic B1 with 1.75 GB)
- Optimize vector index size in code

**Database Locked Errors:**
- Expected with SQLite in multi-instance scenarios
- Consider upgrading to Azure SQL for production

### CI/CD Integration

For automated deployments, integrate with:

**GitHub Actions:**
```yaml
name: Deploy to Azure Web Apps

on:
  push:
    branches: [ main ]

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v2
    
    - name: Login to ACR
      uses: azure/docker-login@v1
      with:
        login-server: websitesregistry.azurecr.io
        username: ${{ secrets.ACR_USERNAME }}
        password: ${{ secrets.ACR_PASSWORD }}
    
    - name: Build and push
      run: |
        docker build -t websitesregistry.azurecr.io/derpresearch:${{ github.sha }} .
        docker push websitesregistry.azurecr.io/derpresearch:${{ github.sha }}
    
    - name: Deploy to Azure Web App
      uses: azure/webapps-deploy@v2
      with:
        app-name: derpresearch
        images: websitesregistry.azurecr.io/derpresearch:${{ github.sha }}
```

## 🚀 Production Deployment Checklist

- [ ] **Use Azure Key Vault** for secrets (integrate with App Settings)
- [ ] **Enable HTTPS** enforcement (automatic with Azure Web Apps)
- [ ] **Configure CORS** for specific origins (update `Program.cs`)
- [ ] **Add rate limiting** to prevent abuse
- [ ] **Monitor costs** (Azure OpenAI API usage, container registry storage)
- [ ] **Migrate database** (move from SQLite to Azure SQL or PostgreSQL)
- [ ] **Implement logging** (Application Insights integration)
- [ ] **Set up health checks** (add `/health` endpoint)
- [ ] **Configure auto-scaling** (based on CPU/memory metrics)
- [ ] **Enable container registry webhooks** (for automated deployments)
- [ ] **Set up monitoring alerts** (for errors, high latency, costs)
- [ ] **Implement backup strategy** (if using persistent storage)

## 📝 Customization

### Add New Agent

1. Create interface in `Interfaces/`
2. Implement in `Agents/`
3. Register in `Program.cs`
4. Use in `OrchestratorService`

### Change LLM Provider

Modify `LLMService.cs` to support:
- OpenAI API
- Anthropic Claude
- Local models (Ollama)
- Other providers

### Enhance Search

Replace `SearchService.cs` with:
- Brave Search API
- Google Custom Search
- Bing Search API
- Full web scraping with Playwright

## 🐛 Troubleshooting

### Database locked errors
SQLite is single-threaded. Consider connection pooling or switching to PostgreSQL.

### Search results empty
DuckDuckGo HTML structure may change. Check `ParseDuckDuckGoResults()` method.

### Out of memory
Increase vector index capacity or implement disk-based FAISS.

### Slow responses
- Reduce `MaxResults` in search config
- Use `gpt-4o-mini` for faster (less accurate) responses
- Implement caching at multiple layers

## 📄 License

MIT License - see LICENSE file for details

## 🙏 Acknowledgments

- Inspired by OpenAI's ChatGPT Deep Research
- Built with ASP.NET Core 9.0
- Uses Azure OpenAI Service
- Vector search inspired by FAISS

## 🤝 Contributing

Contributions welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Submit a pull request

---

**Built with ❤️ using ASP.NET Core and Azure OpenAI**
