namespace Otapewin.Extensions;

/// <summary>
/// String extension methods for common operations
/// </summary>
public static class StringExtensions
{
    private const string UserNamePlaceholder = "{userName}";

    // Cache the span for repeated comparisons
    private static ReadOnlySpan<char> PlaceholderSpan => "{userName}".AsSpan();

    /// <summary>
    /// Replaces the {userName} placeholder with the specified name.
    /// Optimized with Span operations to reduce allocations.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="name">The name to replace with.</param>
    /// <returns>String with placeholder replaced, or original if input/name is null or empty.</returns>
    public static string ReplaceWithName(this string? input, string? name)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(name))
        {
            return input ?? string.Empty;
        }

        // Fast path: check if placeholder exists using Span
        if (!input.AsSpan().Contains(PlaceholderSpan, StringComparison.OrdinalIgnoreCase))
        {
            return input;
        }

        // Use the built-in Replace which is highly optimized in .NET 9
        return input.Replace(UserNamePlaceholder, name, StringComparison.OrdinalIgnoreCase);
    }
}
