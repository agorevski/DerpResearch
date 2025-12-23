using DeepResearch.WebApp.Models;
using FluentAssertions;

namespace DerpResearch.Tests.Unit.Models;

/// <summary>
/// Unit tests for strongly-typed value objects.
/// Validates the primitive obsession fix (anti-pattern #6).
/// </summary>
public class ValueObjectsTests
{
    #region ConversationId Tests

    [Fact]
    public void ConversationId_New_GeneratesValidGuid()
    {
        // Act
        var id = ConversationId.New();

        // Assert
        id.Value.Should().NotBeNullOrEmpty();
        Guid.TryParse(id.Value, out _).Should().BeTrue();
    }

    [Fact]
    public void ConversationId_Parse_CreatesFromString()
    {
        // Arrange
        var expected = "test-conversation-123";

        // Act
        var id = ConversationId.Parse(expected);

        // Assert
        id.Value.Should().Be(expected);
    }

    [Fact]
    public void ConversationId_Constructor_ThrowsOnEmpty()
    {
        // Act & Assert
        var action = () => new ConversationId("");
        action.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void ConversationId_ImplicitConversion_ReturnsValue()
    {
        // Arrange
        var id = ConversationId.Parse("test-id");

        // Act
        string stringValue = id;

        // Assert
        stringValue.Should().Be("test-id");
    }

    [Fact]
    public void ConversationId_TryParse_ReturnsFalseForNull()
    {
        // Act
        var result = ConversationId.TryParse(null, out var id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ConversationId_TryParse_ReturnsTrueForValidString()
    {
        // Act
        var result = ConversationId.TryParse("valid-id", out var id);

        // Assert
        result.Should().BeTrue();
        id.Value.Should().Be("valid-id");
    }

    #endregion

    #region MessageId Tests

    [Fact]
    public void MessageId_New_GeneratesValidGuid()
    {
        // Act
        var id = MessageId.New();

        // Assert
        id.Value.Should().NotBeNullOrEmpty();
        Guid.TryParse(id.Value, out _).Should().BeTrue();
    }

    [Fact]
    public void MessageId_Constructor_ThrowsOnEmpty()
    {
        // Act & Assert
        var action = () => new MessageId("   ");
        action.Should().Throw<ArgumentException>();
    }

    #endregion

    #region MemoryId Tests

    [Fact]
    public void MemoryId_New_GeneratesValidGuid()
    {
        // Act
        var id = MemoryId.New();

        // Assert
        id.Value.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region VectorId Tests

    [Fact]
    public void VectorId_New_CreatesWithValue()
    {
        // Act
        var id = VectorId.New(42);

        // Assert
        id.Value.Should().Be(42);
    }

    [Fact]
    public void VectorId_Constructor_ThrowsOnNegative()
    {
        // Act & Assert
        var action = () => new VectorId(-1);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be negative*");
    }

    [Fact]
    public void VectorId_ImplicitConversion_ReturnsInt()
    {
        // Arrange
        var id = VectorId.New(100);

        // Act
        int intValue = id;

        // Assert
        intValue.Should().Be(100);
    }

    #endregion

    #region MessageRole Tests

    [Theory]
    [InlineData(MessageRole.System, "system")]
    [InlineData(MessageRole.User, "user")]
    [InlineData(MessageRole.Assistant, "assistant")]
    public void MessageRole_ToLowerString_ReturnsCorrectString(MessageRole role, string expected)
    {
        // Act
        var result = role.ToLowerString();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("system", MessageRole.System)]
    [InlineData("user", MessageRole.User)]
    [InlineData("assistant", MessageRole.Assistant)]
    [InlineData("SYSTEM", MessageRole.System)]
    [InlineData("User", MessageRole.User)]
    public void MessageRole_ParseMessageRole_ParsesCorrectly(string input, MessageRole expected)
    {
        // Act
        var result = MessageRoleExtensions.ParseMessageRole(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void MessageRole_ParseMessageRole_ThrowsOnInvalid()
    {
        // Act & Assert
        var action = () => MessageRoleExtensions.ParseMessageRole("invalid");
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid message role*");
    }

    [Theory]
    [InlineData("user", true, MessageRole.User)]
    [InlineData("invalid", false, MessageRole.User)]
    [InlineData(null, false, MessageRole.User)]
    [InlineData("", false, MessageRole.User)]
    public void MessageRole_TryParseMessageRole_ReturnsExpected(
        string? input, bool expectedResult, MessageRole expectedRole)
    {
        // Act
        var result = MessageRoleExtensions.TryParseMessageRole(input, out var role);

        // Assert
        result.Should().Be(expectedResult);
        if (expectedResult)
            role.Should().Be(expectedRole);
    }

    #endregion

    #region ConfidenceScore Tests

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void ConfidenceScore_ValidValues_CreatesSuccessfully(double value)
    {
        // Act
        var score = new ConfidenceScore(value);

        // Assert
        score.Value.Should().Be(value);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    public void ConfidenceScore_InvalidValues_ThrowsException(double value)
    {
        // Act & Assert
        var action = () => new ConfidenceScore(value);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*between 0 and 1*");
    }

    [Fact]
    public void ConfidenceScore_FromPercentage_ConvertsCorrectly()
    {
        // Act
        var score = ConfidenceScore.FromPercentage(85);

        // Assert
        score.Value.Should().Be(0.85);
    }

    [Theory]
    [InlineData(0.8, 0.7, true)]
    [InlineData(0.7, 0.7, true)]
    [InlineData(0.6, 0.7, false)]
    public void ConfidenceScore_IsHighConfidence_EvaluatesCorrectly(
        double value, double threshold, bool expected)
    {
        // Arrange
        var score = new ConfidenceScore(value);

        // Act & Assert
        score.IsHighConfidence(threshold).Should().Be(expected);
    }

    [Theory]
    [InlineData(0.4, 0.5, true)]
    [InlineData(0.5, 0.5, false)]
    [InlineData(0.6, 0.5, false)]
    public void ConfidenceScore_IsLowConfidence_EvaluatesCorrectly(
        double value, double threshold, bool expected)
    {
        // Arrange
        var score = new ConfidenceScore(value);

        // Act & Assert
        score.IsLowConfidence(threshold).Should().Be(expected);
    }

    [Fact]
    public void ConfidenceScore_ImplicitConversion_ReturnsDouble()
    {
        // Arrange
        var score = new ConfidenceScore(0.75);

        // Act
        double doubleValue = score;

        // Assert
        doubleValue.Should().Be(0.75);
    }

    [Fact]
    public void ConfidenceScore_ToString_FormatsAsPercentage()
    {
        // Arrange
        var score = new ConfidenceScore(0.85);

        // Act
        var result = score.ToString();

        // Assert
        result.Should().Contain("85");
    }

    #endregion

    #region ValidatedUrl Tests

    [Fact]
    public void ValidatedUrl_ValidUrl_CreatesSuccessfully()
    {
        // Act
        var url = new ValidatedUrl("https://example.com/path");

        // Assert
        url.Value.Should().Be("https://example.com/path");
        url.Uri.Should().NotBeNull();
        url.Uri.Host.Should().Be("example.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("relative/path/only")]
    public void ValidatedUrl_InvalidUrl_ThrowsException(string value)
    {
        // Act & Assert
        var action = () => new ValidatedUrl(value);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidatedUrl_TryCreate_ReturnsTrueForValid()
    {
        // Act
        var result = ValidatedUrl.TryCreate("https://example.com", out var url);

        // Assert
        result.Should().BeTrue();
        url.Value.Should().Be("https://example.com");
    }

    [Fact]
    public void ValidatedUrl_TryCreate_ReturnsFalseForInvalid()
    {
        // Act
        var result = ValidatedUrl.TryCreate("not-valid", out _);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region CorrelationId Tests

    [Fact]
    public void CorrelationId_New_GeneratesValidGuid()
    {
        // Act
        var id = CorrelationId.New();

        // Assert
        id.Value.Should().NotBeNullOrEmpty();
        Guid.TryParse(id.Value, out _).Should().BeTrue();
    }

    [Fact]
    public void CorrelationId_Parse_CreatesFromString()
    {
        // Arrange
        var expected = "correlation-123";

        // Act
        var id = CorrelationId.Parse(expected);

        // Assert
        id.Value.Should().Be(expected);
    }

    [Fact]
    public void CorrelationId_Constructor_ThrowsOnEmpty()
    {
        // Act & Assert
        var action = () => new CorrelationId("");
        action.Should().Throw<ArgumentException>();
    }

    #endregion
}
