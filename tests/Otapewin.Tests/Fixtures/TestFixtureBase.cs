using AutoFixture;
using Microsoft.Extensions.Options;
using Otapewin;

namespace SecondBrain.Tests.Fixtures;

/// <summary>
/// AutoFixture customization for BrainConfig with valid test data.
/// </summary>
public sealed class BrainConfigCustomization : ICustomization
{
    private readonly string _tempDir;

    public BrainConfigCustomization(string tempDir)
    {
        _tempDir = tempDir;
    }

    public void Customize(IFixture fixture)
    {
        fixture.Register(() => new BrainConfig
        {
            OpenAIKey = "test-api-key",
            VaultPath = _tempDir,
            InputFile = "Inbox.md",
            Model = "gpt-4o-mini",
            ArchivePath = "Archive",
            ArchivePrefix = "Memory Archive - ",
            TagPrefix = "@ignore",
            FocusPath = "Focuses",
            FocusPrefix = "Weekly Focus - ",
            YourName = "TestUser",
            Tags = [],
            Prompts = new PromptConfig
            {
                DefaultTagPrompt = "default-prompt",
                DailyPrompt = "daily-prompt",
                WeeklyDefaultPrompt = "weekly-prompt",
                WeeklyCoachPrompt = "coach-prompt",
                WeeklyIntentionsPrompt = "intentions-prompt",
                BacklogReviewPrompt = "backlog-prompt"
            }
        });

        fixture.Register<IOptions<BrainConfig>>(() =>
       Options.Create(fixture.Create<BrainConfig>()));
    }
}

/// <summary>
/// Base test fixture with common dependencies.
/// </summary>
public abstract class TestFixtureBase : IDisposable
{
    protected readonly string TempDir;
    protected readonly IFixture Fixture;

    protected TestFixtureBase()
    {
        TempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempDir);

        Fixture = new Fixture();
        Fixture.Customize(new BrainConfigCustomization(TempDir));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                if (Directory.Exists(TempDir))
                {
                    Directory.Delete(TempDir, true);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
