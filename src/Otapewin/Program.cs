using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Otapewin.Clients;
using Otapewin.Helpers;
using Otapewin.Workers;

namespace Otapewin;

internal static class Program
{
    private const string AppName = "Otapewin CLI";

    public static async Task<int> Main(string[] args)
    {
        // Show banner for interactive sessions
        if (!Console.IsOutputRedirected)
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
            ConsoleUi.Banner(AppName, version);
        }

        var rootCommand = new RootCommand("Otapewin CLI - AI-powered task and note management")
        {
            CreateDailyCommand(),
            CreateWeeklyCommand(),
            CreateBacklogCommand()
        };

        // If no command is provided, run all three commands (daily, weekly, backlog)
        rootCommand.SetHandler(async (InvocationContext context) =>
        {
            var host = context.BindingContext.GetRequiredService<IHost>();
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Otapewin.RunAll");

            var daily = host.Services.GetRequiredService<DailyWorker>();
            var weekly = host.Services.GetRequiredService<WeeklyWorker>();
            var backlog = host.Services.GetRequiredService<BacklogReviewWorker>();

            try
            {
                ConsoleUi.Title("Running All: Daily, Weekly, Backlog");
                logger.LogInformation("Running all commands because no specific command was provided");

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
                logger.LogError(ex, "Run-all failed");
                context.ExitCode = 1;
            }
        });

        var commandLineBuilder = new CommandLineBuilder(rootCommand)
            .UseHost(
            _ => Host.CreateDefaultBuilder(args),
            host =>
            {
                host.ConfigureServices((context, services) =>
                {
                    // Configuration with validation
                    services.AddOptions<BrainConfig>()
                    .Bind(context.Configuration)
                    .ValidateDataAnnotations()
                    .ValidateOnStart();
                    // Register workers
                    services.AddTransient<DailyWorker>();
                    services.AddTransient<WeeklyWorker>();
                    services.AddTransient<BacklogReviewWorker>();
                    // Register clients
                    services.AddSingleton<IOpenAIClient, OpenAIClient>();
                    // OpenTelemetry
                    ConfigureOpenTelemetry(services, context.Configuration);
                });

                host.ConfigureLogging((context, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();

                    if (context.HostingEnvironment.IsDevelopment())
                    {
                        logging.AddDebug();
                    }
                });

                host.ConfigureAppConfiguration((context, config) =>
                {
                    // Make local appsettings optional for public repo. Prefer environment variables.
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
                    config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
                    config.AddEnvironmentVariables(prefix: "OTAPEWIN_");
                    config.AddCommandLine(args);
                });
            })
            .UseDefaults()
            .Build();

        return await commandLineBuilder.InvokeAsync(args);
    }

    private static Command CreateDailyCommand()
    {
        var command = new Command("daily", "Process daily tasks and notes");

        command.SetHandler(async (InvocationContext context) =>
        {
            var host = context.BindingContext.GetRequiredService<IHost>();
            var logger = host.Services.GetRequiredService<ILogger<DailyWorker>>();
            var worker = host.Services.GetRequiredService<DailyWorker>();
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

            try
            {
                ConsoleUi.Title("Daily Processing");
                logger.LogInformation("Starting daily command");
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
                logger.LogError(ex, "Daily command failed");
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateWeeklyCommand()
    {
        var command = new Command("weekly", "Generate weekly review and summary");

        command.SetHandler(async (InvocationContext context) =>
        {
            var host = context.BindingContext.GetRequiredService<IHost>();
            var logger = host.Services.GetRequiredService<ILogger<WeeklyWorker>>();
            var worker = host.Services.GetRequiredService<WeeklyWorker>();
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

            try
            {
                ConsoleUi.Title("Weekly Review");
                logger.LogInformation("Starting weekly command");
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
                logger.LogError(ex, "Weekly command failed");
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateBacklogCommand()
    {
        var command = new Command("backlog", "Review and organize backlog items");

        command.SetHandler(async (InvocationContext context) =>
        {
            var host = context.BindingContext.GetRequiredService<IHost>();
            var logger = host.Services.GetRequiredService<ILogger<BacklogReviewWorker>>();
            var worker = host.Services.GetRequiredService<BacklogReviewWorker>();
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

            try
            {
                ConsoleUi.Title("Backlog Review");
                logger.LogInformation("Starting backlog review command");
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
                logger.LogError(ex, "Backlog review failed");
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static void ConfigureOpenTelemetry(IServiceCollection services, IConfiguration configuration)
    {
        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService("Otapewin", serviceVersion: "1.0.0");

        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                .SetResourceBuilder(resourceBuilder)
                .AddSource("Otapewin.*")
                .AddConsoleExporter();
            })
            .WithMetrics(builder =>
            {
                builder
                .SetResourceBuilder(resourceBuilder)
                .AddRuntimeInstrumentation()
                .AddConsoleExporter();
            });
    }
}
