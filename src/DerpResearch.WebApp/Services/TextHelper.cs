namespace DeepResearch.WebApp.Services;

internal static class TextHelper
{
    /// <summary>
    /// Return the leading maxLength characters of text, or the whole string if shorter.
    /// </summary>
    public static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength];
}
