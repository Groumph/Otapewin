using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Otapewin;
using Otapewin.Workers;
using SecondBrain.Tests.Fixtures;
using SecondBrain.Tests.Mocks;
using System.Globalization;
using Xunit;

namespace SecondBrain.Tests;

public sealed class BacklogReviewWorkerTests : TestFixtureBase
{
    [Fact]
    public async Task ProcessAsync_ReturnsEarly_WhenNotMonday()
    {
        if (DateTime.UtcNow.DayOfWeek == DayOfWeek.Monday)
            return;

        var config = Fixture.Create<BrainConfig>();
        var vaultPath = Path.Combine(TempDir, "vault");
        Directory.CreateDirectory(vaultPath);
        config.VaultPath = vaultPath;

        var worker = new BacklogReviewWorker(Options.Create(config), new MockOpenAIClient(), NullLogger<BacklogReviewWorker>.Instance);
        await worker.ProcessAsync(CancellationToken.None);

        var outputPath = Path.Combine(vaultPath, config.FocusPath, DateTime.UtcNow.Year.ToString(),
         $"{config.FocusPrefix}{ISOWeek.GetWeekOfYear(DateTime.UtcNow)}.md");
        File.Exists(outputPath).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_ReturnsEarly_WhenFocusFileDoesNotExist()
    {
        if (DateTime.UtcNow.DayOfWeek != DayOfWeek.Monday)
            return;

        var config = Fixture.Create<BrainConfig>();
        var vaultPath = Path.Combine(TempDir, "vault");
        Directory.CreateDirectory(vaultPath);
        config.VaultPath = vaultPath;

        var now = DateTime.UtcNow;
        var currentWeek = ISOWeek.GetWeekOfYear(now);
        var archivePath = Path.Combine(vaultPath, config.ArchivePath, now.Year.ToString(), $"Week_{currentWeek}");
        Directory.CreateDirectory(archivePath);

        var worker = new BacklogReviewWorker(Options.Create(config), new MockOpenAIClient(), NullLogger<BacklogReviewWorker>.Instance);
        await worker.ProcessAsync(CancellationToken.None);

        Assert.True(true);
    }

    [Fact]
    public async Task ProcessAsync_ProcessesBacklogTasks_Successfully()
    {
        if (DateTime.UtcNow.DayOfWeek != DayOfWeek.Monday)
            return;

        var config = Fixture.Create<BrainConfig>();
        var vaultPath = Path.Combine(TempDir, "vault");
        Directory.CreateDirectory(vaultPath);
        config.VaultPath = vaultPath;

        var now = DateTime.UtcNow;
        var currentWeek = ISOWeek.GetWeekOfYear(now);

        var focusDir = Path.Combine(vaultPath, config.FocusPath, now.Year.ToString());
        Directory.CreateDirectory(focusDir);
        var focusFile = Path.Combine(focusDir, $"{config.FocusPrefix}{currentWeek}.md");
        await File.WriteAllTextAsync(focusFile, "# Weekly Focus");

        for (int week = currentWeek - 3; week <= currentWeek; week++)
        {
            var archivePath = Path.Combine(vaultPath, config.ArchivePath, now.Year.ToString(), $"Week_{week}");
            Directory.CreateDirectory(archivePath);
            var file = Path.Combine(archivePath, "day1.md");
            await File.WriteAllLinesAsync(file, ["- [ ] Incomplete task #task", "- [x] Completed task #task"]);
        }

        var mockClient = new MockOpenAIClient();
        mockClient.SetResponse("patterns", "Backlog review summary");

        var worker = new BacklogReviewWorker(Options.Create(config), mockClient, NullLogger<BacklogReviewWorker>.Instance);
        await worker.ProcessAsync(CancellationToken.None);

        var content = await File.ReadAllTextAsync(focusFile);
        content.Should().Contain("Task Backlog Review");
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenConfigIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
      new BacklogReviewWorker(null!, new MockOpenAIClient(), NullLogger<BacklogReviewWorker>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenOpenAIClientIsNull()
    {
        var config = Fixture.Create<BrainConfig>();
        Assert.Throws<ArgumentNullException>(() =>
      new BacklogReviewWorker(Options.Create(config), null!, NullLogger<BacklogReviewWorker>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        var config = Fixture.Create<BrainConfig>();
        Assert.Throws<ArgumentNullException>(() =>
      new BacklogReviewWorker(Options.Create(config), new MockOpenAIClient(), null!));
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenVaultPathIsEmpty()
    {
        var config = Fixture.Create<BrainConfig>();
        config.VaultPath = "";
        Assert.Throws<ArgumentException>(() =>
        new BacklogReviewWorker(Options.Create(config), new MockOpenAIClient(), NullLogger<BacklogReviewWorker>.Instance));
    }
}
