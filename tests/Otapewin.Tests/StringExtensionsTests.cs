using FluentAssertions;
using Otapewin.Extensions;
using Xunit;

namespace SecondBrain.Tests;

public sealed class StringExtensionsTests
{
    [Fact]
    public void ReplaceWithName_ReplacesPlaceholder_WithProvidedName()
    {
        var input = "Hello {userName}, welcome!";
        var result = input.ReplaceWithName("Alice");
        result.Should().Be("Hello Alice, welcome!");
    }

    [Fact]
    public void ReplaceWithName_IsCaseInsensitive()
    {
        var input = "Hello {USERNAME}, welcome!";
        var result = input.ReplaceWithName("Bob");
        result.Should().Be("Hello Bob, welcome!");
    }

    [Fact]
    public void ReplaceWithName_ReturnsEmptyString_WhenInputIsNull()
    {
        string? input = null;
        var result = input.ReplaceWithName("Charlie");
        result.Should().BeEmpty();
    }

    [Fact]
    public void ReplaceWithName_ReturnsEmptyString_WhenInputIsEmpty()
    {
        var input = "";
        var result = input.ReplaceWithName("Dave");
        result.Should().BeEmpty();
    }

    [Fact]
    public void ReplaceWithName_ReturnsOriginal_WhenNameIsNull()
    {
        var input = "Hello {userName}";
        var result = input.ReplaceWithName(null);
        result.Should().Be("Hello {userName}");
    }

    [Fact]
    public void ReplaceWithName_ReturnsOriginal_WhenNameIsEmpty()
    {
        var input = "Hello {userName}";
        var result = input.ReplaceWithName("");
        result.Should().Be("Hello {userName}");
    }

    [Fact]
    public void ReplaceWithName_ReplacesMultipleOccurrences()
    {
        var input = "{userName}, meet {userName}!";
        var result = input.ReplaceWithName("Eve");
        result.Should().Be("Eve, meet Eve!");
    }

    [Fact]
    public void ReplaceWithName_HandlesNoPlaceholder()
    {
        var input = "Hello world!";
        var result = input.ReplaceWithName("Frank");
        result.Should().Be("Hello world!");
    }
}
