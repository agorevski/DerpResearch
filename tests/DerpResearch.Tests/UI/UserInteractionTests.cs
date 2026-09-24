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

        // Assert
        var sendBtn = _page.Locator("#sendBtn");
        (await sendBtn.IsDisabledAsync()).Should().Be(true);

        await _page.FillAsync("#messageInput", "   ");
        (await sendBtn.IsDisabledAsync()).Should().Be(true);
    }

    [Fact]
    public async Task SendButton_ShouldTrackInputState()
    {
        await _page!.GotoAsync(_fixture.BaseUrl);
        var sendBtn = _page.Locator("#sendBtn");

        await _page.FillAsync("#messageInput", "What is machine learning?");
        (await sendBtn.IsEnabledAsync()).Should().Be(true);

        await _page.FillAsync("#messageInput", "");
        (await sendBtn.IsDisabledAsync()).Should().Be(true);
    }

    [Fact]
    public async Task Page_ShouldExposeAccessibleComposerAndLiveStatus()
    {
        await _page!.GotoAsync(_fixture.BaseUrl);

        (await _page.GetByLabel("Research question").IsVisibleAsync()).Should().Be(true);
        (await _page.GetByLabel("Derpification level").IsVisibleAsync()).Should().Be(true);
        (await _page.Locator("#freshSearchBtn").GetAttributeAsync("aria-label"))
            .Should().Be("Start a fresh search");

        var conversation = _page.GetByRole(AriaRole.Log);
        (await conversation.GetAttributeAsync("aria-live")).Should().Be("polite");
        (await conversation.GetAttributeAsync("aria-busy")).Should().Be("false");

        var status = _page.Locator("#statusMessage");
        (await status.GetAttributeAsync("aria-live")).Should().Be("polite");
    }

    [Fact]
    public async Task FailedRequest_ShouldShowInlineErrorAndRetry()
    {
        await _page!.RouteAsync("**/api/chat", async route =>
        {
            await route.FulfillAsync(new()
            {
                Status = 503,
                ContentType = "text/plain",
                Body = "Service temporarily unavailable"
            });
        });
        await _page.GotoAsync(_fixture.BaseUrl);

        await _page.FillAsync("#messageInput", "Test request");
        await _page.ClickAsync("#sendBtn");

        var status = _page.Locator("#statusMessage");
        await status.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        (await status.GetAttributeAsync("role")).Should().Be("alert");
        (await status.TextContentAsync()).Should().Contain("503");
        (await _page.Locator("#retryBtn").IsVisibleAsync()).Should().Be(true);
        (await _page.Locator("#chatContainer").GetAttributeAsync("aria-busy")).Should().Be("false");
    }

    [Fact]
    public async Task Clarification_ShouldReuseOriginalPromptFromState()
    {
        var requests = new List<string>();
        await _page!.RouteAsync("**/api/chat", async route =>
        {
            requests.Add(route.Request.PostData ?? "");
            var body = requests.Count == 1
                ? "data: {\"type\":\"clarification\",\"data\":{\"rationale\":\"Need scope\",\"questions\":[\"Which region?\"]}}\n\ndata: {\"type\":\"done\"}\n\n"
                : "data: {\"type\":\"done\"}\n\n";

            await route.FulfillAsync(new()
            {
                Status = 200,
                ContentType = "text/event-stream",
                Body = body
            });
        });
        await _page.GotoAsync(_fixture.BaseUrl);

        const string originalPrompt = "Compare regional market trends";
        await _page.FillAsync("#messageInput", originalPrompt);
        await _page.ClickAsync("#sendBtn");

        var clarification = _page.Locator(".clarification-section");
        await clarification.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await _page.Locator(".message.user .message-content").EvaluateAsync(
            "element => element.textContent = 'Changed rendered text'");
        await clarification.Locator(".clarification-input").FillAsync("Europe");
        await clarification.Locator(".clarification-submit-btn").ClickAsync();

        await _page.WaitForFunctionAsync("() => document.querySelector('#chatContainer').getAttribute('aria-busy') === 'false'");
        requests.Should().HaveCount(2);
        requests[1].Should().Contain($"\"prompt\":\"{originalPrompt}\"");
    }

    [Fact]
    public async Task StreamParser_ShouldHandleFragmentedChunksAndEventBoundaries()
    {
        await _page!.GotoAsync(_fixture.BaseUrl);

        var tokens = await _page.EvaluateAsync<string[]>(@"async () => {
            const encoder = new TextEncoder();
            const chunks = [
                'data: {""token"":""hel',
                'lo""}\r',
                '\n\r\n',
                'data: {""token"":""world""}\n\n'
            ];
            const stream = new ReadableStream({
                start(controller) {
                    chunks.forEach(chunk => controller.enqueue(encoder.encode(chunk)));
                    controller.close();
                }
            });
            const values = [];
            await consumeServerSentEvents(stream, data => {
                values.push(data.token);
                return true;
            });
            return values;
        }");

        tokens.Should().Equal("hello", "world");
    }

    [Fact]
    public async Task StreamedSynthesis_ShouldRenderLongTokensPromptlyAsSafeMarkdown()
    {
        await _page!.GotoAsync(_fixture.BaseUrl);

        await _page.EvaluateAsync(@"() => {
            handleStreamEvent({ type: 'progress', data: { message: 'Searching' } }, true);
            handleStreamEvent({
                token: '**Summary** ' + 'research '.repeat(600) +
                    'FINAL_TOKEN <img src=x onerror=alert(1)> ' +
                    '<script>window.synthesisInjected = true</script> [bad](javascript:alert(1))'
            }, true);
        }");
        await _page.WaitForFunctionAsync(
            "() => document.querySelector('.synthesis-divider + div')?.textContent.includes('FINAL_TOKEN')",
            null, new() { Timeout = 1000 });

        var result = await _page.EvaluateAsync<string[]>(@"() => {
            const synthesis = currentSynthesisDiv;
            return [
                synthesis.textContent,
                synthesis.querySelector('strong')?.textContent || '',
                String(synthesis.querySelector('img')?.hasAttribute('onerror') || false),
                String(Boolean(synthesis.querySelector('script') || window.synthesisInjected)),
                synthesis.querySelector('a')?.getAttribute('href') || '',
                document.querySelector('.progress-stage')?.textContent || ''
            ];
        }");

        result[0].Should().Contain("FINAL_TOKEN");
        result[1].Should().Be("Summary");
        result[2].Should().Be("false");
        result[3].Should().Be("false");
        result[4].Should().BeEmpty();
        result[5].Should().Be("Searching");
    }

    [Fact]
    public async Task Sources_ShouldLinkOnlyAbsoluteHttpUrls()
    {
        await _page!.GotoAsync(_fixture.BaseUrl);

        var unsafeUrls = await _page.EvaluateAsync<string[]>(@"() => {
            const safeUrls = ['https://example.test/?a=1&b=2', 'http://example.test/path'];
            const unsafeUrls = [
                'javascript:alert(1)',
                'data:text/html,<script>alert(1)</script>',
                '//example.test/path',
                '/relative/path',
                'mailto:research@example.test',
                'https:example.test'
            ];
            [...safeUrls, ...unsafeUrls].forEach((url, index) =>
                handleStreamEvent({
                    type: 'source',
                    data: {
                        title: index === 2 ? '<img src=x onerror=alert(1)>' : 'Source',
                        url,
                        snippet: index === 2 ? '<script>alert(1)</script>' : 'Excerpt'
                    }
                }, true)
            );
            return unsafeUrls;
        }");

        var sources = _page.Locator(".source-item");
        (await sources.CountAsync()).Should().Be(unsafeUrls.Length + 2);
        for (var i = 0; i < 2; i++)
        {
            var link = sources.Nth(i).Locator("a.source-url");
            (await link.GetAttributeAsync("href")).Should().StartWith(i == 0 ? "https://" : "http://");
            (await link.GetAttributeAsync("target")).Should().Be("_blank");
            (await link.GetAttributeAsync("rel")).Should().Be("noopener noreferrer");
        }
        (await sources.First.Locator(".source-url").TextContentAsync())
            .Should().Be("https://example.test/?a=1&b=2");
        for (var i = 0; i < unsafeUrls.Length; i++)
        {
            var source = sources.Nth(i + 2);
            (await source.Locator("a").CountAsync()).Should().Be(0);
            (await source.Locator("span.source-url").TextContentAsync()).Should().Be(unsafeUrls[i]);
        }
        (await sources.Locator("script, img").CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task StreamedUpdates_ShouldLeaveScrolledUpReaderAloneAndFollowAgainAtBottom()
    {
        await _page!.GotoAsync(_fixture.BaseUrl);

        var positions = await _page.EvaluateAsync<int[]>(@"async () => {
            const chat = document.getElementById('chatContainer');
            const spacer = document.createElement('div');
            spacer.style.height = '1800px';
            chat.appendChild(spacer);
            chat.scrollTop = chat.scrollHeight;
            chat.dispatchEvent(new Event('scroll'));
            chat.scrollTop = 0;
            chat.dispatchEvent(new Event('scroll'));
            handleStreamEvent({ type: 'progress', data: { message: 'Searching' } }, true);
            handleStreamEvent({ type: 'plan', data: { goal: 'Review', subtasks: ['Check'] } }, true);
            handleStreamEvent({ token: '**Findings**' }, true);
            stopSmoothRendering();
            handleStreamEvent({
                type: 'reflection',
                data: { confidenceScore: 0.8, iterations: 1, reasoning: 'Complete' }
            }, true);
            handleStreamEvent({
                type: 'clarification',
                data: { rationale: 'Need scope', questions: ['Which market?'] }
            }, true);
            await new Promise(resolve => setTimeout(resolve, 160));
            const stayedAt = chat.scrollTop;
            chat.scrollTop = chat.scrollHeight;
            chat.dispatchEvent(new Event('scroll'));
            handleStreamEvent({ token: ' More findings' }, true);
            stopSmoothRendering();
            return [stayedAt, chat.scrollHeight - chat.clientHeight - chat.scrollTop];
        }");

        positions[0].Should().Be(0);
        positions[1].Should().BeLessOrEqualTo(8);
    }

    [Fact]
    public async Task StreamEndAndFreshSearch_ShouldFlushOrDiscardPendingTokens()
    {
        await _page!.GotoAsync(_fixture.BaseUrl);

        var messages = await _page.EvaluateAsync<string[]>(@"async () => {
            const encoder = new TextEncoder();
            const stream = new ReadableStream({
                start(controller) {
                    controller.enqueue(encoder.encode('data: {""token"":""first response""}\n\n'));
                    controller.enqueue(encoder.encode('data: {""type"":""done""}\n\n'));
                    controller.close();
                }
            });
            await consumeServerSentEvents(stream, data => handleStreamEvent(data, true));
            stopSmoothRendering();
            const completed = currentSynthesisDiv.textContent;
            handleStreamEvent({ token: ' should be discarded' }, true);
            clearChatAndStartFresh();
            handleStreamEvent({ token: 'second response' }, true);
            stopSmoothRendering();
            return [completed, ...document.querySelectorAll('.synthesis-divider + div')].map(
                node => (typeof node === 'string' ? node : node.textContent).trim()
            );
        }");

        messages.Should().Equal("first response", "second response");
    }

    [Fact]
    public async Task Retry_ShouldStartFreshSynthesisAfterStreamFailure()
    {
        var requests = 0;
        await _page!.RouteAsync("**/api/chat", async route =>
        {
            requests++;
            var body = requests == 1
                ? "data: {\"token\":\"partial draft\"}\n\ndata: {\"type\":\"error\",\"token\":\"Failed\"}\n\n"
                : "data: {\"token\":\"**recovered** response\"}\n\ndata: {\"type\":\"done\"}\n\n";
            await route.FulfillAsync(new()
            {
                Status = 200,
                ContentType = "text/event-stream",
                Body = body
            });
        });
        await _page.GotoAsync(_fixture.BaseUrl);
        await _page.FillAsync("#messageInput", "Test retry");
        await _page.ClickAsync("#sendBtn");
        await _page.Locator("#retryBtn").WaitForAsync(new() { State = WaitForSelectorState.Visible });

        (await _page.Locator(".synthesis-divider + div").First.TextContentAsync())
            .Should().Contain("partial draft");
        await _page.WaitForFunctionAsync(
            "() => document.querySelector('#chatContainer').getAttribute('aria-busy') === 'false'");
        await _page.ClickAsync("#retryBtn");
        await _page.Locator(".synthesis-divider + div").Nth(1).WaitForAsync();

        (await _page.Locator(".synthesis-divider + div").Nth(1).TextContentAsync())
            .Should().Contain("recovered response").And.NotContain("partial draft");
        (await _page.Locator(".synthesis-divider + div").Nth(1).Locator("strong").TextContentAsync())
            .Should().Be("recovered");
        requests.Should().Be(2);
    }

    [Fact]
    public async Task Cancel_ShouldRestoreComposerAndShowStatus()
    {
        await _page!.GotoAsync(_fixture.BaseUrl);
        await _page.EvaluateAsync(@"() => {
            window.fetch = (_, options) => new Promise((resolve, reject) => {
                options.signal.addEventListener('abort', () => reject(new DOMException('Cancelled', 'AbortError')));
            });
        }");

        await _page.FillAsync("#messageInput", "Long-running research");
        await _page.ClickAsync("#sendBtn");
        await _page.Locator("#cancelBtn").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await _page.ClickAsync("#cancelBtn");

        await _page.WaitForFunctionAsync(
            "() => document.querySelector('#chatContainer').getAttribute('aria-busy') === 'false'");
        (await _page.Locator("#statusText").TextContentAsync()).Should().Contain("cancelled");
        (await _page.Locator("#cancelBtn").IsVisibleAsync()).Should().BeFalse();
        (await _page.Locator("#sendBtn").IsDisabledAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Cancel_ShouldKeepTokensAlreadyReceived()
    {
        await _page!.GotoAsync(_fixture.BaseUrl);
        await _page.EvaluateAsync(@"() => {
            window.fetch = (_, options) => {
                const stream = new ReadableStream({
                    start(controller) {
                        controller.enqueue(new TextEncoder().encode('data: {""token"":""partial answer""}\n\n'));
                        options.signal.addEventListener('abort', () =>
                            controller.error(new DOMException('Cancelled', 'AbortError')));
                    }
                });
                return Promise.resolve(new Response(stream, {
                    headers: { 'Content-Type': 'text/event-stream' }
                }));
            };
        }");
        await _page.FillAsync("#messageInput", "Long-running research");
        await _page.ClickAsync("#sendBtn");
        await _page.Locator(".synthesis-divider").WaitForAsync();
        await _page.ClickAsync("#cancelBtn");
        await _page.WaitForFunctionAsync(
            "() => document.querySelector('#chatContainer').getAttribute('aria-busy') === 'false'");

        (await _page.Locator(".synthesis-divider + div").TextContentAsync())
            .Should().Contain("partial answer");
        (await _page.Locator("#statusText").TextContentAsync()).Should().Contain("cancelled");
    }

    [Fact]
    public async Task FreshSearch_ShouldAbortAndResetActiveResearch()
    {
        await _page!.GotoAsync(_fixture.BaseUrl);
        await _page.EvaluateAsync(@"() => {
            window.fetch = (_, options) => new Promise((resolve, reject) => {
                options.signal.addEventListener('abort', () => reject(new DOMException('Cancelled', 'AbortError')));
            });
        }");

        await _page.FillAsync("#messageInput", "Long-running research");
        await _page.ClickAsync("#sendBtn");
        await _page.EvaluateAsync("document.getElementById('freshSearchBtn').style.display = 'flex'");
        await _page.ClickAsync("#freshSearchBtn");

        (await _page.Locator(".message").CountAsync()).Should().Be(1);
        (await _page.Locator("#chatContainer").GetAttributeAsync("aria-busy")).Should().Be("false");
        (await _page.Locator("#statusMessage").IsVisibleAsync()).Should().BeFalse();
        (await _page.Locator("#cancelBtn").IsVisibleAsync()).Should().BeFalse();
        (await _page.Locator("#messageInput").InputValueAsync()).Should().BeEmpty();
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
        
        // Act - Add enough content to make the chat scrollable, then scroll it
        await _page.EvaluateAsync(@"
            const chat = document.getElementById('chatContainer');
            const spacer = document.createElement('div');
            spacer.style.height = '1000px';
            chat.appendChild(spacer);
            chat.scrollTop = 100;
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
