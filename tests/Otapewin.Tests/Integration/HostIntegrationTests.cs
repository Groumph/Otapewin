using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Otapewin;
using Otapewin.Clients;
using SecondBrain.Tests.Mocks;
using Otapewin.Workers;
using Xunit;

namespace SecondBrain.Tests.Integration;

/// <summary>
/// Integration tests for the complete DI container and configuration.
/// </summary>
public sealed class HostIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _vaultPath;

    public HostIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _vaultPath = Path.Combine(_tempDir, "vault");
        Directory.CreateDirectory(_vaultPath);
    }

    [Fact]
    public void Host_ShouldResolveAllServices()
    {
        // Arrange & Act
        var host = CreateTestHost();

        // Assert
        host.Services.GetRequiredService<IOptions<BrainConfig>>().Should().NotBeNull();
        host.Services.GetRequiredService<DailyWorker>().Should().NotBeNull();
        host.Services.GetRequiredService<WeeklyWorker>().Should().NotBeNull();
        host.Services.GetRequiredService<BacklogReviewWorker>().Should().NotBeNull();
        host.Services.GetRequiredService<IOpenAIClient>().Should().NotBeNull();
    }

    [Fact]
    public void Configuration_ShouldValidateOnStartup()
    {
        // Arrange
        var invalidConfigJson = @"{
   ""OpenAIKey"": """",
   ""VaultPath"": """",
   ""InputFile"": """"
 }";

        var configFile = Path.Combine(_tempDir, "invalid-appsettings.json");
        File.WriteAllText(configFile, invalidConfigJson);

        // Act & Assert
        var act = () =>
 {
     var host = Host.CreateDefaultBuilder()
       .ConfigureAppConfiguration(config =>
         {
             config.Sources.Clear();
             config.AddJsonFile(configFile);
         })
         .ConfigureServices((context, services) =>
     {
         services.AddOptions<BrainConfig>()
   .Bind(context.Configuration)
  .ValidateDataAnnotations()
.ValidateOnStart();

         services.AddTransient<DailyWorker>();
         services.AddSingleton<IOpenAIClient, MockOpenAIClient>();
         services.AddLogging();
     })
        .Build();

     // This should throw on validation
     var _ = host.Services.GetRequiredService<IOptions<BrainConfig>>().Value;
 };

        act.Should().Throw<OptionsValidationException>("invalid configuration should fail validation");
    }

    [Fact]
    public async Task DailyWorker_ShouldWorkThroughDI()
    {
        // Arrange
        var host = CreateTestHost();
        var worker = host.Services.GetRequiredService<DailyWorker>();

        var inputPath = Path.Combine(_vaultPath, "Inbox.md");
        File.WriteAllText(inputPath, "Test content\n- [ ] Task #task");

        // Act
        await worker.ProcessAsync(CancellationToken.None);

        // Assert
        var isoWeek = System.Globalization.ISOWeek.GetWeekOfYear(DateTime.UtcNow.Date);
        var focusPath = Path.Combine(_vaultPath, "Focuses", DateTime.UtcNow.Year.ToString(), $"Weekly Focus - {isoWeek}.md");

        File.Exists(focusPath).Should().BeTrue("daily worker should create focus file");
    }

    private IHost CreateTestHost()
    {
        var configJson = $$"""
{
    "OpenAIKey": "test-key",
    "VaultPath": "{{_vaultPath.Replace("\\", "\\\\")}}",
    "InputFile": "Inbox.md",
    "Model": "gpt-4o-mini",
  "ArchivePath": "Archive",
    "ArchivePrefix": "Memory Archive - ",
    "TagPrefix": "@ignore",
    "FocusPath": "Focuses",
    "FocusPrefix": "Weekly Focus - ",
    "YourName": "TestUser",
    "Tags": [],
    "Prompts": {
        "DefaultTagPrompt": "default",
        "DailyPrompt": "daily",
        "WeeklyDefaultPrompt": "weekly",
    "WeeklyCoachPrompt": "coach",
        "WeeklyIntentionsPrompt": "intentions",
        "BacklogReviewPrompt": "backlog"
    }
}
""";
        var configFile = Path.Combine(_tempDir, "appsettings.json");
        File.WriteAllText(configFile, configJson);

        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.Sources.Clear();
                config.AddJsonFile(configFile);
            })
  .ConfigureServices((context, services) =>
   {
       services.AddOptions<BrainConfig>()
         .Bind(context.Configuration)
                .ValidateDataAnnotations()
                .ValidateOnStart();

       services.AddTransient<DailyWorker>();
       services.AddTransient<WeeklyWorker>();
       services.AddTransient<BacklogReviewWorker>();

       // Use mock for testing
       services.AddSingleton<IOpenAIClient, MockOpenAIClient>();

       services.AddLogging(logging =>
                  {
                      logging.SetMinimumLevel(LogLevel.Debug);
                      logging.AddConsole();
                  });
   })
 .Build();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
