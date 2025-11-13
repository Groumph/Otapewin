namespace Otapewin.Helpers;

/// <summary>
/// Tag matching utilities for extracting tagged content
/// </summary>
public static class TagMatcher
{
    /// <summary>
    /// Builds all recognized hashtags for a tag.
    /// </summary>
    /// <param name="tag">The tag configuration.</param>
    /// <returns>Enumerable of hashtag variations.</returns>
    public static IEnumerable<string> Hashtags(TagConfig tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag.Name);

        yield return $"#{tag.Name.ToLowerInvariant()}";
    }

    /// <summary>
    /// Extracts lines containing specific tags and groups them by tag name.
    /// Uses optimized string searching with Span operations.
    /// </summary>
    /// <param name="allAvailableTags">Dictionary of tag names to line lists.</param>
    /// <param name="lines">Lines to process.</param>
    /// <returns>Updated dictionary with extracted tagged lines.</returns>
    public static Dictionary<string, List<string>> ExtractTaggedSections(
        Dictionary<string, List<string>> allAvailableTags,
        List<string> lines)
    {
        ArgumentNullException.ThrowIfNull(allAvailableTags);
        ArgumentNullException.ThrowIfNull(lines);

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            ReadOnlySpan<char> lineSpan = line.AsSpan();

            // Check each tag using optimized Span-based search
            foreach (string tag in allAvailableTags.Keys)
            {
                string fullTag = $"#{tag}";

                // Use Span-based search for better performance
                if (lineSpan.Contains(fullTag, StringComparison.OrdinalIgnoreCase))
                {
                    // Only trim if necessary - use Span to check
                    ReadOnlySpan<char> trimmed = lineSpan.Trim();
                    allAvailableTags[tag].Add(trimmed.Length == lineSpan.Length ? line : trimmed.ToString());
                    break;
                }
            }
        }

        return allAvailableTags;
    }
}
