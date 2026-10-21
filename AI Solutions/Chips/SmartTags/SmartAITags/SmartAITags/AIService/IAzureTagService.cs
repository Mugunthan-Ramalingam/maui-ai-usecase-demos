namespace SmartAITags;

/// <summary>
/// Azure OpenAI tag-generation contract used by the SmartAITags sample.
/// Implementations must throw on failure (network/HTTP/JSON errors) rather
/// than returning magic-prefixed error strings; the ViewModel surfaces those
/// exceptions through its bound <c>ErrorMessage</c> property.
/// </summary>
public interface IAzureTagService
{
    Task<string> GetCompletionAsync(string prompt, CancellationToken cancellationToken = default);
}
