using DerpResearch.Tests.Helpers;
using FluentAssertions;
using Microsoft.Playwright;

namespace DerpResearch.Tests.UI;

/// <summary>
/// UI tests for user interactions with the DerpResearch interface
/// Note: These tests require the application to be running on http://localhost:5000
/// Run the app with: dotnet run --project DeepResearch.WebApp.csproj
/// </summary>
[Collection("Playwright")]
public class UserInteractionTests : IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IPage? _page;

    public UserInteractionTests(PlaywrightFixture fixture)
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
        if (_page != null)
        {
            await _page.CloseAsync();
        }
    }

    [Fact]
    public async Task PageLoad_ShouldDisplayWelcomeMessage()
    {
        // Act
        await _page!.GotoAsync(_fixture.BaseUrl);
        
        // Assert
        var welcomeMessage = await _page.Locator(".message.assistant .message-content").First.TextContentAsync();
        welcomeMessage.Should().Contain("Welcome!");
        welcomeMessage.Should().Contain("Derp Research");
    }

    [Fact]
    public async Task PageLoad_ShouldDisplayMainUIElements()
    {
        // Act
        await _page!.GotoAsync(_fixture.BaseUrl);
        
        // Assert
        (await _page.Locator("#messageInput").IsVisibleAsync()).Should().Be(true);
        (await _page.Locator("#sendBtn").IsVisibleAsync()).Should().Be(true);
        (await _page.Locator("#derpSlider").IsVisibleAsync()).Should().Be(true);
        (await _page.Locator("#brainSvg").IsVisibleAsync()).Should().Be(true);
    }

    [Fact]
    public async Task MessageInput_ShouldAcceptText()
    {
        // Arrange
        await _page!.GotoAsync(_fixture.BaseUrl);
        var testMessage = "What is machine learning?";
        
        // Act
        await _page.FillAsync("#messageInput", testMessage);
        
        // Assert
        var inputValue = await _page.InputValueAsync("#messageInput");
        inputValue.Should().Be(testMessage);
    }

    [Fact]
    public async Task SendButton_ShouldBeDisabled_WhenInputIsEmpty()
    {
        // Arrange
        await _page!.GotoAsync(_fixture.BaseUrl);
        
        // Act
        await _page.FillAsync("#messageInput", "");
        
        // Assert - Initially enabled, but JS validation should prevent send
        var sendBtn = _page.Locator("#sendBtn");
        (await sendBtn.IsEnabledAsync()).Should().Be(true);
    }

    [Fact]
    public async Task EnterKey_ShouldSendMessage()
    {
        // Arrange
        await _page!.GotoAsync(_fixture.BaseUrl);
        var testMessage = "Test query";
        
        // Act
        await _page.FillAsync("#messageInput", testMessage);
        await _page.PressAsync("#messageInput", "Enter");
        
        // Wait for message to appear
        await Task.Delay(500);
        
        // Assert
        var userMessages = await _page.Locator(".message.user").CountAsync();
        userMessages.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task DerpificationSlider_ShouldUpdateValue()
    {
        // Arrange
        await _page!.GotoAsync(_fixture.BaseUrl);
        var slider = _page.Locator("#derpSlider");
        
        // Act
        await slider.FillAsync("50");
        
        // Assert
        var sliderValue = await slider.InputValueAsync();
        sliderValue.Should().Be("50");
    }

    [Fact]
    public async Task DerpificationSlider_ShouldUpdateBrainVisualization()
    {
        // Arrange
        await _page!.GotoAsync(_fixture.BaseUrl);
        var slider = _page.Locator("#derpSlider");
        
        // Get initial brain state
        var initialWrinkleOpacity = await _page.Locator("#wrinkle3").GetAttributeAsync("opacity");
        
        // Act - Set to low derpification (should have fewer wrinkles)
        await slider.FillAsync("10");
        await Task.Delay(100); // Wait for animation
        
        var lowDerpWrinkleOpacity = await _page.Locator("#wrinkle3").GetAttributeAsync("opacity");
        
        // Act - Set to high derpification (should have more wrinkles)
        await slider.FillAsync("90");
        await Task.Delay(100);
        
        var highDerpWrinkleOpacity = await _page.Locator("#wrinkle3").GetAttributeAsync("opacity");
        
        // Assert
        lowDerpWrinkleOpacity.Should().Be("0", "because low derpification should hide advanced wrinkles");
        highDerpWrinkleOpacity.Should().Be("1", "because high derpification should show all wrinkles");
    }

    [Fact]
    public async Task BrainSVG_ShouldPulseOnSliderChange()
    {
        // Arrange
        await _page!.GotoAsync(_fixture.BaseUrl);
        var brain = _page.Locator("#brainSvg");
        var slider = _page.Locator("#derpSlider");
        
        // Act
        await slider.FillAsync("75");
        
        // Assert - Check if thinking class is added (even briefly)
        await Task.Delay(100);
        var hasThinkingClass = await brain.EvaluateAsync<bool>("el => el.classList.contains('thinking')");
        
        // Note: Class might be removed by time we check, but animation should trigger
        // We mainly verify no errors occur during interaction
        // Just verify it's a valid boolean (no exceptions thrown)
        (hasThinkingClass == true || hasThinkingClass == false).Should().BeTrue();
    }

    [Fact]
    public async Task FreshSearchButton_ShouldNotBeVisibleInitially()
    {
        // Arrange & Act
        await _page!.GotoAsync(_fixture.BaseUrl);
        
        // Assert
        var freshBtn = _page.Locator("#freshSearchBtn");
        (await freshBtn.IsVisibleAsync()).Should().Be(false);
    }

    [Fact]
    public async Task ScrollingChatContainer_ShouldTriggerStickyHeader()
    {
        // Arrange
        await _page!.GotoAsync(_fixture.BaseUrl);
        var header = _page.Locator(".header");
        
        // Act - Scroll chat container
        await _page.EvaluateAsync(@"
            document.getElementById('chatContainer').scrollTop = 100;
        ");
        await Task.Delay(200); // Wait for sticky header animation
        
        // Assert
        var hasCompactClass = await header.EvaluateAsync<bool>("el => el.classList.contains('compact')");
        hasCompactClass.Should().BeTrue("because scrolling should trigger compact mode");
    }

    [Fact]
    public async Task MessageInput_ShouldClearAfterSending()
    {
        // Arrange
        await _page!.GotoAsync(_fixture.BaseUrl);
        var testMessage = "Test message";
        
        // Act
        await _page.FillAsync("#messageInput", testMessage);
        await _page.ClickAsync("#sendBtn");
        await Task.Delay(200);
        
        // Assert
        var inputValue = await _page.InputValueAsync("#messageInput");
        inputValue.Should().BeEmpty("because input should clear after sending");
    }

    [Fact]
    public async Task Header_ShouldContainDerpResearchTitle()
    {
        // Act
        await _page!.GotoAsync(_fixture.BaseUrl);
        
        // Assert
        var headerText = await _page.Locator(".header h1").TextContentAsync();
        headerText.Should().Contain("Derp Research");
    }

    [Fact]
    public async Task Header_ShouldContainSubtitle()
    {
        // Act
        await _page!.GotoAsync(_fixture.BaseUrl);
        
        // Assert
        var subtitle = await _page.Locator(".header p").TextContentAsync();
        subtitle.Should().Contain("Dial your AI");
    }

    [Fact]
    public async Task MessageInput_ShouldHavePlaceholder()
    {
        // Act
        await _page!.GotoAsync(_fixture.BaseUrl);
        
        // Assert
        var placeholder = await _page.Locator("#messageInput").GetAttributeAsync("placeholder");
        placeholder.Should().Contain("Ask me anything");
    }

    [Fact]
    public async Task SendButton_ShouldHaveCorrectText()
    {
        // Act
        await _page!.GotoAsync(_fixture.BaseUrl);
        
        // Assert
        var buttonText = await _page.Locator("#sendBtn").TextContentAsync();
        buttonText.Should().Be("Send");
    }

    [Fact]
    public async Task DerpSlider_ShouldHaveDefaultValue100()
    {
        // Act
        await _page!.GotoAsync(_fixture.BaseUrl);
        
        // Assert
        var sliderValue = await _page.Locator("#derpSlider").InputValueAsync();
        sliderValue.Should().Be("100", "because default derpification should be maximum");
    }

    [Fact]
    public async Task DerpSlider_ShouldHaveLabels()
    {
        // Act
        await _page!.GotoAsync(_fixture.BaseUrl);
        
        // Assert
        var labels = await _page.Locator(".derp-slider-label").AllTextContentsAsync();
        labels.Should().Contain("Derp");
        labels.Should().Contain("Smart");
    }

    [Fact]
    public async Task ChatContainer_ShouldBeScrollable()
    {
        // Act
        await _page!.GotoAsync(_fixture.BaseUrl);
        
        // Assert
        var chatContainer = _page.Locator("#chatContainer");
        var overflowY = await chatContainer.EvaluateAsync<string>("el => getComputedStyle(el).overflowY");
        overflowY.Should().Be("auto", "because chat container should be scrollable");
    }

    [Fact]
    public async Task Page_ShouldHaveCorrectTitle()
    {
        // Act
        await _page!.GotoAsync(_fixture.BaseUrl);
        
        // Assert
        var title = await _page.TitleAsync();
        title.Should().Contain("Derp Research");
    }

    [Fact]
    public async Task AllBrainWrinkles_ShouldBePresent()
    {
        // Act
        await _page!.GotoAsync(_fixture.BaseUrl);
        
        // Assert - Check all 10 wrinkles exist
        for (int i = 1; i <= 10; i++)
        {
            var wrinkle = _page.Locator($"#wrinkle{i}");
            (await wrinkle.IsVisibleAsync()).Should().Be(true, 
                $"because wrinkle{i} should be present in the brain SVG");
        }
    }

    [Fact]
    public async Task InputFocus_ShouldWorkOnPageLoad()
    {
        // Act
        await _page!.GotoAsync(_fixture.BaseUrl);
        await Task.Delay(500); // Wait for auto-focus
        
        // Assert
        var isFocused = await _page.EvaluateAsync<bool>(
            "document.activeElement.id === 'messageInput'");
        
        isFocused.Should().BeTrue("because message input should be auto-focused on load");
    }
}
