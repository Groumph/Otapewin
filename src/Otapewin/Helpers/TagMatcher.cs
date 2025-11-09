using System.Buffers;

namespace Otapewin.Helpers;

public static class TagMatcher
{
    // Cache for common patterns to reduce allocations
    private static readonly SearchValues<char> WhitespaceChars = SearchValues.Create(" \t\r\n");

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

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var lineSpan = line.AsSpan();

            // Check each tag using optimized Span-based search
            foreach (var tag in allAvailableTags.Keys)
            {
                var fullTag = $"#{tag}";

                // Use Span-based search for better performance
                if (lineSpan.Contains(fullTag, StringComparison.OrdinalIgnoreCase))
                {
                    // Only trim if necessary - use Span to check
                    var trimmed = lineSpan.Trim();
                    allAvailableTags[tag].Add(trimmed.Length == lineSpan.Length ? line : trimmed.ToString());
                    break;
                }
            }
        }

        return allAvailableTags;
    }
}
