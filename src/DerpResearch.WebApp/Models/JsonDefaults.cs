using System.Text.Json;

namespace DeepResearch.WebApp.Models;

/// <summary>
/// Shared JsonSerializerOptions. The camelCase instance was previously duplicated
/// across the controller and several services; a single cached instance is also
/// what System.Text.Json recommends for performance.
/// </summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
