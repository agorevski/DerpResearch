# Anti-Patterns in DerpResearch

This document provides a summary of development anti-patterns identified in the DerpResearch codebase. For the complete detailed analysis, see [docs/ANTI-PATTERNS.md](docs/ANTI-PATTERNS.md).

## Severity Legend

- 🔴 **Critical**: Major architectural issues requiring immediate attention
- 🟡 **High**: Significant problems affecting maintainability or performance  
- 🟢 **Medium**: Issues that should be addressed but aren't blocking
- ✅ **Fixed**: Previously identified issues that have been resolved

---

## Summary of Issues

### Active Issues

| # | Anti-Pattern | Severity | Location |
|---|-------------|----------|----------|
| 1 | **God Object Pattern** | 🔴 Critical | `OrchestratorService.cs` - 600+ lines handling too many responsibilities |
| 2 | **Tight Coupling to Azure OpenAI** | 🟡 High | `LLMService.cs` - Direct SDK dependency, no provider abstraction |
| 3 | **No Cancellation Token Support** | 🟡 High | Multiple async methods - Long-running operations can't be cancelled |
| 4 | **Magic Strings** | 🟡 High | Multiple files - Configuration keys as string literals |
| 5 | **Magic Numbers** | 🟢 Medium | Multiple files - Hardcoded values without explanation |
| 6 | **Primitive Obsession** | 🟡 High | Throughout - Using strings for domain concepts (IDs, roles) |
| 7 | **Manual Connection Management** | 🟡 High | `MemoryService.cs` - Verbose, repetitive database code |
| 8 | **No Transaction Support** | 🟡 High | `MemoryService.cs` - Multi-step operations without transactions |
| 9 | **Exception Swallowing** | 🟡 High | `MemoryService.StoreMemoryAsync()` - Silent failures in chunking loop |
| 10 | **No LLM Circuit Breaker** | 🟡 High | `LLMService.cs` - No resilience for Azure OpenAI calls |
| 11 | **Anemic Domain Models** | 🟡 High | `Models/` - Data containers with no behavior |
| 12 | **Missing Correlation IDs** | 🟡 High | Logging - No distributed tracing support |
| 13 | **Missing Integration Tests** | 🟢 Medium | Tests - Only unit tests, no E2E workflow tests |

### Fixed Issues (2025-11-15 to 2025-11-16)

| # | Anti-Pattern | Solution |
|---|-------------|----------|
| ✅ | Fire-and-Forget Database Init | `DatabaseInitializationService` with proper lifecycle |
| ✅ | Inconsistent ConfigureAwait | Removed all `.ConfigureAwait(false)` calls |
| ✅ | Sync in Async Code | `SimpleFaissIndex.SearchAsync()` with `Task.Run` |
| ✅ | In-Memory State Loss | `PersistentFaissIndex` with SQLite persistence |
| ✅ | No Circuit Breaker for Search | `ResilientSearchService` with circuit breaker |
| ✅ | No Rate Limiting | Integrated into `ResilientSearchService` |

---

## Priority Recommendations

### Immediate (Critical 🔴)

1. **Break down OrchestratorService** - Split into `WorkflowCoordinator`, `ProgressStreamingService`, `ClarificationManager`, `IterativeResearchManager`

### High Priority (🟡)

1. Add `CancellationToken` support to all async operations
2. Implement circuit breaker for LLM calls (similar to `ResilientSearchService`)
3. Fix exception swallowing - return `StoreMemoryResult` with success/failure info
4. Add proper transaction support for database operations
5. Create strongly-typed configuration classes
6. Extract Azure OpenAI coupling into provider abstraction
7. Implement correlation IDs for observability

### Medium Priority (🟢)

1. Add timeouts for LLM operations
2. Convert `StreamToken.Type` string to enum
3. Add integration tests for end-to-end workflows
4. Improve logging consistency
5. Add value objects for domain concepts

---

## Quick Reference Examples

### God Object - OrchestratorService (Before)

```csharp
public class OrchestratorService : IOrchestratorService
{
    // 10+ dependencies - too many responsibilities
    private readonly IClarificationAgent _clarificationAgent;
    private readonly IPlannerAgent _plannerAgent;
    private readonly ISearchAgent _searchAgent;
    // ... plus 600 lines of complex logic
}
```

### Recommended Refactoring (After)

```csharp
// Split into focused services
public interface IWorkflowCoordinator { }
public interface IProgressStreamingService { }
public interface IClarificationManager { }
public interface IIterativeResearchManager { }

public class OrchestratorService : IOrchestratorService
{
    // Thin coordinator, delegates to specialized services
    private readonly IWorkflowCoordinator _workflow;
    private readonly IProgressStreamingService _streaming;
}
```

### Magic Strings (Before)

```csharp
var maxIterations = int.Parse(_config["Reflection:MaxIterations"] ?? "2");
```

### Strongly-Typed Configuration (After)

```csharp
public class ReflectionConfiguration
{
    public const string Section = "Reflection";
    public int MaxIterations { get; set; } = 2;
    public double ConfidenceThreshold { get; set; } = 0.7;
}

// Usage with IOptions<T>
public class ReflectionAgent
{
    public ReflectionAgent(IOptions<ReflectionConfiguration> config)
    {
        _maxIterations = config.Value.MaxIterations; // Type-safe!
    }
}
```

---

## Documentation Links

- **Full Anti-Patterns Analysis**: [docs/ANTI-PATTERNS.md](docs/ANTI-PATTERNS.md)
- **Critical Fixes Summary**: [docs/CRITICAL-FIXES-SUMMARY.md](docs/CRITICAL-FIXES-SUMMARY.md)
- **Architecture Guide**: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)

---

*Last updated: 2025-12-19*
