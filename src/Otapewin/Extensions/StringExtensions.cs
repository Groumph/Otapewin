using System.Buffers;

namespace Otapewin.Extensions;

/// <summary>
/// String extension methods for common operations
/// </summary>
public static class StringExtensions
{
    private const string UserNamePlaceholder = "{userName}";

    // Use SearchValues for optimized string searching (.NET 8+, enhanced in .NET 10)
    private static readonly SearchValues<char> _placeholderSearchValues =
        SearchValues.Create("{userName}");

    /// <summary>
    /// Replaces the {userName} placeholder with the specified name.
    /// Optimized with SearchValues and Span operations to reduce allocations.
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

        // Fast path: check if placeholder exists using SearchValues (optimized in .NET 10)
        if (input.AsSpan().IndexOfAny(_placeholderSearchValues) < 0)
        {
            return input;
        }

        // Use the built-in Replace which is highly optimized in .NET 10
        // .NET 10 has significant improvements to String.Replace with vectorization
        return input.Replace(UserNamePlaceholder, name, StringComparison.OrdinalIgnoreCase);
    }
}
