namespace DeepResearch.WebApp.Services;

internal static class RetryHelper
{
    /// <summary>
    /// Exponential backoff delay for a 1-based retry attempt: 2^attempt seconds.
    /// </summary>
    public static TimeSpan GetBackoffDelay(int attempt)
        => TimeSpan.FromSeconds(Math.Pow(2, attempt));
}
