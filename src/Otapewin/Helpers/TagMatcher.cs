using System.Collections.Frozen;

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
    /// Uses optimized string searching with Span operations and FrozenDictionary.
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

        // Pre-build hashtag lookup using FrozenDictionary for O(1) lookups with minimal memory
        // FrozenDictionary in .NET 10 has improved hashing and cache locality
        FrozenDictionary<string, string> hashtagToTagMap = allAvailableTags.Keys
            .ToFrozenDictionary(
                tag => $"#{tag}",
                tag => tag,
                StringComparer.OrdinalIgnoreCase);

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            ReadOnlySpan<char> lineSpan = line.AsSpan();

            // Check each hashtag using optimized FrozenDictionary lookup
            foreach ((string hashtag, string tagName) in hashtagToTagMap)
            {
                // Use Span-based search for better performance with SIMD in .NET 10
                if (lineSpan.Contains(hashtag, StringComparison.OrdinalIgnoreCase))
                {
                    // Only trim if necessary - use Span to check
                    ReadOnlySpan<char> trimmed = lineSpan.Trim();
                    allAvailableTags[tagName].Add(trimmed.Length == lineSpan.Length ? line : trimmed.ToString());
                    break;
                }
            }
        }

        return allAvailableTags;
    }
}
