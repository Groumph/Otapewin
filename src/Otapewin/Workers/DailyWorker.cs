using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Otapewin.Clients;
using Otapewin.Helpers;
using System.Collections.Concurrent;
using System.Globalization;

namespace Otapewin.Workers;

/// <summary>
/// Daily worker for processing inbox content
/// </summary>
public sealed class DailyWorker : IWorker
{
    private readonly BrainConfig _config;
    private readonly string _vault;
    private readonly string _inputPath;
    private readonly IOpenAIClient _openAIClient;
    private readonly ILogger<DailyWorker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DailyWorker"/> class
    /// </summary>
    public DailyWorker(
        IOptions<BrainConfig> config,
        IOpenAIClient openAIClient,
        ILogger<DailyWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(openAIClient);
        ArgumentNullException.ThrowIfNull(logger);

        _config = config.Value;
        ArgumentException.ThrowIfNullOrWhiteSpace(_config.VaultPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(_config.InputFile);

        _openAIClient = openAIClient;
        _logger = logger;
        _vault = _config.VaultPath;
        _inputPath = Path.Combine(_vault, _config.InputFile);

        LogWorkerInitialized(_logger, _vault, _config.InputFile);
    }

    /// <summary>
    /// Process daily inbox content
    /// </summary>
    public async Task ProcessAsync(CancellationToken token)
    {
        LogStartingDailyProcessing(_logger);

        // Check file existence
        if (!File.Exists(_inputPath))
        {
            LogInboxFileDoesNotExist(_logger);
            return;
        }

        string[] allLines = await File.ReadAllLinesAsync(_inputPath, token).ConfigureAwait(false);

        if (allLines.Length == 0)
        {
            LogInboxIsEmpty(_logger);
            return;
        }

        LogProcessingLines(_logger, allLines.Length);

        // Cache DateTime to avoid multiple calls
        DateTime now = DateTime.UtcNow;
        DateOnly today = DateOnly.FromDateTime(now);
        int isoWeek = ISOWeek.GetWeekOfYear(today.ToDateTime(TimeOnly.MinValue));

        // Use string interpolation with DefaultInterpolatedStringHandler (optimized in .NET 10)
        string yearString = $"{now.Year}";
        string weekString = $"{isoWeek}";

        // Build tag dictionary with case-insensitive lookups
        Dictionary<string, List<string>> allAvailableTags = _config.Tags.ToDictionary(
            t => t.Name,
            _ => new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        ReadOnlySpan<char> tagPrefixSpan = _config.TagPrefix.AsSpan();

        // Use collection expressions for cleaner initialization (.NET 10)
        List<string> processLinesList = new(allLines.Length);
        List<string> ignoredLinesList = [];

        // Process lines with Span for reduced allocations
        foreach (string line in allLines)
        {
            ReadOnlySpan<char> lineSpan = line.AsSpan();
            ReadOnlySpan<char> trimmedStart = lineSpan.TrimStart();

            if (trimmedStart.StartsWith(tagPrefixSpan, StringComparison.OrdinalIgnoreCase))
            {
                ignoredLinesList.Add(line);
            }
            else
            {
                processLinesList.Add(line);
            }
        }

        LogIgnoredLines(_logger, ignoredLinesList.Count, _config.TagPrefix);

        _ = TagMatcher.ExtractTaggedSections(allAvailableTags, processLinesList);

        foreach ((string tag, List<string> lines) in allAvailableTags)
        {
            if (lines.Count > 0)
            {
                LogFoundLinesWithTag(_logger, lines.Count, tag);
            }
        }

        // Filter general content using Span for efficiency
        List<string> generalContent = new(processLinesList.Count);

        // Cache spans for repeated comparisons
        ReadOnlySpan<char> completedTaskPrefix = "- [x]";
        ReadOnlySpan<char> lookupTag = "#lookup";

        foreach (string line in processLinesList)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            ReadOnlySpan<char> lineSpan = line.AsSpan().TrimStart();

            if (!lineSpan.StartsWith(completedTaskPrefix, StringComparison.OrdinalIgnoreCase) &&
                !lineSpan.Contains(lookupTag, StringComparison.OrdinalIgnoreCase))
            {
                generalContent.Add(line);
            }
        }

        token.ThrowIfCancellationRequested();

        // Only call API if we have content
        string generalSummary = generalContent.Count > 0
            ? await _openAIClient.SummarizeAsync(string.Join('\n', generalContent), token).ConfigureAwait(false)
            : string.Empty;

        // Process lookups in parallel with proper concurrency control
        List<(string Query, string Response)> lookupResults = [];
        if (allAvailableTags.TryGetValue("lookup", out List<string>? lookups) && lookups.Count > 0)
        {
            LogProcessingLookups(_logger, lookups.Count);

            // Use Parallel.ForEachAsync with optimized concurrency (.NET 10 improved scheduling)
            ConcurrentBag<(string Query, string Response)> lookupResultsBag = [];

            await Parallel.ForEachAsync(
                lookups,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Min(lookups.Count, Environment.ProcessorCount),
                    CancellationToken = token
                },
                async (line, ct) =>
                {
                    string result = await _openAIClient.LookupAsync(line, ct).ConfigureAwait(false);
                    lookupResultsBag.Add((Query: line, Response: result));
                }).ConfigureAwait(false);

            lookupResults.AddRange(lookupResultsBag);
        }

        // Process tasks efficiently using Span comparisons
        List<string> completed = [];
        List<string> pending = [];

        if (allAvailableTags.TryGetValue("task", out List<string>? tasks))
        {
            // Cache span literals for better performance
            ReadOnlySpan<char> completedLower = "- [x]";
            ReadOnlySpan<char> completedUpper = "- [X]";

            foreach (string task in tasks)
            {
                ReadOnlySpan<char> trimmed = task.AsSpan().TrimStart();

                if (trimmed.StartsWith(completedLower, StringComparison.Ordinal) ||
                    trimmed.StartsWith(completedUpper, StringComparison.Ordinal))
                {
                    completed.Add(task);
                }
                else
                {
                    pending.Add(task);
                }
            }

            LogTasksSummary(_logger, completed.Count, pending.Count);
        }

        // Build output efficiently with pre-allocated capacity
        string outputPath = Path.Combine(_vault, _config.FocusPath, yearString, $"{_config.FocusPrefix}{weekString}.md");

        await EnsureDirectoryExistsAsync(outputPath, token).ConfigureAwait(false);

        // Estimate capacity based on content
        int estimatedCapacity = 50 + allAvailableTags.Sum(kvp => kvp.Value.Count) +
                               completed.Count + pending.Count + (lookupResults.Count * 3);
        List<string> output = new(capacity: estimatedCapacity);

        if (!File.Exists(outputPath))
        {
            output.Add($"# 📝 Weekly Focus - {weekString}");
        }

        output.Add(string.Empty);
        output.Add($"# 🔥 Day - {today:yyyy-MM-dd}");

        if (!string.IsNullOrEmpty(generalSummary))
        {
            output.Add($"\n## Summary\n{generalSummary}");
        }

        // Add tag sections
        foreach (string tag in allAvailableTags.Keys)
        {
            if (allAvailableTags[tag].Count > 0)
            {
                output.Add($"\n## {tag} Notes");
                output.AddRange(allAvailableTags[tag]);
            }
        }

        // Add task summary
        if (completed.Count > 0 || pending.Count > 0)
        {
            output.Add("\n## Task Summary");
            if (completed.Count > 0)
            {
                output.Add("**Completed:**");
                output.AddRange(completed);
            }
            if (pending.Count > 0)
            {
                output.Add("\n**Pending:**");
                output.AddRange(pending);
            }
        }

        // Add lookup results
        if (lookupResults.Count > 0)
        {
            output.Add("\n## Lookup Results");
            foreach ((string q, string a) in lookupResults)
            {
                output.Add($"{q}:> {a}");
                output.Add(string.Empty);
            }
        }

        output.Add(string.Empty);

        token.ThrowIfCancellationRequested();

        // Use optimized file write with FileStreamOptions
        await WriteAllLinesOptimizedAsync(outputPath, output, append: true, token).ConfigureAwait(false);

        LogWroteDailySummary(_logger, outputPath);

        // Archive processed content
        string archivePath = Path.Combine(_vault, _config.ArchivePath, yearString,
            $"Week_{weekString}", $"{_config.ArchivePrefix}{today:yyyy-MM-dd}.md");

        await EnsureDirectoryExistsAsync(archivePath, token).ConfigureAwait(false);
        await EnsureDirectoryExistsAsync(_inputPath, token).ConfigureAwait(false);

        await WriteAllLinesOptimizedAsync(archivePath, processLinesList, append: false, token).ConfigureAwait(false);
        await WriteAllLinesOptimizedAsync(_inputPath, ignoredLinesList, append: false, token).ConfigureAwait(false);

        LogDailyProcessingCompleted(_logger, processLinesList.Count);
    }

    private static async ValueTask EnsureDirectoryExistsAsync(string filePath, CancellationToken token)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            // Use Task.Run for CPU-bound Directory.CreateDirectory
            _ = await Task.Run(() => Directory.CreateDirectory(directory), token).ConfigureAwait(false);
        }
    }

    private static async ValueTask WriteAllLinesOptimizedAsync(
        string path,
        IEnumerable<string> lines,
        bool append,
        CancellationToken token)
    {
        // .NET 10 improved FileStream performance with better buffer management
        FileStreamOptions options = new()
        {
            Mode = append ? FileMode.Append : FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            BufferSize = 8192 // Increased buffer size for better throughput in .NET 10
        };

        await using FileStream stream = new(path, options);
        await using StreamWriter writer = new(stream);

        foreach (string line in lines)
        {
            // Use Memory<char> for optimized async writes (.NET 10 improvements)
            await writer.WriteLineAsync(line.AsMemory(), token).ConfigureAwait(false);
        }
    }

    #region LoggerMessage Delegates

    private static readonly Action<ILogger, string, string, Exception?> _logWorkerInitialized =
        LoggerMessage.Define<string, string>(
            LogLevel.Debug,
            new EventId(1, nameof(LogWorkerInitialized)),
            "DailyWorker initialized with vault: {VaultPath}, input: {InputFile}");

    private static readonly Action<ILogger, Exception?> _logStartingDailyProcessing =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(2, nameof(LogStartingDailyProcessing)),
            "Starting daily processing");

    private static readonly Action<ILogger, Exception?> _logInboxFileDoesNotExist =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(3, nameof(LogInboxFileDoesNotExist)),
            "Inbox file does not exist, nothing to process");

    private static readonly Action<ILogger, Exception?> _logInboxIsEmpty =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(4, nameof(LogInboxIsEmpty)),
            "Inbox is empty, nothing to process");

    private static readonly Action<ILogger, int, Exception?> _logProcessingLines =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(5, nameof(LogProcessingLines)),
            "Processing {LineCount} lines from inbox");

    private static readonly Action<ILogger, int, string, Exception?> _logIgnoredLines =
        LoggerMessage.Define<int, string>(
            LogLevel.Debug,
            new EventId(6, nameof(LogIgnoredLines)),
            "Ignored {IgnoredCount} lines with prefix {TagPrefix}");

    private static readonly Action<ILogger, int, string, Exception?> _logFoundLinesWithTag =
        LoggerMessage.Define<int, string>(
            LogLevel.Information,
            new EventId(7, nameof(LogFoundLinesWithTag)),
            "Found {Count} lines with tag #{Tag}");

    private static readonly Action<ILogger, int, Exception?> _logProcessingLookups =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(8, nameof(LogProcessingLookups)),
            "Processing {LookupCount} lookup queries");

    private static readonly Action<ILogger, int, int, Exception?> _logTasksSummary =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(9, nameof(LogTasksSummary)),
            "Tasks: {CompletedCount} completed, {PendingCount} pending");

    private static readonly Action<ILogger, string, Exception?> _logWroteDailySummary =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(10, nameof(LogWroteDailySummary)),
            "Wrote daily summary to {OutputPath}");

    private static readonly Action<ILogger, int, Exception?> _logDailyProcessingCompleted =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(11, nameof(LogDailyProcessingCompleted)),
            "Daily processing completed successfully. Archived {ProcessedCount} lines");

    private static void LogWorkerInitialized(ILogger logger, string vault, string input) => _logWorkerInitialized(logger, vault, input, null);
    private static void LogStartingDailyProcessing(ILogger logger) => _logStartingDailyProcessing(logger, null);
    private static void LogInboxFileDoesNotExist(ILogger logger) => _logInboxFileDoesNotExist(logger, null);
    private static void LogInboxIsEmpty(ILogger logger) => _logInboxIsEmpty(logger, null);
    private static void LogProcessingLines(ILogger logger, int count) => _logProcessingLines(logger, count, null);
    private static void LogIgnoredLines(ILogger logger, int count, string prefix) => _logIgnoredLines(logger, count, prefix, null);
    private static void LogFoundLinesWithTag(ILogger logger, int count, string tag) => _logFoundLinesWithTag(logger, count, tag, null);
    private static void LogProcessingLookups(ILogger logger, int count) => _logProcessingLookups(logger, count, null);
    private static void LogTasksSummary(ILogger logger, int completed, int pending) => _logTasksSummary(logger, completed, pending, null);
    private static void LogWroteDailySummary(ILogger logger, string path) => _logWroteDailySummary(logger, path, null);
    private static void LogDailyProcessingCompleted(ILogger logger, int count) => _logDailyProcessingCompleted(logger, count, null);

    #endregion
}
