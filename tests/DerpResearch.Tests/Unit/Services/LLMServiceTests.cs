using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;
using DeepResearch.WebApp.Services;
using DerpResearch.Tests.Helpers;
using FluentAssertions;
using Moq;

namespace DerpResearch.Tests.Unit.Services;

public class LLMServiceTests
{
    private class Widget
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }

    private static LLMService CreateServiceReturning(string providerResponse)
    {
        var provider = new Mock<ILLMProvider>();
        provider.Setup(p => p.CompleteAsync(
                It.IsAny<LLMRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(providerResponse);
        return new LLMService(provider.Object, TestMockFactory.CreateLogger<LLMService>().Object);
    }

    [Fact]
    public async Task GetStructuredOutput_ShouldParsePlainJson()
    {
        var service = CreateServiceReturning("{\"name\":\"bolt\",\"count\":3}");

        var result = await service.GetStructuredOutput<Widget>("prompt");

        result.Should().NotBeNull();
        result!.Name.Should().Be("bolt");
        result.Count.Should().Be(3);
    }

    [Fact]
    public async Task GetStructuredOutput_ShouldStripJsonCodeFence()
    {
        var service = CreateServiceReturning("```json\n{\"name\":\"bolt\",\"count\":3}\n```");

        var result = await service.GetStructuredOutput<Widget>("prompt");

        result.Should().NotBeNull();
        result!.Name.Should().Be("bolt");
        result.Count.Should().Be(3);
    }

    [Fact]
    public async Task GetStructuredOutput_ShouldStripBareCodeFence()
    {
        var service = CreateServiceReturning("```\n{\"name\":\"nut\",\"count\":7}\n```");

        var result = await service.GetStructuredOutput<Widget>("prompt");

        result.Should().NotBeNull();
        result!.Name.Should().Be("nut");
        result.Count.Should().Be(7);
    }

    [Fact]
    public async Task GetStructuredOutput_ShouldReturnNull_WhenResponseIsNotJson()
    {
        var service = CreateServiceReturning("I could not answer that.");

        var result = await service.GetStructuredOutput<Widget>("prompt");

        result.Should().BeNull();
    }
}
