namespace DeepResearch.WebApp.Models;

/// <summary>
/// Shared constants for derpification level tier boundaries.
/// Ensures consistent thresholds across all agents.
/// </summary>
public static class DerpificationConstants
{
    /// <summary>Maximum level for "Derp" (simple/elementary) mode.</summary>
    public const int DerpMaxLevel = 33;
    
    /// <summary>Maximum level for "Average" (balanced) mode. Above this is "Smart" mode.</summary>
    public const int AverageMaxLevel = 66;
}
