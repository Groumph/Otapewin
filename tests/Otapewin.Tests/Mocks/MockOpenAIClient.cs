using Otapewin.Clients;

namespace SecondBrain.Tests.Mocks;

/// <summary>
/// Mock OpenAI client for testing without making actual API calls.
/// </summary>
public sealed class MockOpenAIClient : IOpenAIClient
{
    private readonly Dictionary<string, string> _responses = new();

    public void SetResponse(string key, string response)
    {
        _responses[key] = response;
    }

    public Task<string> SummarizeAsync(string content, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_responses.TryGetValue("summarize", out var response)
       ? response
        : $"[Mock Summary of {content.Length} characters]");
    }

    public Task<string> LookupAsync(string query, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_responses.TryGetValue($"lookup:{query}", out var response)
   ? response
   : $"[Mock Lookup: {query}]");
    }

    public Task<string> SummarizePatternsAsync(string systemPrompt, string content, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_responses.TryGetValue("patterns", out var response)
     ? response
            : $"[Mock Pattern Summary with prompt: {systemPrompt.Substring(0, Math.Min(50, systemPrompt.Length))}...]");
    }
}
