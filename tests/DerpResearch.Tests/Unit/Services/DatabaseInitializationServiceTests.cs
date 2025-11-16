using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DerpResearch.Tests.Unit.Services;

public class DatabaseInitializationServiceTests
{
    [Fact]
    public async Task ExecuteAsync_Success_MarksHealthCheckAsHealthy()
    {
        // Arrange
        var mockMemoryService = new Mock<IMemoryService>();
        mockMemoryService
            .Setup(m => m.InitializeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var healthCheck = new InitializationHealthCheck();
        var mockLogger = new Mock<ILogger<DatabaseInitializationService>>();
        var mockLifetime = new Mock<IHostApplicationLifetime>();

        var service = new DatabaseInitializationService(
            mockMemoryService.Object,
            healthCheck,
            mockLogger.Object,
            mockLifetime.Object);

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Give background task time to complete

        // Assert
        var result = await healthCheck.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());
        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy, result.Status);
        mockMemoryService.Verify(m => m.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Failure_MarksHealthCheckAsUnhealthy()
    {
        // Arrange
        var mockMemoryService = new Mock<IMemoryService>();
        mockMemoryService
            .Setup(m => m.InitializeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database initialization failed"));

        var healthCheck = new InitializationHealthCheck();
        var mockLogger = new Mock<ILogger<DatabaseInitializationService>>();
        var mockLifetime = new Mock<IHostApplicationLifetime>();

        var service = new DatabaseInitializationService(
            mockMemoryService.Object,
            healthCheck,
            mockLogger.Object,
            mockLifetime.Object);

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Give background task time to complete

        // Assert
        var result = await healthCheck.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());
        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Database initialization failed", result.Description);
        mockLifetime.Verify(l => l.StopApplication(), Times.Once);
    }

    [Fact]
    public async Task InitializationHealthCheck_ThreadSafety_NoRaceConditions()
    {
        // Arrange
        var healthCheck = new InitializationHealthCheck();
        var tasks = new List<Task>();

        // Act - Multiple threads accessing health check simultaneously
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() => healthCheck.MarkAsHealthy()));
            tasks.Add(Task.Run(() => healthCheck.MarkAsFailed(new Exception("Test"))));
            tasks.Add(Task.Run(async () => await healthCheck.CheckHealthAsync(
                new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext())));
        }

        await Task.WhenAll(tasks);

        // Assert - No exceptions thrown (test passes if no exceptions)
        Assert.True(true);
    }
}
