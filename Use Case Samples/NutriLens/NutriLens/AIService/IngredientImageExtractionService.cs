using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NutriLens.Services;

public sealed class IngredientImageExtractionService : IIngredientImageExtractionService
{
    private const string ExtractionPrompt = """
                Read the entire food label image edge to edge, including text near every border and corner.
                Do not crop, ignore, or summarize any part of the visible ingredient statement or Nutrition Facts table.

                Transcribe these sections in the order shown:
                Ingredients:
                - Copy the complete ingredient statement and sub-ingredients in their printed order.
                - Preserve commas, parentheses, and allergen statements that are part of the ingredient section.
                - Put each top-level ingredient on its own line; keep sub-ingredients with their parent ingredient.

                Nutrition Facts:
                - Record serving size, servings per container, and calories.
                - Record every visible nutrient row with its amount and % Daily Value, including fat, cholesterol,
                    sodium, carbohydrates, fiber, sugars, added sugars, protein, vitamins, and minerals.
                - Keep each nutrient name, number, unit, and % Daily Value together. Do not omit rows at the
                    top, bottom, left, or right of the table. Use [unclear] only for characters that cannot be read.

                Return only the two labeled sections. Do not invent or estimate unreadable values. If one section
                is not visible, return its heading followed by "Not visible".
                """;

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(120)
    };

    public async Task<string> ExtractContentAsync(
        FileResult image,
        CancellationToken cancellationToken = default)
    {
        if (image is null)
            throw new ArgumentNullException(nameof(image));

        AzureOpenAIConfig.EnsureConfigured();

        await using var imageStream = await image.OpenReadAsync();
        using var imageBuffer = new MemoryStream();
        await imageStream.CopyToAsync(imageBuffer, cancellationToken);
        var imageBytes = imageBuffer.ToArray();

        if (imageBytes.Length == 0)
            throw new InvalidOperationException("The selected image is empty.");

        var base64 = Convert.ToBase64String(imageBytes);
        var mediaType = Path.GetExtension(image.FileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };
        var imageUrl = $"data:{mediaType};base64,{base64}";

        var requestBody = new
        {
            model = AzureOpenAIConfig.ConfiguredDeploymentName,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = ExtractionPrompt },
                        new
                        {
                            type = "image_url",
                            image_url = new { url = imageUrl, detail = "high" }
                        }
                    }
                }
            },
            max_completion_tokens = 3000,
            reasoning_effort = "minimal"
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            AzureOpenAIConfig.GetChatCompletionsUri());
        request.Headers.Add("api-key", AzureOpenAIConfig.ConfiguredApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        using var response = await Http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Azure OpenAI image extraction failed (HTTP {(int)response.StatusCode}). " +
                "Check the deployment, credentials, and service availability.");
        }

        using var document = JsonDocument.Parse(responseBody);
        var text = ReadCompletionText(document.RootElement);

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("No readable label content could be extracted from the selected product image.");

        return text.Trim();
    }

    private static string ReadCompletionText(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0 ||
            !choices[0].TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var content))
        {
            return string.Empty;
        }

        if (content.ValueKind == JsonValueKind.String)
            return content.GetString() ?? string.Empty;

        if (content.ValueKind == JsonValueKind.Array)
        {
            return string.Concat(content.EnumerateArray()
                .Where(part => part.ValueKind == JsonValueKind.Object &&
                    part.TryGetProperty("text", out _))
                .Select(part => part.GetProperty("text").GetString() ?? string.Empty));
        }

        return string.Empty;
    }
}