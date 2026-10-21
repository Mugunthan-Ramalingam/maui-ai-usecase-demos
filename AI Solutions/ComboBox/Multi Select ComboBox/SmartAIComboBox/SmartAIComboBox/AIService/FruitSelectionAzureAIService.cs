using System.ClientModel;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace SmartAIComboBox;

/// <summary>
/// Azure OpenAI fruit-selection service for the SmartAIComboBox sample.
/// Uses the same Azure OpenAI SDK flow and deployment as the SmartAITags sample.
/// Errors are surfaced as exceptions (caller publishes to its bound
/// <c>ErrorMessage</c>) — never as magic-prefixed strings.
/// </summary>
public sealed class FruitSelectionAzureAIService : IFruitSelectionAIService
{
    private const string Endpoint =
        "Your Endpoint";

    private const string DeploymentName = "Your Deployment Name";

    private static readonly string ApiKey =
        "Your API Key";

    /// <summary>
    /// Sends the user prompt through the Azure OpenAI SDK and returns the raw
    /// completion text. Throws on request failures so the caller can surface
    /// them through its own error channel.
    /// </summary>
    public async Task<string> GetCompletionAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        if (ApiKey == "REPLACE_WITH_ROTATED_AZURE_OPENAI_API_KEY")
            throw new InvalidOperationException(
                "Set a rotated Azure OpenAI API key in FruitSelectionAzureAIService before running the sample.");

        var client = new AzureOpenAIClient(new Uri(Endpoint), new AzureKeyCredential(ApiKey));
        var chatClient = client.GetChatClient(DeploymentName);
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(
                "You are an AI fruit-selection assistant for a MultiSelect fruit menu. " +
                "Choose fruits from the provided menu that match the user's natural-language request. " +
                "Return ONLY the exact fruit Names from the menu, one per line. " +
                "Do NOT include tags, numbers, dashes, bullets, explanations, or any preamble. " +
                "If no fruit matches, return exactly the word: Empty"),
            new UserChatMessage(prompt)
        };

        ChatCompletion completion;
        try
        {
            completion = await chatClient.CompleteChatAsync(
                messages,
                cancellationToken: cancellationToken);
        }
        catch (ClientResultException ex) when (ex.Status == 401)
        {
            throw new InvalidOperationException(
                "Azure OpenAI returned 401. Verify that the API key belongs to the resource at the configured endpoint.", ex);
        }
        catch (ClientResultException ex) when (ex.Status == 404)
        {
            throw new InvalidOperationException(
                $"Azure OpenAI returned 404. Verify that deployment '{DeploymentName}' exists on the configured resource.", ex);
        }

        var content = string.Concat(completion.Content.Select(part => part.Text));
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Azure OpenAI returned empty content.");

        return content.Trim();
    }
}
