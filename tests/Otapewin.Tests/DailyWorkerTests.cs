using System.Globalization;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Otapewin;
using SecondBrain.Tests.Fixtures;
using SecondBrain.Tests.Mocks;
using Otapewin.Workers;
using Xunit;

namespace SecondBrain.Tests;

public sealed class DailyWorkerTests : TestFixtureBase
{
    [Fact]
    public async Task ProcessAsync_WithNonExistentFile_ReturnsEarly()
    {
        var config = Fixture.Create<BrainConfig>();
        var vaultPath = Path.Combine(TempDir, "vault");
        Directory.CreateDirectory(vaultPath);
        config.VaultPath = vaultPath;

        var worker = new DailyWorker(Options.Create(config), new MockOpenAIClient(), NullLogger<DailyWorker>.Instance);
        await worker.ProcessAsync(CancellationToken.None);

        var focusPath = Path.Combine(vaultPath, config.FocusPath);
        Directory.Exists(focusPath).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_WithEmptyInbox_ReturnsEarly()
    {
        var config = Fixture.Create<BrainConfig>();
        var vaultPath = Path.Combine(TempDir, "vault");
        Directory.CreateDirectory(vaultPath);
        config.VaultPath = vaultPath;

        var inputPath = Path.Combine(vaultPath, config.InputFile);
        File.WriteAllText(inputPath, string.Empty);

        var worker = new DailyWorker(Options.Create(config), new MockOpenAIClient(), NullLogger<DailyWorker>.Instance);
        await worker.ProcessAsync(CancellationToken.None);

        var focusDir = Path.Combine(vaultPath, config.FocusPath);
        if (Directory.Exists(focusDir))
        {
            Directory.GetFiles(focusDir, "*", SearchOption.AllDirectories).Should().BeEmpty();
        }
    }

    [Fact]
    public async Task ProcessAsync_WithValidContent_CreatesOutputFiles()
    {
        var config = Fixture.Create<BrainConfig>();
        var vaultPath = Path.Combine(TempDir, "vault");
        Directory.CreateDirectory(vaultPath);
        config.VaultPath = vaultPath;
        config.Tags = [new TagConfig { Name = "task", Prompt = "Summarize tasks" }];

        var inputPath = Path.Combine(vaultPath, config.InputFile);
        await File.WriteAllTextAsync(inputPath, "Test content #task\nAnother line");

        var mockClient = new MockOpenAIClient();
        mockClient.SetResponse("summarize", "Test summary");

        var worker = new DailyWorker(Options.Create(config), mockClient, NullLogger<DailyWorker>.Instance);
        await worker.ProcessAsync(CancellationToken.None);

        var isoWeek = ISOWeek.GetWeekOfYear(DateTime.UtcNow.Date);
        var focusPath = Path.Combine(vaultPath, config.FocusPath, DateTime.UtcNow.Year.ToString(), $"{config.FocusPrefix}{isoWeek}.md");

        File.Exists(focusPath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(focusPath);
        content.Should().Contain("Test summary");
    }

    [Fact]
    public async Task ProcessAsync_WithIgnoredLines_FiltersCorrectly()
    {
        var config = Fixture.Create<BrainConfig>();
        var vaultPath = Path.Combine(TempDir, "vault");
        Directory.CreateDirectory(vaultPath);
        config.VaultPath = vaultPath;
        config.TagPrefix = "@ignore";

        var inputPath = Path.Combine(vaultPath, config.InputFile);
        await File.WriteAllTextAsync(inputPath, "Normal line\n@ignore This should stay\nAnother normal line");

        var mockClient = new MockOpenAIClient();
        mockClient.SetResponse("summarize", "Summary");

        var worker = new DailyWorker(Options.Create(config), mockClient, NullLogger<DailyWorker>.Instance);
        await worker.ProcessAsync(CancellationToken.None);

        var remainingContent = await File.ReadAllTextAsync(inputPath);
        remainingContent.Should().Contain("@ignore This should stay");
        remainingContent.Should().NotContain("Normal line");
    }

    [Fact]
    public async Task ProcessAsync_WithLookupTag_ProcessesLookups()
    {
        var config = Fixture.Create<BrainConfig>();
        var vaultPath = Path.Combine(TempDir, "vault");
        Directory.CreateDirectory(vaultPath);
        config.VaultPath = vaultPath;
        config.Tags = [new TagConfig { Name = "lookup", Prompt = null }];

        var inputPath = Path.Combine(vaultPath, config.InputFile);
        await File.WriteAllTextAsync(inputPath, "What is AI? #lookup");

        var mockClient = new MockOpenAIClient();
        mockClient.SetResponse("lookup", "AI is artificial intelligence");

        var worker = new DailyWorker(Options.Create(config), mockClient, NullLogger<DailyWorker>.Instance);
        await worker.ProcessAsync(CancellationToken.None);

        var isoWeek = ISOWeek.GetWeekOfYear(DateTime.UtcNow.Date);
        var focusPath = Path.Combine(vaultPath, config.FocusPath, DateTime.UtcNow.Year.ToString(), $"{config.FocusPrefix}{isoWeek}.md");

        var content = await File.ReadAllTextAsync(focusPath);
        content.Should().Contain("Lookup Results");
    }

    [Fact]
    public async Task ProcessAsync_WithTasks_SeparatesCompletedAndPending()
    {
        var config = Fixture.Create<BrainConfig>();
        var vaultPath = Path.Combine(TempDir, "vault");
        Directory.CreateDirectory(vaultPath);
        config.VaultPath = vaultPath;
        config.Tags = [new TagConfig { Name = "task", Prompt = null }];

        var inputPath = Path.Combine(vaultPath, config.InputFile);
        await File.WriteAllTextAsync(inputPath, "- [x] Completed task #task\n- [ ] Pending task #task\n- [X] Another completed #task");

        var mockClient = new MockOpenAIClient();
        var worker = new DailyWorker(Options.Create(config), mockClient, NullLogger<DailyWorker>.Instance);
        await worker.ProcessAsync(CancellationToken.None);

        var isoWeek = ISOWeek.GetWeekOfYear(DateTime.UtcNow.Date);
        var focusPath = Path.Combine(vaultPath, config.FocusPath, DateTime.UtcNow.Year.ToString(), $"{config.FocusPrefix}{isoWeek}.md");

        var content = await File.ReadAllTextAsync(focusPath);
        content.Should().Contain("Completed:");
        content.Should().Contain("Pending:");
    }

    [Fact]
    public async Task ProcessAsync_WithNoGeneralContent_SkipsEmptySummary()
    {
        var config = Fixture.Create<BrainConfig>();
        var vaultPath = Path.Combine(TempDir, "vault");
        Directory.CreateDirectory(vaultPath);
        config.VaultPath = vaultPath;

        var inputPath = Path.Combine(vaultPath, config.InputFile);
        await File.WriteAllTextAsync(inputPath, "- [x] Only completed tasks");

        var mockClient = new MockOpenAIClient();
        var worker = new DailyWorker(Options.Create(config), mockClient, NullLogger<DailyWorker>.Instance);
        await worker.ProcessAsync(CancellationToken.None);

        var isoWeek = ISOWeek.GetWeekOfYear(DateTime.UtcNow.Date);
        var focusPath = Path.Combine(vaultPath, config.FocusPath, DateTime.UtcNow.Year.ToString(), $"{config.FocusPrefix}{isoWeek}.md");

        File.Exists(focusPath).Should().BeTrue();
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenConfigIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
  new DailyWorker(null!, new MockOpenAIClient(), NullLogger<DailyWorker>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenOpenAIClientIsNull()
    {
        var config = Fixture.Create<BrainConfig>();
        Assert.Throws<ArgumentNullException>(() =>
            new DailyWorker(Options.Create(config), null!, NullLogger<DailyWorker>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        var config = Fixture.Create<BrainConfig>();
        Assert.Throws<ArgumentNullException>(() =>
            new DailyWorker(Options.Create(config), new MockOpenAIClient(), null!));
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenVaultPathIsEmpty()
    {
        var config = Fixture.Create<BrainConfig>();
        config.VaultPath = "";
        Assert.Throws<ArgumentException>(() =>
            new DailyWorker(Options.Create(config), new MockOpenAIClient(), NullLogger<DailyWorker>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenInputFileIsEmpty()
    {
        var config = Fixture.Create<BrainConfig>();
        config.InputFile = "";
        Assert.Throws<ArgumentException>(() =>
  new DailyWorker(Options.Create(config), new MockOpenAIClient(), NullLogger<DailyWorker>.Instance));
    }
}
