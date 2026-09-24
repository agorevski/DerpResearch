using DeepResearch.WebApp.Interfaces;
using DeepResearch.WebApp.Models;

namespace DeepResearch.WebApp.Services;

public static class LLMProviderRegistration
{
    public static IServiceCollection AddSelectedLLMProvider(
        this IServiceCollection services, IConfiguration configuration, bool useMockServices)
    {
        if (useMockServices)
        {
            return services;
        }

        var provider = configuration.GetSection(LLMConfiguration.Section).Get<LLMConfiguration>()?.Provider
            ?? new LLMConfiguration().Provider;

        if (provider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
        {
            var config = configuration.GetSection(AzureOpenAIConfiguration.Section).Get<AzureOpenAIConfiguration>()
                ?? new AzureOpenAIConfiguration();
            ValidateUrl(config.Endpoint, "AzureOpenAI:Endpoint");
            ValidateKey(config.ApiKey, "AzureOpenAI:ApiKey");
            ValidateModel(config.Deployments?.Chat, "AzureOpenAI:Deployments:Chat");
            ValidateModel(config.Deployments?.ChatMini, "AzureOpenAI:Deployments:ChatMini");
            ValidateModel(config.Deployments?.Embedding, "AzureOpenAI:Deployments:Embedding");
            services.Configure<AzureOpenAIConfiguration>(configuration.GetSection(AzureOpenAIConfiguration.Section));
            services.AddSingleton<ILLMProvider, AzureOpenAIProvider>();
        }
        else if (provider.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase) ||
                 provider.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase))
        {
            var config = configuration.GetSection(OpenAICompatibleConfiguration.Section).Get<OpenAICompatibleConfiguration>()
                ?? new OpenAICompatibleConfiguration();
            ValidateUrl(config.BaseUrl, "OpenAICompatible:BaseUrl");
            ValidateKey(config.ApiKey, "OpenAICompatible:ApiKey");
            ValidateModel(config.Models?.Chat, "OpenAICompatible:Models:Chat");
            ValidateModel(config.Models?.ChatMini, "OpenAICompatible:Models:ChatMini");
            ValidateModel(config.Models?.Embedding, "OpenAICompatible:Models:Embedding");

            if (!string.IsNullOrEmpty(config.Embeddings?.BaseUrl))
            {
                ValidateUrl(config.Embeddings.BaseUrl, "OpenAICompatible:Embeddings:BaseUrl");
            }
            if (!string.IsNullOrEmpty(config.Embeddings?.ApiKey))
            {
                ValidateKey(config.Embeddings.ApiKey, "OpenAICompatible:Embeddings:ApiKey");
            }

            services.Configure<OpenAICompatibleConfiguration>(
                configuration.GetSection(OpenAICompatibleConfiguration.Section));
            services.AddHttpClient();
            services.AddSingleton<ILLMProvider, OpenAICompatibleProvider>();
        }
        else
        {
            throw new InvalidOperationException(
                "LLM:Provider must be AzureOpenAI, OpenAICompatible, or OpenRouter.");
        }

        return services;
    }

    private static void ValidateUrl(string? value, string setting)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException($"{setting} must be an absolute HTTP(S) API root without credentials, query, or fragment.");
        }
    }

    private static void ValidateKey(string? value, string setting)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() ||
            value.Contains('\r') || value.Contains('\n'))
        {
            throw new InvalidOperationException($"{setting} must be a non-empty API key.");
        }
    }

    private static void ValidateModel(string? value, string setting)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
        {
            throw new InvalidOperationException($"{setting} must be a non-empty model ID.");
        }
    }
}
