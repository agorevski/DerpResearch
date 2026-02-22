namespace DeepResearch.WebApp.Services;

/// <summary>
/// Sanitizes user input before inclusion in LLM prompts to mitigate prompt injection.
/// </summary>
public static class PromptSanitizer
{
    /// <summary>
    /// Sanitizes user input for safe inclusion in LLM prompts.
    /// Strips control characters, escapes prompt delimiters, and enforces length limits.
    /// </summary>
    public static string Sanitize(string input, int maxLength = 10000)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Truncate to max length
        if (input.Length > maxLength)
            input = input[..maxLength];

        // Remove control characters (except newlines and tabs which may be legitimate)
        input = new string(input.Where(c => !char.IsControl(c) || c == '\n' || c == '\r' || c == '\t').ToArray());

        // Escape sequences that could be used for prompt injection
        // Replace common prompt boundary markers
        input = input.Replace("```", "'''");
        input = input.Replace("---", "___");
        
        // Escape triple quotes that might break prompt boundaries
        input = input.Replace("\"\"\"", "'''");

        return input.Trim();
    }
}
