using System.Text.Json.Serialization;

namespace SmartRTEFormatter.Models;

public sealed class FormattedDocument
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("sections")]
    public List<FormattedSection> Sections { get; set; } = new();
}

public sealed class FormattedSection
{
    [JsonPropertyName("heading")]
    public string Heading { get; set; } = string.Empty;

    [JsonPropertyName("paragraphs")]
    public List<string> Paragraphs { get; set; } = new();

    [JsonPropertyName("bulletItems")]
    public List<string> BulletItems { get; set; } = new();

    [JsonPropertyName("numberedItems")]
    public List<string> NumberedItems { get; set; } = new();

    [JsonPropertyName("actionItems")]
    public List<string> ActionItems { get; set; } = new();
}

public sealed class FormattingResult
{
    public required string HtmlContent { get; init; }

    public required string StructuredJson { get; init; }
}
