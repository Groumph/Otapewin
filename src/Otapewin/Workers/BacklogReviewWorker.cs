using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Otapewin.Clients;

namespace Otapewin.Workers;

public sealed class BacklogReviewWorker : IWorker
{
    private readonly BrainConfig _config;
    private readonly string _vault;
    private readonly IOpenAIClient _openAIClient;
    private readonly ILogger<BacklogReviewWorker> _logger;

    // Optimized search patterns using SearchValues (.NET 8+)
    private static readonly SearchValues<char> TaskCompletedChars = SearchValues.Create("xX");

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

        _logger.LogDebug("BacklogReviewWorker initialized with vault: {VaultPath}", _vault);
    }

    public async Task ProcessAsync(CancellationToken token)
    {
        _logger.LogInformation("Starting backlog review");

        if (DateTime.UtcNow.DayOfWeek != DayOfWeek.Monday)
        {
            _logger.LogInformation("Not Monday, skipping backlog review");
            return;
        }

        var today = DateTime.UtcNow;
        var year = today.Year;
        var currentWeek = ISOWeek.GetWeekOfYear(today);
        var startWeek = currentWeek - 3;

        _logger.LogInformation("Reviewing backlog from week {StartWeek} to {CurrentWeek}", startWeek, currentWeek);

        // Check if output file exists first to avoid unnecessary work
        var isoWeek = ISOWeek.GetWeekOfYear(today);
        var outputPath = Path.Combine(_vault, _config.FocusPath, year.ToString(), $"{_config.FocusPrefix}{isoWeek}.md");

        if (!File.Exists(outputPath))
        {
            _logger.LogWarning("Weekly focus file not found: {OutputPath}", outputPath);
            return;
        }

        // Use ConcurrentBag for thread-safe collection during parallel processing
        var backlogTasksBag = new ConcurrentBag<string>();

        // Pre-create week range to process
        var weeksToProcess = Enumerable.Range(startWeek, currentWeek - startWeek + 1).ToList();

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
                var archiveDir = Path.Combine(_vault, _config.ArchivePath, year.ToString(), $"Week_{week}");

                if (!Directory.Exists(archiveDir))
                {
                    _logger.LogDebug("Archive directory not found for week {Week}: {Directory}", week, archiveDir);
                    return;
                }

                var files = Directory.GetFiles(archiveDir, "*.md", SearchOption.TopDirectoryOnly);
                _logger.LogDebug("Processing {FileCount} files from week {Week}", files.Length, week);

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
                        var lines = await File.ReadAllLinesAsync(file, fileCt).ConfigureAwait(false);

                        // Optimized patterns for matching
                        ReadOnlySpan<char> taskTag = "#task";
                        ReadOnlySpan<char> completedLower = "- [x]";
                        ReadOnlySpan<char> completedUpper = "- [X]";

                        // Use Span<T> for efficient string operations
                        foreach (var line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line))
                                continue;

                            var lineSpan = line.AsSpan();
                            var trimmedLine = lineSpan.TrimStart();

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

        // Deduplicate tasks using FrozenSet for optimal performance (.NET 8+)
        var backlogTasks = backlogTasksBag
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (backlogTasks.Count == 0)
        {
            _logger.LogInformation("No backlog tasks found");
            return;
        }

        _logger.LogInformation("Found {TaskCount} unique backlog tasks", backlogTasks.Count);

        token.ThrowIfCancellationRequested();

        // Use efficient string joining with pre-calculated capacity
        var estimatedLength = backlogTasks.Sum(t => t.Length + 1);
        var sb = new System.Text.StringBuilder(capacity: estimatedLength);

        for (int i = 0; i < backlogTasks.Count; i++)
        {
            sb.Append(backlogTasks[i]);
            if (i < backlogTasks.Count - 1)
            {
                sb.Append('\n');
            }
        }

        var tasksContent = sb.ToString();

        var review = await _openAIClient.SummarizePatternsAsync(
            _config.Prompts.BacklogReviewPrompt,
            tasksContent,
            token).ConfigureAwait(false);

        var output = new List<string>(capacity: 5)
        {
            $"---{Environment.NewLine}",
            "## 🧹 Task Backlog Review",
            review
        };

        // Use optimized file write
        await WriteAllLinesOptimizedAsync(outputPath, output, append: true, token).ConfigureAwait(false);

        _logger.LogInformation("Backlog review completed successfully. Review written to {OutputPath}", outputPath);
    }

    private static async ValueTask WriteAllLinesOptimizedAsync(
        string path,
        IEnumerable<string> lines,
        bool append,
        CancellationToken token)
    {
        var options = new FileStreamOptions
        {
            Mode = append ? FileMode.Append : FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            BufferSize = 4096
        };

        await using var stream = new FileStream(path, options);
        await using var writer = new StreamWriter(stream);

        foreach (var line in lines)
        {
            await writer.WriteLineAsync(line.AsMemory(), token).ConfigureAwait(false);
        }
    }
}
