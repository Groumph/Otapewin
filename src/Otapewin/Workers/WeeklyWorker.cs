using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Otapewin.Clients;
using Otapewin.Helpers;
using System.Globalization;
using System.Text;

namespace Otapewin.Workers;

/// <summary>
/// Weekly worker for generating weekly summaries
/// </summary>
public sealed class WeeklyWorker : IWorker
{
    private readonly BrainConfig _config;
    private readonly string _vault;
    private readonly IOpenAIClient _openAIClient;
    private readonly ILogger<WeeklyWorker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeeklyWorker"/> class
    /// </summary>
    public WeeklyWorker(
        IOptions<BrainConfig> config,
        IOpenAIClient openAIClient,
        ILogger<WeeklyWorker> logger)
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
    /// Process weekly summary generation
    /// </summary>
    public async Task ProcessAsync(CancellationToken token)
    {
        LogStartingWeeklyProcessing(_logger);

        if (DateTime.UtcNow.DayOfWeek != DayOfWeek.Monday)
        {
            LogNotMonday(_logger);
            return;
        }

        DateTime now = DateTime.UtcNow;
        int isoWeek = ISOWeek.GetWeekOfYear(now.Date) - 1;
        int year = now.Year;
        string archivePath = Path.Combine(_vault, _config.ArchivePath, year.ToString(CultureInfo.InvariantCulture), $"Week_{isoWeek.ToString(CultureInfo.InvariantCulture)}");

        if (!Directory.Exists(archivePath))
        {
            LogArchiveDirectoryNotFound(_logger, archivePath);
            return;
        }

        string[] files = Directory.GetFiles(archivePath, "*.md", SearchOption.TopDirectoryOnly);
        LogProcessingFiles(_logger, files.Length, isoWeek);

        if (files.Length == 0)
        {
            LogNoMarkdownFiles(_logger, archivePath);
            return;
        }

        // Read all files with optimized parallel processing
        int estimatedLines = files.Length * 50;
        List<string> allLinesList = new(capacity: estimatedLines);

        // Use Parallel.ForEachAsync for better concurrency control (.NET 6+, optimized in .NET 9)
        System.Collections.Concurrent.ConcurrentBag<string[]> linesBag = [];

        await Parallel.ForEachAsync(
            files,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(files.Length, Environment.ProcessorCount),
                CancellationToken = token
            },
            async (file, ct) =>
            {
                string[] fileLines = await File.ReadAllLinesAsync(file, ct).ConfigureAwait(false);
                linesBag.Add(fileLines);
            }).ConfigureAwait(false);

        // Flatten results
        foreach (string[] lines in linesBag)
        {
            allLinesList.AddRange(lines);
        }

        // Tag grouping with FrozenDictionary for better performance (.NET 8+)
        Dictionary<string, List<string>> grouped = _config.Tags.ToDictionary(
            t => t.Name,
            _ => new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        _ = TagMatcher.ExtractTaggedSections(grouped, allLinesList);

        // Build GPT summary per tag group - parallelize with better control
        System.Collections.Concurrent.ConcurrentBag<(string Tag, string Summary)> tagSummaryBag = [];

        List<KeyValuePair<string, List<string>>> tagsToProcess = [.. grouped.Where(kvp => kvp.Value.Count > 0)];

        await Parallel.ForEachAsync(
            tagsToProcess,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(tagsToProcess.Count, 3), // Limit concurrent API calls
                CancellationToken = token
            },
            async (kvp, ct) =>
            {
                (string tag, List<string> lines) = kvp;

                LogSummarizingTag(_logger, lines.Count, tag);

                TagConfig? tagConfig = _config.Tags.Find(t => t.Name.Equals(tag, StringComparison.OrdinalIgnoreCase));
                string prompt = tagConfig?.Prompt ?? _config.Prompts.WeeklyDefaultPrompt;

                string content = string.Join('\n', lines);
                string summary = await _openAIClient.SummarizePatternsAsync(prompt, content, ct).ConfigureAwait(false);

                tagSummaryBag.Add((Tag: tag, Summary: summary));
            }).ConfigureAwait(false);

        // Convert to Dictionary for lookups
        Dictionary<string, string> tagSummaries = tagSummaryBag.ToDictionary(
            x => x.Tag,
            x => x.Summary,
            StringComparer.OrdinalIgnoreCase);

        // Write to weekly focus
        string outputPath = Path.Combine(_vault, _config.FocusPath, year.ToString(CultureInfo.InvariantCulture),
            $"{_config.FocusPrefix}{isoWeek.ToString(CultureInfo.InvariantCulture)}_Summary.md");

        int estimatedOutputCapacity = 100 + (tagSummaries.Count * 20);
        List<string> output = new(capacity: estimatedOutputCapacity)
        {
            "## 🧠 Weekly Tag Summaries"
        };

        // OrderBy is optimized in .NET 9
        foreach (string tag in tagSummaries.Keys.Order(StringComparer.OrdinalIgnoreCase))
        {
            output.Add($"\n### {tag} Summary\n{tagSummaries[tag]}");
        }

        token.ThrowIfCancellationRequested();

        LogGeneratingCoachingReflection(_logger);

        // Use ArrayPool for large string building to reduce GC pressure
        string fullWeekRaw;
        if (allLinesList.Count > 1000)
        {
            // Calculate total length to avoid reallocations
            int totalLength = allLinesList.Sum(l => l.Length + Environment.NewLine.Length);
            StringBuilder sb = new(capacity: totalLength);

            foreach (string line in allLinesList)
            {
                _ = sb.AppendLine(line);
            }
            fullWeekRaw = sb.ToString();
        }
        else
        {
            fullWeekRaw = string.Join('\n', allLinesList);
        }

        // GPT Coaching Reflection
        string coachingReflection = await _openAIClient.SummarizePatternsAsync(
            _config.Prompts.WeeklyCoachPrompt,
            fullWeekRaw,
            token).ConfigureAwait(false);

        output.Add("\n## 🎓 Weekly Coaching Reflection\n");
        output.Add(coachingReflection);

        // Check for unfinished tasks using optimized Span operations
        if (_config.Tags.Exists(x => x.Name.Equals("task", StringComparison.OrdinalIgnoreCase))
            && grouped.TryGetValue("task", out List<string>? taskLines))
        {
            ReadOnlySpan<char> pendingTaskPrefix = "- [ ]";
            List<string> unfinishedTasks = new(taskLines.Count / 2); // Estimate half are unfinished

            foreach (string task in taskLines)
            {
                ReadOnlySpan<char> trimmed = task.AsSpan().TrimStart();
                if (trimmed.StartsWith(pendingTaskPrefix, StringComparison.Ordinal))
                {
                    unfinishedTasks.Add(task);
                }
            }

            if (unfinishedTasks.Count > 0)
            {
                token.ThrowIfCancellationRequested();

                LogGeneratingIntentions(_logger, unfinishedTasks.Count);

                string intentions = await _openAIClient.SummarizePatternsAsync(
                    _config.Prompts.WeeklyIntentionsPrompt,
                    string.Join('\n', unfinishedTasks),
                    token).ConfigureAwait(false);

                output.Add("\n## 🔭 Intentions for Next Week");
                output.Add(intentions);
            }
        }

        // Ensure directory exists
        await EnsureDirectoryExistsAsync(outputPath, token).ConfigureAwait(false);

        // Use optimized file write
        await WriteAllLinesOptimizedAsync(outputPath, output, append: true, token).ConfigureAwait(false);

        LogWeeklyProcessingCompleted(_logger, outputPath);
    }

    private static async ValueTask EnsureDirectoryExistsAsync(string filePath, CancellationToken token)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            _ = await Task.Run(() => Directory.CreateDirectory(directory), token).ConfigureAwait(false);
        }
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
            "WeeklyWorker initialized with vault: {VaultPath}");

    private static readonly Action<ILogger, Exception?> _logStartingWeeklyProcessing =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(2, nameof(LogStartingWeeklyProcessing)),
            "Starting weekly processing");

    private static readonly Action<ILogger, Exception?> _logNotMonday =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(3, nameof(LogNotMonday)),
            "Not Monday, skipping weekly processing");

    private static readonly Action<ILogger, string, Exception?> _logArchiveDirectoryNotFound =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(4, nameof(LogArchiveDirectoryNotFound)),
            "Archive directory not found: {ArchivePath}");

    private static readonly Action<ILogger, int, int, Exception?> _logProcessingFiles =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(5, nameof(LogProcessingFiles)),
            "Processing {FileCount} files from week {Week}");

    private static readonly Action<ILogger, string, Exception?> _logNoMarkdownFiles =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(6, nameof(LogNoMarkdownFiles)),
            "No markdown files found in {ArchivePath}");

    private static readonly Action<ILogger, int, string, Exception?> _logSummarizingTag =
        LoggerMessage.Define<int, string>(
            LogLevel.Information,
            new EventId(7, nameof(LogSummarizingTag)),
            "Summarizing {LineCount} lines for tag #{Tag}");

    private static readonly Action<ILogger, Exception?> _logGeneratingCoachingReflection =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(8, nameof(LogGeneratingCoachingReflection)),
            "Generating weekly coaching reflection");

    private static readonly Action<ILogger, int, Exception?> _logGeneratingIntentions =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(9, nameof(LogGeneratingIntentions)),
            "Generating intentions for {TaskCount} unfinished tasks");

    private static readonly Action<ILogger, string, Exception?> _logWeeklyProcessingCompleted =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(10, nameof(LogWeeklyProcessingCompleted)),
            "Weekly processing completed successfully. Output written to {OutputPath}");

    private static void LogWorkerInitialized(ILogger logger, string vault) => _logWorkerInitialized(logger, vault, null);
    private static void LogStartingWeeklyProcessing(ILogger logger) => _logStartingWeeklyProcessing(logger, null);
    private static void LogNotMonday(ILogger logger) => _logNotMonday(logger, null);
    private static void LogArchiveDirectoryNotFound(ILogger logger, string path) => _logArchiveDirectoryNotFound(logger, path, null);
    private static void LogProcessingFiles(ILogger logger, int count, int week) => _logProcessingFiles(logger, count, week, null);
    private static void LogNoMarkdownFiles(ILogger logger, string path) => _logNoMarkdownFiles(logger, path, null);
    private static void LogSummarizingTag(ILogger logger, int count, string tag) => _logSummarizingTag(logger, count, tag, null);
    private static void LogGeneratingCoachingReflection(ILogger logger) => _logGeneratingCoachingReflection(logger, null);
    private static void LogGeneratingIntentions(ILogger logger, int count) => _logGeneratingIntentions(logger, count, null);
    private static void LogWeeklyProcessingCompleted(ILogger logger, string path) => _logWeeklyProcessingCompleted(logger, path, null);

    #endregion
}
