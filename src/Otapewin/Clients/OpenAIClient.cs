using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Otapewin.Extensions;

namespace Otapewin.Clients;

public sealed class OpenAIClient : IOpenAIClient
{
    private readonly BrainConfig _config;
    private readonly ChatClient _client;
    private readonly ILogger<OpenAIClient> _logger;

    public OpenAIClient(IOptions<BrainConfig> config, ILogger<OpenAIClient> logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);

        _config = config.Value;
        ArgumentException.ThrowIfNullOrWhiteSpace(_config.OpenAIKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(_config.Model);

        _logger = logger;
        _client = new ChatClient(model: _config.Model, apiKey: _config.OpenAIKey);

        _logger.LogDebug("OpenAI client initialized with model {Model}", _config.Model);
    }

    public async Task<string> SummarizeAsync(string content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        _logger.LogInformation("Starting content summarization (length: {Length} characters)", content.Length);

        content = content.ReplaceWithName(_config.YourName);

        // Use List with capacity for better performance
        var messages = new List<ChatMessage>(capacity: 2)
        {
            new SystemChatMessage(_config.Prompts.DailyPrompt),
            new UserChatMessage($"Here is the Obsidian Memory Inbox content:\n\n{content}")
        };

        try
        {
            ChatCompletion completion = await _client
                .CompleteChatAsync(messages, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var responseText = GetFirstContentOrDefault(completion);

            _logger.LogInformation("Summarization complete (response length: {Length} characters)", responseText.Length);
            _logger.LogDebug("AI Response: {Response}", responseText);

            return responseText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to summarize content");
            throw;
        }
    }

    public async Task<string> LookupAsync(string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        _logger.LogInformation("Performing lookup for query: {Query}", query);

        query = query.ReplaceWithName(_config.YourName);

        var messages = new List<ChatMessage>(capacity: 2)
        {
            new SystemChatMessage("You're an assistant doing live lookups or research."),
            new UserChatMessage($"Please expand or explain: {query}")
        };

        try
        {
            ChatCompletion completion = await _client
                .CompleteChatAsync(messages, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var result = GetFirstContentOrDefault(completion);

            _logger.LogInformation("Lookup complete for query: {Query}", query);
            _logger.LogDebug("Lookup result: {Result}", result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform lookup for query: {Query}", query);
            throw;
        }
    }

    public async Task<string> SummarizePatternsAsync(string systemPrompt, string content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        _logger.LogInformation("Summarizing patterns (content length: {Length} characters)", content.Length);

        var request = new List<ChatMessage>(capacity: 2)
        {
            new SystemChatMessage(systemPrompt.ReplaceWithName(_config.YourName)),
            new UserChatMessage(content.ReplaceWithName(_config.YourName))
        };

        try
        {
            ChatCompletion completion = await _client
                .CompleteChatAsync(request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var result = GetFirstContentOrDefault(completion);

            _logger.LogInformation("Pattern summarization complete (response length: {Length} characters)", result.Length);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to summarize patterns");
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
        if (completion.Content.TryGetNonEnumeratedCount(out var count) && count == 0)
        {
            return DefaultResponse;
        }

        // Get first content or default
        var firstContent = completion.Content.FirstOrDefault();
        return firstContent?.Text ?? DefaultResponse;
    }
}
