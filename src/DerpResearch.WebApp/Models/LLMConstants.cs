namespace DeepResearch.WebApp.Models;

/// <summary>
/// Shared defaults for LLM calls. Centralizes the default deployment/model name
/// that was previously hardcoded across the LLM service interfaces and implementations.
/// </summary>
public static class LLMConstants
{
    /// <summary>Default chat deployment/model name used when a caller does not specify one.</summary>
    public const string DefaultDeploymentName = "gpt-4o";
}
