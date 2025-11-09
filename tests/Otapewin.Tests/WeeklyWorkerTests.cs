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

public sealed class WeeklyWorkerTests : TestFixtureBase
{
    [Fact]
    public async Task ProcessAsync_ReturnsEarly_WhenNotMonday()
    {
        if (DateTime.UtcNow.DayOfWeek == DayOfWeek.Monday) return;

        var config = Fixture.Create<BrainConfig>();
        var vaultPath = Path.Combine(TempDir, "vault");
        Directory.CreateDirectory(vaultPath);
        config.VaultPath = vaultPath;

        var worker = new WeeklyWorker(Options.Create(config), new MockOpenAIClient(), NullLogger<WeeklyWorker>.Instance);
        await worker.ProcessAsync(CancellationToken.None);

        var week = ISOWeek.GetWeekOfYear(DateTime.UtcNow.Date) - 1;
        var outputPath = Path.Combine(vaultPath, config.FocusPath, DateTime.UtcNow.Year.ToString(), $"{config.FocusPrefix}{week}_Summary.md");
        File.Exists(outputPath).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_ReturnsEarly_WhenArchiveDirectoryDoesNotExist()
    {
        if (DateTime.UtcNow.DayOfWeek != DayOfWeek.Monday) return;

        var config = Fixture.Create<BrainConfig>();
        var vaultPath = Path.Combine(TempDir, "vault");
        Directory.CreateDirectory(vaultPath);
        config.VaultPath = vaultPath;

        var worker = new WeeklyWorker(Options.Create(config), new MockOpenAIClient(), NullLogger<WeeklyWorker>.Instance);
        await worker.ProcessAsync(CancellationToken.None);

        var week = ISOWeek.GetWeekOfYear(DateTime.UtcNow.Date) - 1;
        var outputPath = Path.Combine(vaultPath, config.FocusPath, DateTime.UtcNow.Year.ToString(), $"{config.FocusPrefix}{week}_Summary.md");
        File.Exists(outputPath).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_ReturnsEarly_WhenNoMarkdownFilesFound()
    {
        if (DateTime.UtcNow.DayOfWeek != DayOfWeek.Monday) return;

        var config = Fixture.Create<BrainConfig>();
        var vaultPath = Path.Combine(TempDir, "vault");
        Directory.CreateDirectory(vaultPath);
        config.VaultPath = vaultPath;

        var now = DateTime.UtcNow;
        var isoWeek = ISOWeek.GetWeekOfYear(now.Date) - 1;
        var archivePath = Path.Combine(vaultPath, config.ArchivePath, now.Year.ToString(), $"Week_{isoWeek}");
        Directory.CreateDirectory(archivePath);

        var worker = new WeeklyWorker(Options.Create(config), new MockOpenAIClient(), NullLogger<WeeklyWorker>.Instance);
        await worker.ProcessAsync(CancellationToken.None);

        var outputPath = Path.Combine(vaultPath, config.FocusPath, now.Year.ToString(), $"{config.FocusPrefix}{isoWeek}_Summary.md");
        File.Exists(outputPath).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_CreatesWeeklySummary_WithValidArchiveFiles()
    {
        if (DateTime.UtcNow.DayOfWeek != DayOfWeek.Monday) return;

        var config = Fixture.Create<BrainConfig>();
        var vaultPath = Path.Combine(TempDir, "vault");
        Directory.CreateDirectory(vaultPath);
        config.VaultPath = vaultPath;
        config.Tags = [new TagConfig { Name = "task", Prompt = "Summarize tasks" }];

        var now = DateTime.UtcNow;
        var isoWeek = ISOWeek.GetWeekOfYear(now.Date) - 1;
        var archivePath = Path.Combine(vaultPath, config.ArchivePath, now.Year.ToString(), $"Week_{isoWeek}");
        Directory.CreateDirectory(archivePath);

        var file1 = Path.Combine(archivePath, "day1.md");
        await File.WriteAllLinesAsync(file1, ["Some content #task", "More content"]);

        var mockClient = new MockOpenAIClient();
        mockClient.SetResponse("patterns", "Test summary");

        var worker = new WeeklyWorker(Options.Create(config), mockClient, NullLogger<WeeklyWorker>.Instance);
        await worker.ProcessAsync(CancellationToken.None);

        var outputPath = Path.Combine(vaultPath, config.FocusPath, now.Year.ToString(), $"{config.FocusPrefix}{isoWeek}_Summary.md");
        File.Exists(outputPath).Should().BeTrue();

        var content = await File.ReadAllTextAsync(outputPath);
        content.Should().Contain("Weekly Tag Summaries");
        content.Should().Contain("Weekly Coaching Reflection");
    }

    [Fact]
    public async Task ProcessAsync_GeneratesIntentions_WhenUnfinishedTasksExist()
    {
        if (DateTime.UtcNow.DayOfWeek != DayOfWeek.Monday) return;

        var config = Fixture.Create<BrainConfig>();
        var vaultPath = Path.Combine(TempDir, "vault");
        Directory.CreateDirectory(vaultPath);
        config.VaultPath = vaultPath;
        config.Tags = [new TagConfig { Name = "task", Prompt = "Summarize tasks" }];

        var now = DateTime.UtcNow;
        var isoWeek = ISOWeek.GetWeekOfYear(now.Date) - 1;
        var archivePath = Path.Combine(vaultPath, config.ArchivePath, now.Year.ToString(), $"Week_{isoWeek}");
        Directory.CreateDirectory(archivePath);

        var file = Path.Combine(archivePath, "tasks.md");
        await File.WriteAllLinesAsync(file, ["- [ ] Incomplete task #task", "- [x] Completed #task"]);

        var mockClient = new MockOpenAIClient();
        mockClient.SetResponse("patterns", "Summary");

        var worker = new WeeklyWorker(Options.Create(config), mockClient, NullLogger<WeeklyWorker>.Instance);
        await worker.ProcessAsync(CancellationToken.None);

        var outputPath = Path.Combine(vaultPath, config.FocusPath, now.Year.ToString(), $"{config.FocusPrefix}{isoWeek}_Summary.md");
        var content = await File.ReadAllTextAsync(outputPath);
        content.Should().Contain("Intentions for Next Week");
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenConfigIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WeeklyWorker(null!, new MockOpenAIClient(), NullLogger<WeeklyWorker>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenOpenAIClientIsNull()
    {
        var config = Fixture.Create<BrainConfig>();
        Assert.Throws<ArgumentNullException>(() =>
    new WeeklyWorker(Options.Create(config), null!, NullLogger<WeeklyWorker>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        var config = Fixture.Create<BrainConfig>();
        Assert.Throws<ArgumentNullException>(() =>
     new WeeklyWorker(Options.Create(config), new MockOpenAIClient(), null!));
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenVaultPathIsEmpty()
    {
        var config = Fixture.Create<BrainConfig>();
        config.VaultPath = "";
        Assert.Throws<ArgumentException>(() =>
        new WeeklyWorker(Options.Create(config), new MockOpenAIClient(), NullLogger<WeeklyWorker>.Instance));
    }
}
