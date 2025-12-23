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
| 1 | **God Object Pattern** | 🔴 Critical | `OrchestratorService.cs` - 500+ lines handling too many responsibilities |
| 6 | **Primitive Obsession** | 🟡 High | Throughout - Using strings for domain concepts (IDs, roles) |
| 12 | **Missing Correlation IDs** | 🟡 High | Logging - No distributed tracing support |
| 13 | **Missing Integration Tests** | 🟢 Medium | Tests - Only unit tests, no E2E workflow tests |

---

## Priority Recommendations

### Immediate (Critical 🔴)

1. **Break down OrchestratorService** - Split into `WorkflowCoordinator`, `ProgressStreamingService`, `ClarificationManager`, `IterativeResearchManager`

### High Priority (🟡)

1. Implement correlation IDs for observability
2. Add value objects for domain concepts (ConversationId, ConfidenceScore)

### Medium Priority (🟢)

1. Add integration tests for end-to-end workflows
2. Improve logging consistency

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
    // ... plus 500 lines of complex logic
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

---

## Documentation Links

- **Full Anti-Patterns Analysis**: [docs/ANTI-PATTERNS.md](docs/ANTI-PATTERNS.md)
- **Critical Fixes Summary**: [docs/CRITICAL-FIXES-SUMMARY.md](docs/CRITICAL-FIXES-SUMMARY.md)
- **Architecture Guide**: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
