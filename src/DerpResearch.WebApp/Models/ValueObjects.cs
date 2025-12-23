namespace DeepResearch.WebApp.Models;

/// <summary>
/// Strongly-typed identifier for conversations.
/// Prevents primitive obsession by encapsulating conversation ID logic.
/// </summary>
public readonly record struct ConversationId
{
    public string Value { get; }

    public ConversationId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ConversationId cannot be empty", nameof(value));
        Value = value;
    }

    public static ConversationId New() => new(Guid.NewGuid().ToString());
    public static ConversationId Parse(string value) => new(value);
    public static bool TryParse(string? value, out ConversationId result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = default;
            return false;
        }
        result = new ConversationId(value);
        return true;
    }

    public static implicit operator string(ConversationId id) => id.Value;
    public override string ToString() => Value;
}

/// <summary>
/// Strongly-typed identifier for messages.
/// </summary>
public readonly record struct MessageId
{
    public string Value { get; }

    public MessageId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("MessageId cannot be empty", nameof(value));
        Value = value;
    }

    public static MessageId New() => new(Guid.NewGuid().ToString());
    public static MessageId Parse(string value) => new(value);

    public static implicit operator string(MessageId id) => id.Value;
    public override string ToString() => Value;
}

/// <summary>
/// Strongly-typed identifier for memory chunks.
/// </summary>
public readonly record struct MemoryId
{
    public string Value { get; }

    public MemoryId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("MemoryId cannot be empty", nameof(value));
        Value = value;
    }

    public static MemoryId New() => new(Guid.NewGuid().ToString());
    public static MemoryId Parse(string value) => new(value);

    public static implicit operator string(MemoryId id) => id.Value;
    public override string ToString() => Value;
}

/// <summary>
/// Strongly-typed identifier for vector IDs in FAISS index.
/// </summary>
public readonly record struct VectorId
{
    public int Value { get; }

    public VectorId(int value)
    {
        if (value < 0)
            throw new ArgumentException("VectorId cannot be negative", nameof(value));
        Value = value;
    }

    public static VectorId New(int value) => new(value);

    public static implicit operator int(VectorId id) => id.Value;
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Strongly-typed roles for chat messages.
/// Replaces magic strings like "user", "assistant", "system".
/// </summary>
public enum MessageRole
{
    System,
    User,
    Assistant
}

/// <summary>
/// Extension methods for MessageRole enum.
/// </summary>
public static class MessageRoleExtensions
{
    public static string ToLowerString(this MessageRole role) => role switch
    {
        MessageRole.System => "system",
        MessageRole.User => "user",
        MessageRole.Assistant => "assistant",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    public static MessageRole ParseMessageRole(string role) => role.ToLowerInvariant() switch
    {
        "system" => MessageRole.System,
        "user" => MessageRole.User,
        "assistant" => MessageRole.Assistant,
        _ => throw new ArgumentException($"Invalid message role: {role}", nameof(role))
    };

    public static bool TryParseMessageRole(string? role, out MessageRole result)
    {
        result = MessageRole.User;
        if (string.IsNullOrWhiteSpace(role))
            return false;

        switch (role.ToLowerInvariant())
        {
            case "system":
                result = MessageRole.System;
                return true;
            case "user":
                result = MessageRole.User;
                return true;
            case "assistant":
                result = MessageRole.Assistant;
                return true;
            default:
                return false;
        }
    }
}

/// <summary>
/// Value object for confidence scores (0.0 to 1.0 range).
/// Encapsulates validation and behavior for confidence values.
/// </summary>
public readonly record struct ConfidenceScore
{
    public double Value { get; }

    public ConfidenceScore(double value)
    {
        if (value < 0 || value > 1)
            throw new ArgumentException("Confidence score must be between 0 and 1", nameof(value));
        Value = value;
    }

    public static ConfidenceScore FromPercentage(double percentage) => 
        new(percentage / 100.0);

    public bool IsHighConfidence(double threshold = 0.7) => Value >= threshold;
    public bool IsLowConfidence(double threshold = 0.5) => Value < threshold;
    public bool MeetsThreshold(double threshold) => Value >= threshold;

    public static implicit operator double(ConfidenceScore score) => score.Value;
    public override string ToString() => $"{Value:P0}";
}

/// <summary>
/// Value object for validated URLs.
/// </summary>
public readonly record struct ValidatedUrl
{
    public string Value { get; }
    public Uri Uri { get; }

    public ValidatedUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("URL cannot be empty", nameof(value));

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            throw new ArgumentException($"Invalid URL: {value}", nameof(value));

        Value = value;
        Uri = uri;
    }

    public static bool TryCreate(string? value, out ValidatedUrl result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
            return false;

        result = new ValidatedUrl(value);
        return true;
    }

    public static implicit operator string(ValidatedUrl url) => url.Value;
    public override string ToString() => Value;
}

/// <summary>
/// Value object for correlation IDs used in distributed tracing.
/// </summary>
public readonly record struct CorrelationId
{
    public string Value { get; }

    public CorrelationId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("CorrelationId cannot be empty", nameof(value));
        Value = value;
    }

    public static CorrelationId New() => new(Guid.NewGuid().ToString());
    public static CorrelationId Parse(string value) => new(value);
    public static bool TryParse(string? value, out CorrelationId result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = default;
            return false;
        }
        result = new CorrelationId(value);
        return true;
    }

    public static implicit operator string(CorrelationId id) => id.Value;
    public override string ToString() => Value;
}
