namespace Otapewin.Helpers;

/// <summary>
/// Provides professional console UI helper methods for displaying formatted messages.
/// Supports both interactive console and Windows Scheduler environments.
/// Optimized for .NET 9 with reduced allocations and better performance.
/// </summary>
public static class ConsoleUi
{
    private static readonly Lock ConsoleLock = new(); // .NET 9 Lock type
    private static readonly bool _colorsSupported = InitializeColorSupport();

    // Cache common strings to reduce allocations
    private static readonly string[] LogLevelPadded =
    [
        "INFO   ",
        "SUCCESS",
        "WARN   ",
        "ERROR  ",
        "DEBUG  ",
        "START  ",
        "DONE   ",
        "FAILED "
    ];

    private static bool InitializeColorSupport()
    {
        // Detect if console supports colors (fails in Windows Scheduler)
        try
        {
            _ = Console.ForegroundColor;
            return !Console.IsOutputRedirected;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Displays a title with underline.
    /// </summary>
    /// <param name="text">The title text.</param>
    public static void Title(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        lock (ConsoleLock)
        {
            Console.WriteLine();
            WriteWithColor(text, ConsoleColor.Cyan, inline: true);
            Console.WriteLine();
            WriteWithColor(CreateUnderline(text.Length, '═'), ConsoleColor.Cyan);
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Displays an informational message.
    /// </summary>
    /// <param name="text">The message text.</param>
    public static void Info(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        WriteLogMessage(LogLevelPadded[0], text, ConsoleColor.White);
    }

    /// <summary>
    /// Displays a success message.
    /// </summary>
    /// <param name="text">The message text.</param>
    public static void Success(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        WriteLogMessage(LogLevelPadded[1], text, ConsoleColor.Green, "✓");
    }

    /// <summary>
    /// Displays a warning message.
    /// </summary>
    /// <param name="text">The message text.</param>
    public static void Warn(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        WriteLogMessage(LogLevelPadded[2], text, ConsoleColor.Yellow, "⚠");
    }

    /// <summary>
    /// Displays an error message to stderr.
    /// </summary>
    /// <param name="text">The error message text.</param>
    public static void Error(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        WriteLogMessage(LogLevelPadded[3], text, ConsoleColor.Red, "✗", useStdErr: true);
    }

    /// <summary>
    /// Displays a debug message (only in development).
    /// </summary>
    /// <param name="text">The debug message text.</param>
    public static void Debug(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        WriteLogMessage(LogLevelPadded[4], text, ConsoleColor.Gray, "•");
    }

    /// <summary>
    /// Executes an action with a status message.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="message">The status message.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>The result of the action.</returns>
    public static T Status<T>(string message, Func<T> action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(action);

        DateTime startTime = DateTime.UtcNow;
        WriteLogMessage(LogLevelPadded[5], message, ConsoleColor.Cyan, "▶");

        try
        {
            T result = action();
            TimeSpan duration = DateTime.UtcNow - startTime;
            WriteLogMessage(LogLevelPadded[6], $"{message} (took {duration.TotalMilliseconds:F0}ms)", ConsoleColor.Green, "✓");
            return result;
        }
        catch (Exception ex)
        {
            TimeSpan duration = DateTime.UtcNow - startTime;
            WriteLogMessage(LogLevelPadded[7], $"{message} (after {duration.TotalMilliseconds:F0}ms): {ex.Message}", ConsoleColor.Red, "✗", useStdErr: true);
            throw;
        }
    }

    /// <summary>
    /// Executes an async action with a status message.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="message">The status message.</param>
    /// <param name="action">The async action to execute.</param>
    /// <returns>Task with the result of the action.</returns>
    public static async Task<T> StatusAsync<T>(string message, Func<Task<T>> action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(action);

        DateTime startTime = DateTime.UtcNow;
        WriteLogMessage(LogLevelPadded[5], message, ConsoleColor.Cyan, "▶");

        try
        {
            T result = await action().ConfigureAwait(false);
            TimeSpan duration = DateTime.UtcNow - startTime;
            WriteLogMessage(LogLevelPadded[6], $"{message} (took {duration.TotalMilliseconds:F0}ms)", ConsoleColor.Green, "✓");
            return result;
        }
        catch (Exception ex)
        {
            TimeSpan duration = DateTime.UtcNow - startTime;
            WriteLogMessage(LogLevelPadded[7], $"{message} (after {duration.TotalMilliseconds:F0}ms): {ex.Message}", ConsoleColor.Red, "✗", useStdErr: true);
            throw;
        }
    }

    /// <summary>
    /// Displays tag counts in a professional table format.
    /// </summary>
    /// <param name="counts">Dictionary of tag names to counts.</param>
    public static void TagCounts(IDictionary<string, int> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);

        if (counts.Count == 0)
        {
            return;
        }

        lock (ConsoleLock)
        {
            Console.WriteLine();
            WriteWithColor("┌─ Tag Summary ─────────────────────", ConsoleColor.Cyan);

            // Use TryGetNonEnumeratedCount for better performance
            int maxKeyLen = counts.Keys.Max(k => k?.Length ?? 0);
            int totalCount = counts.Values.Sum();

            foreach ((string key, int value) in counts.OrderByDescending(x => x.Value))
            {
                double percentage = totalCount > 0 ? value * 100.0 / totalCount : 0;
                int barLength = Math.Min((int)(percentage / 5), 20);

                Console.Write("│ ");
                WriteWithColor(key.PadRight(maxKeyLen), ConsoleColor.White, inline: true);
                Console.Write(" │ ");
                WriteWithColor($"{value,4}", ConsoleColor.Cyan, inline: true);
                Console.Write(" │ ");
                WriteWithColor($"{percentage,5:F1}%", ConsoleColor.Gray, inline: true);
                Console.Write(" │ ");
                WriteWithColor(CreateBar(barLength), ConsoleColor.Green, inline: true);
                Console.WriteLine(" │");
            }

            WriteWithColor($"└─ Total: {totalCount} items ───────────────", ConsoleColor.Cyan);
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Displays a progress indicator.
    /// </summary>
    /// <param name="current">Current progress value.</param>
    /// <param name="total">Total value.</param>
    /// <param name="message">Optional message.</param>
    public static void Progress(int current, int total, string? message = null)
    {
        if (total <= 0)
        {
            return;
        }

        lock (ConsoleLock)
        {
            int percentage = (int)(current * 100.0 / total);
            const int barLength = 40;
            int filled = current * barLength / total;

            string bar = CreateProgressBar(filled, barLength);

            Console.Write($"\r│ Progress: [{bar}] {percentage,3}% ({current}/{total})");

            if (!string.IsNullOrWhiteSpace(message))
            {
                Console.Write($" - {message}");
            }

            if (current >= total)
            {
                Console.WriteLine();
            }
        }
    }

    /// <summary>
    /// Displays a separator line.
    /// </summary>
    public static void Separator()
    {
        lock (ConsoleLock)
        {
            int width = Console.WindowWidth > 0 ? Math.Min(Console.WindowWidth, 80) : 80;
            WriteWithColor(CreateUnderline(width, '─'), ConsoleColor.DarkGray);
        }
    }

    /// <summary>
    /// Displays a banner with application information.
    /// </summary>
    /// <param name="appName">Application name.</param>
    /// <param name="version">Version string.</param>
    public static void Banner(string appName, string version)
    {
        lock (ConsoleLock)
        {
            Console.WriteLine();
            WriteWithColor("╔═══════════════════════════════════════════════════════════════╗", ConsoleColor.Cyan);
            WriteWithColor(string.Concat("║  ", appName.PadRight(59), "║"), ConsoleColor.Cyan);
            WriteWithColor(string.Concat("║  Version ", version.PadRight(51), "║"), ConsoleColor.Gray);
            WriteWithColor("╚═══════════════════════════════════════════════════════════════╝", ConsoleColor.Cyan);
            Console.WriteLine();
        }
    }

    #region Private Helpers

    private static void WriteLogMessage(string level, string message, ConsoleColor color, string? icon = null, bool useStdErr = false)
    {
        lock (ConsoleLock)
        {
            DateTime timestamp = DateTime.UtcNow;
            string timestampString;

            // Use TryFormat for better performance with stackalloc
            Span<char> timestampBuffer = stackalloc char[23];
            if (timestamp.TryFormat(timestampBuffer, out int charsWritten, "yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture))
            {
                timestampString = timestampBuffer[..charsWritten].ToString();
            }
            else
            {
                // Fallback to standard formatting
                timestampString = timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
            }

            TextWriter output = useStdErr ? Console.Error : Console.Out;

            if (_colorsSupported && !useStdErr)
            {
                // Colorful output for interactive console
                Console.Write("[");
                WriteWithColor(timestampString, ConsoleColor.Gray, inline: true);
                Console.Write("] [");
                WriteWithColor(level, color, inline: true);
                Console.Write("]");

                if (icon != null)
                {
                    Console.Write(" ");
                    WriteWithColor(icon, color, inline: true);
                }

                Console.Write(" ");
                Console.WriteLine(message);
            }
            else
            {
                // Plain output for Windows Scheduler / redirected output
                string iconText = icon != null ? $"{icon} " : "";
                output.WriteLine($"[{timestampString}] [{level}] {iconText}{message}");
            }
        }
    }

    private static void WriteWithColor(string text, ConsoleColor color, bool inline = false)
    {
        if (_colorsSupported)
        {
            ConsoleColor originalColor = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = color;
                if (inline)
                {
                    Console.Write(text);
                }
                else
                {
                    Console.WriteLine(text);
                }
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }
        else
        {
            if (inline)
            {
                Console.Write(text);
            }
            else
            {
                Console.WriteLine(text);
            }
        }
    }

    /// <summary>
    /// Creates an underline string efficiently using string.Create (.NET Core 2.1+)
    /// </summary>
    private static string CreateUnderline(int length, char character)
    {
        return string.Create(length, character, static (span, ch) =>
        {
            span.Fill(ch);
        });
    }

    /// <summary>
    /// Creates a progress bar string efficiently
    /// </summary>
    private static string CreateProgressBar(int filled, int total)
    {
        return string.Create(total, (filled, total), static (span, state) =>
        {
            span[..state.filled].Fill('█');
            span[state.filled..].Fill('░');
        });
    }

    /// <summary>
    /// Creates a bar string for tag counts efficiently
    /// </summary>
    private static string CreateBar(int length)
    {
        const int maxLength = 20;
        int actualLength = Math.Min(length, maxLength);

        return string.Create(maxLength, actualLength, static (span, len) =>
        {
            span[..len].Fill('█');
            span[len..].Fill(' ');
        });
    }

    #endregion
}
