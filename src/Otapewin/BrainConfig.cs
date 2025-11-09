using System.ComponentModel.DataAnnotations;

namespace Otapewin;

public sealed class BrainConfig
{
    [Required(ErrorMessage = "OpenAI API key is required")]
    public string OpenAIKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vault path is required")]
    public string VaultPath { get; set; } = string.Empty;

    [Required(ErrorMessage = "Input file is required")]
    public string InputFile { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-4o-mini";

    public string ArchivePath { get; set; } = "Archive";

    public string ArchivePrefix { get; set; } = "Memory Archive - ";

    public string TagPrefix { get; set; } = "@ignore";

    public string FocusPath { get; set; } = "Focuses";

    public string FocusPrefix { get; set; } = "Weekly Focus - ";

    public string YourName { get; set; } = "User";

    public List<TagConfig> Tags { get; set; } = [];

    [Required(ErrorMessage = "Prompts configuration is required")]
    public PromptConfig Prompts { get; set; } = new();
}

public sealed class TagConfig
{
    [Required(ErrorMessage = "Tag name is required")]
    public string Name { get; set; } = string.Empty;

    public string? Prompt { get; set; }
}

public sealed class PromptConfig
{
    [Required]
    public string DefaultTagPrompt { get; set; } = string.Empty;

    [Required]
    public string DailyPrompt { get; set; } = string.Empty;

    [Required]
    public string WeeklyDefaultPrompt { get; set; } = string.Empty;

    [Required]
    public string WeeklyCoachPrompt { get; set; } = string.Empty;

    [Required]
    public string WeeklyIntentionsPrompt { get; set; } = string.Empty;

    [Required]
    public string BacklogReviewPrompt { get; set; } = string.Empty;
}
