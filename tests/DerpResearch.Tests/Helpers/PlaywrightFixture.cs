using Microsoft.Playwright;

namespace DerpResearch.Tests.Helpers;

/// <summary>
/// Fixture for Playwright browser setup and teardown
/// Implements IAsyncLifetime for async initialization
/// </summary>
public class PlaywrightFixture : IAsyncDisposable
{
    public IPlaywright? Playwright { get; private set; }
    public IBrowser? Browser { get; private set; }
    public string BaseUrl { get; set; } = "http://localhost:5000";

    public async Task InitializeAsync()
    {
        // Install Playwright browsers if needed
        // Note: Run "pwsh bin/Debug/net9.0/playwright.ps1 install" to install browsers
        
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        
        Browser = await Playwright.Chromium.LaunchAsync(new()
        {
            Headless = true, // Run headless in CI/CD
            SlowMo = 0 // Can be increased for debugging (milliseconds)
        });
    }

    public async Task<IPage> CreatePageAsync()
    {
        if (Browser == null)
        {
            await InitializeAsync();
        }
        
        var context = await Browser!.NewContextAsync(new()
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
        });
        
        var page = await context.NewPageAsync();
        return page;
    }

    public async Task<IPage> CreateMobilePageAsync()
    {
        if (Browser == null)
        {
            await InitializeAsync();
        }
        
        var context = await Browser!.NewContextAsync(new()
        {
            ViewportSize = new ViewportSize { Width = 375, Height = 667 }, // iPhone SE size
            UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 14_0 like Mac OS X) AppleWebKit/605.1.15",
            IsMobile = true,
            HasTouch = true
        });
        
        var page = await context.NewPageAsync();
        return page;
    }

    public async Task<IPage> CreateTabletPageAsync()
    {
        if (Browser == null)
        {
            await InitializeAsync();
        }
        
        var context = await Browser!.NewContextAsync(new()
        {
            ViewportSize = new ViewportSize { Width = 768, Height = 1024 }, // iPad size
            UserAgent = "Mozilla/5.0 (iPad; CPU OS 14_0 like Mac OS X) AppleWebKit/605.1.15",
            IsMobile = true,
            HasTouch = true
        });
        
        var page = await context.NewPageAsync();
        return page;
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser != null)
        {
            await Browser.DisposeAsync();
        }
        
        Playwright?.Dispose();
    }
}

/// <summary>
/// Collection fixture for sharing Playwright instance across tests
/// </summary>
[CollectionDefinition("Playwright")]
public class PlaywrightCollection : ICollectionFixture<PlaywrightFixture>
{
    // This class has no code, and is never created.
    // Its purpose is to be the place to apply [CollectionDefinition] and the ICollectionFixture<> interface.
}
