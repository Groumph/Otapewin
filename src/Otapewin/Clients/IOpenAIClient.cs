namespace Otapewin.Clients;

/// <summary>
/// Interface for OpenAI client operations.
/// </summary>
public interface IOpenAIClient
{
    /// <summary>
    /// Summarizes the provided content using the daily prompt.
    /// </summary>
    /// <param name="content">The content to summarize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The summarized content.</returns>
    Task<string> SummarizeAsync(string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a lookup query.
    /// </summary>
    /// <param name="query">The query to look up.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The lookup result.</returns>
    Task<string> LookupAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Summarizes patterns using a custom system prompt.
    /// </summary>
    /// <param name="systemPrompt">The system prompt to use.</param>
    /// <param name="content">The content to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pattern summary.</returns>
    Task<string> SummarizePatternsAsync(string systemPrompt, string content, CancellationToken cancellationToken = default);
}
