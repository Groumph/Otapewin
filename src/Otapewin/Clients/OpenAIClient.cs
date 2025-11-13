using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Otapewin.Extensions;

namespace Otapewin.Clients;

/// <summary>
/// OpenAI client for summarization and lookup operations
/// </summary>
public sealed class OpenAIClient : IOpenAIClient
{
    private readonly BrainConfig _config;
    private readonly ChatClient _client;
    private readonly ILogger<OpenAIClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIClient"/> class
    /// </summary>
    public OpenAIClient(IOptions<BrainConfig> config, ILogger<OpenAIClient> logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);

        _config = config.Value;
        ArgumentException.ThrowIfNullOrWhiteSpace(_config.OpenAIKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(_config.Model);

        _logger = logger;
        _client = new ChatClient(model: _config.Model, apiKey: _config.OpenAIKey);

        LogClientInitialized(_logger, _config.Model);
    }

    /// <summary>
    /// Summarizes content using the configured AI model
    /// </summary>
    public async Task<string> SummarizeAsync(string content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        LogStartingSummarization(_logger, content.Length);

        content = content.ReplaceWithName(_config.YourName);

        // Use List with capacity for better performance
        List<ChatMessage> messages =
        [
            new SystemChatMessage(_config.Prompts.DailyPrompt),
            new UserChatMessage($"Here is the Obsidian Memory Inbox content:\n\n{content}")
        ];

        try
        {
            ChatCompletion completion = await _client
                .CompleteChatAsync(messages, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            string responseText = GetFirstContentOrDefault(completion);

            LogSummarizationComplete(_logger, responseText.Length);
            LogAiResponse(_logger, responseText);

            return responseText;
        }
        catch (Exception ex)
        {
            LogSummarizationFailed(_logger, ex);
            throw;
        }
    }

    /// <summary>
    /// Performs a lookup query using the configured AI model
    /// </summary>
    public async Task<string> LookupAsync(string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        LogLookupStarting(_logger, query);

        query = query.ReplaceWithName(_config.YourName);

        List<ChatMessage> messages =
        [
            new SystemChatMessage("You're an assistant doing live lookups or research."),
            new UserChatMessage($"Please expand or explain: {query}")
        ];

        try
        {
            ChatCompletion completion = await _client
                .CompleteChatAsync(messages, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            string result = GetFirstContentOrDefault(completion);

            LogLookupComplete(_logger, query);
            LogLookupResult(_logger, result);

            return result;
        }
        catch (Exception ex)
        {
            LogLookupFailed(_logger, query, ex);
            throw;
        }
    }

    /// <summary>
    /// Summarizes patterns using a custom system prompt
    /// </summary>
    public async Task<string> SummarizePatternsAsync(string systemPrompt, string content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        LogPatternSummarizationStarting(_logger, content.Length);

        List<ChatMessage> request =
        [
            new SystemChatMessage(systemPrompt.ReplaceWithName(_config.YourName)),
            new UserChatMessage(content.ReplaceWithName(_config.YourName))
        ];

        try
        {
            ChatCompletion completion = await _client
                .CompleteChatAsync(request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            string result = GetFirstContentOrDefault(completion);

            LogPatternSummarizationComplete(_logger, result.Length);

            return result;
        }
        catch (Exception ex)
        {
            LogPatternSummarizationFailed(_logger, ex);
            throw;
        }
    }

    /// <summary>
    /// Extracts the first content text from completion or returns a default message.
    /// Optimized to avoid unnecessary allocations.
    /// </summary>
    private static string GetFirstContentOrDefault(ChatCompletion completion)
    {
        const string DefaultResponse = "[No response]";

        // Use TryGetNonEnumeratedCount for better performance (.NET 6+)
        if (completion.Content.TryGetNonEnumeratedCount(out int count) && count == 0)
        {
            return DefaultResponse;
        }

        // Get first content or default
        ChatMessageContentPart? firstContent = completion.Content.FirstOrDefault();
        return firstContent?.Text ?? DefaultResponse;
    }

    #region LoggerMessage Delegates

    private static readonly Action<ILogger, string, Exception?> _logClientInitialized =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(1, nameof(LogClientInitialized)),
            "OpenAI client initialized with model {Model}");

    private static readonly Action<ILogger, int, Exception?> _logStartingSummarization =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(2, nameof(LogStartingSummarization)),
            "Starting content summarization (length: {Length} characters)");

    private static readonly Action<ILogger, int, Exception?> _logSummarizationComplete =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(3, nameof(LogSummarizationComplete)),
            "Summarization complete (response length: {Length} characters)");

    private static readonly Action<ILogger, string, Exception?> _logAiResponse =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(4, nameof(LogAiResponse)),
            "AI Response: {Response}");

    private static readonly Action<ILogger, Exception?> _logSummarizationFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(5, nameof(LogSummarizationFailed)),
            "Failed to summarize content");

    private static readonly Action<ILogger, string, Exception?> _logLookupStarting =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(6, nameof(LogLookupStarting)),
            "Performing lookup for query: {Query}");

    private static readonly Action<ILogger, string, Exception?> _logLookupComplete =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(7, nameof(LogLookupComplete)),
            "Lookup complete for query: {Query}");

    private static readonly Action<ILogger, string, Exception?> _logLookupResult =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(8, nameof(LogLookupResult)),
            "Lookup result: {Result}");

    private static readonly Action<ILogger, string, Exception?> _logLookupFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(9, nameof(LogLookupFailed)),
            "Failed to perform lookup for query: {Query}");

    private static readonly Action<ILogger, int, Exception?> _logPatternSummarizationStarting =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(10, nameof(LogPatternSummarizationStarting)),
            "Summarizing patterns (content length: {Length} characters)");

    private static readonly Action<ILogger, int, Exception?> _logPatternSummarizationComplete =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(11, nameof(LogPatternSummarizationComplete)),
            "Pattern summarization complete (response length: {Length} characters)");

    private static readonly Action<ILogger, Exception?> _logPatternSummarizationFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(12, nameof(LogPatternSummarizationFailed)),
            "Failed to summarize patterns");

    private static void LogClientInitialized(ILogger logger, string model) => _logClientInitialized(logger, model, null);
    private static void LogStartingSummarization(ILogger logger, int length) => _logStartingSummarization(logger, length, null);
    private static void LogSummarizationComplete(ILogger logger, int length) => _logSummarizationComplete(logger, length, null);
    private static void LogAiResponse(ILogger logger, string response) => _logAiResponse(logger, response, null);
    private static void LogSummarizationFailed(ILogger logger, Exception ex) => _logSummarizationFailed(logger, ex);
    private static void LogLookupStarting(ILogger logger, string query) => _logLookupStarting(logger, query, null);
    private static void LogLookupComplete(ILogger logger, string query) => _logLookupComplete(logger, query, null);
    private static void LogLookupResult(ILogger logger, string result) => _logLookupResult(logger, result, null);
    private static void LogLookupFailed(ILogger logger, string query, Exception ex) => _logLookupFailed(logger, query, ex);
    private static void LogPatternSummarizationStarting(ILogger logger, int length) => _logPatternSummarizationStarting(logger, length, null);
    private static void LogPatternSummarizationComplete(ILogger logger, int length) => _logPatternSummarizationComplete(logger, length, null);
    private static void LogPatternSummarizationFailed(ILogger logger, Exception ex) => _logPatternSummarizationFailed(logger, ex);

    #endregion
}
