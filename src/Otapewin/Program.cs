using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Otapewin.Clients;
using Otapewin.Helpers;
using Otapewin.Workers;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Otapewin;

internal static class Program
{
    private const string AppName = "Otapewin CLI";

    public static async Task<int> Main(string[] args)
    {
        // Show banner for interactive sessions
        if (!Console.IsOutputRedirected)
        {
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            string versionString = version?.ToString(3) ?? "1.0.0";
            ConsoleUi.Banner(AppName, versionString);
        }

        RootCommand rootCommand = new("Otapewin CLI - AI-powered task and note management")
        {
            CreateDailyCommand(),
            CreateWeeklyCommand(),
            CreateBacklogCommand()
        };

        // If no command is provided, run all three commands (daily, weekly, backlog)
        rootCommand.SetHandler(async context =>
        {
            IHost host = context.BindingContext.GetRequiredService<IHost>();
            IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Otapewin.RunAll");

            DailyWorker daily = host.Services.GetRequiredService<DailyWorker>();
            WeeklyWorker weekly = host.Services.GetRequiredService<WeeklyWorker>();
            BacklogReviewWorker backlog = host.Services.GetRequiredService<BacklogReviewWorker>();

            try
            {
                ConsoleUi.Title("Running All: Daily, Weekly, Backlog");
                LogRunningAllCommands(logger);

                // Run sequentially; each worker observes cancellation via lifetime token
                ConsoleUi.Title("Daily Processing");
                await daily.ProcessAsync(lifetime.ApplicationStopping);

                ConsoleUi.Title("Weekly Review");
                await weekly.ProcessAsync(lifetime.ApplicationStopping);

                ConsoleUi.Title("Backlog Review");
                await backlog.ProcessAsync(lifetime.ApplicationStopping);

                ConsoleUi.Success("All commands completed successfully");
                context.ExitCode = 0;
            }
            catch (OperationCanceledException)
            {
                ConsoleUi.Warn("Run-all cancelled by user");
                context.ExitCode = 130;
            }
            catch (Exception ex)
            {
                ConsoleUi.Error($"Run-all failed: {ex.Message}");
                LogRunAllFailed(logger, ex);
                context.ExitCode = 1;
            }
        });

        Parser commandLineBuilder = new CommandLineBuilder(rootCommand)
            .UseHost(
            _ => Host.CreateDefaultBuilder(args),
            host =>
            {
                _ = host.ConfigureServices((context, services) =>
                {
                    // Configuration with validation
                    OptionsBuilder<BrainConfig> optionsBuilder = services.AddOptions<BrainConfig>()
                        .Bind(context.Configuration);

                    _ = ValidateDataAnnotationsWithTrimSuppression(optionsBuilder);

                    // Register workers
                    _ = services.AddTransient<DailyWorker>();
                    _ = services.AddTransient<WeeklyWorker>();
                    _ = services.AddTransient<BacklogReviewWorker>();
                    // Register clients
                    _ = services.AddSingleton<IOpenAIClient, OpenAIClient>();
                    // OpenTelemetry
                    ConfigureOpenTelemetry(services);
                });

                _ = host.ConfigureLogging((context, logging) =>
                {
                    _ = logging.ClearProviders();
                    _ = logging.AddConsole();

                    if (context.HostingEnvironment.IsDevelopment())
                    {
                        _ = logging.AddDebug();
                    }
                });

                _ = host.ConfigureAppConfiguration((context, config) =>
                {
                    // Make local appsettings optional for public repo. Prefer environment variables.
                    _ = config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
                    _ = config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
                    _ = config.AddEnvironmentVariables(prefix: "OTAPEWIN_");
                    _ = config.AddCommandLine(args);
                });
            })
            .UseDefaults()
            .Build();

        return await commandLineBuilder.InvokeAsync(args);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Options validation uses data annotations which require reflection, but this is acceptable for CLI configuration")]
    [UnconditionalSuppressMessage("Trimming", "IL2091:'TOptions' generic argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicParameterlessConstructor'", Justification = "Options validation uses data annotations which require reflection, but this is acceptable for CLI configuration")]
    private static OptionsBuilder<BrainConfig> ValidateDataAnnotationsWithTrimSuppression(OptionsBuilder<BrainConfig> optionsBuilder) =>
        optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

    private static Command CreateDailyCommand()
    {
        Command command = new("daily", "Process daily tasks and notes");

        command.SetHandler(async context =>
        {
            IHost host = context.BindingContext.GetRequiredService<IHost>();
            ILogger<DailyWorker> logger = host.Services.GetRequiredService<ILogger<DailyWorker>>();
            DailyWorker worker = host.Services.GetRequiredService<DailyWorker>();
            IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

            try
            {
                ConsoleUi.Title("Daily Processing");
                LogDailyCommandStarting(logger);
                await worker.ProcessAsync(lifetime.ApplicationStopping);
                ConsoleUi.Success("Daily command completed successfully");
                context.ExitCode = 0;
            }
            catch (OperationCanceledException)
            {
                ConsoleUi.Warn("Daily command cancelled by user");
                context.ExitCode = 130;
            }
            catch (Exception ex)
            {
                ConsoleUi.Error($"Daily command failed: {ex.Message}");
                LogDailyCommandFailed(logger, ex);
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateWeeklyCommand()
    {
        Command command = new("weekly", "Generate weekly review and summary");

        command.SetHandler(async context =>
        {
            IHost host = context.BindingContext.GetRequiredService<IHost>();
            ILogger<WeeklyWorker> logger = host.Services.GetRequiredService<ILogger<WeeklyWorker>>();
            WeeklyWorker worker = host.Services.GetRequiredService<WeeklyWorker>();
            IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

            try
            {
                ConsoleUi.Title("Weekly Review");
                LogWeeklyCommandStarting(logger);
                await worker.ProcessAsync(lifetime.ApplicationStopping);
                ConsoleUi.Success("Weekly command completed successfully");
                context.ExitCode = 0;
            }
            catch (OperationCanceledException)
            {
                ConsoleUi.Warn("Weekly command cancelled by user");
                context.ExitCode = 130;
            }
            catch (Exception ex)
            {
                ConsoleUi.Error($"Weekly command failed: {ex.Message}");
                LogWeeklyCommandFailed(logger, ex);
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateBacklogCommand()
    {
        Command command = new("backlog", "Review and organize backlog items");

        command.SetHandler(async context =>
        {
            IHost host = context.BindingContext.GetRequiredService<IHost>();
            ILogger<BacklogReviewWorker> logger = host.Services.GetRequiredService<ILogger<BacklogReviewWorker>>();
            BacklogReviewWorker worker = host.Services.GetRequiredService<BacklogReviewWorker>();
            IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

            try
            {
                ConsoleUi.Title("Backlog Review");
                LogBacklogCommandStarting(logger);
                await worker.ProcessAsync(lifetime.ApplicationStopping);
                ConsoleUi.Success("Backlog review command completed successfully");
                context.ExitCode = 0;
            }
            catch (OperationCanceledException)
            {
                ConsoleUi.Warn("Backlog review cancelled by user");
                context.ExitCode = 130;
            }
            catch (Exception ex)
            {
                ConsoleUi.Error($"Backlog review failed: {ex.Message}");
                LogBacklogCommandFailed(logger, ex);
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static void ConfigureOpenTelemetry(IServiceCollection services)
    {
        ResourceBuilder resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService("Otapewin", serviceVersion: "1.0.0");

        _ = services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                _ = builder
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource("Otapewin.*")
                    .AddConsoleExporter();
            })
            .WithMetrics(builder =>
            {
                _ = builder
                    .SetResourceBuilder(resourceBuilder)
                    .AddRuntimeInstrumentation()
                    .AddConsoleExporter();
            });
    }

    #region LoggerMessage Delegates

    private static readonly Action<ILogger, Exception?> _logRunningAllCommands =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1, nameof(LogRunningAllCommands)),
            "Running all commands because no specific command was provided");

    private static readonly Action<ILogger, Exception?> _logRunAllFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2, nameof(LogRunAllFailed)),
            "Run-all failed");

    private static readonly Action<ILogger, Exception?> _logDailyCommandStarting =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(3, nameof(LogDailyCommandStarting)),
            "Starting daily command");

    private static readonly Action<ILogger, Exception?> _logDailyCommandFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(4, nameof(LogDailyCommandFailed)),
            "Daily command failed");

    private static readonly Action<ILogger, Exception?> _logWeeklyCommandStarting =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(5, nameof(LogWeeklyCommandStarting)),
            "Starting weekly command");

    private static readonly Action<ILogger, Exception?> _logWeeklyCommandFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(6, nameof(LogWeeklyCommandFailed)),
            "Weekly command failed");

    private static readonly Action<ILogger, Exception?> _logBacklogCommandStarting =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(7, nameof(LogBacklogCommandStarting)),
            "Starting backlog review command");

    private static readonly Action<ILogger, Exception?> _logBacklogCommandFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(8, nameof(LogBacklogCommandFailed)),
            "Backlog review failed");

    private static void LogRunningAllCommands(ILogger logger) => _logRunningAllCommands(logger, null);
    private static void LogRunAllFailed(ILogger logger, Exception ex) => _logRunAllFailed(logger, ex);
    private static void LogDailyCommandStarting(ILogger logger) => _logDailyCommandStarting(logger, null);
    private static void LogDailyCommandFailed(ILogger logger, Exception ex) => _logDailyCommandFailed(logger, ex);
    private static void LogWeeklyCommandStarting(ILogger logger) => _logWeeklyCommandStarting(logger, null);
    private static void LogWeeklyCommandFailed(ILogger logger, Exception ex) => _logWeeklyCommandFailed(logger, ex);
    private static void LogBacklogCommandStarting(ILogger logger) => _logBacklogCommandStarting(logger, null);
    private static void LogBacklogCommandFailed(ILogger logger, Exception ex) => _logBacklogCommandFailed(logger, ex);

    #endregion
}
