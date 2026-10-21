namespace SmartAIComboBox;

/// <summary>
/// Azure OpenAI fruit-selection contract. Implementations throw on failure
/// (network/HTTP/JSON errors); the ViewModel surfaces those exceptions via
/// its bound <c>ErrorMessage</c> property — no magic-prefixed strings.
/// </summary>
public interface IFruitSelectionAIService
{
    Task<string> GetCompletionAsync(string prompt, CancellationToken cancellationToken = default);
}
