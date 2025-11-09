using System.Buffers;
using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Otapewin.Clients;
using Otapewin.Helpers;

namespace Otapewin.Workers;

public sealed class DailyWorker : IWorker
{
    private readonly BrainConfig _config;
    private readonly string _vault;
    private readonly string _inputPath;
    private readonly IOpenAIClient _openAIClient;
    private readonly ILogger<DailyWorker> _logger;

    // Optimized search patterns using SearchValues (.NET 8+)
    private static readonly SearchValues<char> TaskCompletedChars = SearchValues.Create("- [xX]");
    private static readonly SearchValues<char> WhitespaceChars = SearchValues.Create(" \t\r\n");

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

        _logger.LogDebug("DailyWorker initialized with vault: {VaultPath}, input: {InputFile}",
            _vault, _config.InputFile);
    }

    public async Task ProcessAsync(CancellationToken token)
    {
        _logger.LogInformation("Starting daily processing");

        // Check file existence
        if (!File.Exists(_inputPath))
        {
            _logger.LogInformation("Inbox file does not exist, nothing to process");
            return;
        }

        var allLines = await File.ReadAllLinesAsync(_inputPath, token).ConfigureAwait(false);

        if (allLines.Length == 0)
        {
            _logger.LogInformation("Inbox is empty, nothing to process");
            return;
        }

        _logger.LogInformation("Processing {LineCount} lines from inbox", allLines.Length);

        // Cache DateTime to avoid multiple calls
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var isoWeek = System.Globalization.ISOWeek.GetWeekOfYear(today.ToDateTime(TimeOnly.MinValue));

        // Use FrozenDictionary for tag configuration (faster lookups in .NET 8+)
        var tagNames = _config.Tags.Select(t => t.Name).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        var allAvailableTags = _config.Tags.ToDictionary(
            t => t.Name,
            _ => new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        var tagPrefixSpan = _config.TagPrefix.AsSpan();

        // Use List with pre-allocated capacity for better performance
        var processLinesList = new List<string>(allLines.Length);
        var ignoredLinesList = new List<string>();

        // Process lines with Span for reduced allocations
        foreach (var line in allLines)
        {
            var lineSpan = line.AsSpan();
            var trimmedStart = lineSpan.TrimStart();

            if (trimmedStart.StartsWith(tagPrefixSpan, StringComparison.OrdinalIgnoreCase))
            {
                ignoredLinesList.Add(line);
            }
            else
            {
                processLinesList.Add(line);
            }
        }

        _logger.LogDebug("Ignored {IgnoredCount} lines with prefix {TagPrefix}", ignoredLinesList.Count, _config.TagPrefix);

        TagMatcher.ExtractTaggedSections(allAvailableTags, processLinesList);

        foreach (var (tag, lines) in allAvailableTags)
        {
            if (lines.Count > 0)
            {
                _logger.LogInformation("Found {Count} lines with tag #{Tag}", lines.Count, tag);
            }
        }

        // Filter general content using Span for efficiency
        var generalContent = new List<string>(processLinesList.Count);

        ReadOnlySpan<char> completedTaskPrefix = "- [x]";
        ReadOnlySpan<char> lookupTag = "#lookup";

        foreach (var line in processLinesList)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var lineSpan = line.AsSpan().TrimStart();

            if (!lineSpan.StartsWith(completedTaskPrefix, StringComparison.OrdinalIgnoreCase) &&
                !lineSpan.Contains(lookupTag, StringComparison.OrdinalIgnoreCase))
            {
                generalContent.Add(line);
            }
        }

        token.ThrowIfCancellationRequested();

        // Only call API if we have content
        var generalSummary = generalContent.Count > 0
            ? await _openAIClient.SummarizeAsync(string.Join('\n', generalContent), token).ConfigureAwait(false)
            : string.Empty;

        // Process lookups in parallel with proper concurrency control
        var lookupResults = new List<(string Query, string Response)>();
        if (allAvailableTags.TryGetValue("lookup", out var lookups) && lookups.Count > 0)
        {
            _logger.LogInformation("Processing {LookupCount} lookup queries", lookups.Count);

            // Use Parallel.ForEachAsync for better control (new in .NET 6+, optimized in .NET 9)
            var lookupResultsBag = new System.Collections.Concurrent.ConcurrentBag<(string Query, string Response)>();

            await Parallel.ForEachAsync(
                lookups,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Min(lookups.Count, 4),
                    CancellationToken = token
                },
                async (line, ct) =>
                {
                    var result = await _openAIClient.LookupAsync(line, ct).ConfigureAwait(false);
                    lookupResultsBag.Add((Query: line, Response: result));
                }).ConfigureAwait(false);

            lookupResults.AddRange(lookupResultsBag);
        }

        // Process tasks efficiently using Span comparisons
        var completed = new List<string>();
        var pending = new List<string>();

        if (allAvailableTags.TryGetValue("task", out var tasks))
        {
            ReadOnlySpan<char> completedLower = "- [x]";
            ReadOnlySpan<char> completedUpper = "- [X]";

            foreach (var task in tasks)
            {
                var trimmed = task.AsSpan().TrimStart();

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

            _logger.LogInformation("Tasks: {CompletedCount} completed, {PendingCount} pending",
                completed.Count, pending.Count);
        }

        // Build output efficiently with pre-allocated capacity
        var outputPath = Path.Combine(_vault, _config.FocusPath, now.Year.ToString(),
            $"{_config.FocusPrefix}{isoWeek}.md");

        await EnsureDirectoryExistsAsync(outputPath, token).ConfigureAwait(false);

        // Estimate capacity based on content
        var estimatedCapacity = 50 + allAvailableTags.Sum(kvp => kvp.Value.Count) +
                               completed.Count + pending.Count + lookupResults.Count * 3;
        var output = new List<string>(capacity: estimatedCapacity);

        if (!File.Exists(outputPath))
        {
            output.Add($"# 📝 Weekly Focus - {isoWeek}");
        }

        output.Add(string.Empty);
        output.Add($"# 🔥 Day - {today:yyyy-MM-dd}");

        if (!string.IsNullOrEmpty(generalSummary))
        {
            output.Add($"\n## Summary\n{generalSummary}");
        }

        // Add tag sections
        foreach (var tag in allAvailableTags.Keys)
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
            foreach (var (q, a) in lookupResults)
            {
                output.Add($"{q}:> {a}");
                output.Add(string.Empty);
            }
        }

        output.Add(string.Empty);

        token.ThrowIfCancellationRequested();

        // Use optimized file write with FileStreamOptions
        await WriteAllLinesOptimizedAsync(outputPath, output, append: true, token).ConfigureAwait(false);

        _logger.LogInformation("Wrote daily summary to {OutputPath}", outputPath);

        // Archive processed content
        var archivePath = Path.Combine(_vault, _config.ArchivePath, now.Year.ToString(),
            $"Week_{isoWeek}", $"{_config.ArchivePrefix}{today:yyyy-MM-dd}.md");

        await EnsureDirectoryExistsAsync(archivePath, token).ConfigureAwait(false);
        await EnsureDirectoryExistsAsync(_inputPath, token).ConfigureAwait(false);

        await WriteAllLinesOptimizedAsync(archivePath, processLinesList, append: false, token).ConfigureAwait(false);
        await WriteAllLinesOptimizedAsync(_inputPath, ignoredLinesList, append: false, token).ConfigureAwait(false);

        _logger.LogInformation("Daily processing completed successfully. Archived {ProcessedCount} lines", processLinesList.Count);
    }

    private static async ValueTask EnsureDirectoryExistsAsync(string filePath, CancellationToken token)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            await Task.Run(() => Directory.CreateDirectory(directory), token).ConfigureAwait(false);
        }
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
            BufferSize = 4096 // Optimal buffer size for most scenarios
        };

        await using var stream = new FileStream(path, options);
        await using var writer = new StreamWriter(stream);

        foreach (var line in lines)
        {
            await writer.WriteLineAsync(line.AsMemory(), token).ConfigureAwait(false);
        }
    }
}
