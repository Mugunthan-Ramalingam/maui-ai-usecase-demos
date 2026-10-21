using System.Text.Json.Serialization;

namespace SmartRTEFormatter;

/// <summary>
/// Represents the structured document returned by the AI formatting service.
/// </summary>
public sealed class FormattedDocument
{
    /// <summary>
    /// Gets or sets the title of the formatted document.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ordered sections in the document.
    /// </summary>
    [JsonPropertyName("sections")]
    public List<FormattedSection> Sections { get; set; } = new();
}

/// <summary>
/// Represents a document section and its supported content groups.
/// </summary>
public sealed class FormattedSection
{
    /// <summary>
    /// Gets or sets the section heading.
    /// </summary>
    [JsonPropertyName("heading")]
    public string Heading { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the prose paragraphs in the section.
    /// </summary>
    [JsonPropertyName("paragraphs")]
    public List<string> Paragraphs { get; set; } = new();

    /// <summary>
    /// Gets or sets the unordered list items in the section.
    /// </summary>
    [JsonPropertyName("bulletItems")]
    public List<string> BulletItems { get; set; } = new();

    /// <summary>
    /// Gets or sets the ordered list items in the section.
    /// </summary>
    [JsonPropertyName("numberedItems")]
    public List<string> NumberedItems { get; set; } = new();

    /// <summary>
    /// Gets or sets the action items in the section.
    /// </summary>
    [JsonPropertyName("actionItems")]
    public List<string> ActionItems { get; set; } = new();
}

/// <summary>
/// Contains the formatted output returned to the view model.
/// </summary>
public sealed class FormattingResult
{
    /// <summary>
    /// Gets the HTML representation of the formatted document.
    /// </summary>
    public required string HtmlContent { get; init; }
}
