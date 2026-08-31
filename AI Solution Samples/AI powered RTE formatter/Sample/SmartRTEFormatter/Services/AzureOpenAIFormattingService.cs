using System.Text;
using System.Text.Json;
using SmartRTEFormatter.Models;

namespace SmartRTEFormatter.Services;

public sealed class AzureOpenAIFormattingService : IAIFormattingService
{
    private const string BaseEndpoint = "ENDPOINT";
    private const string DeploymentName = "gpt-5-mini";
    private readonly string? apiKey = "API_KEY";

    private readonly HttpClient httpClient = new();

    private const string SystemPrompt =
    """
    You are an enterprise-grade document formatting assistant.

    Transform unstructured or semi-structured content into a professional document structure.

    Rules:

    1. Return ONLY valid JSON.
    2. Do not return HTML.
    3. Do not return Markdown.
    4. Do not return explanations.
    5. Do not duplicate information across sections.
    6. Preserve all information from the source.
    7. Do not invent facts.
    8. Create meaningful section headings.
    9. Extract metrics, achievements and highlights into bulletItems.
    10. Use numberedItems only when sequence or order matters.
    11. Use actionItems only for tasks, responsibilities or follow-up activities.
    12. Keep paragraphs concise and professional.
    13. Consolidate repeated information into a single section.
    14. Use empty arrays when a category does not apply.

    Return exactly this JSON structure:

    {
      "title":"string",
      "sections":[
        {
          "heading":"string",
          "paragraphs":["string"],
          "bulletItems":["string"],
          "numberedItems":["string"],
          "actionItems":["string"]
        }
      ]
    }
    """;



    public async Task<FormattingResult> FormatAsync(
        string content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var demoDocument = CreateDemoDocument(content);

            return new FormattingResult
            {
                HtmlContent = CreateHtml(demoDocument),
                StructuredJson = SerializeDocument(demoDocument)
            };
        }

        var requestUri = $"{BaseEndpoint}/chat/completions";

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            requestUri);

        request.Headers.Add("api-key", apiKey);

        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                model = DeploymentName,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = SystemPrompt
                    },
                    new
                    {
                        role = "user",
                        content =
                        $"""
                        Convert the following content into a structured business document.

                        Requirements:

                        - Create logical sections.
                        - Preserve all facts.
                        - Remove duplicated information.
                        - Group metrics under bulletItems.
                        - Use numberedItems only when sequence matters.
                        - Use actionItems for tasks and responsibilities.
                        - Create professional section headings.
                        - Return valid JSON only.

                        Content:

                        {content}
                        """
                    }
                },
                max_completion_tokens = 2500,
                reasoning_effort = "minimal"
            }),
            Encoding.UTF8,
            "application/json");

        using var response =
            await httpClient.SendAsync(request, cancellationToken);

        var responseBody =
            await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"HTTP {(int)response.StatusCode}: {responseBody}");
        }

        using var responseDocument =
            JsonDocument.Parse(responseBody);

        var structuredContent =
            responseDocument.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

        if (string.IsNullOrWhiteSpace(structuredContent))
        {
            throw new InvalidOperationException(
                "Azure OpenAI returned no formatted content.");
        }

        var cleanedJson = RemoveCodeFence(structuredContent);

        var formattedDocument =
            JsonSerializer.Deserialize<FormattedDocument>(
                cleanedJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        if (formattedDocument is null)
        {
            throw new JsonException(
                "Azure OpenAI returned an invalid structured document.");
        }

        return new FormattingResult
        {
            HtmlContent = CreateHtml(formattedDocument),
            StructuredJson = SerializeDocument(formattedDocument)
        };
    }

    private static string RemoveCodeFence(string content)
    {
        return content
            .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static FormattedDocument CreateDemoDocument(string content)
    {
        return new FormattedDocument
        {
            Title = "Formatted Document",
            Sections =
            [
                new FormattedSection
                {
                    Heading = "Content",
                    Paragraphs = content
                        .Split(
                            ['\r', '\n'],
                            StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim())
                        .ToList(),
                    BulletItems = [],
                    NumberedItems = [],
                    ActionItems = []
                }
            ]
        };
    }

    private static string SerializeDocument(
        FormattedDocument document)
    {
        return JsonSerializer.Serialize(
            document,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });
    }

    private static string CreateHtml(
        FormattedDocument document)
    {
        var html = new StringBuilder();

        AppendHeading(html, document.Title, 1);

        foreach (var section in document.Sections)
        {
            AppendHeading(html, section.Heading, 2);

            AppendParagraphs(html, section.Paragraphs);

            AppendSectionList(
                html,
                "Key Points",
                section.BulletItems,
                "ul");

            AppendSectionList(
                html,
                "Steps",
                section.NumberedItems,
                "ol");

            AppendSectionList(
                html,
                "Action Items",
                section.ActionItems,
                "ul",
                "action-items");
        }

        return html.ToString();
    }

    private static void AppendHeading(
        StringBuilder html,
        string text,
        int level)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        html.Append(
            $"<h{level}>{System.Net.WebUtility.HtmlEncode(text)}</h{level}>");
    }

    private static void AppendParagraphs(
        StringBuilder html,
        IEnumerable<string> paragraphs)
    {
        foreach (var paragraph in paragraphs
                     .Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            html.Append(
                $"<p>{System.Net.WebUtility.HtmlEncode(paragraph)}</p>");
        }
    }

    private static void AppendSectionList(
        StringBuilder html,
        string title,
        IEnumerable<string> items,
        string tag,
        string? cssClass = null)
    {
        var values = items
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (values.Count == 0)
            return;

        html.Append($"<h3>{System.Net.WebUtility.HtmlEncode(title)}</h3>");

        if (string.IsNullOrWhiteSpace(cssClass))
        {
            html.Append($"<{tag}>");
        }
        else
        {
            html.Append($"<{tag} class=\"{cssClass}\">");
        }

        foreach (var item in values)
        {
            html.Append(
                $"<li>{System.Net.WebUtility.HtmlEncode(item)}</li>");
        }

        html.Append($"</{tag}>");
    }
}