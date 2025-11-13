using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Otapewin.Clients;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Otapewin.Workers;

/// <summary>
/// Backlog review worker for analyzing pending tasks
/// </summary>
public sealed class BacklogReviewWorker : IWorker
{
    private readonly BrainConfig _config;
    private readonly string _vault;
    private readonly IOpenAIClient _openAIClient;
    private readonly ILogger<BacklogReviewWorker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BacklogReviewWorker"/> class
    /// </summary>
    public BacklogReviewWorker(
        IOptions<BrainConfig> config,
        IOpenAIClient openAIClient,
        ILogger<BacklogReviewWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(openAIClient);
        ArgumentNullException.ThrowIfNull(logger);

        _config = config.Value;
        ArgumentException.ThrowIfNullOrWhiteSpace(_config.VaultPath);

        _openAIClient = openAIClient;
        _logger = logger;
        _vault = _config.VaultPath;

        LogWorkerInitialized(_logger, _vault);
    }

    /// <summary>
    /// Process backlog review
    /// </summary>
    public async Task ProcessAsync(CancellationToken token)
    {
        LogStartingBacklogReview(_logger);

        if (DateTime.UtcNow.DayOfWeek != DayOfWeek.Monday)
        {
            LogNotMonday(_logger);
            return;
        }

        DateTime today = DateTime.UtcNow;
        int year = today.Year;
        int currentWeek = ISOWeek.GetWeekOfYear(today);
        int startWeek = currentWeek - 3;

        LogReviewingBacklog(_logger, startWeek, currentWeek);

        // Check if output file exists first to avoid unnecessary work
        int isoWeek = ISOWeek.GetWeekOfYear(today);
        string outputPath = Path.Combine(_vault, _config.FocusPath, year.ToString(CultureInfo.InvariantCulture), $"{_config.FocusPrefix}{isoWeek.ToString(CultureInfo.InvariantCulture)}.md");

        if (!File.Exists(outputPath))
        {
            LogWeeklyFocusFileNotFound(_logger, outputPath);
            return;
        }

        // Use ConcurrentBag for thread-safe collection during parallel processing
        ConcurrentBag<string> backlogTasksBag = [];

        // Pre-create week range to process
        List<int> weeksToProcess = [.. Enumerable.Range(startWeek, currentWeek - startWeek + 1)];

        // Process weeks with optimized parallel control using Parallel.ForEachAsync (.NET 6+, optimized .NET 9)
        await Parallel.ForEachAsync(
            weeksToProcess,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(weeksToProcess.Count, Environment.ProcessorCount),
                CancellationToken = token
            },
            async (week, ct) =>
            {
                string archiveDir = Path.Combine(_vault, _config.ArchivePath, year.ToString(CultureInfo.InvariantCulture), $"Week_{week.ToString(CultureInfo.InvariantCulture)}");

                if (!Directory.Exists(archiveDir))
                {
                    LogArchiveDirectoryNotFound(_logger, week, archiveDir);
                    return;
                }

                string[] files = Directory.GetFiles(archiveDir, "*.md", SearchOption.TopDirectoryOnly);
                LogProcessingFilesForWeek(_logger, files.Length, week);

                if (files.Length == 0)
                {
                    return;
                }

                // Process files within each week in parallel
                await Parallel.ForEachAsync(
                    files,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Math.Min(files.Length, 4),
                        CancellationToken = ct
                    },
                    async (file, fileCt) =>
                    {
                        string[] lines = await File.ReadAllLinesAsync(file, fileCt).ConfigureAwait(false);

                        // Optimized patterns for matching
                        ReadOnlySpan<char> taskTag = "#task";
                        ReadOnlySpan<char> completedLower = "- [x]";
                        ReadOnlySpan<char> completedUpper = "- [X]";

                        // Use Span<T> for efficient string operations
                        foreach (string line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line))
                            {
                                continue;
                            }

                            ReadOnlySpan<char> lineSpan = line.AsSpan();
                            ReadOnlySpan<char> trimmedLine = lineSpan.TrimStart();

                            // Check if line contains task tag and is not completed
                            if (trimmedLine.Contains(taskTag, StringComparison.OrdinalIgnoreCase) &&
                                !trimmedLine.StartsWith(completedLower, StringComparison.Ordinal) &&
                                !trimmedLine.StartsWith(completedUpper, StringComparison.Ordinal))
                            {
                                backlogTasksBag.Add(line);
                            }
                        }
                    }).ConfigureAwait(false);
            }).ConfigureAwait(false);

        // Deduplicate tasks using HashSet for optimal performance
        List<string> backlogTasks = [.. backlogTasksBag
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        if (backlogTasks.Count == 0)
        {
            LogNoBacklogTasksFound(_logger);
            return;
        }

        LogFoundBacklogTasks(_logger, backlogTasks.Count);

        token.ThrowIfCancellationRequested();

        // Use efficient string joining with pre-calculated capacity
        int estimatedLength = backlogTasks.Sum(t => t.Length + 1);
        StringBuilder sb = new(capacity: estimatedLength);

        for (int i = 0; i < backlogTasks.Count; i++)
        {
            _ = sb.Append(backlogTasks[i]);
            if (i < backlogTasks.Count - 1)
            {
                _ = sb.Append('\n');
            }
        }

        string tasksContent = sb.ToString();

        string review = await _openAIClient.SummarizePatternsAsync(
            _config.Prompts.BacklogReviewPrompt,
            tasksContent,
            token).ConfigureAwait(false);

        List<string> output = new(capacity: 5)
        {
            $"---{Environment.NewLine}",
            "## ?? Task Backlog Review",
            review
        };

        // Use optimized file write
        await WriteAllLinesOptimizedAsync(outputPath, output, append: true, token).ConfigureAwait(false);

        LogBacklogReviewCompleted(_logger, outputPath);
    }

    private static async ValueTask WriteAllLinesOptimizedAsync(
        string path,
        IEnumerable<string> lines,
        bool append,
        CancellationToken token)
    {
        FileStreamOptions options = new()
        {
            Mode = append ? FileMode.Append : FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            BufferSize = 4096
        };

        await using FileStream stream = new(path, options);
        await using StreamWriter writer = new(stream);

        foreach (string line in lines)
        {
            await writer.WriteLineAsync(line.AsMemory(), token).ConfigureAwait(false);
        }
    }

    #region LoggerMessage Delegates

    private static readonly Action<ILogger, string, Exception?> _logWorkerInitialized =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(1, nameof(LogWorkerInitialized)),
            "BacklogReviewWorker initialized with vault: {VaultPath}");

    private static readonly Action<ILogger, Exception?> _logStartingBacklogReview =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(2, nameof(LogStartingBacklogReview)),
            "Starting backlog review");

    private static readonly Action<ILogger, Exception?> _logNotMonday =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(3, nameof(LogNotMonday)),
            "Not Monday, skipping backlog review");

    private static readonly Action<ILogger, int, int, Exception?> _logReviewingBacklog =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(4, nameof(LogReviewingBacklog)),
            "Reviewing backlog from week {StartWeek} to {CurrentWeek}");

    private static readonly Action<ILogger, string, Exception?> _logWeeklyFocusFileNotFound =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(5, nameof(LogWeeklyFocusFileNotFound)),
            "Weekly focus file not found: {OutputPath}");

    private static readonly Action<ILogger, int, string, Exception?> _logArchiveDirectoryNotFound =
        LoggerMessage.Define<int, string>(
            LogLevel.Debug,
            new EventId(6, nameof(LogArchiveDirectoryNotFound)),
            "Archive directory not found for week {Week}: {Directory}");

    private static readonly Action<ILogger, int, int, Exception?> _logProcessingFilesForWeek =
        LoggerMessage.Define<int, int>(
            LogLevel.Debug,
            new EventId(7, nameof(LogProcessingFilesForWeek)),
            "Processing {FileCount} files from week {Week}");

    private static readonly Action<ILogger, Exception?> _logNoBacklogTasksFound =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(8, nameof(LogNoBacklogTasksFound)),
            "No backlog tasks found");

    private static readonly Action<ILogger, int, Exception?> _logFoundBacklogTasks =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(9, nameof(LogFoundBacklogTasks)),
            "Found {TaskCount} unique backlog tasks");

    private static readonly Action<ILogger, string, Exception?> _logBacklogReviewCompleted =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(10, nameof(LogBacklogReviewCompleted)),
            "Backlog review completed successfully. Review written to {OutputPath}");

    private static void LogWorkerInitialized(ILogger logger, string vault) => _logWorkerInitialized(logger, vault, null);
    private static void LogStartingBacklogReview(ILogger logger) => _logStartingBacklogReview(logger, null);
    private static void LogNotMonday(ILogger logger) => _logNotMonday(logger, null);
    private static void LogReviewingBacklog(ILogger logger, int start, int current) => _logReviewingBacklog(logger, start, current, null);
    private static void LogWeeklyFocusFileNotFound(ILogger logger, string path) => _logWeeklyFocusFileNotFound(logger, path, null);
    private static void LogArchiveDirectoryNotFound(ILogger logger, int week, string dir) => _logArchiveDirectoryNotFound(logger, week, dir, null);
    private static void LogProcessingFilesForWeek(ILogger logger, int count, int week) => _logProcessingFilesForWeek(logger, count, week, null);
    private static void LogNoBacklogTasksFound(ILogger logger) => _logNoBacklogTasksFound(logger, null);
    private static void LogFoundBacklogTasks(ILogger logger, int count) => _logFoundBacklogTasks(logger, count, null);
    private static void LogBacklogReviewCompleted(ILogger logger, string path) => _logBacklogReviewCompleted(logger, path, null);

    #endregion
}
