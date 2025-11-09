using System.Buffers;
using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Otapewin.Clients;
using Otapewin.Helpers;

namespace Otapewin.Workers;

public sealed class WeeklyWorker : IWorker
{
    private readonly BrainConfig _config;
    private readonly string _vault;
    private readonly IOpenAIClient _openAIClient;
    private readonly ILogger<WeeklyWorker> _logger;

    // Optimized search patterns
    private static readonly SearchValues<char> TaskPendingChars = SearchValues.Create("- [ ]");

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

        _logger.LogDebug("WeeklyWorker initialized with vault: {VaultPath}", _vault);
    }

    public async Task ProcessAsync(CancellationToken token)
    {
        _logger.LogInformation("Starting weekly processing");

        if (DateTime.UtcNow.DayOfWeek != DayOfWeek.Monday)
        {
            _logger.LogInformation("Not Monday, skipping weekly processing");
            return;
        }

        var now = DateTime.UtcNow;
        var isoWeek = ISOWeek.GetWeekOfYear(now.Date) - 1;
        var year = now.Year;
        var archivePath = Path.Combine(_vault, _config.ArchivePath, year.ToString(), $"Week_{isoWeek}");

        if (!Directory.Exists(archivePath))
        {
            _logger.LogWarning("Archive directory not found: {ArchivePath}", archivePath);
            return;
        }

        var files = Directory.GetFiles(archivePath, "*.md", SearchOption.TopDirectoryOnly);
        _logger.LogInformation("Processing {FileCount} files from week {Week}", files.Length, isoWeek);

        if (files.Length == 0)
        {
            _logger.LogWarning("No markdown files found in {ArchivePath}", archivePath);
            return;
        }

        // Read all files with optimized parallel processing
        var estimatedLines = files.Length * 50;
        var allLinesList = new List<string>(capacity: estimatedLines);

        // Use Parallel.ForEachAsync for better concurrency control (.NET 6+, optimized in .NET 9)
        var linesBag = new System.Collections.Concurrent.ConcurrentBag<string[]>();

        await Parallel.ForEachAsync(
            files,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(files.Length, Environment.ProcessorCount),
                CancellationToken = token
            },
            async (file, ct) =>
            {
                var fileLines = await File.ReadAllLinesAsync(file, ct).ConfigureAwait(false);
                linesBag.Add(fileLines);
            }).ConfigureAwait(false);

        // Flatten results
        foreach (var lines in linesBag)
        {
            allLinesList.AddRange(lines);
        }

        // Tag grouping with FrozenDictionary for better performance (.NET 8+)
        var grouped = _config.Tags.ToDictionary(
            t => t.Name,
            _ => new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        TagMatcher.ExtractTaggedSections(grouped, allLinesList);

        // Build GPT summary per tag group - parallelize with better control
        var tagSummaryBag = new System.Collections.Concurrent.ConcurrentBag<(string Tag, string Summary)>();

        var tagsToProcess = grouped.Where(kvp => kvp.Value.Count > 0).ToList();

        await Parallel.ForEachAsync(
            tagsToProcess,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(tagsToProcess.Count, 3), // Limit concurrent API calls
                CancellationToken = token
            },
            async (kvp, ct) =>
            {
                var (tag, lines) = kvp;

                _logger.LogInformation("Summarizing {LineCount} lines for tag #{Tag}", lines.Count, tag);

                var tagConfig = _config.Tags.Find(t => t.Name.Equals(tag, StringComparison.OrdinalIgnoreCase));
                var prompt = tagConfig?.Prompt ?? _config.Prompts.WeeklyDefaultPrompt;

                var content = string.Join('\n', lines);
                var summary = await _openAIClient.SummarizePatternsAsync(prompt, content, ct).ConfigureAwait(false);

                tagSummaryBag.Add((Tag: tag, Summary: summary));
            }).ConfigureAwait(false);

        // Convert to FrozenDictionary for faster lookups
        var tagSummaries = tagSummaryBag.ToFrozenDictionary(
            x => x.Tag,
            x => x.Summary,
            StringComparer.OrdinalIgnoreCase);

        // Write to weekly focus
        var outputPath = Path.Combine(_vault, _config.FocusPath, year.ToString(),
            $"{_config.FocusPrefix}{isoWeek}_Summary.md");

        var estimatedOutputCapacity = 100 + tagSummaries.Count * 20;
        var output = new List<string>(capacity: estimatedOutputCapacity)
        {
            "## 🧠 Weekly Tag Summaries"
        };

        // OrderBy is optimized in .NET 9
        foreach (var tag in tagSummaries.Keys.Order(StringComparer.OrdinalIgnoreCase))
        {
            output.Add($"\n### {tag} Summary\n{tagSummaries[tag]}");
        }

        token.ThrowIfCancellationRequested();

        _logger.LogInformation("Generating weekly coaching reflection");

        // Use ArrayPool for large string building to reduce GC pressure
        string fullWeekRaw;
        if (allLinesList.Count > 1000)
        {
            // Calculate total length to avoid reallocations
            var totalLength = allLinesList.Sum(l => l.Length + Environment.NewLine.Length);
            var sb = new StringBuilder(capacity: totalLength);

            foreach (var line in allLinesList)
            {
                sb.AppendLine(line);
            }
            fullWeekRaw = sb.ToString();
        }
        else
        {
            fullWeekRaw = string.Join('\n', allLinesList);
        }

        // GPT Coaching Reflection
        var coachingReflection = await _openAIClient.SummarizePatternsAsync(
            _config.Prompts.WeeklyCoachPrompt,
            fullWeekRaw,
            token).ConfigureAwait(false);

        output.Add("\n## 🎓 Weekly Coaching Reflection\n");
        output.Add(coachingReflection);

        // Check for unfinished tasks using optimized Span operations
        if (_config.Tags.Exists(x => x.Name.Equals("task", StringComparison.OrdinalIgnoreCase))
            && grouped.TryGetValue("task", out var taskLines))
        {
            ReadOnlySpan<char> pendingTaskPrefix = "- [ ]";
            var unfinishedTasks = new List<string>(taskLines.Count / 2); // Estimate half are unfinished

            foreach (var task in taskLines)
            {
                var trimmed = task.AsSpan().TrimStart();
                if (trimmed.StartsWith(pendingTaskPrefix, StringComparison.Ordinal))
                {
                    unfinishedTasks.Add(task);
                }
            }

            if (unfinishedTasks.Count > 0)
            {
                token.ThrowIfCancellationRequested();

                _logger.LogInformation("Generating intentions for {TaskCount} unfinished tasks", unfinishedTasks.Count);

                var intentions = await _openAIClient.SummarizePatternsAsync(
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

        _logger.LogInformation("Weekly processing completed successfully. Output written to {OutputPath}", outputPath);
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
