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
                        "You are a strict calendar date resolver. Resolve only clear, unambiguous date requests. " +
                        "Use the provided reference date as today and calculate the correct Gregorian date. " +
                        "For generic or vague requests that do not specify a concrete date type, do not guess. " +
                        "Examples of unsupported vague input: 'some date', 'a date soon', 'good date', 'holiday', 'special day', " +
                        "'random date', 'date after next month', 'interesting date'. " +
                        "For explicit relative phrases, use these rules: 'today' = today, 'tomorrow' = next calendar day, " +
                        "'next X' or 'upcoming X' = the first future occurrence after today, never a past date, " +
                        "'this X' = current calendar period, 'last X' = previous occurrence, and 'in N days/weeks/months' = " +
                        "add the exact number of units from today. " +
                        "For recurring holidays, include the correct year and move to the next year if the current year has already passed. " +
                        "If the request mentions 'Independence Day' without a country, interpret as US Independence Day on July 4. " +
                        "If a request is ambiguous or not specifically date-related, return: 'INVALID_REQUEST'. " +
                        "Otherwise return exactly one date in yyyy-MM-dd format and nothing else."
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