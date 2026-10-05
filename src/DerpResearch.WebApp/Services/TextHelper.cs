namespace DeepResearch.WebApp.Services;

internal static class TextHelper
{
    /// <summary>
    /// Return the leading maxLength characters of text, or the whole string if shorter.
    /// </summary>
    public static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength];

    /// <summary>
    /// Remove a leading ``` or ```json code fence and its matching trailing fence, if present.
    /// </summary>
    public static string StripCodeFence(string text)
    {
        var result = text.Trim();
        var fenced = false;
        if (result.StartsWith("```json", StringComparison.Ordinal))
        {
            result = result["```json".Length..];
            fenced = true;
        }
        else if (result.StartsWith("```", StringComparison.Ordinal))
        {
            result = result["```".Length..];
            fenced = true;
        }
        if (fenced && result.EndsWith("```", StringComparison.Ordinal))
        {
            result = result[..^"```".Length];
        }
        return result;
    }
}
