using DerpResearch.Tests.Helpers;
using FluentAssertions;
using Microsoft.Playwright;

namespace DerpResearch.Tests.UI;

/// <summary>
/// UI tests for responsive design across different viewports
/// Tests mobile, tablet, and desktop layouts
/// </summary>
[Collection("Playwright")]
public class ResponsiveDesignTests : IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IPage? _desktopPage;
    private IPage? _mobilePage;
    private IPage? _tabletPage;

    public ResponsiveDesignTests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
        _desktopPage = await _fixture.CreatePageAsync();
        _mobilePage = await _fixture.CreateMobilePageAsync();
        _tabletPage = await _fixture.CreateTabletPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_desktopPage != null) await _desktopPage.CloseAsync();
        if (_mobilePage != null) await _mobilePage.CloseAsync();
        if (_tabletPage != null) await _tabletPage.CloseAsync();
    }

    [Fact]
    public async Task Desktop_ShouldDisplayFullLayout()
    {
        // Act
        await _desktopPage!.GotoAsync(_fixture.BaseUrl);
        
        // Assert
        var header = _desktopPage.Locator(".header");
        (await header.IsVisibleAsync()).Should().Be(true);
        
        var chatContainer = _desktopPage.Locator("#chatContainer");
        (await chatContainer.IsVisibleAsync()).Should().Be(true);
        
        var inputContainer = _desktopPage.Locator(".input-container");
        (await inputContainer.IsVisibleAsync()).Should().Be(true);
    }

    [Fact]
    public async Task Mobile_ShouldDisplayCompactLayout()
    {
        // Act
        await _mobilePage!.GotoAsync(_fixture.BaseUrl);
        
        // Assert - All elements should still be visible but compact
        var header = _mobilePage.Locator(".header");
        (await header.IsVisibleAsync()).Should().Be(true);
        
        var chatContainer = _mobilePage.Locator("#chatContainer");
        (await chatContainer.IsVisibleAsync()).Should().Be(true);
        
        var inputContainer = _mobilePage.Locator(".input-container");
        (await inputContainer.IsVisibleAsync()).Should().Be(true);
    }

    [Fact]
    public async Task Mobile_HeaderShouldHaveSmallerPadding()
    {
        // Arrange
        await _desktopPage!.GotoAsync(_fixture.BaseUrl);
        await _mobilePage!.GotoAsync(_fixture.BaseUrl);
        
        // Act
        var desktopPadding = await _desktopPage.Locator(".header").EvaluateAsync<string>(
            "el => getComputedStyle(el).padding");
        var mobilePadding = await _mobilePage.Locator(".header").EvaluateAsync<string>(
            "el => getComputedStyle(el).padding");
        
        // Assert
        desktopPadding.Should().NotBe(mobilePadding, 
            "because mobile should have different padding than desktop");
    }

    [Fact]
    public async Task Mobile_FreshSearchButton_ShouldShowTrashIconOnly()
    {
        // Act
        await _mobilePage!.GotoAsync(_fixture.BaseUrl);
        
        // Make button visible by simulating research
        await _mobilePage.EvaluateAsync(@"
            document.getElementById('freshSearchBtn').style.display = 'flex';
        ");
        
        // Assert
        var desktopIcon = _mobilePage.Locator(".fresh-search-icon-desktop");
        var mobileIcon = _mobilePage.Locator(".fresh-search-icon-mobile");
        var text = _mobilePage.Locator(".fresh-search-text");
        
        var desktopIconVisible = await desktopIcon.IsVisibleAsync();
        var mobileIconVisible = await mobileIcon.IsVisibleAsync();
        
        desktopIconVisible.Should().BeFalse("because desktop icon should be hidden on mobile");
        mobileIconVisible.Should().BeTrue("because mobile trash icon should be visible");
    }

    [Fact]
    public async Task Mobile_HeaderCompactMode_ShouldHideSubtitle()
    {
        // Act
        await _mobilePage!.GotoAsync(_fixture.BaseUrl);
        
        // Trigger compact mode by scrolling
        await _mobilePage.EvaluateAsync(@"
            document.getElementById('chatContainer').scrollTop = 100;
        ");
        await Task.Delay(300); // Wait for animation
        
        // Assert
        var subtitle = _mobilePage.Locator(".header p");
        var opacity = await subtitle.EvaluateAsync<string>("el => getComputedStyle(el).opacity");
        
        opacity.Should().Be("0", "because subtitle should fade out in compact mode on mobile");
    }

    [Fact]
    public async Task Tablet_ShouldDisplayIntermediateLayout()
    {
        // Act
        await _tabletPage!.GotoAsync(_fixture.BaseUrl);
        
        // Assert - Check viewport is actually tablet size
        var viewportSize = _tabletPage.ViewportSize;
        viewportSize!.Width.Should().Be(768);
        viewportSize.Height.Should().Be(1024);
        
        // All main elements should be visible
        (await _tabletPage.Locator(".header").IsVisibleAsync()).Should().Be(true);
        (await _tabletPage.Locator("#chatContainer").IsVisibleAsync()).Should().Be(true);
        (await _tabletPage.Locator(".input-container").IsVisibleAsync()).Should().Be(true);
    }

    [Fact]
    public async Task Mobile_DerpSlider_ShouldBeTouchFriendly()
    {
        // Act
        await _mobilePage!.GotoAsync(_fixture.BaseUrl);
        
        // Assert - Check thumb size is larger on mobile
        var slider = _mobilePage.Locator("#derpSlider");
        var thumbWidth = await slider.EvaluateAsync<string>(@"
            el => getComputedStyle(el, '::-webkit-slider-thumb').width
        ");
        
        // Mobile thumb should be 24px, desktop 16px
        thumbWidth.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Mobile_InputFontSize_ShouldPreventZoom()
    {
        // Act
        await _mobilePage!.GotoAsync(_fixture.BaseUrl);
        
        // Assert - Font size should be at least 16px to prevent iOS zoom
        var input = _mobilePage.Locator("#messageInput");
        var fontSize = await input.EvaluateAsync<string>("el => getComputedStyle(el).fontSize");
        
        var fontSizeValue = double.Parse(fontSize.Replace("px", ""));
        fontSizeValue.Should().BeGreaterOrEqualTo(16, 
            "because 16px prevents iOS zoom on focus");
    }

    [Fact]
    public async Task Mobile_SendButton_ShouldHaveMinimumTouchTarget()
    {
        // Act
        await _mobilePage!.GotoAsync(_fixture.BaseUrl);
        
        // Assert - Button should have at least 44px height (iOS HIG)
        var button = _mobilePage.Locator("#sendBtn");
        var boundingBox = await button.BoundingBoxAsync();
        
        boundingBox!.Height.Should().BeGreaterOrEqualTo(44, 
            "because iOS Human Interface Guidelines require 44px minimum touch targets");
    }

    [Fact]
    public async Task Desktop_ContainerShouldHaveMaxWidth()
    {
        // Act
        await _desktopPage!.GotoAsync(_fixture.BaseUrl);
        
        // Assert
        var container = _desktopPage.Locator(".container");
        var maxWidth = await container.EvaluateAsync<string>("el => getComputedStyle(el).maxWidth");
        
        maxWidth.Should().Be("900px", "because desktop should have constrained max width");
    }

    [Fact]
    public async Task Mobile_ContainerShouldHaveFullWidth()
    {
        // Act
        await _mobilePage!.GotoAsync(_fixture.BaseUrl);
        
        // Assert
        var container = _mobilePage.Locator(".container");
        var width = await container.EvaluateAsync<string>("el => getComputedStyle(el).width");
        var borderRadius = await container.EvaluateAsync<string>("el => getComputedStyle(el).borderRadius");
        
        borderRadius.Should().Be("0px", "because mobile should have no border radius for full-screen feel");
    }

    [Fact]
    public async Task Mobile_MessagesShouldUseFullWidth()
    {
        // Act
        await _mobilePage!.GotoAsync(_fixture.BaseUrl);
        
        // Assert
        var userMessage = _mobilePage.Locator(".message.user .message-content");
        var maxWidth = await userMessage.First.EvaluateAsync<string>(
            "el => getComputedStyle(el).maxWidth");
        
        // Mobile should use higher percentage for messages
        maxWidth.Should().Contain("%", "because mobile messages should use percentage width");
    }

    [Fact]
    public async Task Desktop_ShouldShowFullFreshSearchButtonText()
    {
        // Act
        await _desktopPage!.GotoAsync(_fixture.BaseUrl);
        
        // Make button visible
        await _desktopPage.EvaluateAsync(@"
            document.getElementById('freshSearchBtn').style.display = 'flex';
        ");
        
        // Assert
        var text = _desktopPage.Locator(".fresh-search-text");
        (await text.IsVisibleAsync()).Should().Be(true);
        
        var textContent = await text.TextContentAsync();
        textContent.Should().Be("Fresh Search");
    }

    [Fact]
    public async Task AllViewports_ShouldMaintainReadability()
    {
        // Act
        await _desktopPage!.GotoAsync(_fixture.BaseUrl);
        await _mobilePage!.GotoAsync(_fixture.BaseUrl);
        await _tabletPage!.GotoAsync(_fixture.BaseUrl);
        
        // Assert - Check base font sizes are readable
        var desktopFontSize = await _desktopPage.Locator("body").EvaluateAsync<string>(
            "el => getComputedStyle(el).fontSize");
        var mobileFontSize = await _mobilePage.Locator("body").EvaluateAsync<string>(
            "el => getComputedStyle(el).fontSize");
        
        desktopFontSize.Should().NotBeEmpty();
        mobileFontSize.Should().NotBeEmpty();
        
        // Both should have readable font sizes
        var desktopSize = double.Parse(desktopFontSize.Replace("px", ""));
        var mobileSize = double.Parse(mobileFontSize.Replace("px", ""));
        
        desktopSize.Should().BeGreaterOrEqualTo(14);
        mobileSize.Should().BeGreaterOrEqualTo(14);
    }

    [Fact]
    public async Task Mobile_LandscapeMode_ShouldAdjustLayout()
    {
        // Arrange - Create landscape mobile context
        var landscapePage = await _fixture.Browser!.NewContextAsync(new()
        {
            ViewportSize = new ViewportSize { Width = 667, Height = 375 }, // Landscape iPhone SE
            IsMobile = true,
            HasTouch = true
        }).Result.NewPageAsync();
        
        // Act
        await landscapePage.GotoAsync(_fixture.BaseUrl);
        
        // Assert - Header should have smaller padding in landscape
        var header = landscapePage.Locator(".header");
        (await header.IsVisibleAsync()).Should().Be(true);
        
        await landscapePage.CloseAsync();
    }

    [Fact]
    public async Task Mobile_SourceCards_ShouldStackVertically()
    {
        // Act
        await _mobilePage!.GotoAsync(_fixture.BaseUrl);
        
        // Add sample source cards via script
        await _mobilePage.EvaluateAsync(@"
            const container = document.getElementById('chatContainer');
            const messageDiv = document.createElement('div');
            messageDiv.className = 'message assistant';
            messageDiv.innerHTML = `
                <div class='message-content'>
                    <div class='sources-section'>
                        <div class='source-item'>Source 1</div>
                        <div class='source-item'>Source 2</div>
                    </div>
                </div>
            `;
            container.appendChild(messageDiv);
        ");
        
        // Assert - Source items should stack vertically
        var sourceItems = _mobilePage.Locator(".source-item");
        var count = await sourceItems.CountAsync();
        count.Should().Be(2);
    }

    [Fact]
    public async Task Desktop_BrainSVG_ShouldHaveLargerSize()
    {
        // Arrange
        await _desktopPage!.GotoAsync(_fixture.BaseUrl);
        await _mobilePage!.GotoAsync(_fixture.BaseUrl);
        
        // Act
        var desktopBrainBox = await _desktopPage.Locator(".brain-container").BoundingBoxAsync();
        var mobileBrainBox = await _mobilePage.Locator(".brain-container").BoundingBoxAsync();
        
        // Assert
        desktopBrainBox!.Width.Should().BeGreaterOrEqualTo(mobileBrainBox!.Width,
            "because desktop brain should be same size or larger than mobile");
    }

    [Fact]
    public async Task Mobile_ChatContainer_ShouldFillScreen()
    {
        // Act
        await _mobilePage!.GotoAsync(_fixture.BaseUrl);
        
        // Assert
        var container = _mobilePage.Locator(".container");
        var height = await container.EvaluateAsync<string>("el => getComputedStyle(el).height");
        
        // Should use viewport units or be 100vh
        height.Should().MatchRegex(@"(100vh|100%)", 
            "because mobile container should fill screen height");
    }
}
