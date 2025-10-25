# DerpResearch Test Suite

This directory contains comprehensive unit tests, integration tests, and UI/UX tests for the DerpResearch application.

## 📋 Table of Contents

- [Overview](#overview)
- [Test Structure](#test-structure)
- [Prerequisites](#prerequisites)
- [Running Tests](#running-tests)
- [Test Coverage](#test-coverage)
- [Writing New Tests](#writing-new-tests)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)

## 🎯 Overview

The test suite provides comprehensive coverage across three main categories:

1. **Unit Tests** - Test individual components in isolation with mocked dependencies
2. **Integration Tests** - Test component interactions and full workflows
3. **UI Tests** - Test user interface and user experience with Playwright

### Test Frameworks & Tools

- **xUnit** - Primary testing framework for .NET
- **Moq** - Mocking framework for dependencies
- **FluentAssertions** - Readable assertion library
- **Playwright** - End-to-end UI testing
- **Coverlet** - Code coverage reporting
- **Microsoft.AspNetCore.Mvc.Testing** - Integration testing

## 📁 Test Structure

```
DerpResearch.Tests/
├── Unit/                          # Unit tests for isolated components
│   ├── Controllers/               # Controller tests
│   │   └── ChatControllerTests.cs
│   ├── Agents/                    # Agent tests
│   │   ├── ClarificationAgentTests.cs
│   │   ├── PlannerAgentTests.cs
│   │   ├── SearchAgentTests.cs
│   │   ├── SynthesisAgentTests.cs
│   │   └── ReflectionAgentTests.cs
│   ├── Services/                  # Service tests
│   │   ├── LLMServiceTests.cs
│   │   ├── MemoryServiceTests.cs
│   │   ├── OrchestratorServiceTests.cs
│   │   ├── SearchServiceTests.cs
│   │   ├── WebContentFetcherTests.cs
│   │   └── TextChunkerTests.cs
│   └── Memory/                    # Memory component tests
│       └── SimpleFaissIndexTests.cs
├── Integration/                   # Integration tests
│   ├── FullWorkflowTests.cs
│   ├── HealthCheckTests.cs
│   └── MockModeTests.cs
├── UI/                           # UI/UX tests with Playwright
│   ├── UserInteractionTests.cs
│   ├── StreamingTests.cs
│   ├── ClarificationFlowTests.cs
│   ├── ResponsiveDesignTests.cs
│   ├── AccessibilityTests.cs
│   └── PerformanceTests.cs
└── Helpers/                       # Test utilities and helpers
    ├── TestDataBuilder.cs         # Create test data objects
    ├── MockFactory.cs             # Create mock objects
    └── PlaywrightFixture.cs       # Playwright test fixture
```

## 🔧 Prerequisites

### 1. .NET SDK

Ensure .NET 9.0 SDK is installed:

```bash
dotnet --version
# Should show 9.0.x or higher
```

### 2. Playwright Browsers

For UI tests, install Playwright browsers:

```bash
# From the DerpResearch.Tests directory
pwsh bin/Debug/net9.0/playwright.ps1 install
```

Or on Linux/Mac:

```bash
playwright install
```

### 3. Running Application (for UI tests)

UI tests require the application to be running. Start it with:

```bash
# From the project root
dotnet run --project DeepResearch.WebApp.csproj
```

The application should be accessible at `http://localhost:5000`

## 🚀 Running Tests

### Run All Tests

```bash
# From the solution root
dotnet test

# With detailed output
dotnet test --logger "console;verbosity=detailed"
```

### Run Specific Test Categories

```bash
# Run only unit tests
dotnet test --filter "FullyQualifiedName~Unit"

# Run only UI tests
dotnet test --filter "FullyQualifiedName~UI"

# Run only integration tests
dotnet test --filter "FullyQualifiedName~Integration"
```

### Run Specific Test Classes

```bash
# Run ChatController tests
dotnet test --filter "FullyQualifiedName~ChatControllerTests"

# Run TextChunker tests
dotnet test --filter "FullyQualifiedName~TextChunkerTests"
```

### Run Specific Test Method

```bash
dotnet test --filter "FullyQualifiedName~ChatControllerTests.Chat_ShouldSetCorrectResponseHeaders"
```

### Run with Code Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Coverage reports will be generated in `TestResults/` directory.

### Generate HTML Coverage Report

```bash
# Install reportgenerator tool (once)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML report
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"CoverageReport" -reporttypes:Html

# Open report
start CoverageReport/index.html  # Windows
open CoverageReport/index.html   # Mac
xdg-open CoverageReport/index.html # Linux
```

## 📊 Test Coverage

### Coverage Goals

- **Unit Tests**: 80%+ coverage for all services and agents
- **Integration Tests**: All critical user workflows
- **UI Tests**: All user-facing features and responsive behaviors

### Current Coverage Areas

#### Backend Unit Tests
- ✅ ChatController - All endpoints and error handling
- ✅ TextChunker - Text splitting and overlap logic
- ✅ All Agents - Question generation, planning, search, synthesis, reflection
- ✅ LLM Service - Response generation and streaming
- ✅ Memory Service - Conversation management and vector search
- ✅ Search Service - Query execution and caching
- ✅ Web Content Fetcher - HTML parsing and content extraction

#### UI Tests
- ✅ User interactions (typing, clicking, keyboard shortcuts)
- ✅ Derpification slider and brain visualization
- ✅ Message sending and display
- ✅ Responsive design (mobile, tablet, desktop)
- ✅ Sticky header behavior
- ✅ Touch targets and accessibility
- ✅ Fresh search functionality

## ✍️ Writing New Tests

### Unit Test Template

```csharp
using DerpResearch.Tests.Helpers;
using FluentAssertions;
using Moq;

namespace DerpResearch.Tests.Unit.Services;

public class MyServiceTests
{
    private readonly Mock<IDependency> _mockDependency;
    private readonly MyService _service;

    public MyServiceTests()
    {
        _mockDependency = MockFactory.CreateDependency();
        _service = new MyService(_mockDependency.Object);
    }

    [Fact]
    public async Task MethodName_ShouldDoExpectedThing_WhenCondition()
    {
        // Arrange
        var input = TestDataBuilder.CreateTestData();
        
        // Act
        var result = await _service.DoSomethingAsync(input);
        
        // Assert
        result.Should().NotBeNull();
        result.Property.Should().Be("expected value");
        
        _mockDependency.Verify(d => d.WasCalled(), Times.Once);
    }
}
```

### UI Test Template

```csharp
using DerpResearch.Tests.Helpers;
using FluentAssertions;
using Microsoft.Playwright;

namespace DerpResearch.Tests.UI;

[Collection("Playwright")]
public class MyUITests : IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IPage? _page;

    public MyUITests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
        _page = await _fixture.CreatePageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_page != null) await _page.CloseAsync();
    }

    [Fact]
    public async Task UIElement_ShouldBehaveCorrectly()
    {
        // Arrange
        await _page!.GotoAsync(_fixture.BaseUrl);
        
        // Act
        await _page.ClickAsync("#myButton");
        
        // Assert
        var result = await _page.Locator("#result").TextContentAsync();
        result.Should().Contain("expected text");
    }
}
```

## 🎯 Best Practices

### General Guidelines

1. **Test Naming Convention**: `MethodName_ShouldExpectedBehavior_WhenCondition`
2. **Arrange-Act-Assert**: Structure all tests with clear AAA pattern
3. **One Assertion Per Test**: Keep tests focused and specific
4. **Test Independence**: Tests should not depend on each other
5. **Use Test Data Builders**: Utilize `TestDataBuilder` for creating test objects
6. **Mock External Dependencies**: Use `MockFactory` for consistent mocking

### Unit Test Best Practices

- Mock all external dependencies (databases, APIs, file system)
- Test edge cases and error conditions
- Verify method calls on mocks using `Verify()`
- Use `FluentAssertions` for readable assertions
- Keep tests fast (< 100ms per test)

### UI Test Best Practices

- Wait for elements to be visible before interacting
- Use semantic locators (IDs, accessible labels) over CSS selectors
- Test on multiple viewports (desktop, tablet, mobile)
- Clean up resources in `DisposeAsync()`
- Take screenshots on failures for debugging
- Keep UI tests isolated from backend changes when possible

### What to Test

✅ **DO Test**:
- Business logic and algorithms
- Error handling and edge cases
- Public API contracts
- User-facing features and workflows
- Responsive design breakpoints
- Accessibility features

❌ **DON'T Test**:
- Framework internals
- Third-party libraries
- Trivial getters/setters
- Auto-generated code

## 🔍 Troubleshooting

### Common Issues

#### Playwright Browsers Not Installed

```
Error: Executable doesn't exist at ...
```

**Solution**: Install browsers with:
```bash
pwsh bin/Debug/net9.0/playwright.ps1 install
```

#### UI Tests Failing - App Not Running

```
Error: net::ERR_CONNECTION_REFUSED
```

**Solution**: Start the application:
```bash
dotnet run --project DeepResearch.WebApp.csproj
```

#### Tests Timeout

**Solution**: Increase timeout in test or check for deadlocks:
```csharp
[Fact(Timeout = 10000)] // 10 seconds
public async Task MyTest() { }
```

#### Mock Verification Failures

```
Expected invocation on the mock once, but was 0 times
```

**Solution**: Check that:
- Setup matches the exact parameters
- Method is actually being called in the code
- Using `It.IsAny<T>()` for flexible matching

#### Flaky UI Tests

**Solution**:
- Add explicit waits: `await page.WaitForSelectorAsync("#element")`
- Use Playwright's auto-wait feature
- Avoid fixed `Task.Delay()` - use conditional waits
- Check for race conditions

### Debug Mode

Run tests in debug mode to step through:

```bash
# Visual Studio: Test Explorer → Right-click → Debug
# VS Code: Set breakpoint → Debug Test
# CLI: Not directly supported, use IDE
```

### Verbose Logging

```bash
dotnet test --logger "console;verbosity=detailed"
```

### Playwright Debug Mode

Set environment variable:

```bash
# Windows
set PWDEBUG=1
dotnet test --filter "FullyQualifiedName~UI"

# Linux/Mac
PWDEBUG=1 dotnet test --filter "FullyQualifiedName~UI"
```

## 📈 CI/CD Integration

### GitHub Actions Example

```yaml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      
      - name: Install Playwright
        run: pwsh DerpResearch.Tests/bin/Debug/net9.0/playwright.ps1 install
      
      - name: Run Unit Tests
        run: dotnet test --filter "FullyQualifiedName~Unit" --collect:"XPlat Code Coverage"
      
      - name: Start Application
        run: dotnet run --project DeepResearch.WebApp.csproj &
      
      - name: Run UI Tests
        run: dotnet test --filter "FullyQualifiedName~UI"
      
      - name: Upload Coverage
        uses: codecov/codecov-action@v3
```

## 🤝 Contributing

When adding new features, please:

1. Write tests first (TDD approach)
2. Ensure all tests pass before committing
3. Maintain or improve code coverage
4. Update this README if adding new test patterns
5. Add helpful comments for complex test scenarios

## 📚 Additional Resources

- [xUnit Documentation](https://xunit.net/)
- [Moq Quickstart](https://github.com/moq/moq4/wiki/Quickstart)
- [FluentAssertions Documentation](https://fluentassertions.com/introduction)
- [Playwright for .NET](https://playwright.dev/dotnet/)
- [Microsoft Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)

## 📧 Support

For questions or issues with tests:
- Check existing test examples in this directory
- Review troubleshooting section above
- Open an issue with [TEST] prefix
