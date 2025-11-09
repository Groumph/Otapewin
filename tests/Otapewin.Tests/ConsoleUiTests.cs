using FluentAssertions;
using Otapewin.Helpers;
using Xunit;

namespace SecondBrain.Tests;

public sealed class ConsoleUiTests
{
    [Fact]
    public void Title_ThrowsArgumentNullException_WhenTextIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => ConsoleUi.Title(null!));
        ex.ParamName.Should().Be("text");
    }

    [Fact]
    public void Title_ThrowsArgumentException_WhenTextIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => ConsoleUi.Title(""));
    }

    [Fact]
    public void Title_ThrowsArgumentException_WhenTextIsWhitespace()
    {
        Assert.Throws<ArgumentException>(() => ConsoleUi.Title("   "));
    }

    [Fact]
    public void Info_ThrowsArgumentNullException_WhenTextIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => ConsoleUi.Info(null!));
        ex.ParamName.Should().Be("text");
    }

    [Fact]
    public void Info_ThrowsArgumentException_WhenTextIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => ConsoleUi.Info(""));
    }

    [Fact]
    public void Success_ThrowsArgumentNullException_WhenTextIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => ConsoleUi.Success(null!));
        ex.ParamName.Should().Be("text");
    }

    [Fact]
    public void Success_ThrowsArgumentException_WhenTextIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => ConsoleUi.Success(""));
    }

    [Fact]
    public void Warn_ThrowsArgumentNullException_WhenTextIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => ConsoleUi.Warn(null!));
        ex.ParamName.Should().Be("text");
    }

    [Fact]
    public void Warn_ThrowsArgumentException_WhenTextIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => ConsoleUi.Warn(""));
    }

    [Fact]
    public void Error_ThrowsArgumentNullException_WhenTextIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => ConsoleUi.Error(null!));
        ex.ParamName.Should().Be("text");
    }

    [Fact]
    public void Error_ThrowsArgumentException_WhenTextIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => ConsoleUi.Error(""));
    }

    [Fact]
    public void Debug_ThrowsArgumentNullException_WhenTextIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => ConsoleUi.Debug(null!));
        ex.ParamName.Should().Be("text");
    }

    [Fact]
    public void Debug_ThrowsArgumentException_WhenTextIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => ConsoleUi.Debug(""));
    }

    [Fact]
    public void Status_ThrowsArgumentNullException_WhenMessageIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => ConsoleUi.Status<int>(null!, () => 42));
        ex.ParamName.Should().Be("message");
    }

    [Fact]
    public void Status_ThrowsArgumentException_WhenMessageIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => ConsoleUi.Status<int>("", () => 42));
    }

    [Fact]
    public void Status_ThrowsArgumentNullException_WhenActionIsNull()
    {
        Func<int>? action = null;
        Assert.Throws<ArgumentNullException>(() => ConsoleUi.Status<int>("message", action!));
    }

    [Fact]
    public void Status_ReturnsActionResult()
    {
        var result = ConsoleUi.Status("Testing", () => 42);
        result.Should().Be(42);
    }

    [Fact]
    public void Status_PropagatesException()
    {
        Assert.Throws<InvalidOperationException>(() =>
  ConsoleUi.Status<int>("Testing", () => throw new InvalidOperationException("Test error")));
    }

    [Fact]
    public async Task StatusAsync_ThrowsArgumentNullException_WhenMessageIsNull()
    {
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => ConsoleUi.StatusAsync<int>(null!, () => Task.FromResult(42)));
        ex.ParamName.Should().Be("message");
    }

    [Fact]
    public async Task StatusAsync_ThrowsArgumentException_WhenMessageIsEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => ConsoleUi.StatusAsync<int>("", () => Task.FromResult(42)));
    }

    [Fact]
    public async Task StatusAsync_ThrowsArgumentNullException_WhenActionIsNull()
    {
        Func<Task<int>>? action = null;
        await Assert.ThrowsAsync<ArgumentNullException>(() => ConsoleUi.StatusAsync("message", action!));
    }

    [Fact]
    public async Task StatusAsync_ReturnsActionResult()
    {
        var result = await ConsoleUi.StatusAsync("Testing", () => Task.FromResult(42));
        result.Should().Be(42);
    }

    [Fact]
    public async Task StatusAsync_PropagatesException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
  ConsoleUi.StatusAsync<int>("Testing", () => throw new InvalidOperationException("Test error")));
    }

    [Fact]
    public void TagCounts_ThrowsArgumentNullException_WhenCountsIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => ConsoleUi.TagCounts(null!));
    }

    [Fact]
    public void TagCounts_HandlesEmptyDictionary()
    {
        var counts = new Dictionary<string, int>();
        ConsoleUi.TagCounts(counts);
    }

    [Fact]
    public void TagCounts_DisplaysCounts()
    {
        var counts = new Dictionary<string, int>
        {
            ["task"] = 10,
            ["sleep"] = 5
        };
        ConsoleUi.TagCounts(counts);
    }

    [Fact]
    public void Progress_HandlesZeroTotal()
    {
        ConsoleUi.Progress(0, 0);
    }

    [Fact]
    public void Progress_HandlesNegativeTotal()
    {
        ConsoleUi.Progress(5, -1);
    }

    [Fact]
    public void Progress_DisplaysProgress()
    {
        ConsoleUi.Progress(50, 100);
    }

    [Fact]
    public void Progress_DisplaysProgressWithMessage()
    {
        ConsoleUi.Progress(50, 100, "Processing files");
    }

    [Fact]
    public void Progress_CompletesAtTotal()
    {
        ConsoleUi.Progress(100, 100);
    }

    [Fact]
    public void Separator_DisplaysSeparator()
    {
        try
        {
            ConsoleUi.Separator();
        }
        catch (IOException)
        {
            // Expected when no console available
        }
    }

    [Fact]
    public void Banner_DisplaysBanner()
    {
        ConsoleUi.Banner("Test App", "1.0.0");
    }
}
