using System.ComponentModel.DataAnnotations;

namespace Otapewin;

/// <summary>
/// Brain configuration for the Otapewin CLI application
/// </summary>
public sealed class BrainConfig
{
    /// <summary>
    /// Gets or sets the OpenAI API key
    /// </summary>
    [Required(ErrorMessage = "OpenAI API key is required")]
    public string OpenAIKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the vault path
    /// </summary>
    [Required(ErrorMessage = "Vault path is required")]
    public string VaultPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the input file
    /// </summary>
    [Required(ErrorMessage = "Input file is required")]
    public string InputFile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OpenAI model to use
    /// </summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Gets or sets the archive path
    /// </summary>
    public string ArchivePath { get; set; } = "Archive";

    /// <summary>
    /// Gets or sets the archive prefix
    /// </summary>
    public string ArchivePrefix { get; set; } = "Memory Archive - ";

    /// <summary>
    /// Gets or sets the tag prefix for ignored content
    /// </summary>
    public string TagPrefix { get; set; } = "@ignore";

    /// <summary>
    /// Gets or sets the focus path
    /// </summary>
    public string FocusPath { get; set; } = "Focuses";

    /// <summary>
    /// Gets or sets the focus file prefix
    /// </summary>
    public string FocusPrefix { get; set; } = "Weekly Focus - ";

    /// <summary>
    /// Gets or sets the user's name for personalization
    /// </summary>
    public string YourName { get; set; } = "User";

    /// <summary>
    /// Gets or sets the list of tags to process
    /// </summary>
    public List<TagConfig> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets the prompt configurations
    /// </summary>
    [Required(ErrorMessage = "Prompts configuration is required")]
    public PromptConfig Prompts { get; set; } = new();
}

/// <summary>
/// Tag configuration
/// </summary>
public sealed class TagConfig
{
    /// <summary>
    /// Gets or sets the tag name
    /// </summary>
    [Required(ErrorMessage = "Tag name is required")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the custom prompt for this tag
    /// </summary>
    public string? Prompt { get; set; }
}

/// <summary>
/// Prompt configuration for AI operations
/// </summary>
public sealed class PromptConfig
{
    /// <summary>
    /// Gets or sets the default tag prompt
    /// </summary>
    [Required]
    public string DefaultTagPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the daily processing prompt
    /// </summary>
    [Required]
    public string DailyPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the weekly default summary prompt
    /// </summary>
    [Required]
    public string WeeklyDefaultPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the weekly coaching reflection prompt
    /// </summary>
    [Required]
    public string WeeklyCoachPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the weekly intentions prompt
    /// </summary>
    [Required]
    public string WeeklyIntentionsPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the backlog review prompt
    /// </summary>
    [Required]
    public string BacklogReviewPrompt { get; set; } = string.Empty;
}
