using System.Text;
using System.Text.Json;

namespace SmartAIDatePicker.AIService;

public sealed class AzureOpenAIService : IAzureOpenAIService
{
    private const string BaseEndpoint = "ENDPOINT_URL";

    private const string DeploymentName =
        "gpt-5-mini";

    private const string ApiKey = "API_KEY";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public string? LastError { get; private set; }

    public string LastRawResponse { get; private set; } = string.Empty;

    public async Task<string> GetCompletion(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        LastError = null;
        LastRawResponse = string.Empty;

        var requestBody = new
        {
            model = DeploymentName,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content =
                        "You resolve natural-language questions to one specific Gregorian calendar date. " +
                        "Answer date questions from every category, including historical events and wars, " +
                        "births and deaths, inventions, holidays, anniversaries, weekdays, leap days, " +
                        "seasons, month boundaries, and relative dates. Use the supplied reference date as today. " +
                        "For historical events, return the commonly accepted start or occurrence date; for a war, " +
                        "return the date it began unless the user explicitly asks for its end. " +
                        "For 'next X' or 'upcoming X', return the first occurrence strictly after today. " +
                        "For 'this X', use the current period; for 'last X', use the previous occurrence; " +
                        "and for 'in N days/weeks/months', add exactly N units to today. " +
                        "For recurring holidays, use the next occurrence when no year is given. " +
                        "If 'Independence Day' has no country, use US Independence Day, July 4. " +
                        "Resolve common aliases such as WWI/First World War and leap day (February 29). " +
                        "Do not invent a date for a genuinely ambiguous request. " +
                        "If the request is vague, asks for multiple dates, or is not date-related, return INVALID_REQUEST. " +
                        "Return exactly one date in yyyy-MM-dd format and nothing else, or exactly INVALID_REQUEST."
                },
                new
                {
                    role = "user",
                    content = prompt
                }
            },
            max_completion_tokens = 100,
            reasoning_effort = "minimal"
        };

        var url = $"{BaseEndpoint}/chat/completions";

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            url);

        request.Headers.Add("api-key", ApiKey);

        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        var response = await Http.SendAsync(
            request,
            cancellationToken);

        var raw = await response.Content.ReadAsStringAsync(
            cancellationToken);

        LastRawResponse = raw;

        System.Diagnostics.Debug.WriteLine(
            $"[AzureAI] Status: {(int)response.StatusCode}");

        System.Diagnostics.Debug.WriteLine(
            $"[AzureAI] Raw Response: {raw}");

        if (!response.IsSuccessStatusCode)
        {
            LastError =
                $"HTTP {(int)response.StatusCode}\n\n{raw}";

            return LastError;
        }

        using var document = JsonDocument.Parse(raw);

        if (!document.RootElement.TryGetProperty(
                "choices",
                out var choices))
        {
            LastError =
                $"No 'choices' property found.\n\nRaw Response:\n{raw}";

            return LastError;
        }

        if (choices.GetArrayLength() == 0)
        {
            LastError = "Azure returned no choices.";
            return LastError;
        }

        var content = choices[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            LastError =
                $"Azure returned empty content.\n\nRaw Response:\n{raw}";

            return LastError;
        }

        return content.Trim();
    }
}